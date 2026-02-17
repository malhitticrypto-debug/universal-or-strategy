// V12.44 MODULAR: Order Callbacks Module (Split from Orders.cs)
// Contains: OnOrderUpdate, OnAccountOrderUpdate, OnPositionUpdate, OnExecutionUpdate
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
        #region Order Callbacks

        protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice,
            int quantity, int filled, double averageFillPrice, OrderState orderState,
            DateTime time, ErrorCode error, string nativeError)
        {
            try
            {
                string orderName = order.Name;

                // Entry filled
                if (entryOrders.Values.Contains(order) && orderState == OrderState.Filled)
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        string entryName = kvp.Key;
                        PositionInfo pos = kvp.Value;

                        // V8.30: Verify position still exists
                        if (!activePositions.ContainsKey(entryName)) continue;

                        // V8.30: Thread-safe check
                        if (entryOrders.TryGetValue(entryName, out var entryOrder) && entryOrder == order && !pos.EntryFilled)
                        {
                            pos.EntryFilled = true;
                            if (!pos.IsFollower)
                            {
                                int masterFillQty = filled > 0 ? filled : quantity;
                                SymmetryGuardOnMasterFill(entryName, pos, averageFillPrice, masterFillQty, time.ToUniversalTime());
                            }

                            // Store intended entry price for slippage calculation
                            double intendedEntryPrice = pos.EntryPrice;

                            // V12.18 PRICE INTEGRITY: Guard against zero fill price (NT8 race condition)
                            if (averageFillPrice <= 0)
                            {
                                Print(string.Format("[PRICE_GUARD] CRITICAL: averageFillPrice={0} for {1}. Keeping intended price {2:F2}. NOT re-anchoring.",
                                    averageFillPrice, entryName, intendedEntryPrice));
                                SubmitBracketOrders(entryName, pos);
                                continue;  // Skip all price re-anchoring, use intended prices
                            }

                            string tradeType = pos.IsRMATrade ? "RMA" : "OR";
                            if (pos.IsMOMOTrade) tradeType = "MOMO"; // V8.22: Logging
                            if (pos.IsFFMATrade) tradeType = "FFMA";
                            if (pos.IsTRENDTrade) tradeType = "TREND";
                            if (pos.IsRetestTrade) tradeType = "RETEST";

                            Print(string.Format("{0} ENTRY FILLED: {1} {2} @ {3:F2} (intended: {4:F2})",
                                tradeType,
                                pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                                pos.TotalContracts,
                                averageFillPrice,
                                intendedEntryPrice));

                            // V8.22: UNIVERSAL STOP CAP FIX
                            // Determine the intended stop distance
                            double stopDistance = 0;

                            if (pos.IsRMATrade)
                            {
                                // For RMA, use current ATR to be precise
                                Print(string.Format("ðŸ" DIAGNOSTIC: RMA Entry Filled. Raw ATR used: {0:F4} | Multiplier: {1:F2} | Calc Stop: {2:F4} pts",
                                    currentATR, RMAStopATRMultiplier, currentATR * RMAStopATRMultiplier));
                                stopDistance = currentATR * RMAStopATRMultiplier;

                                // Recalculate RMA targets based on fill
                                // v5.13 FIX: T1 uses FIXED points, T2/T3 use ATR
                                double t2Distance = currentATR * RMAT1ATRMultiplier;  // 0.5x ATR
                                double t3Distance = currentATR * RMAT2ATRMultiplier;  // 1.0x ATR

                                // T1 = Fixed 1pt (NOT ATR-based)
                                pos.Target1Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + Target1FixedPoints
                                    : averageFillPrice - Target1FixedPoints;
                                // T2 = 0.5x ATR
                                pos.Target2Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + t2Distance
                                    : averageFillPrice - t2Distance;
                                // T3 = 1.0x ATR
                                pos.Target3Price = pos.Direction == MarketPosition.Long
                                    ? averageFillPrice + t3Distance
                                    : averageFillPrice - t3Distance;
                            }
                            else
                            {
                                // For other trades, use the distance from the intended setup
                                stopDistance = Math.Abs(pos.InitialStopPrice - intendedEntryPrice);
                            }

                            // GLOBAL SAFETY CAP: Absolutely NO stop > 8.0 points
                            double originalDist = stopDistance;
                            stopDistance = Math.Min(stopDistance, 12.0);

                            if (stopDistance < originalDist)
                            {
                                Print(string.Format("CRITICAL: {0} Stop capped at 12.0 pts (Calculated: {1:F2} pts)",
                                    tradeType, originalDist));
                            }

                            // Re-anchor stop to ACTUAL fill price
                            pos.InitialStopPrice = pos.Direction == MarketPosition.Long
                                ? averageFillPrice - stopDistance
                                : averageFillPrice + stopDistance;
                            pos.CurrentStopPrice = pos.InitialStopPrice;

                            if (Math.Abs(averageFillPrice - intendedEntryPrice) > tickSize)
                            {
                                Print(string.Format("{0} PRICES ADJUSTED for fill slippage: Stop={1:F2} (Dist={2:F2})",
                                    tradeType, pos.InitialStopPrice, stopDistance));
                            }

                            // Update to actual fill price
                            pos.EntryPrice = averageFillPrice;
                            pos.ExtremePriceSinceEntry = averageFillPrice;

                            SubmitBracketOrders(entryName, pos);
                        }
                    }
                }

                // Target 1 filled
                if (target1Orders.Values.Contains(order) && orderState == OrderState.Filled)
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        if (target1Orders.TryGetValue(kvp.Key, out var t1Order) && t1Order == order)
                        {
                            PositionInfo pos = kvp.Value;
                            // V12.1101E [SK-01/A-1]: First-Writer-Wins guard — prevents double-decrement
                            // if OnOrderUpdate + OnExecutionUpdate both fire for the same T1 fill.
                            bool alreadyProcessed;
                            lock (stateLock)
                            {
                                alreadyProcessed = pos.T1Filled;
                                if (!alreadyProcessed)
                                {
                                    pos.T1Filled = true;
                                    pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - pos.T1Contracts);
                                }
                            }
                            if (alreadyProcessed)
                            {
                                Print(string.Format("[1101E GUARD] T1 already processed for {0} — skipping duplicate OnOrderUpdate fill", kvp.Key));
                                break;
                            }
                            // V8.11: Added entry name to logging
                            Print(string.Format("T1 FILLED ({0}): {1} contracts @ {2:F2} | Remaining: {3}",
                                kvp.Key, pos.T1Contracts, averageFillPrice, pos.RemainingContracts));

                            // Update stop quantity
                            UpdateStopQuantity(kvp.Key, pos);
                            break;
                        }
                    }
                }

                // Target 2 filled
                if (target2Orders.Values.Contains(order) && orderState == OrderState.Filled)
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        if (target2Orders.TryGetValue(kvp.Key, out var t2Order) && t2Order == order)
                        {
                            PositionInfo pos = kvp.Value;
                            // V12.1101E [SK-01/A-1]: First-Writer-Wins guard — T2
                            bool alreadyProcessed;
                            lock (stateLock)
                            {
                                alreadyProcessed = pos.T2Filled;
                                if (!alreadyProcessed)
                                {
                                    pos.T2Filled = true;
                                    pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - pos.T2Contracts);
                                }
                            }
                            if (alreadyProcessed)
                            {
                                Print(string.Format("[1101E GUARD] T2 already processed for {0} — skipping duplicate OnOrderUpdate fill", kvp.Key));
                                break;
                            }
                            // V8.11: Added entry name to logging
                            Print(string.Format("T2 FILLED ({0}): {1} contracts @ {2:F2} | Remaining: {3}",
                                kvp.Key, pos.T2Contracts, averageFillPrice, pos.RemainingContracts));

                            // Update stop quantity
                            UpdateStopQuantity(kvp.Key, pos);
                            break;
                        }
                    }
                }

                // v5.13: Target 3 filled
                if (target3Orders.Values.Contains(order) && orderState == OrderState.Filled)
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        if (target3Orders.TryGetValue(kvp.Key, out var t3Order) && t3Order == order)
                        {
                            PositionInfo pos = kvp.Value;
                            // V12.1101E [SK-01/A-1]: First-Writer-Wins guard — T3
                            bool alreadyProcessed;
                            lock (stateLock)
                            {
                                alreadyProcessed = pos.T3Filled;
                                if (!alreadyProcessed)
                                {
                                    pos.T3Filled = true;
                                    pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - pos.T3Contracts);
                                }
                            }
                            if (alreadyProcessed)
                            {
                                Print(string.Format("[1101E GUARD] T3 already processed for {0} — skipping duplicate OnOrderUpdate fill", kvp.Key));
                                break;
                            }
                            // V8.11: Added entry name to logging
                            Print(string.Format("T3 FILLED ({0}): {1} contracts @ {2:F2} | Remaining: {3} (T4 runner)",
                                kvp.Key, pos.T3Contracts, averageFillPrice, pos.RemainingContracts));

                            // Update stop quantity - only T4 runner remains
                            UpdateStopQuantity(kvp.Key, pos);
                            break;
                        }
                    }
                }

                // Stop filled - position closed
                // V8.2 FIX: Check both by object reference AND by order name prefix
                // This handles trailed stops that have DateTime.Ticks suffix in their name
                if (orderState == OrderState.Filled && orderName.StartsWith("Stop_"))
                {
                    // Try exact object match first
                    bool foundByReference = false;
                    if (stopOrders.Values.Contains(order))
                    {
                        // V8.30: Thread-safe snapshot iteration
                        foreach (var kvp in activePositions.ToArray())
                        {
                            if (!activePositions.ContainsKey(kvp.Key)) continue;
                            if (stopOrders.TryGetValue(kvp.Key, out var stopOrder) && stopOrder == order)
                            {
                                PositionInfo pos = kvp.Value;
                                Print(string.Format("STOP FILLED: {0} contracts @ {1:F2}", pos.RemainingContracts, averageFillPrice));
                                CleanupPosition(kvp.Key);
                                foundByReference = true;
                                break;
                            }
                        }
                    }

                    // V8.2 FIX: Fallback - match by order name prefix
                    // Order name format: "Stop_TREND_175232_E2_12345678" - extract "TREND_175232_E2"
                    if (!foundByReference)
                    {
                        // Extract entry name from stop order name (removes "Stop_" prefix and optional "_timestamp" suffix)
                        string stopPrefix = "Stop_";
                        string entryNameFromOrder = orderName.Substring(stopPrefix.Length);
                        // Remove timestamp suffix if present (format: _123456789012345)
                        int lastUnderscore = entryNameFromOrder.LastIndexOf('_');
                        if (lastUnderscore > 0 && entryNameFromOrder.Length - lastUnderscore > 10)
                        {
                            entryNameFromOrder = entryNameFromOrder.Substring(0, lastUnderscore);
                        }

                        // V8.30: Thread-safe access
                        if (activePositions.TryGetValue(entryNameFromOrder, out var pos))
                        {
                            Print(string.Format("STOP FILLED (by name): {0} contracts @ {1:F2}", pos.RemainingContracts, averageFillPrice));
                            CleanupPosition(entryNameFromOrder);
                        }
                    }
                }

                // Order rejected
                if (orderState == OrderState.Rejected)
                {
                    Print(string.Format("ORDER REJECTED: {0} | Error: {1}", orderName, nativeError));

                    // CRITICAL v5.8: Check if this was a stop order rejection
                    if (stopOrders.Values.Contains(order))
                    {
                        Print(string.Format("âš ï¸ CRITICAL: Stop order REJECTED: {0}", orderName));

                        // V8.30: Thread-safe snapshot iteration
                        foreach (var kvp in activePositions.ToArray())
                        {
                            if (!activePositions.ContainsKey(kvp.Key)) continue;
                            if (stopOrders.TryGetValue(kvp.Key, out var stopOrder) && stopOrder == order)
                            {
                                PositionInfo pos = kvp.Value;
                                Print(string.Format("âš ï¸ Position {0} is UNPROTECTED: {1} {2} contracts @ {3:F2}",
                                    kvp.Key,
                                    pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                                    pos.RemainingContracts,
                                    pos.EntryPrice));

                                // V12.12: Remove stale rejected stop, then re-submit directly
                                // Cannot use UpdateStopQuantity â€" it early-exits if stopOrders is empty (line 3044)
                                // and the cancel-replace flow doesn't apply to a rejected (non-working) order.
                                Print(string.Format("Attempting to re-submit stop for {0}...", kvp.Key));
                                stopOrders.TryRemove(kvp.Key, out _);
                                CreateNewStopOrder(kvp.Key, pos.RemainingContracts, pos.CurrentStopPrice, pos.Direction);
                                break;
                            }
                        }
                    }

                    // V12.12: Target order rejected - remove stale reference from dictionary
                    Print(string.Format("[GHOST_FIX] Order {0} terminated (REJECTED). Nullifying reference.", orderName));
                    RemoveGhostOrderRef(order, "REJECTED");
                }

                // V12: Entry order price changed
                // This detects when user drags the order line to a new price
                if (entryOrders.Values.Contains(order) && (orderState == OrderState.Accepted || orderState == OrderState.Working))
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        string entryName = kvp.Key;
                        PositionInfo pos = kvp.Value;

                        // V8.30: Thread-safe check
                        if (entryOrders.TryGetValue(entryName, out var entryOrd) && entryOrd == order && !pos.EntryFilled)
                        {
                            // Get the new price from the order (limit orders use limitPrice, stop orders use stopPrice)
                            double newPrice = limitPrice > 0 ? limitPrice : stopPrice;

                            // V12.18 PRICE INTEGRITY: Guard against zero price from NT8 race condition
                            if (newPrice <= 0)
                            {
                                Print(string.Format("[PRICE_GUARD] Entry-moved: newPrice={0} for {1}. Skipping re-anchor.", newPrice, entryName));
                                break;
                            }

                            // Check if price changed (with tick tolerance)
                            if (Math.Abs(newPrice - pos.EntryPrice) > tickSize * 0.5)
                            {
                                double oldPrice = pos.EntryPrice;
                                pos.EntryPrice = newPrice;

                                Print(string.Format("V12: Entry order MOVED: {0} | {1:F2} â†' {2:F2}", entryName, oldPrice, newPrice));

                                // V12 SIMA: Legacy slave broadcast removed
                            }
                            break;
                        }
                    }
                }

                // V12.13: Coordination flag â€" prevents redundant ghost-scan after explicit handling
                bool handledByExplicitCleanup = false;

                // V8.11: Stop order cancelled - check for pending replacement
                // V12.13: Extended to also match "S_" prefix (replacement stops from CreateNewStopOrder)
                // V14 GHOST FIX: Added check for RemainingContracts > 0 to prevent ghost stop resurrection
                if ((orderName.StartsWith("Stop_") || orderName.StartsWith("S_")) && orderState == OrderState.Cancelled)
                {
                    // V8.30: Thread-safe snapshot iteration with TryRemove
                    foreach (var kvp in pendingStopReplacements.ToArray())
                    {
                        string entryName = kvp.Key;
                        PendingStopReplacement pending = kvp.Value;

                        // V8.24 FIX: REMOVED recursive 'Contains' check. STRICT object match only.
                        if (activePositions.TryGetValue(entryName, out var pos) && pending.OldOrder == order)
                        {
                            // V14 GHOST FIX: CRITICAL CHECK
                            // Only create replacement if we actually hold a position!
                            if (pos.RemainingContracts > 0)
                            {
                                Print(string.Format("STOP CANCELLED (confirmed): {0} | Creating replacement...", entryName));

                                // Create the replacement stop
                                CreateNewStopOrder(entryName, pending.Quantity, pending.StopPrice, pending.Direction);
                            }
                            else
                            {
                                Print(string.Format("V14 GHOST FIX: Stop cancelled for FLAT position {0}. Discarding replacement.", entryName));
                            }

                            // V8.30: Thread-safe removal with count decrement
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                            {
                                Interlocked.Decrement(ref pendingReplacementCount);
                            }
                            handledByExplicitCleanup = true;
                            break;
                        }
                        else if (!activePositions.ContainsKey(entryName))
                        {
                            Print(string.Format("STOP CANCELLED: {0} ignored (position already closed/cleaned)", entryName));
                            // V8.30: Thread-safe removal with count decrement
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                            {
                                Interlocked.Decrement(ref pendingReplacementCount);
                            }
                            handledByExplicitCleanup = true;
                            break;
                        }
                    }
                }

                // V12.13c: Manual stop cancel â€" clean up dictionary ref, no auto re-submission
                // User can cancel their own stops; position becomes unprotected by design
                if (!handledByExplicitCleanup && orderState == OrderState.Cancelled &&
                    (orderName.StartsWith("Stop_") || orderName.StartsWith("S_")))
                {
                    foreach (var kvp in stopOrders.ToArray())
                    {
                        if (kvp.Value == order)
                        {
                            string entryName = kvp.Key;
                            if (stopOrders.TryRemove(entryName, out _))
                            {
                                Print(string.Format("[GHOST_FIX] Order Stop_{0} terminated (CANCELLED). Nullifying reference.", entryName));
                                handledByExplicitCleanup = true;
                            }
                            break;
                        }
                    }
                }

                // V12.13: Manual target cancel â€" user cancelled target from chart
                if (!handledByExplicitCleanup && orderState == OrderState.Cancelled)
                {
                    var targetDicts = new (ConcurrentDictionary<string, Order> dict, string label)[]
                    {
                        (target1Orders, "T1"), (target2Orders, "T2"),
                        (target3Orders, "T3"), (target4Orders, "T4"),
                    };
                    foreach (var (dict, label) in targetDicts)
                    {
                        foreach (var kvp in dict.ToArray())
                        {
                            if (kvp.Value == order)
                            {
                                if (dict.TryRemove(kvp.Key, out _))
                                {
                                    Print(string.Format("[GHOST_FIX] Order {0}_{1} terminated (CANCELLED). Nullifying reference.", label, kvp.Key));
                                    handledByExplicitCleanup = true;
                                }
                                break;
                            }
                        }
                        if (handledByExplicitCleanup) break;
                    }
                }

                // V12: Entry order cancelled
                if (entryOrders.Values.Contains(order) && orderState == OrderState.Cancelled)
                {
                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        string entryName = kvp.Key;
                        PositionInfo pos = kvp.Value;

                        if (entryOrders.TryGetValue(entryName, out var entryOrder) && entryOrder == order && !pos.EntryFilled)
                        {
                            Print(string.Format("[GHOST_FIX] Order Entry_{0} terminated (CANCELLED). Nullifying reference. Full teardown.", entryName));

                            // Clean up local state
                            CleanupPosition(entryName);
                            handledByExplicitCleanup = true;
                            break;
                        }
                    }
                }

                // V12.13: Terminal catch-all â€" ONLY fires if no explicit handler above already cleaned this order
                if (!handledByExplicitCleanup &&
                    (orderState == OrderState.Cancelled || orderState == OrderState.Rejected ||
                     orderState == OrderState.Unknown))
                {
                    string reason = orderState.ToString().ToUpper();
                    RemoveGhostOrderRef(order, reason);
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnOrderUpdate: " + ex.Message);
            }
        }

        private void OnAccountOrderUpdate(object sender, OrderEventArgs e)
        {
            try
            {
                if (e == null || e.Order == null) return;

                Order order = e.Order;
                if (order.Instrument != null && order.Instrument.FullName != Instrument.FullName) return;

                if (order.OrderState != OrderState.Cancelled && order.OrderState != OrderState.Rejected &&
                    order.OrderState != OrderState.Unknown)
                {
                    return;
                }

                Account acct = sender as Account;
                string acctName = acct != null ? acct.Name : "UNKNOWN";
                string reason = order.OrderState.ToString().ToUpper();
                string orderId = order.OrderId ?? "NULL";

                // V12.17: Enhanced trace logging
                Print(string.Format("[GHOST-AUDIT] OnAccountOrderUpdate ENTRY: Name={0} | Id={1} | State={2} | Acct={3}",
                    order.Name, orderId, reason, acctName));

                // V12.17: Match by reference OR by OrderId (NT8 may pass a different object for the same logical order)
                string matchedEntry = null;
                string matchedBy = "NONE";
                foreach (var kvp in activePositions.ToArray())
                {
                    if (!activePositions.ContainsKey(kvp.Key)) continue;
                    PositionInfo pos = kvp.Value;
                    if (!pos.IsFollower || pos.ExecutingAccount == null) continue;
                    if (acct != null && pos.ExecutingAccount != acct) continue;

                    // V12.17: Dual match - reference equality OR OrderId string match
                    if ((entryOrders.TryGetValue(kvp.Key, out var eOrder) && (eOrder == order || (eOrder != null && eOrder.OrderId == order.OrderId))) ||
                        (stopOrders.TryGetValue(kvp.Key, out var sOrder) && (sOrder == order || (sOrder != null && sOrder.OrderId == order.OrderId))) ||
                        (target1Orders.TryGetValue(kvp.Key, out var t1Order) && (t1Order == order || (t1Order != null && t1Order.OrderId == order.OrderId))) ||
                        (target2Orders.TryGetValue(kvp.Key, out var t2Order) && (t2Order == order || (t2Order != null && t2Order.OrderId == order.OrderId))) ||
                        (target3Orders.TryGetValue(kvp.Key, out var t3Order) && (t3Order == order || (t3Order != null && t3Order.OrderId == order.OrderId))) ||
                        (target4Orders.TryGetValue(kvp.Key, out var t4Order) && (t4Order == order || (t4Order != null && t4Order.OrderId == order.OrderId))))
                    {
                        matchedEntry = kvp.Key;
                        matchedBy = "DUAL";
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(matchedEntry) && activePositions.TryGetValue(matchedEntry, out var matchedPos))
                {
                    Print(string.Format("[GHOST-AUDIT] MATCHED: Entry={0} | MatchBy={1} | Acct={2}", matchedEntry, matchedBy, acctName));

                    if (matchedPos.IsFollower && matchedPos.ExecutingAccount != null)
                    {
                        if (entryOrders.TryGetValue(matchedEntry, out var entryOrder) &&
                            (entryOrder == order || (entryOrder != null && entryOrder.OrderId == order.OrderId)) &&
                            !matchedPos.EntryFilled)
                        {
                            Print(string.Format("[SIMA] Follower entry terminal: {0} on {1} ({2}) - tearing down", matchedEntry, acctName, reason));
                            CleanupPosition(matchedEntry);
                            return;
                        }

                        Print(string.Format("[SIMA] Follower order terminal: {0} on {1} ({2}) | Id={3}", order.Name, acctName, reason, orderId));
                        RemoveGhostOrderRef(order, reason);
                        return;
                    }
                }
                else
                {
                    Print(string.Format("[GHOST-AUDIT] NO MATCH in activePositions for OrderId={0} Name={1} on {2}", orderId, order.Name, acctName));

                    // V12.18 SIMA CASCADE: If a master-account order was cancelled,
                    // check if any follower positions share the same base signal and tear them down.
                    if (EnableSIMA && order.OrderState == OrderState.Cancelled)
                    {
                        string orderSignal = order.Name;
                        foreach (var kvp in activePositions.ToArray())
                        {
                            PositionInfo cascadePos = kvp.Value;
                            if (!cascadePos.IsFollower) continue;
                            // Match if the follower's signal name contains the order's signal
                            if (kvp.Key.Contains(orderSignal) || orderSignal.Contains(kvp.Key))
                            {
                                // Only tear down if the follower's entry hasn't filled yet
                                if (!cascadePos.EntryFilled)
                                {
                                    Print(string.Format("[GHOST_FIX] SIMA CASCADE: Master cancel of {0} triggers follower teardown for {1} on {2}",
                                        orderSignal, kvp.Key, cascadePos.ExecutingAccount != null ? cascadePos.ExecutingAccount.Name : "NULL"));
                                    CleanupPosition(kvp.Key);
                                }
                            }
                        }
                    }
                }

                // Fallback: clear any stale reference for terminal follower order states
                RemoveGhostOrderRef(order, reason);
            }
            catch (Exception ex)
            {
                Print("ERROR OnAccountOrderUpdate: " + ex.Message);
            }
        }

        protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
        {
            try
            {
                // Check for EXTERNAL close (position went flat from outside strategy)
                if (marketPosition == MarketPosition.Flat)
                {
                    // V8.22: Even if activePositions is empty (strategy restart), we should scan for orphans
                    if (activePositions.Count == 0)
                    {
                        Print("EXTERNAL CLOSE/RESTART DETECTED - Scanning for orphaned bracket orders...");
                        ReconcileOrphanedOrders("Position went flat");
                        return;
                    }

                    // Check if we still have any positions that think they're filled
                    List<string> positionsToCleanup = new List<string>();

                    // V8.30: Thread-safe snapshot iteration
                    foreach (var kvp in activePositions.ToArray())
                    {
                        if (!activePositions.ContainsKey(kvp.Key)) continue;
                        PositionInfo pos = kvp.Value;
                        if (pos.EntryFilled && pos.RemainingContracts > 0)
                        {
                            Print("EXTERNAL CLOSE DETECTED - Position went flat. Cancelling orphaned orders...");

                            // V8.30: Thread-safe order access
                            if (stopOrders.TryGetValue(kvp.Key, out var stopOrder))
                            {
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                }
                            }

                            // Cancel orphaned target orders
                            if (target1Orders.TryGetValue(kvp.Key, out var t1Order))
                            {
                                if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(t1Order);
                                }
                            }

                            if (target2Orders.TryGetValue(kvp.Key, out var t2Order))
                            {
                                if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(t2Order);
                                }
                            }

                            // v5.13: Cancel T3/T4 orphaned orders
                            if (target3Orders.TryGetValue(kvp.Key, out var t3Order))
                            {
                                if (t3Order != null && (t3Order.OrderState == OrderState.Working || t3Order.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(t3Order);
                                }
                            }

                            positionsToCleanup.Add(kvp.Key);
                        }
                    }

                    // REMOVED v5.7: DO NOT cancel unrelated pending entry orders!
                    // The old logic here cancelled ALL pending entries when position went flat,
                    // which incorrectly cancelled opposite-side OR entries (e.g., ORShort when ORLong closed)
                    // Pending entries should remain active - they are independent trades!

                    // Clean up positions
                    foreach (string key in positionsToCleanup)
                    {
                        CleanupPosition(key);
                    }

                    if (positionsToCleanup.Count > 0)
                    {
                        Print("Cleanup complete - Strategy still running, ready for new entries.");
                    }
                }

                // V14 ADAPTIVE VISIBILITY: Broadcast current position size to panel
                // This allows the panel to hide/show T1-T5 buttons based on trade size
                // V14 FIX: Use State check instead of Connection.Status to avoid CS0120
                if (State == State.Realtime)
                {
                    int totalQuantity = 0;
                    if (Position != null) totalQuantity = Position.Quantity;

                    // V14 FIX: Use SendResponseToRemote (from UI.cs) instead of direct panel reference
                    // This works because the Panel is a TCP Client connected to this strategy
                    SendResponseToRemote($"SYNC_TARGET_STATE|{totalQuantity}");
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnPositionUpdate: " + ex.Message);
            }
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            try
            {
                if (execution == null || execution.Order == null) return;

                string orderName = execution.Order.Name;
                if (string.IsNullOrEmpty(orderName)) return;

                // V12.Hardening: Dedup guard — prevent double-decrement if OnOrderUpdate + OnExecutionUpdate both fire for same fill
                if (!string.IsNullOrEmpty(executionId))
                {
                    lock (executionDeduplicateLock)
                    {
                        if (!processedExecutionIds.Add(executionId))
                        {
                            Print(string.Format("[DEDUP] Skipping duplicate execution {0} for {1}", executionId, orderName));
                            return;
                        }
                        // Bounded pruning: keep at most MaxProcessedExecutionIds entries
                        processedExecutionIdQueue.Enqueue(executionId);
                        while (processedExecutionIdQueue.Count > MaxProcessedExecutionIds)
                            processedExecutionIds.Remove(processedExecutionIdQueue.Dequeue());
                    }
                }

                // V12.12: Compliance tracking for single-account mode
                if (EnableComplianceHub && !EnableSIMA)
                {
                    TrackTradeEntry(Account, execution);
                    UpdateAccountMetricsFromAccount(Account);
                    LogApexPerformance();
                }

                // Helper: Extract entry name from order name (removes prefix and optional timestamp suffix)
                Func<string, string, string> extractEntryName = (name, prefix) =>
                {
                    if (!name.StartsWith(prefix)) return "";
                    string entryPart = name.Substring(prefix.Length);
                    // Strip timestamp suffix if present (format: _123456789012345)
                    int lastUnderscore = entryPart.LastIndexOf('_');
                    if (lastUnderscore > 0 && entryPart.Length - lastUnderscore > 10)
                        entryPart = entryPart.Substring(0, lastUnderscore);
                    return entryPart;
                };

                // ============================================================
                // 1. STOP LOSS FILL - Manual OCO: Cancel all remaining targets
                // ============================================================
                if (orderName.StartsWith("Stop_"))
                {
                    string entryName = extractEntryName(orderName, "Stop_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        // Decrement RemainingContracts by the filled quantity
                        pos.RemainingContracts -= quantity;

                        Print(string.Format("STOP FILLED: {0} @ {1:F2}. Cancelling targets.", quantity, price));

                        // Manual OCO: Cancel all remaining profit targets immediately
                        int cancelledTargets = 0;

                        // Cancel T1
                        if (target1Orders.TryRemove(entryName, out var t1Order))
                        {
                            if (t1Order != null && (t1Order.OrderState == OrderState.Working || t1Order.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(t1Order);
                                cancelledTargets++;
                            }
                        }

                        // Cancel T2
                        if (target2Orders.TryRemove(entryName, out var t2Order))
                        {
                            if (t2Order != null && (t2Order.OrderState == OrderState.Working || t2Order.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(t2Order);
                                cancelledTargets++;
                            }
                        }

                        // Cancel T3
                        if (target3Orders.TryRemove(entryName, out var t3Order))
                        {
                            if (t3Order != null && (t3Order.OrderState == OrderState.Working || t3Order.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(t3Order);
                                cancelledTargets++;
                            }
                        }

                        // Cancel T4 if present
                        if (target4Orders.TryRemove(entryName, out var t4Order))
                        {
                            if (t4Order != null && (t4Order.OrderState == OrderState.Working || t4Order.OrderState == OrderState.Accepted))
                            {
                                CancelOrder(t4Order);
                                cancelledTargets++;
                            }
                        }

                        if (cancelledTargets > 0)
                        {
                            Print(string.Format("OCO: Cancelled {0} target orders for {1}", cancelledTargets, entryName));
                        }

                        // Remove stop order reference
                        stopOrders.TryRemove(entryName, out _);

                        // Clean up pending replacements if any
                        if (pendingStopReplacements.TryRemove(entryName, out _))
                        {
                            Interlocked.Decrement(ref pendingReplacementCount);
                        }

                        // If position is fully closed, remove from activePositions
                        if (pos.RemainingContracts <= 0)
                        {
                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                            entryOrders.TryRemove(entryName, out _);
                            Print(string.Format("Position {0} fully closed by stop.", entryName));
                        }
                    }
                }

                // ============================================================
                // 2. TARGET 1 FILL - Reduce stop quantity
                // ============================================================
                else if (orderName.StartsWith("T1_"))
                {
                    string entryName = extractEntryName(orderName, "T1_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        // V12.1101E [SK-01/A-1]: First-Writer-Wins guard — mirror of OnOrderUpdate guard.
                        // If OnOrderUpdate already decremented for this fill, skip the decrement here.
                        bool alreadyProcessed;
                        lock (stateLock)
                        {
                            alreadyProcessed = pos.T1Filled;
                            if (!alreadyProcessed)
                            {
                                pos.T1Filled = true;
                                pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - quantity);
                            }
                        }
                        if (alreadyProcessed)
                        {
                            Print(string.Format("[1101E GUARD] T1 already processed for {0} — skipping duplicate OnExecutionUpdate fill", entryName));
                            target1Orders.TryRemove(entryName, out _);
                            return;
                        }

                        Print(string.Format("TARGET FILLED: {0} @ {1:F2}. Reducing stop. Remaining: {2}",
                            quantity, price, pos.RemainingContracts));

                        // Update stop quantity to match new position size
                        if (pos.RemainingContracts > 0)
                        {
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed, cancel stop
                            if (stopOrders.TryRemove(entryName, out var stopOrder))
                            {
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                    Print(string.Format("OCO: Cancelled stop for fully closed position {0}", entryName));
                                }
                            }
                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                        }

                        // Remove T1 order reference
                        target1Orders.TryRemove(entryName, out _);
                    }
                }

                // ============================================================
                // 3. TARGET 2 FILL - Reduce stop quantity
                // ============================================================
                else if (orderName.StartsWith("T2_"))
                {
                    string entryName = extractEntryName(orderName, "T2_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        // V12.1101E [SK-01/A-1]: First-Writer-Wins guard — T2
                        bool alreadyProcessed;
                        lock (stateLock)
                        {
                            alreadyProcessed = pos.T2Filled;
                            if (!alreadyProcessed)
                            {
                                pos.T2Filled = true;
                                pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - quantity);
                            }
                        }
                        if (alreadyProcessed)
                        {
                            Print(string.Format("[1101E GUARD] T2 already processed for {0} — skipping duplicate OnExecutionUpdate fill", entryName));
                            target2Orders.TryRemove(entryName, out _);
                            return;
                        }

                        Print(string.Format("TARGET FILLED: {0} @ {1:F2}. Reducing stop. Remaining: {2}",
                            quantity, price, pos.RemainingContracts));

                        // Update stop quantity to match new position size
                        if (pos.RemainingContracts > 0)
                        {
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed, cancel stop
                            if (stopOrders.TryRemove(entryName, out var stopOrder))
                            {
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                    Print(string.Format("OCO: Cancelled stop for fully closed position {0}", entryName));
                                }
                            }
                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                        }

                        // Remove T2 order reference
                        target2Orders.TryRemove(entryName, out _);
                    }
                }

                // ============================================================
                // 4. TARGET 3 FILL - Reduce stop quantity
                // ============================================================
                else if (orderName.StartsWith("T3_"))
                {
                    string entryName = extractEntryName(orderName, "T3_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        // V12.1101E [SK-01/A-1]: First-Writer-Wins guard — T3
                        bool alreadyProcessed;
                        lock (stateLock)
                        {
                            alreadyProcessed = pos.T3Filled;
                            if (!alreadyProcessed)
                            {
                                pos.T3Filled = true;
                                pos.RemainingContracts = Math.Max(0, pos.RemainingContracts - quantity);
                            }
                        }
                        if (alreadyProcessed)
                        {
                            Print(string.Format("[1101E GUARD] T3 already processed for {0} — skipping duplicate OnExecutionUpdate fill", entryName));
                            target3Orders.TryRemove(entryName, out _);
                            return;
                        }

                        Print(string.Format("TARGET FILLED: {0} @ {1:F2}. Reducing stop. Remaining: {2}",
                            quantity, price, pos.RemainingContracts));

                        // Update stop quantity to match new position size
                        if (pos.RemainingContracts > 0)
                        {
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed, cancel stop
                            if (stopOrders.TryRemove(entryName, out var stopOrder))
                            {
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                    Print(string.Format("OCO: Cancelled stop for fully closed position {0}", entryName));
                                }
                            }
                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                        }

                        // Remove T3 order reference
                        target3Orders.TryRemove(entryName, out _);
                    }
                }

                // ============================================================
                // 5. TRIM EXECUTION - V10.3.1: Enhanced Stop Integrity
                // ============================================================
                // ðŸ"¥ CRITICAL: When a TRIM executes, we MUST reduce the stop order quantity
                // to match the new position size. If we don't, hitting the stop after a trim
                // would close more contracts than we hold, creating an unintended REVERSE position.
                // Example: Long 4 contracts, stop at 4. Trim 2 (now Long 2). If stop stays at 4,
                // getting stopped out would SELL 4 (close 2 + go SHORT 2) = DISASTER.
                else if (orderName.StartsWith("Trim_"))
                {
                    string entryName = extractEntryName(orderName, "Trim_");
                    if (!string.IsNullOrEmpty(entryName) && activePositions.TryGetValue(entryName, out PositionInfo pos))
                    {
                        // Track previous quantity for logging
                        int previousQty = pos.RemainingContracts;

                        // Deduct ONLY the execution quantity (handle partial fills correctly)
                        pos.RemainingContracts -= quantity;

                        Print(string.Format("TRIM EXECUTION: {0} contracts closed for {1}. Position: {2} â†' {3}",
                            quantity, entryName, previousQty, pos.RemainingContracts));

                        // V10.3.1 FIX: MANDATORY stop quantity reduction to prevent reverse position
                        if (pos.RemainingContracts > 0)
                        {
                            Print(string.Format("STOP INTEGRITY: Reducing stop quantity from {0} to {1} for {2}",
                                previousQty, pos.RemainingContracts, entryName));
                            UpdateStopQuantity(entryName, pos);
                        }
                        else
                        {
                            // Position fully closed by trim, cancel stop
                            Print(string.Format("TRIM FLATTEN: Position {0} fully closed. Cancelling stop.", entryName));
                            if (stopOrders.TryRemove(entryName, out var stopOrder))
                            {
                                if (stopOrder != null && (stopOrder.OrderState == OrderState.Working || stopOrder.OrderState == OrderState.Accepted))
                                {
                                    CancelOrder(stopOrder);
                                }
                            }

                            // Also clean up any pending replacements
                            if (pendingStopReplacements.TryRemove(entryName, out _))
                            {
                                Interlocked.Decrement(ref pendingReplacementCount);
                            }

                            activePositions.TryRemove(entryName, out _);
                            SymmetryGuardForgetEntry(entryName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print("Error OnExecutionUpdate: " + ex.Message);
            }
        }

        #endregion
    }
}
