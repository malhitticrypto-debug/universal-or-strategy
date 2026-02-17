// V12.44 MODULAR: Order Management Module (Split from Orders.cs)
// Contains: Bracket orders, stop management, position sync, flatten, cleanup, reconciliation
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using System.Net;
using System.Net.Sockets;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class UniversalORStrategyV12_002_Dev : Strategy
    {
        #region Order Submission & Stop Management

        private void SubmitBracketOrders(string entryName, PositionInfo pos)
        {
            if (pos.BracketSubmitted) return;

            try
            {
                // Validate stop price
                double validatedStopPrice = ValidateStopPrice(pos.Direction, pos.InitialStopPrice);

                // Submit initial stop for all contracts
                Order stopOrder = pos.Direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.StopMarket, pos.TotalContracts, 0, validatedStopPrice, "", "Stop_" + entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.StopMarket, pos.TotalContracts, 0, validatedStopPrice, "", "Stop_" + entryName);

                stopOrders[entryName] = stopOrder;

                // Submit T1 limit order ONLY if T1 quantity > 0 AND TotalContracts > 1
                // V8.15: For 1-contract trades, we treat it as a runner (no initial target)
                if (pos.T1Contracts > 0 && pos.TotalContracts > 1)
                {
                    Order t1Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T1Contracts, pos.Target1Price, 0, "", "T1_" + entryName);

                    target1Orders[entryName] = t1Order;
                }
                else if (pos.TotalContracts == 1)
                {
                    Print(string.Format("V8.15: 1-contract trade detected for {0}. Treating as RUNNER (no initial target).", entryName));
                }

                // Submit T2 limit order ONLY if T2 quantity > 0
                if (pos.T2Contracts > 0)
                {
                    Order t2Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T2Contracts, pos.Target2Price, 0, "", "T2_" + entryName);

                    target2Orders[entryName] = t2Order;
                }

                // v5.13: Submit T3 limit order ONLY if T3 quantity > 0
                if (pos.T3Contracts > 0)
                {
                    Order t3Order = pos.Direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Limit, pos.T3Contracts, pos.Target3Price, 0, "", "T3_" + entryName)
                        : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Limit, pos.T3Contracts, pos.Target3Price, 0, "", "T3_" + entryName);

                    target3Orders[entryName] = t3Order;
                }

                // NOTE: T4 (runner) has no limit order - it trails with stop

                pos.BracketSubmitted = true;
                pos.CurrentStopPrice = validatedStopPrice;

                // Build bracket summary message with all 4 targets
                StringBuilder bracketMsg = new StringBuilder();
                string tradeType = pos.IsRMATrade ? "RMA" : "OR";
                bracketMsg.AppendFormat("{0} BRACKET V8.0: Stop@{1:F2}", tradeType, validatedStopPrice);
                if (pos.T1Contracts > 0)
                    bracketMsg.AppendFormat(" | T1:{0}@{1:F2}(+{2}pt)", pos.T1Contracts, pos.Target1Price, Target1FixedPoints);
                if (pos.T2Contracts > 0)
                    bracketMsg.AppendFormat(" | T2:{0}@{1:F2}", pos.T2Contracts, pos.Target2Price);
                if (pos.T3Contracts > 0)
                    bracketMsg.AppendFormat(" | T3:{0}@{1:F2}", pos.T3Contracts, pos.Target3Price);
                if (pos.T4Contracts > 0)
                    bracketMsg.AppendFormat(" | T4:{0}@trail", pos.T4Contracts);

                Print(bracketMsg.ToString());
            }
            catch (Exception ex)
            {
                Print("ERROR SubmitBracketOrders: " + ex.Message);
            }
        }

        private void UpdateStopQuantity(string entryName, PositionInfo pos)
        {
            if (!stopOrders.ContainsKey(entryName)) return;
            if (pos.RemainingContracts <= 0) return;
            // V12.41: No trailing/updates before entry fill is confirmed
            if (!pos.EntryFilled) return;

            try
            {
                Order currentStop = stopOrders[entryName];

                // V8.11 FIX: Store pending replacement BEFORE cancelling
                // This ensures we only create a new stop when the old one is confirmed cancelled
                if (currentStop != null && (currentStop.OrderState == OrderState.Working || currentStop.OrderState == OrderState.Accepted))
                {
                    // V8.31: Check if there's already a pending replacement to prevent duplicates
                    if (pendingStopReplacements.ContainsKey(entryName))
                    {
                        // Just update the quantity, don't create a new pending
                        if (pendingStopReplacements.TryGetValue(entryName, out var existingPending))
                        {
                            existingPending.Quantity = pos.RemainingContracts;
                            Print(string.Format("V8.31: Updated existing pending replacement for {0} to {1} contracts", entryName, pos.RemainingContracts));
                        }
                        return;
                    }

                    // Store the replacement info
                    var newPending = new PendingStopReplacement
                    {
                        EntryName = entryName,
                        Quantity = pos.RemainingContracts,
                        StopPrice = pos.CurrentStopPrice,
                        Direction = pos.Direction,
                        OldOrder = currentStop,
                        CreatedTime = DateTime.Now  // V8.31: Added for timeout support
                    };

                    // V8.31: Thread-safe add
                    if (pendingStopReplacements.TryAdd(entryName, newPending))
                    {
                        Interlocked.Increment(ref pendingReplacementCount);
                    }

                    // Cancel old stop - replacement will be created in OnOrderUpdate when confirmed
                    CancelOrder(currentStop);
                    Print(string.Format("STOP CANCEL PENDING: {0} | Will replace with {1} contracts @ {2:F2}",
                        entryName, pos.RemainingContracts, pos.CurrentStopPrice));
                }
                else
                {
                    // No existing stop to cancel, create new one directly
                    // V12.41: Pass the entry name for stricter validation
                    CreateNewStopOrder(entryName, pos.RemainingContracts, pos.CurrentStopPrice, pos.Direction);
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("âš ï¸ ERROR UpdateStopQuantity for {0}: {1}", entryName, ex.Message));
                Print(string.Format("âš ï¸ POSITION MAY BE UNPROTECTED: {0} contracts", pos.RemainingContracts));
            }
        }

        // V8.11: Helper method to create a new stop order
        // V8.31: Added guard to prevent duplicate stop creation
        private void CreateNewStopOrder(string entryName, int quantity, double stopPrice, MarketPosition direction)
        {
            try
            {
                // V12.41 ZOMBIE GUARD: Block stop creation if position is flat or entry not filled
                if (activePositions.TryGetValue(entryName, out var targetPos))
                {
                    if (targetPos.RemainingContracts <= 0)
                    {
                        Print(string.Format("[STOP_GUARD] BLOCKED zombie stop for {0} - Position is FLAT (Remaining=0)", entryName));
                        return;
                    }
                    if (!targetPos.EntryFilled)
                    {
                        Print(string.Format("[STOP_GUARD] BLOCKED early stop for {0} - Fill not yet confirmed", entryName));
                        return;
                    }
                }
                else
                {
                    Print(string.Format("[STOP_GUARD] BLOCKED orphan stop for {0} - No tracking record found", entryName));
                    return;
                }

                // V8.31: Check if a working stop already exists for this entry to prevent duplicates
                if (stopOrders.TryGetValue(entryName, out var existingStop))
                {
                    if (existingStop != null && (existingStop.OrderState == OrderState.Working || existingStop.OrderState == OrderState.Accepted))
                    {
                        Print(string.Format("V8.31: SKIPPING duplicate stop creation for {0} - stop already working", entryName));
                        return;
                    }
                }

                Order newStop = null;
                OrderAction exitAction = direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;

                // V12.3: Route to correct account (fleet follower vs local)
                if (activePositions.TryGetValue(entryName, out var pos) && pos.IsFollower && pos.ExecutingAccount != null)
                {
                    // Fleet follower: use Account API
                    string sigName = "S_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    newStop = pos.ExecutingAccount.CreateOrder(Instrument, exitAction,
                        OrderType.StopMarket, TimeInForce.Gtc, quantity, 0, stopPrice, sigName, sigName, null);
                    pos.ExecutingAccount.Submit(new[] { newStop });
                }
                else
                {
                    // Local: use SubmitOrderUnmanaged with truncated signal name
                    string suffix = (DateTime.Now.Ticks % 100000000).ToString();
                    string sigName = "S_" + entryName + "_" + suffix;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    newStop = SubmitOrderUnmanaged(0, exitAction, OrderType.StopMarket, quantity, 0, stopPrice, "", sigName);
                }

                if (newStop == null)
                {
                    Print(string.Format("âš ï¸ CRITICAL ERROR: Stop order submission returned NULL for {0}!", entryName));
                    Print(string.Format("âš ï¸ POSITION UNPROTECTED: {0} {1} contracts @ {2:F2}",
                        direction == MarketPosition.Long ? "LONG" : "SHORT", quantity, stopPrice));

                    // Attempt to flatten position immediately
                    Print(string.Format("âš ï¸ Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                    return;
                }

                stopOrders[entryName] = newStop;
                Print(string.Format("STOP QTY UPDATED: {0} contracts @ {1:F2} (Order: {2})",
                    quantity, stopPrice, newStop.Name));
            }
            catch (Exception ex)
            {
                Print(string.Format("âš ï¸ ERROR CreateNewStopOrder for {0}: {1}", entryName, ex.Message));
            }
        }

        private double ValidateStopPrice(MarketPosition direction, double desiredStopPrice)
        {
            // V12.41: Use real-time price instead of stale bar Close[0]
            double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
            double tickSize = Instrument.MasterInstrument.TickSize;
            double minDistance = 2 * tickSize;

            if (direction == MarketPosition.Long)
            {
                if (desiredStopPrice >= currentPrice)
                {
                    double validStop = currentPrice - minDistance;
                    Print(string.Format("STOP VALIDATION: Adjusted LONG stop from {0:F2} to {1:F2} (was at/above market)",
                        desiredStopPrice, validStop));
                    return validStop;
                }
            }
            else
            {
                if (desiredStopPrice <= currentPrice)
                {
                    double validStop = currentPrice + minDistance;
                    Print(string.Format("STOP VALIDATION: Adjusted SHORT stop from {0:F2} to {1:F2} (was at/below market)",
                        desiredStopPrice, validStop));
                    return validStop;
                }
            }

            return desiredStopPrice;
        }

        #endregion


        // V12.46: Trailing Stops region moved to Trailing.cs


        #region Position Sync & Flatten

        private void SyncPositionState()
        {
            List<string> toRemove = new List<string>();

            // V8.30: Thread-safe snapshot iteration
            foreach (var kvp in activePositions.ToArray())
            {
                PositionInfo pos = kvp.Value;
                if (pos.EntryFilled && pos.RemainingContracts <= 0)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string key in toRemove)
            {
                CleanupPosition(key);
            }
        }

        /// <summary>
        /// V12 SIMA: Chase If Touch - iterates the unified entryOrders dictionary which contains
        /// BOTH local and fleet follower limit orders. When price touches a working limit,
        /// the order is converted to market so it fills immediately.
        /// Local orders: ChangeOrder(). Follower orders: cancel + resubmit via ExecutingAccount.
        /// </summary>
        private void ManageCIT()
        {
            if (activePositions.Count == 0 && entryOrders.Count == 0) return;
            if (string.IsNullOrEmpty(ChaseIfTouchPoints) || ChaseIfTouchPoints == "0") return;

            double citOffset = 0;
            if (!double.TryParse(ChaseIfTouchPoints, out citOffset)) return;

            // Iterate ALL entry orders in the unified dictionary (local + every fleet account)
            foreach (var kvp in entryOrders.ToArray())
            {
                string key = kvp.Key;
                Order order = kvp.Value;
                if (order == null || order.OrderState != OrderState.Working) continue;
                if (order.OrderType != OrderType.Limit) continue; // only chase limit entries

                double currentPrice = (order.OrderAction == OrderAction.Buy) ? High[0] : Low[0];
                double limitPrice = order.LimitPrice;

                bool triggerChase = (order.OrderAction == OrderAction.Buy)
                    ? (currentPrice >= limitPrice)
                    : (currentPrice <= limitPrice);

                if (!triggerChase) continue;

                // Determine local vs follower
                PositionInfo pos = null;
                activePositions.TryGetValue(key, out pos);
                bool isFollower = pos != null && pos.IsFollower && pos.ExecutingAccount != null;

                try
                {
                    if (isFollower)
                    {
                        // Fleet follower: cancel limit, resubmit as market via account API
                        Account followerAcct = pos.ExecutingAccount;
                        Print($"[CIT] FLEET chase: {key} on {followerAcct.Name} | Limit {limitPrice:F2} -> MKT @ {currentPrice:F2}");

                        followerAcct.Cancel(new[] { order });

                        Order mktOrder = followerAcct.CreateOrder(Instrument, order.OrderAction, OrderType.Market,
                            TimeInForce.Gtc, order.Quantity, 0, 0, "", "CIT_" + key, null);
                        followerAcct.Submit(new[] { mktOrder });

                        entryOrders[key] = mktOrder; // update reference
                    }
                    else
                    {
                        // Local account: ChangeOrder converts limit to market
                        Print($"[CIT] LOCAL chase: {key} | Limit {limitPrice:F2} -> MKT @ {currentPrice:F2}");
                        ChangeOrder(order, order.Quantity, 0, 0);
                    }
                }
                catch (Exception ex)
                {
                    Print($"[CIT] ERROR chasing {key}: {ex.Message}");
                }
            }
        }

        private void FlattenAll()
        {
            isFlattenRunning = true; // V12.13b: Suppress stop re-submit during flatten
            try
            {
                // V10 GHOST FIX: Scan for actual live position even if activePositions is empty
                int liveQty = 0;
                MarketPosition liveDir = MarketPosition.Flat;
                if (Position != null)
                {
                    liveQty = Position.Quantity;
                    liveDir = Position.MarketPosition;
                }

                if (activePositions.Count == 0 && liveQty > 0)
                {
                     Print(string.Format("FLATTEN GHOST: Closing ORPHANED position of {0} contracts", liveQty));
                     if (liveDir == MarketPosition.Long)
                         SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, liveQty, 0, 0, "", "Flatten_Ghost");
                     else
                         SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, liveQty, 0, 0, "", "Flatten_Ghost");

                     return;
                }

                if (activePositions.Count == 0 && Position.MarketPosition == MarketPosition.Flat)
                {
                    Print("FLATTEN: No active positions to close");
                    // Still run SIMA flatten just in case of desync
                    if (EnableSIMA) FlattenAllApexAccounts();
                    return;
                }

                Print("FLATTEN: Closing all positions...");

                // V12.13b: Removed ExitLong/ExitShort block (managed-mode methods incompatible with IsUnmanaged=true)
                // Unmanaged flatten via SubmitOrderUnmanaged is handled below at the per-position level

                // 2. Clear all pending entry orders on Master
                foreach (var entryOrder in entryOrders.Values)
                {
                    if (entryOrder != null && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted))
                        CancelOrder(entryOrder);
                }

                // 3. Flatten SIMA Fleet
                if (EnableSIMA)
                {
                    FlattenAllApexAccounts();
                }

                // V12.2: Reset Sync State
                isLongArmed = false;
                isShortArmed = false;

                List<string> positionsToCleanup = new List<string>();

                // V8.30: Thread-safe snapshot iteration
                foreach (var kvp in activePositions.ToArray())
                {
                    if (!activePositions.ContainsKey(kvp.Key)) continue;
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (pos.EntryFilled)
                    {
                        Print(string.Format("FLATTEN: Closing filled {0} position",
                            pos.Direction == MarketPosition.Long ? "LONG" : "SHORT"));

                        // V8.31: Cancel ALL bracket orders comprehensively
                        // Cancel stop order (may have multiple from rapid trailing)
                        if (stopOrders.TryGetValue(entryName, out var stopOrder))
                        {
                            if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted || stopOrder.OrderState == OrderState.Submitted))
                            {
                                CancelOrder(stopOrder);
                                Print(string.Format("FLATTEN: Cancelling stop for {0}", entryName));
                            }
                        }

                        // V8.31: Also clear any pending stop replacements to prevent orphaned stops
                        if (pendingStopReplacements.TryRemove(entryName, out _))
                        {
                            Interlocked.Decrement(ref pendingReplacementCount);
                            Print(string.Format("V8.31: Cleared pending stop replacement for {0}", entryName));
                        }

                        // Cancel T1 order
                        if (target1Orders.TryGetValue(entryName, out var t1Order))
                        {
                            if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted || t1Order.OrderState == OrderState.Submitted))
                                CancelOrder(t1Order);
                        }

                        // Cancel T2 order
                        if (target2Orders.TryGetValue(entryName, out var t2Order))
                        {
                            if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted || t2Order.OrderState == OrderState.Submitted))
                                CancelOrder(t2Order);
                        }

                        // V8.31: Cancel T3 order
                        if (target3Orders.TryGetValue(entryName, out var t3Order))
                        {
                            if (t3Order != null && (t3Order.OrderState == OrderState.Working || t3Order.OrderState == OrderState.Accepted || t3Order.OrderState == OrderState.Submitted))
                                CancelOrder(t3Order);
                        }

                        // V8.31: Cancel T4 order
                        if (target4Orders.TryGetValue(entryName, out var t4Order))
                        {
                            if (t4Order != null && (t4Order.OrderState == OrderState.Working || t4Order.OrderState == OrderState.Accepted || t4Order.OrderState == OrderState.Submitted))
                                CancelOrder(t4Order);
                        }

                        // V8.28 FIX: Use LIVE position quantity instead of cached RemainingContracts
                        int livePositionQty = 0;
                        try
                        {
                            if (Position != null && Position.MarketPosition != MarketPosition.Flat)
                                livePositionQty = Position.Quantity;
                        }
                        catch (Exception pEx) { Print("Flatten Error reading Position: " + pEx.Message); }

                        // Use the smaller of cached and live to avoid overselling
                        // V10 DIAGNOSTIC: Print values
                        Print(string.Format("FLATTEN DIAGNOSTIC: Entry={0} Cached={1} Live={2}", entryName, pos.RemainingContracts, livePositionQty));

                        // V10 FLATTEN FIX: Trust cached contracts if live is 0 (latency protection)
                        // If cached says we have contracts, we close them.
                        int flattenQty = pos.RemainingContracts;

                        if (livePositionQty > 0)
                        {
                             // If NinjaTrader agrees we have a position, use the smaller to act safe?
                             // No, if real position is smaller, we might be over-closing.
                             // But if real is larger, we under-close.
                             // Let's stick to closing what we know we opened.
                             flattenQty = pos.RemainingContracts;
                        }

                        // Submit market order to close position
                        if (flattenQty > 0)
                        {
                            Order flattenOrder = pos.Direction == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, flattenQty, 0, 0, "", "Flatten_" + entryName)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, flattenQty, 0, 0, "", "Flatten_" + entryName);

                            if (flattenOrder == null) Print("FLATTEN ERROR: SubmitOrderUnmanaged returned NULL");
                            else Print(string.Format("FLATTEN SENT: {0} {1} contracts", pos.Direction == MarketPosition.Long ? "SELL" : "BUY", flattenQty));
                        }
                        else
                        {
                             Print("FLATTEN SKIPPED: Qty is 0");
                        }

                        positionsToCleanup.Add(entryName);
                    }
                    else
                    {
                        // Cancel pending entry order
                        if (entryOrders.ContainsKey(entryName))
                        {
                            Order entryOrder = entryOrders[entryName];
                            if (entryOrder != null && (entryOrder.OrderState == OrderState.Working || entryOrder.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(entryOrder);
                                Print(string.Format("FLATTEN: Cancelled pending {0} entry order @ {1:F2}",
                                    pos.Direction == MarketPosition.Long ? "LONG" : "SHORT", pos.EntryPrice));
                            }
                        }
                        positionsToCleanup.Add(entryName);
                    }
                }

                foreach (string key in positionsToCleanup)
                {
                    CleanupPosition(key);
                }
            }
            catch (Exception ex)
            {
                Print("ERROR FlattenAll: " + ex.Message);
            }
            finally
            {
                isFlattenRunning = false; // V12.13b: Always release guard
            }
        }

        private void FlattenPositionByName(string entryName)
        {
            if (!activePositions.TryGetValue(entryName, out var pos)) return;

            if (pos.EntryFilled && pos.RemainingContracts > 0)
            {
                Print(string.Format("âš ï¸ EMERGENCY FLATTEN: Closing {0} position due to stop order failure", entryName));

                // V12.3: Determine if this is a fleet follower or local position
                bool isFleetFollower = pos.IsFollower && pos.ExecutingAccount != null;

                // V8.31: Cancel ALL bracket orders first to prevent race conditions
                // V12.3: Use Account.Cancel for fleet followers, CancelOrder for local
                if (stopOrders.TryGetValue(entryName, out var stopOrder) && stopOrder != null)
                {
                    if (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { stopOrder });
                        else CancelOrder(stopOrder);
                    }
                }
                if (target1Orders.TryGetValue(entryName, out var t1Order) && t1Order != null)
                {
                    if (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { t1Order });
                        else CancelOrder(t1Order);
                    }
                }
                if (target2Orders.TryGetValue(entryName, out var t2Order) && t2Order != null)
                {
                    if (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { t2Order });
                        else CancelOrder(t2Order);
                    }
                }
                if (target3Orders.TryGetValue(entryName, out var t3Order) && t3Order != null)
                {
                    if (t3Order.OrderState == OrderState.Working || t3Order.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { t3Order });
                        else CancelOrder(t3Order);
                    }
                }
                if (target4Orders.TryGetValue(entryName, out var t4Order) && t4Order != null)
                {
                    if (t4Order.OrderState == OrderState.Working || t4Order.OrderState == OrderState.Accepted)
                    {
                        if (isFleetFollower) pos.ExecutingAccount.Cancel(new[] { t4Order });
                        else CancelOrder(t4Order);
                    }
                }

                // V8.31: Clear pending replacements
                if (pendingStopReplacements.TryRemove(entryName, out _))
                {
                    Interlocked.Decrement(ref pendingReplacementCount);
                }

                int flattenQty = pos.RemainingContracts;
                OrderAction flattenAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;

                // V12.3: Route flatten order to correct account
                Order flattenOrder = null;
                if (isFleetFollower)
                {
                    // Fleet follower: flatten on the follower's own account
                    string sigName = "EF_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    flattenOrder = pos.ExecutingAccount.CreateOrder(Instrument, flattenAction,
                        OrderType.Market, TimeInForce.Gtc, flattenQty, 0, 0, "", sigName, null);
                    pos.ExecutingAccount.Submit(new[] { flattenOrder });
                }
                else
                {
                    // Local: use SubmitOrderUnmanaged (use live position qty for accuracy)
                    try
                    {
                        if (Position != null && Position.MarketPosition != MarketPosition.Flat)
                            flattenQty = Math.Max(flattenQty, Position.Quantity);
                    }
                    catch { }

                    string sigName = "EF_" + entryName;
                    if (sigName.Length > 50) sigName = sigName.Substring(0, 50);
                    flattenOrder = SubmitOrderUnmanaged(0, flattenAction, OrderType.Market, flattenQty, 0, 0, "", sigName);
                }

                if (flattenOrder != null)
                {
                    Print(string.Format("Emergency flatten order submitted on {0}: {1} {2} contracts at MARKET",
                        isFleetFollower ? pos.ExecutingAccount.Name : "LOCAL",
                        pos.Direction == MarketPosition.Long ? "SELL" : "BUY",
                        flattenQty));
                }
                else
                {
                    Print(string.Format("âš ï¸âš ï¸âš ï¸ CRITICAL: Emergency flatten order FAILED for {0}!", entryName));
                    Print("âš ï¸âš ï¸âš ï¸ MANUAL INTERVENTION REQUIRED - Close position manually in NinjaTrader!");
                }
            }
        }


        private void CleanupPosition(string entryName)
        {
            // V8.17 EMERGENCY FIX: Move removal to TOP to prevent recursion
            // V8.30: Use atomic TryRemove for thread-safe removal
            if (!activePositions.TryRemove(entryName, out _)) return;
            SymmetryGuardForgetEntry(entryName);

            int cancelledStops = 0;
            int cancelledTargets = 0;
            int cancelledEntries = 0;

            // V8.17 FIX: Use explicit dictionary-based cancellation instead of scanning ALL Account.Orders
            // V8.30: Use TryRemove for thread-safe atomic removal

            // Cancel stop order
            if (stopOrders.TryRemove(entryName, out var stopOrder))
            {
                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState.ToString().Contains("Pending")))
                {
                    CancelOrder(stopOrder);
                    cancelledStops++;
                }
            }

            // Cancel T1
            if (target1Orders.TryRemove(entryName, out var t1Order))
            {
                if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState.ToString().Contains("Pending")))
                {
                    CancelOrder(t1Order);
                    cancelledTargets++;
                }
            }

            // Cancel T2
            if (target2Orders.TryRemove(entryName, out var t2Order))
            {
                if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState.ToString().Contains("Pending")))
                {
                    CancelOrder(t2Order);
                    cancelledTargets++;
                }
            }

            // Cancel T3
            if (target3Orders.TryRemove(entryName, out var t3Order))
            {
                if (t3Order != null && (t3Order.OrderState == OrderState.Working || t3Order.OrderState.ToString().Contains("Pending")))
                {
                    CancelOrder(t3Order);
                    cancelledTargets++;
                }
            }

            // Cancel T4/Entry
            if (entryOrders.TryRemove(entryName, out var eOrder))
            {
                if (eOrder != null && (eOrder.OrderState == OrderState.Working || eOrder.OrderState.ToString().Contains("Pending")))
                {
                    CancelOrder(eOrder);
                    cancelledEntries++;
                }
            }

            // V8.30: Thread-safe removal with count decrement for pending replacements
            if (pendingStopReplacements.TryRemove(entryName, out _))
            {
                Interlocked.Decrement(ref pendingReplacementCount);
            }
            target4Orders.TryRemove(entryName, out _);

            // Log cleanup summary
            if (cancelledStops > 0 || cancelledTargets > 0 || cancelledEntries > 0)
            {
                Print(string.Format("CLEANUP SUMMARY for {0}: Stops={1} Targets={2} Entries={3}",
                    entryName, cancelledStops, cancelledTargets, cancelledEntries));
            }
        }

        /// <summary>
        /// V12.12: Remove any ghost order reference (targets, stops, entries) when it reaches a terminal state.
        /// This only clears stale references; it does not alter stop quantities or position state.
        /// </summary>
        private void RemoveGhostOrderRef(Order order, string reason)
        {
            if (order == null) return;

            var orderDicts = new (ConcurrentDictionary<string, Order> dict, string label)[]
            {
                (target1Orders, "T1"),
                (target2Orders, "T2"),
                (target3Orders, "T3"),
                (target4Orders, "T4"),
                (stopOrders, "STOP"),
                (entryOrders, "ENTRY"),
            };

            bool foundInDict = false;
            string removedLabel = null;
            string removedKey = null;
            foreach (var (dict, label) in orderDicts)
            {
                // V12.17: Dual match - reference equality OR OrderId string match
                foreach (var kvp in dict.ToArray())
                {
                    if (kvp.Value == order ||
                        (kvp.Value != null && order != null && kvp.Value.OrderId == order.OrderId))
                    {
                        if (dict.TryRemove(kvp.Key, out _))
                        {
                            string matchType = (kvp.Value == order) ? "REF" : "ORDERID";
                            Print(string.Format("[GHOST_FIX] Order {0}_{1} terminated ({2}). Nullifying reference. (match={3}, OrderId={4})",
                                label, kvp.Key, reason, matchType, order.OrderId ?? "NULL"));
                            foundInDict = true;
                            removedLabel = label;
                            removedKey = kvp.Key;
                        }
                    }
                }
            }

            // V12.17: Position protection audit - if we just removed a STOP, check if position is now unprotected
            if (foundInDict && removedLabel == "STOP" && !string.IsNullOrEmpty(removedKey))
            {
                if (activePositions.TryGetValue(removedKey, out var auditPos) && auditPos.EntryFilled && auditPos.RemainingContracts > 0)
                {
                    if (!stopOrders.ContainsKey(removedKey))
                    {
                        Print(string.Format("V12.17: WARNING UNPROTECTED POSITION: {0} has {1} contracts with NO STOP after {2}. Manual intervention may be required.",
                            removedKey, auditPos.RemainingContracts, reason));
                    }
                }
            }

            // V12.17: If it was not in our dictionaries, classify why
            if (!foundInDict)
            {
                // Only log if it is one of our orders (matching prefix) to avoid noise from other strategies
                if (order.Name.Contains("RMA") || order.Name.Contains("OR") || order.Name.Contains("MOMO") || order.Name.Contains("TREND") ||
                    order.Name.Contains("Stop_") || order.Name.Contains("Tgt_") || order.Name.Contains("Fleet_"))
                {
                    // V12.17: Distinguish expected cascade from suspicious orphan
                    bool positionStillActive = false;
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (order.Name.Contains(kvp.Key))
                        {
                            positionStillActive = true;
                            Print(string.Format("V12.17: WARNING {0} {1} - dict ref gone but position {2} still active (orphan risk, OrderId={3})",
                                order.Name, reason, kvp.Key, order.OrderId ?? "NULL"));
                            break;
                        }
                    }
                    if (!positionStillActive)
                    {
                        Print(string.Format("V12.17: {0} {1} - cleaned by upstream handler (expected cascade, OrderId={2})", order.Name, reason, order.OrderId ?? "NULL"));
                    }
                }
            }
        }

        private void ReconcileOrphanedOrders(string reason)
        {
            try
            {
                if (Account == null) return;

                bool foundOrphans = false;
                foreach (Order order in Account.Orders)
                {
                    if (order == null) continue;

                    // Only look at working orders
                    if (order.OrderState != OrderState.Working && order.OrderState != OrderState.Accepted)
                        continue;

                    // V8.27 CRITICAL FIX: Only process orders for THIS instrument
                    // This prevents cross-instrument cancellation when running multiple strategy instances
                    if (order.Instrument.FullName != Instrument.FullName)
                        continue;

                    // Check if this order has one of our prefix signatures
                    string name = order.Name;
                    if (name.StartsWith("Stop_") || name.StartsWith("T1_") || name.StartsWith("T2_") ||
                        name.StartsWith("T3_") || name.StartsWith("T4_") || name.StartsWith("Flatten_") || name.StartsWith("Trim_"))
                    {
                        // Check if we actually have an active position for this
                        string entryName = "";
                        if (name.Contains("_"))
                        {
                            int firstUnderscore = name.IndexOf('_');
                            entryName = name.Substring(firstUnderscore + 1);
                            // Strip timestamp if present
                            int lastUnderscore = entryName.LastIndexOf('_');
                            if (lastUnderscore > 0 && entryName.Length - lastUnderscore > 10)
                                entryName = entryName.Substring(0, lastUnderscore);
                        }

                        // V10 FIX: Handle TRIM execution state update - MOVED TO OnExecutionUpdate

                        if (string.IsNullOrEmpty(entryName) || !activePositions.ContainsKey(entryName))
                        {
                            Print(string.Format("ORPHANED ORDER DETECTED ({0}): {1} | Cancelling...", reason, name));
                            CancelOrder(order);
                            foundOrphans = true;
                        }
                    }
                }

                // === V12.18 REVERSE AUDIT: Strategy → Broker ===
                // For each tracked order ref, verify it still exists as Working/Accepted
                // in the broker's order collection. If it doesn't, it's a ghost — purge it.
                Print(string.Format("[GHOST_FIX] REVERSE AUDIT START ({0})", reason));
                int reverseGhosts = 0;

                // Build a HashSet of live broker OrderIds for O(1) lookup
                HashSet<string> liveBrokerOrderIds = new HashSet<string>();
                foreach (Order brokerOrder in Account.Orders)
                {
                    if (brokerOrder != null && !string.IsNullOrEmpty(brokerOrder.OrderId) &&
                        (brokerOrder.OrderState == OrderState.Working || brokerOrder.OrderState == OrderState.Accepted))
                    {
                        liveBrokerOrderIds.Add(brokerOrder.OrderId);
                    }
                }

                // Also scan fleet accounts if SIMA is enabled
                if (EnableSIMA)
                {
                    foreach (Account acct in Account.All)
                    {
                        if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foreach (Order fleetOrder in acct.Orders)
                            {
                                if (fleetOrder != null && !string.IsNullOrEmpty(fleetOrder.OrderId) &&
                                    (fleetOrder.OrderState == OrderState.Working || fleetOrder.OrderState == OrderState.Accepted))
                                {
                                    liveBrokerOrderIds.Add(fleetOrder.OrderId);
                                }
                            }
                        }
                    }
                }

                // Check all strategy order dictionaries against live broker orders
                var reverseCheckDicts = new (ConcurrentDictionary<string, Order> dict, string label)[]
                {
                    (stopOrders, "STOP"), (target1Orders, "T1"), (target2Orders, "T2"),
                    (target3Orders, "T3"), (target4Orders, "T4"), (entryOrders, "ENTRY"),
                };

                foreach (var (dict, label) in reverseCheckDicts)
                {
                    foreach (var kvp in dict.ToArray())
                    {
                        Order trackedOrder = kvp.Value;
                        if (trackedOrder == null) continue;

                        // Only audit orders that SHOULD be alive (Working/Accepted)
                        // Terminal orders are cleaned by OnOrderUpdate; this catches leaks
                        bool isTerminal = (trackedOrder.OrderState == OrderState.Cancelled ||
                                           trackedOrder.OrderState == OrderState.Rejected ||
                                           trackedOrder.OrderState == OrderState.Filled ||
                                           trackedOrder.OrderState == OrderState.Unknown);

                        bool notInBroker = !string.IsNullOrEmpty(trackedOrder.OrderId) &&
                                           !liveBrokerOrderIds.Contains(trackedOrder.OrderId);

                        if (isTerminal || notInBroker)
                        {
                            if (dict.TryRemove(kvp.Key, out _))
                            {
                                string state = trackedOrder.OrderState.ToString();
                                Print(string.Format("[GHOST_FIX] REVERSE AUDIT: {0} ghost for {1} purged (State={2}, InBroker={3}, OrderId={4})",
                                    label, kvp.Key, state, !notInBroker, trackedOrder.OrderId ?? "NULL"));
                                reverseGhosts++;
                            }
                        }
                    }
                }

                Print(string.Format("[GHOST_FIX] REVERSE AUDIT COMPLETE: {0} ghosts purged", reverseGhosts));

                if (foundOrphans || reverseGhosts > 0)
                    Print("Orphaned order reconciliation complete.");
            }
            catch (Exception ex)
            {
                Print("ERROR ReconcileOrphanedOrders: " + ex.Message);
            }
        }

        #endregion
    }
}
