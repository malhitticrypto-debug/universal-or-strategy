// V12.44 MODULAR: ATR Auto-Sizing Engine Module (Split from UI.cs)
// Contains: Position sizing, ATR stop calculations, target distribution, pending order sync
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
        #region V12.30 ATR Auto-Sizing Engine

        /// <summary>
        /// V12.30: ATR Auto-Sizing Engine — Core Sizing Method
        /// 1. stopDistanceRaw → Ceiling to whole point
        /// 2. Quantity → Floor(MaxRisk / (ceilingStop * pointValue))
        /// 3. Clamp to [minContracts, max]
        /// </summary>
        private int CalculatePositionSize(double stopDistanceRaw)
        {
            if (stopDistanceRaw <= 0) return Math.Max(1, minContracts);

            // STEP 1: CEILING to whole POINT (e.g. 2.3 → 3.0, 4.0 → 4.0)
            double stopPoints = Math.Ceiling(stopDistanceRaw);

            double riskToUse = MaxRiskAmount;
            double stopDollars = stopPoints * pointValue;
            if (stopDollars <= 0) return Math.Max(1, minContracts);

            // STEP 2: FLOOR the quantity (never exceed $MaxRisk)
            int contracts = (int)Math.Floor(riskToUse / stopDollars);

            // V12.1101E [B-9]: Clamp to [minContracts, maxContracts] — prevents runaway sizing on
            // tiny ATR values (e.g., flat market) from hitting broker limits or compliance thresholds.
            contracts = Math.Max(minContracts, Math.Min(contracts, maxContracts));

            Print($"[V12.30 SIZING] RawStop={stopDistanceRaw:F2} → Ceiling={stopPoints:F0}pt | Risk=${riskToUse:F0} | StopDollars=${stopDollars:F0} | Qty={contracts} | Clamp=[{minContracts},{maxContracts}]");
            return contracts;
        }

        /// <summary>
        /// V12.30: ATR Auto-Sizing Engine — Centralized Stop Distance Calculator
        /// Returns ATR-based stop rounded UP to nearest whole point.
        /// Replaces all inline "currentATR * multiplier" patterns.
        /// </summary>
        private double CalculateATRStopDistance(double atrMultiplier)
        {
            if (currentATR <= 0) return MinimumStop;

            double rawStop = currentATR * atrMultiplier;
            double ceilingStop = Math.Ceiling(rawStop);  // Round UP to whole point
            return Math.Max(MinimumStop, Math.Min(ceilingStop, MaximumStop));
        }

        private void GetTargetDistribution(int contracts, out int t1, out int t2, out int t3, out int t4)
        {
            // V12.40 PRIORITY FILL: Always respect user percentages.
            // For small lots (≤4), the percentage math naturally assigns 0 to lower-priority targets.
            // T4 (runner) gets the remainder — guaranteed ≥1 when contracts > 1.

            // Single contract = pure runner (T4), no split possible
            if (contracts <= 1)
            {
                t1 = 0; t2 = 0; t3 = 0; t4 = contracts;
                return;
            }

            t1 = (int)Math.Floor(contracts * T1ContractPercent / 100.0);
            t2 = (int)Math.Floor(contracts * T2ContractPercent / 100.0);
            t3 = (int)Math.Floor(contracts * T3ContractPercent / 100.0);

            // V12.1101E [MATH-01]: CAP running sum BEFORE computing T4.
            // The old approach computed t4 = contracts - t1 - t2 - t3, which goes negative when
            // percentages sum > 100%. The post-hoc clamp then floored t4 to 0 while t1+t2+t3
            // already consumed contracts+N slots — creating phantom contract totals.
            // Fix: reduce T3 → T2 → T1 proportionally so t4 is always ≥ 1 before subtraction.
            int runningSum = t1 + t2 + t3;
            int maxAllowed = contracts - 1; // always reserve ≥1 slot for T4 runner
            if (runningSum > maxAllowed)
            {
                int excess = runningSum - maxAllowed;
                // Shed from lowest-priority first: T3 → T2 → T1
                t3 = Math.Max(0, t3 - excess);
                excess = (t1 + t2 + t3) - maxAllowed;
                if (excess > 0) { t2 = Math.Max(0, t2 - excess); }
                excess = (t1 + t2 + t3) - maxAllowed;
                if (excess > 0) { t1 = Math.Max(1, t1 - excess); }
            }
            t4 = contracts - t1 - t2 - t3; // guaranteed ≥ 1

            // Ensure T1 gets at least 1 (the quick scalp anchor)
            if (t1 < 1) { t1 = 1; t4 = Math.Max(0, t4 - 1); }

            // Runner (T4) must get at least 1 on multi-contract trades
            if (t4 < 1)
            {
                // Steal from lowest-priority filled target: T3 → T2
                if (t3 > 0) { t3--; t4 = 1; }
                else if (t2 > 0) { t2--; t4 = 1; }
                else { t1 = contracts - 1; t2 = 0; t3 = 0; t4 = 1; }
            }

            // Safety floors: ensure no negatives remain after any edge-case adjustments
            t1 = Math.Max(0, t1); t2 = Math.Max(0, t2); t3 = Math.Max(0, t3); t4 = Math.Max(0, t4);
        }

        /// <summary>
        /// V12.45: Live Sync Engine — Updates unfilled entry orders when ATR
        /// causes ceiling-stop or floor-qty to change.
        /// FLICKER PROTECTION HARDENING:
        /// 1. Order State Guard: Only sync orders in Accepted/Working state (blocks ChangePending)
        /// 2. Tick-Aware Threshold: Uses tickSize instead of hardcoded 0.01
        /// 3. Retry Cooldown: 500ms pause after ChangeOrder failure to prevent broker hammering
        /// </summary>
        private DateTime _lastSyncFailureTime = DateTime.MinValue;  // V12.45: Retry cooldown tracker

        private void SyncPendingOrders()
        {
            if (currentATR <= 0) return;

            // V12.45 RETRY COOLDOWN: If a ChangeOrder failed recently, back off for 500ms
            // This prevents rapid-fire rejections that can cascade into broker throttling
            if ((DateTime.Now - _lastSyncFailureTime).TotalMilliseconds < 500) return;

            foreach (var kvp in activePositions.ToArray())
            {
                PositionInfo pos = kvp.Value;
                string entryName = kvp.Key;

                // Only sync UNFILLED entries
                if (pos.EntryFilled) continue;

                // Skip modes that don't use ATR-based stops
                if (pos.IsFFMATrade || pos.IsMOMOTrade) continue;

                // Get the entry order
                Order entryOrder;
                if (!entryOrders.TryGetValue(entryName, out entryOrder)) continue;
                if (entryOrder == null) continue;

                // V12.45 ORDER STATE GUARD: Only modify orders in stable states
                // Accepted = broker acknowledged, waiting for fill
                // Working  = actively in the order book
                // ChangePending = a ChangeOrder is already in-flight — DO NOT send another
                OrderState currentState = entryOrder.OrderState;
                if (currentState != OrderState.Accepted && currentState != OrderState.Working)
                {
                    if (currentState == OrderState.ChangePending)
                        Print($"[V12.45 SYNC] SKIP {entryName}: ChangeOrder already in-flight (ChangePending)");
                    continue;
                }

                // Determine ATR multiplier for this trade type
                double atrMult = GetATRMultiplierForPosition(pos);
                double newStopDist = CalculateATRStopDistance(atrMult);
                int    newQty     = CalculatePositionSize(newStopDist);

                // V12.45 TICK-AWARE FLICKER CHECK: Use tickSize for meaningful comparison
                // Only sync if ceiling stop changed by at least 1 tick OR quantity changed
                double oldCeilingStop = Math.Ceiling(Math.Abs(pos.EntryPrice - pos.CurrentStopPrice));
                double newCeilingStop = newStopDist;

                double stopDelta = Math.Abs(newCeilingStop - oldCeilingStop);
                if (stopDelta < tickSize && newQty == pos.TotalContracts)
                    continue;  // No material change — skip

                // SYNC: Update quantity and stop
                try
                {
                    double newStopPrice = pos.Direction == MarketPosition.Long
                        ? pos.EntryPrice - newStopDist
                        : pos.EntryPrice + newStopDist;

                    // Update the entry order quantity via ChangeOrder
                    if (newQty != entryOrder.Quantity)
                    {
                        ChangeOrder(entryOrder, newQty, entryOrder.LimitPrice, entryOrder.StopPrice);
                    }

                    // Update position info
                    pos.TotalContracts = newQty;
                    pos.CurrentStopPrice = newStopPrice;
                    pos.InitialStopPrice = newStopPrice;

                    // Recalculate target distribution
                    int t1, t2, t3, t4;
                    GetTargetDistribution(newQty, out t1, out t2, out t3, out t4);
                    pos.T1Contracts = t1;
                    pos.T2Contracts = t2;
                    pos.T3Contracts = t3;
                    pos.T4Contracts = t4;
                    pos.RemainingContracts = newQty;

                    Print($"[V12.45 SYNC] {entryName}: Stop {oldCeilingStop:F0}→{newCeilingStop:F0}pt | Qty {entryOrder.Quantity}→{newQty} | ATR={currentATR:F2}");
                }
                catch (Exception ex)
                {
                    // V12.45 RETRY COOLDOWN: Record failure time to prevent hammering
                    _lastSyncFailureTime = DateTime.Now;
                    Print($"[V12.45 SYNC] ERROR syncing {entryName}: {ex.Message} — cooldown 500ms");
                }
            }
        }

        /// <summary>
        /// V12.30: Returns the ATR multiplier for a given position type.
        /// Used by SyncPendingOrders to determine which multiplier to recalculate with.
        /// </summary>
        private double GetATRMultiplierForPosition(PositionInfo pos)
        {
            if (pos.IsRMATrade) return RMAStopATRMultiplier;
            if (pos.IsTRENDTrade)
            {
                if (pos.IsTRENDEntry1)
                    return isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry1ATRMultiplier;
                return isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry2ATRMultiplier;
            }
            if (pos.IsRetestTrade)
                return isRetestRmaMode ? RMAStopATRMultiplier : RetestATRMultiplier; // V12.Hardening: was isTrendRmaMode (typo)
            return StopMultiplier; // ORB default
        }

        #endregion
    }
}
