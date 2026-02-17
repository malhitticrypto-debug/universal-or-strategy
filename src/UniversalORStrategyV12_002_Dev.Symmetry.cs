// V12.50 SYMMETRY GUARD - Master-Fill Anchored Fleet Risk Isolation
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class UniversalORStrategyV12_002_Dev : Strategy
    {
        #region V12.50 Symmetry Guard

        private sealed class SymmetryDispatchContext
        {
            public string DispatchId;
            public string TradeType;
            public MarketPosition Direction;
            public int ExpectedQuantity;
            public DateTime CreatedUtc;

            public double MasterWeightedFill;
            public int MasterFilledQuantity;
            public double MasterAnchorPrice;
            public bool IsResolved;

            public readonly object Sync = new object();
            public readonly HashSet<string> FollowerEntries = new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class PendingFollowerFill
        {
            public string FleetEntryName;
            public double FleetFillPrice;
            public DateTime QueuedUtc;
        }

        private readonly ConcurrentDictionary<string, SymmetryDispatchContext> symmetryDispatchById =
            new ConcurrentDictionary<string, SymmetryDispatchContext>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, string> symmetryFleetEntryToDispatch =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, string> symmetryMasterEntryToDispatch =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, PendingFollowerFill> symmetryPendingFollowerFills =
            new ConcurrentDictionary<string, PendingFollowerFill>(StringComparer.Ordinal);

        private const int SymmetryMaxSlippageTicks = 4;
        private const double SymmetryMaxSlippageUsdPerContract = 20.0;
        private static readonly TimeSpan SymmetryAnchorWait = TimeSpan.FromMilliseconds(900);
        private static readonly TimeSpan SymmetryDispatchTtl = TimeSpan.FromMinutes(5);

        private string SymmetryGuardBeginDispatch(string tradeType, OrderAction action, int quantity, double requestedEntryPrice)
        {
            string dispatchId = string.Format("SG_{0}_{1}_{2}",
                DateTime.UtcNow.Ticks,
                SymmetryNormalizeTradeType(tradeType),
                (int)action);

            var ctx = new SymmetryDispatchContext
            {
                DispatchId = dispatchId,
                TradeType = SymmetryNormalizeTradeType(tradeType),
                Direction = (action == OrderAction.Buy || action == OrderAction.BuyToCover)
                    ? MarketPosition.Long
                    : MarketPosition.Short,
                ExpectedQuantity = Math.Max(1, quantity),
                CreatedUtc = DateTime.UtcNow,
                MasterAnchorPrice = Instrument != null
                    ? Instrument.MasterInstrument.RoundToTickSize(requestedEntryPrice)
                    : requestedEntryPrice,
                IsResolved = false
            };

            symmetryDispatchById[dispatchId] = ctx;
            return dispatchId;
        }

        private void SymmetryGuardRegisterFollower(string dispatchId, string fleetEntryName)
        {
            if (string.IsNullOrEmpty(dispatchId) || string.IsNullOrEmpty(fleetEntryName))
                return;

            symmetryFleetEntryToDispatch[fleetEntryName] = dispatchId;

            if (symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                lock (ctx.Sync)
                    ctx.FollowerEntries.Add(fleetEntryName);
            }
        }

        private void SymmetryGuardRegisterMasterEntry(string dispatchId, string masterEntryName)
        {
            if (string.IsNullOrEmpty(dispatchId) || string.IsNullOrEmpty(masterEntryName))
                return;
            symmetryMasterEntryToDispatch[masterEntryName] = dispatchId;
        }

        private void SymmetryGuardOnMasterFill(string entryName, PositionInfo masterPos, double averageFillPrice, int fillQty, DateTime fillTimeUtc)
        {
            if (masterPos == null || masterPos.IsFollower || averageFillPrice <= 0 || fillQty <= 0)
                return;

            SymmetryDispatchContext ctx = null;

            if (!string.IsNullOrEmpty(entryName) &&
                symmetryMasterEntryToDispatch.TryGetValue(entryName, out var mappedDispatch) &&
                symmetryDispatchById.TryGetValue(mappedDispatch, out var mappedCtx))
            {
                ctx = mappedCtx;
            }

            if (ctx == null)
            {
                string tradeType = SymmetryInferTradeType(entryName, masterPos);
                ctx = SymmetryFindDispatchForMasterFill(tradeType, masterPos.Direction, fillTimeUtc);
            }

            if (ctx == null)
                return;

            bool resolvedNow = false;
            lock (ctx.Sync)
            {
                if (!ctx.IsResolved)
                {
                    ctx.MasterWeightedFill += averageFillPrice * fillQty;
                    ctx.MasterFilledQuantity += fillQty;

                    double avg = ctx.MasterWeightedFill / Math.Max(1, ctx.MasterFilledQuantity);
                    ctx.MasterAnchorPrice = Instrument.MasterInstrument.RoundToTickSize(avg);
                    ctx.IsResolved = true;
                    resolvedNow = true;
                }
            }

            if (resolvedNow)
            {
                Print(string.Format("[SYMMETRY_GUARD] MASTER ANCHOR LOCKED | Trade={0} | Anchor={1:F2} | FillQty={2}",
                    ctx.TradeType, ctx.MasterAnchorPrice, ctx.MasterFilledQuantity));

                SymmetryGuardTryResolveFollowersForDispatch(ctx.DispatchId, DateTime.UtcNow);
            }
        }

        private SymmetryDispatchContext SymmetryFindDispatchForMasterFill(string tradeType, MarketPosition direction, DateTime fillTimeUtc)
        {
            string norm = SymmetryNormalizeTradeType(tradeType);
            SymmetryDispatchContext best = null;

            foreach (var kvp in symmetryDispatchById.ToArray())
            {
                SymmetryDispatchContext ctx = kvp.Value;
                if (ctx == null || ctx.IsResolved)
                    continue;
                if (ctx.Direction != direction)
                    continue;
                if (!string.Equals(ctx.TradeType, norm, StringComparison.Ordinal))
                    continue;
                if (fillTimeUtc - ctx.CreatedUtc > SymmetryDispatchTtl)
                    continue;

                if (best == null || ctx.CreatedUtc < best.CreatedUtc)
                    best = ctx;
            }

            return best;
        }

        private bool SymmetryGuardOnFollowerFill(string fleetEntryName, PositionInfo followerPos, double followerFillPrice)
        {
            if (followerPos == null || !followerPos.IsFollower)
                return false;

            followerPos.EntryFilled = true;
            if (followerPos.RemainingContracts <= 0)
                followerPos.RemainingContracts = Math.Max(1, followerPos.TotalContracts);

            if (!followerPos.BracketSubmitted)
                SymmetryGuardSubmitFollowerBracket(fleetEntryName, followerPos);

            var pending = new PendingFollowerFill
            {
                FleetEntryName = fleetEntryName,
                FleetFillPrice = followerFillPrice > 0 ? followerFillPrice : followerPos.EntryPrice,
                QueuedUtc = DateTime.UtcNow
            };

            symmetryPendingFollowerFills[fleetEntryName] = pending;

            if (SymmetryGuardTryResolveFollower(fleetEntryName, followerPos, pending, DateTime.UtcNow))
                symmetryPendingFollowerFills.TryRemove(fleetEntryName, out _);

            return true;
        }

        private bool SymmetryGuardIsAnchorPending(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return false;
            return symmetryPendingFollowerFills.ContainsKey(entryName);
        }

        private void SymmetryGuardProcessPendingFollowerFills()
        {
            if (symmetryPendingFollowerFills.IsEmpty)
            {
                SymmetryGuardPruneDispatches();
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            foreach (var kvp in symmetryPendingFollowerFills.ToArray())
            {
                string fleetEntryName = kvp.Key;
                PendingFollowerFill pending = kvp.Value;

                if (!activePositions.TryGetValue(fleetEntryName, out var pos) || pos == null || !pos.IsFollower)
                {
                    symmetryPendingFollowerFills.TryRemove(fleetEntryName, out _);
                    SymmetryGuardForgetEntry(fleetEntryName);
                    continue;
                }

                if (SymmetryGuardTryResolveFollower(fleetEntryName, pos, pending, nowUtc))
                    symmetryPendingFollowerFills.TryRemove(fleetEntryName, out _);
            }

            SymmetryGuardPruneDispatches();
        }

        private bool SymmetryGuardTryResolveFollower(string fleetEntryName, PositionInfo pos, PendingFollowerFill pending, DateTime nowUtc)
        {
            if (!symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var dispatchId) ||
                !symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                if (nowUtc - pending.QueuedUtc >= SymmetryAnchorWait)
                {
                    SymmetryGuardSkipFollower(fleetEntryName, pos, pending.FleetFillPrice, 0, 0, "Missing dispatch context");
                    return true;
                }
                return false;
            }

            if (!ctx.IsResolved)
            {
                if (nowUtc - pending.QueuedUtc >= SymmetryAnchorWait)
                {
                    SymmetryGuardSkipFollower(fleetEntryName, pos, pending.FleetFillPrice, 0, 0, "Master anchor timeout");
                    return true;
                }
                return false;
            }

            double masterAnchor = ctx.MasterAnchorPrice;
            double slippagePoints = Math.Abs(pending.FleetFillPrice - masterAnchor);
            double slippageTicks = tickSize > 0 ? slippagePoints / tickSize : 0.0;
            double slippageUsdPerContract = pointValue > 0 ? slippagePoints * pointValue : 0.0;

            bool breach = slippageTicks > SymmetryMaxSlippageTicks ||
                          slippageUsdPerContract > SymmetryMaxSlippageUsdPerContract;
            if (breach)
            {
                SymmetryGuardSkipFollower(
                    fleetEntryName,
                    pos,
                    pending.FleetFillPrice,
                    slippageTicks,
                    slippageUsdPerContract,
                    string.Format("Slippage Buffer breach vs Master {0:F2}", masterAnchor));
                return true;
            }

            SymmetryGuardApplyMasterAnchor(pos, masterAnchor);

            if (pos.BracketSubmitted)
                SymmetryGuardRetargetExistingFollowerBracket(fleetEntryName, pos);
            else
                SymmetryGuardSubmitFollowerBracket(fleetEntryName, pos);

            Print(string.Format(
                "[SYMMETRY_GUARD] ANCHORED | {0} | Master={1:F2} Fleet={2:F2} Slip={3:F1} ticks (${4:F2}/ct) | Scalp Anchor T1={5:F2} | Runner T4=Trail",
                fleetEntryName, masterAnchor, pending.FleetFillPrice, slippageTicks, slippageUsdPerContract, pos.Target1Price));

            return true;
        }

        private void SymmetryGuardApplyMasterAnchor(PositionInfo pos, double masterAnchor)
        {
            double anchor = Instrument.MasterInstrument.RoundToTickSize(masterAnchor);
            double oldBase = pos.EntryPrice > 0 ? pos.EntryPrice : anchor;

            double stopDist = Math.Abs(oldBase - pos.InitialStopPrice);
            if (stopDist <= 0)
                stopDist = Math.Abs(oldBase - pos.CurrentStopPrice);

            double t1Dist = Math.Abs(pos.Target1Price - oldBase);
            double t2Dist = Math.Abs(pos.Target2Price - oldBase);
            double t3Dist = Math.Abs(pos.Target3Price - oldBase);

            double stop = pos.Direction == MarketPosition.Long ? anchor - stopDist : anchor + stopDist;
            double t1 = pos.Direction == MarketPosition.Long ? anchor + t1Dist : anchor - t1Dist;
            double t2 = pos.Direction == MarketPosition.Long ? anchor + t2Dist : anchor - t2Dist;
            double t3 = pos.Direction == MarketPosition.Long ? anchor + t3Dist : anchor - t3Dist;

            pos.EntryPrice = anchor;
            pos.ExtremePriceSinceEntry = anchor;

            pos.InitialStopPrice = Instrument.MasterInstrument.RoundToTickSize(stop);
            pos.CurrentStopPrice = pos.InitialStopPrice;
            pos.Target1Price = Instrument.MasterInstrument.RoundToTickSize(t1);
            pos.Target2Price = Instrument.MasterInstrument.RoundToTickSize(t2);
            pos.Target3Price = Instrument.MasterInstrument.RoundToTickSize(t3);
        }

        private void SymmetryGuardSubmitFollowerBracket(string fleetEntryName, PositionInfo pos)
        {
            if (pos.BracketSubmitted) return;
            Account acct = pos.ExecutingAccount;
            if (acct == null) return;

            OrderAction exitAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
            double validatedStop = ValidateStopPrice(pos.Direction, pos.CurrentStopPrice);
            string ocoId = "SG_" + DateTime.UtcNow.Ticks.ToString();

            var ordersToSubmit = new List<Order>();

            string stopSig = SymmetryTrim("Stop_" + fleetEntryName, 40);
            Order stop = acct.CreateOrder(Instrument, exitAction, OrderType.StopMarket,
                TimeInForce.Gtc, Math.Max(1, pos.RemainingContracts), 0, validatedStop, ocoId, stopSig, null);
            stopOrders[fleetEntryName] = stop;
            ordersToSubmit.Add(stop);

            if (pos.T1Contracts > 0 && pos.TotalContracts > 1)
            {
                string t1Sig = SymmetryTrim("T1_" + fleetEntryName, 40);
                Order t1 = acct.CreateOrder(Instrument, exitAction, OrderType.Limit,
                    TimeInForce.Gtc, pos.T1Contracts, pos.Target1Price, 0, ocoId, t1Sig, null);
                target1Orders[fleetEntryName] = t1;
                ordersToSubmit.Add(t1);
            }

            if (pos.T2Contracts > 0)
            {
                string t2Sig = SymmetryTrim("T2_" + fleetEntryName, 40);
                Order t2 = acct.CreateOrder(Instrument, exitAction, OrderType.Limit,
                    TimeInForce.Gtc, pos.T2Contracts, pos.Target2Price, 0, ocoId, t2Sig, null);
                target2Orders[fleetEntryName] = t2;
                ordersToSubmit.Add(t2);
            }

            if (pos.T3Contracts > 0)
            {
                string t3Sig = SymmetryTrim("T3_" + fleetEntryName, 40);
                Order t3 = acct.CreateOrder(Instrument, exitAction, OrderType.Limit,
                    TimeInForce.Gtc, pos.T3Contracts, pos.Target3Price, 0, ocoId, t3Sig, null);
                target3Orders[fleetEntryName] = t3;
                ordersToSubmit.Add(t3);
            }

            acct.Submit(ordersToSubmit.ToArray());
            pos.BracketSubmitted = true;
        }

        private void SymmetryGuardRetargetExistingFollowerBracket(string fleetEntryName, PositionInfo pos)
        {
            UpdateStopOrder(fleetEntryName, pos, pos.CurrentStopPrice, pos.CurrentTrailLevel);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, target1Orders, "T1", pos.Target1Price);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, target2Orders, "T2", pos.Target2Price);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, target3Orders, "T3", pos.Target3Price);
        }

        private void SymmetryGuardReplaceExistingFollowerTarget(
            string fleetEntryName,
            PositionInfo pos,
            ConcurrentDictionary<string, Order> dict,
            string targetTag,
            double newPrice)
        {
            if (pos.ExecutingAccount == null) return;
            if (!dict.TryGetValue(fleetEntryName, out var oldTarget) || oldTarget == null) return;

            int qty = oldTarget.Quantity;
            if (qty <= 0)
            {
                if (targetTag == "T1") qty = pos.T1Contracts;
                else if (targetTag == "T2") qty = pos.T2Contracts;
                else if (targetTag == "T3") qty = pos.T3Contracts;
            }
            if (qty <= 0) return;

            if (oldTarget.OrderState == OrderState.Working ||
                oldTarget.OrderState == OrderState.Accepted ||
                oldTarget.OrderState == OrderState.Submitted ||
                oldTarget.OrderState == OrderState.ChangePending)
            {
                pos.ExecutingAccount.Cancel(new[] { oldTarget });
            }

            OrderAction exitAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
            string signalName = SymmetryTrim(targetTag + "_" + fleetEntryName, 40);

            Order replacement = pos.ExecutingAccount.CreateOrder(
                Instrument,
                exitAction,
                OrderType.Limit,
                TimeInForce.Gtc,
                qty,
                Instrument.MasterInstrument.RoundToTickSize(newPrice),
                0,
                "SGT_" + DateTime.UtcNow.Ticks.ToString(),
                signalName,
                null);

            pos.ExecutingAccount.Submit(new[] { replacement });
            dict[fleetEntryName] = replacement;
        }

        private void SymmetryGuardSkipFollower(
            string fleetEntryName,
            PositionInfo pos,
            double fleetFillPrice,
            double slippageTicks,
            double slippageUsdPerContract,
            string reason)
        {
            Print(string.Format(
                "[SYMMETRY_GUARD] SKIP | {0} | {1} | FleetFill={2:F2} | Slip={3:F1} ticks (${4:F2}/ct)",
                fleetEntryName, reason, fleetFillPrice, slippageTicks, slippageUsdPerContract));

            pos.EntryFilled = true;
            if (pos.RemainingContracts <= 0)
                pos.RemainingContracts = Math.Max(1, pos.TotalContracts);

            FlattenPositionByName(fleetEntryName);
            CleanupPosition(fleetEntryName);
            SymmetryGuardForgetEntry(fleetEntryName);
        }

        private void SymmetryGuardTryResolveFollowersForDispatch(string dispatchId, DateTime nowUtc)
        {
            foreach (var kvp in symmetryPendingFollowerFills.ToArray())
            {
                string fleetEntryName = kvp.Key;
                if (!symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var linkedDispatch))
                    continue;
                if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
                    continue;

                if (activePositions.TryGetValue(fleetEntryName, out var pos) && pos != null && pos.IsFollower)
                {
                    if (SymmetryGuardTryResolveFollower(fleetEntryName, pos, kvp.Value, nowUtc))
                        symmetryPendingFollowerFills.TryRemove(fleetEntryName, out _);
                }
            }
        }

        private void SymmetryGuardForgetEntry(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return;

            symmetryPendingFollowerFills.TryRemove(entryName, out _);
            symmetryMasterEntryToDispatch.TryRemove(entryName, out _);

            if (symmetryFleetEntryToDispatch.TryRemove(entryName, out var dispatchId) &&
                symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                lock (ctx.Sync)
                    ctx.FollowerEntries.Remove(entryName);
            }
        }

        private void SymmetryGuardPruneDispatches()
        {
            DateTime nowUtc = DateTime.UtcNow;

            foreach (var kvp in symmetryDispatchById.ToArray())
            {
                SymmetryDispatchContext ctx = kvp.Value;
                if (ctx == null) continue;

                bool remove = false;

                if (nowUtc - ctx.CreatedUtc > SymmetryDispatchTtl)
                {
                    remove = true;
                }
                else if (ctx.IsResolved)
                {
                    bool hasActiveFollowers = false;
                    lock (ctx.Sync)
                    {
                        foreach (string follower in ctx.FollowerEntries)
                        {
                            if (activePositions.ContainsKey(follower))
                            {
                                hasActiveFollowers = true;
                                break;
                            }
                        }
                    }
                    if (!hasActiveFollowers) remove = true;
                }

                if (remove)
                    symmetryDispatchById.TryRemove(kvp.Key, out _);
            }
        }

        private string SymmetryInferTradeType(string entryName, PositionInfo pos)
        {
            if (pos != null)
            {
                if (pos.IsTRENDTrade) return "TREND";
                if (pos.IsRetestTrade) return "RETEST";
                if (pos.IsFFMATrade) return "FFMA";
                if (pos.IsMOMOTrade) return "MOMO";
                if (pos.IsRMATrade) return "RMA";
            }
            return SymmetryNormalizeTradeType(entryName);
        }

        private string SymmetryNormalizeTradeType(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "GENERIC";

            string t = raw.ToUpperInvariant();
            if (t.StartsWith("TREND", StringComparison.Ordinal)) return "TREND";
            if (t.StartsWith("RETEST", StringComparison.Ordinal)) return "RETEST";
            if (t.StartsWith("FFMA", StringComparison.Ordinal)) return "FFMA";
            if (t.StartsWith("MOMO", StringComparison.Ordinal)) return "MOMO";
            if (t.StartsWith("RMA", StringComparison.Ordinal)) return "RMA";
            if (t.StartsWith("OR", StringComparison.Ordinal) || t.Contains("ORLONG") || t.Contains("ORSHORT")) return "OR";
            return "GENERIC";
        }

        private static string SymmetryTrim(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLen ? text : text.Substring(0, maxLen);
        }

        #endregion
    }
}
