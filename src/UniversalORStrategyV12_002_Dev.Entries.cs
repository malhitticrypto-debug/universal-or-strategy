// V12.16 MODULAR: Entry Engine Module (Extracted from main file)
// Contains: FFMA, OR, RMA, MOMO, TREND, RETEST entry logic
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
        #region FFMA Entry Logic (V8.7)

        /// <summary>
        /// V8.7: Check FFMA conditions and execute on reversal candle
        /// SHORT: RSI > 80 + price 10+ pts above 9 EMA + RED candle
        /// LONG: RSI < 20 + price 10+ pts below 9 EMA + GREEN candle
        /// </summary>
        private void CheckFFMAConditions()
        {
            if (!isFFMAModeArmed || !FFMAEnabled) return;
            if (ema9 == null || rsiIndicator == null || currentATR <= 0) return;
            if (CurrentBar < 20) return;

            try
            {
                double ema9Value = ema9[0];
                double rsiValue = rsiIndicator[0];
                double currentPrice = Close[0];
                double distanceFromEMA = currentPrice - ema9Value;

                bool isGreenCandle = Close[0] > Open[0];
                bool isRedCandle = Close[0] < Open[0];

                // SHORT SETUP: RSI > 80 + Price far ABOVE EMA + RED reversal candle
                if (rsiValue > FFMARSIOverbought && distanceFromEMA >= FFMAEMADistance && isRedCandle)
                {
                    Print(string.Format("FFMA SHORT TRIGGERED: RSI={0:F1} > {1} | Distance={2:F2}pts > {3}pts | RED candle",
                        rsiValue, FFMARSIOverbought, distanceFromEMA, FFMAEMADistance));
                    ExecuteFFMAEntry(MarketPosition.Short);
                    return;
                }

                // LONG SETUP: RSI < 20 + Price far BELOW EMA + GREEN reversal candle
                if (rsiValue < FFMARSIOversold && distanceFromEMA <= -FFMAEMADistance && isGreenCandle)
                {
                    Print(string.Format("FFMA LONG TRIGGERED: RSI={0:F1} < {1} | Distance={2:F2}pts (below by {3}pts) | GREEN candle",
                        rsiValue, FFMARSIOversold, distanceFromEMA, FFMAEMADistance));
                    ExecuteFFMAEntry(MarketPosition.Long);
                    return;
                }
            }
            catch (Exception ex)
            {
                Print("ERROR CheckFFMAConditions: " + ex.Message);
            }
        }

        /// <summary>
        /// V8.7: Execute FFMA market order with entry candle high/low as stop
        /// Uses same target system as RMA (T1-T4)
        /// </summary>
        private void ExecuteFFMAEntry(MarketPosition direction)
        {
            try
            {
                double entryPrice = Close[0];  // Market order at current price

                // Stop at entry candle high (short) or low (long)
                double stopPrice = direction == MarketPosition.Long ? Low[0] : High[0];
                double stopDistance = Math.Min(Math.Abs(entryPrice - stopPrice), MaximumStop); // V8.31: Use MaximumStop

                // Validate stop distance
                if (stopDistance < tickSize * 2)
                {
                    Print(string.Format("FFMA: Stop too tight ({0:F2}pts) - using 2 tick minimum", stopDistance));
                    stopPrice = direction == MarketPosition.Long
                        ? entryPrice - (tickSize * 2)
                        : entryPrice + (tickSize * 2);
                    stopDistance = tickSize * 2;
                }

                // V12.Hardening: Final stop-distance guard — prevent CalculatePositionSize(0) → ∞ contracts
                if (stopDistance <= 0)
                {
                    Print("[FFMA REJECT] Stop distance is zero (doji candle or tickSize=0). Aborting entry.");
                    return;
                }

                // Calculate targets (same as RMA: T1 fixed, T2/T3 ATR-based, T4 runner)
                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;

                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                    : entryPrice - (currentATR * RMAT1ATRMultiplier);
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                    : entryPrice - (currentATR * RMAT2ATRMultiplier);

                // Calculate position size based on ATR stop
                int contracts = CalculatePositionSize(stopDistance);

                // 4-target distribution
                int t1Qty, t2Qty, t3Qty, t4Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                string timestamp = DateTime.Now.ToString("HHmmssffff");
                string signalName = direction == MarketPosition.Long ? "FFMALong" : "FFMAShort";
                string entryName = signalName + "_" + timestamp;

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    IsRMATrade = false,
                    IsFFMATrade = true
                };

                activePositions[entryName] = pos;

                // V12.13-D: Notify connected panel clients of position entry
                string syncMsg = string.Format("POSITION_ENTERED|FFMA|{0}", contracts);
                SendResponseToRemote(syncMsg);


                // Submit MARKET order (immediate execution)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, contracts, 0, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, contracts, 0, 0, "", entryName);

                entryOrders[entryName] = entryOrder;

                Print(string.Format("FFMA MARKET ORDER: {0} {1}@MARKET | Stop: {2:F2} (candle {3})",
                    signalName, contracts, stopPrice, direction == MarketPosition.Long ? "low" : "high"));
                Print(string.Format("FFMA TARGETS: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2} | T4:{6}@trail",
                    t1Qty, target1Price, t2Qty, target2Price, t3Qty, target3Price, t4Qty));

                // V12 SIMA: Dispatch to fleet (replaces legacy slave broadcast)
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("FFMA", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Market, entryName);
                }

                // Disarm FFMA after execution (one-shot)
                DeactivateFFMAMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteFFMAEntry: " + ex.Message);
            }
        }

        private void DeactivateFFMAMode()
        {
            isFFMAModeArmed = false;
            // V12.24: Notify panel to reset FFMA Smart Toggle visual
            SendResponseToRemote("FFMA_DISARMED");
            Print("V12.24: FFMA disarmed — sent FFMA_DISARMED to panel");
        }

        #endregion

        #region OR Entry Logic

        private void ExecuteLong()
        {
            // V12.2 Hybrid Sync: Manual Interception
            if (isTosSyncMode)
            {
                if (isLongArmed)
                {
                    // DOUBLE-CLICK BYPASS: If already armed, fire immediately
                    Print("[SYNC] Double-Click Bypass Triggered -> Executing LONG immediately (No ToS Handshake)");
                    isLongArmed = false;
                    // Proceed to entry logic below
                }
                else
                {
                    isLongArmed = true;
                    isShortArmed = false; // Mutually exclusive for simplicity
                    lastArmedTime = DateTime.Now;
                    Print("[SYNC] LONG ENTRY ARMED. Waiting for ToS handshake signal...");
                    return;
                }
            }

            if (!orComplete || sessionRange == 0)
            {
                Print("Cannot enter Long - OR not ready");
                return;
            }

            double entryPrice = sessionHigh + (3 * tickSize);
            double stopDistance = CalculateORStopDistance();
            double stopPrice = entryPrice - stopDistance;

            EnterORPosition(MarketPosition.Long, entryPrice, stopPrice);
        }

        private void ExecuteShort()
        {
            // V12.2 Hybrid Sync: Manual Interception
            if (isTosSyncMode)
            {
                if (isShortArmed)
                {
                    // DOUBLE-CLICK BYPASS: If already armed, fire immediately
                    Print("[SYNC] Double-Click Bypass Triggered -> Executing SHORT immediately (No ToS Handshake)");
                    isShortArmed = false;
                    // Proceed to entry logic below
                }
                else
                {
                    isShortArmed = true;
                    isLongArmed = false; // Mutually exclusive
                    lastArmedTime = DateTime.Now;
                    Print("[SYNC] SHORT ENTRY ARMED. Waiting for ToS handshake signal...");
                    return;
                }
            }

            if (!orComplete || sessionRange == 0)
            {
                Print("Cannot enter Short - OR not ready");
                return;
            }

            double entryPrice = sessionLow - (3 * tickSize);
            double stopDistance = CalculateORStopDistance();
            double stopPrice = entryPrice + stopDistance;

            EnterORPosition(MarketPosition.Short, entryPrice, stopPrice);
        }

        private void EnterORPosition(MarketPosition direction, double entryPrice, double stopPrice)
        {
            try
            {
                // v5.13 FIX: Validate entry price before submitting StopMarket order
                // For LONG: entry must be ABOVE current price (breakout up)
                // For SHORT: entry must be BELOW current price (breakout down)
                // Use lastKnownPrice for real-time accuracy (Close[0] can be stale)
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                if (direction == MarketPosition.Long && entryPrice <= currentPrice)
                {
                    Print(string.Format("OR ENTRY BLOCKED: Long entry {0:F2} already below market {1:F2} - too late for breakout",
                        entryPrice, currentPrice));
                    return;
                }
                if (direction == MarketPosition.Short && entryPrice >= currentPrice)
                {
                    Print(string.Format("OR ENTRY BLOCKED: Short entry {0:F2} already above market {1:F2} - too late for breakout",
                        entryPrice, currentPrice));
                    return;
                }

                double stopDistance = CalculateORStopDistance();
                int contracts = CalculatePositionSize(stopDistance);

                // v5.13: 4-target system with 20/30/30/20 split
                int t1Qty, t2Qty, t3Qty, t4Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                Print(string.Format("POSITION SIZE: {0} contracts \u2192 T1:{1}(20%) T2:{2}(30%) T3:{3}(30%) T4:{4}(20%)",
                    contracts, t1Qty, t2Qty, t3Qty, t4Qty));

                string signalName = direction == MarketPosition.Long ? "ORLong" : "ORShort";
                string timestamp = DateTime.Now.ToString("HHmmssffff");
                string entryName = signalName + "_" + timestamp;

                // v5.13: T1 = Fixed 1 point profit (quick scalp)
                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;

                // v5.13: T2 = 0.5x OR RANGE (using sessionRange, NOT ATR for OR trades)
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (sessionRange * Target2Multiplier)
                    : entryPrice - (sessionRange * Target2Multiplier);

                // v5.13: T3 = 1.0x OR RANGE (using sessionRange, NOT ATR for OR trades)
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (sessionRange * Target3Multiplier)
                    : entryPrice - (sessionRange * Target3Multiplier);

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    IsRMATrade = false
                };

                activePositions[entryName] = pos;

                // V12.13-D: Notify connected panel clients of position entry
                string syncMsg = string.Format("POSITION_ENTERED|OR|{0}", contracts);
                SendResponseToRemote(syncMsg);

                // Submit entry order as stop market (breakout entry)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket, contracts, 0, entryPrice, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.StopMarket, contracts, 0, entryPrice, "", entryName);

                entryOrders[entryName] = entryOrder;

                Print(string.Format("OR ENTRY ORDER: {0} {1}@{2:F2} | Stop: {3:F2} | OR Range: {4:F2}",
                    signalName, contracts, entryPrice, stopPrice, sessionRange));
                Print(string.Format("TARGETS: T1:{0}@{1:F2}(+{2:F2}pt) | T2:{3}@{4:F2}(+{5:F2}OR) | T3:{6}@{7:F2}(+{8:F2}OR) | T4:{9}@trail",
                    t1Qty, target1Price, Target1FixedPoints,
                    t2Qty, target2Price, sessionRange * Target2Multiplier,
                    t3Qty, target3Price, sessionRange * Target3Multiplier, t4Qty));

                // V12 SIMA: Dispatch to fleet (replaces legacy slave broadcast)
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("OR", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Limit);
                }
            }
            catch (Exception ex)
            {
                Print("ERROR EnterORPosition: " + ex.Message);
            }
        }

        private double CalculateORStopDistance()
        {
            // v5.13: Use ATR for OR stop (same as RMA) instead of OR range
            if (currentATR <= 0) return MinimumStop;

            double calculatedStop = CalculateATRStopDistance(StopMultiplier);  // V12.30: Ceiling-rounded
            return calculatedStop;
        }

        #endregion

        // V12 SIMA: BroadcastEntrySignal and V8 Copy Trading region removed.
        // Trade copying is replaced by direct Account.All iteration in ExecuteSmartDispatchEntry.
        // SignalBroadcaster is retained ONLY for IPC app relay (HandleExternalSignal).

        // V11: Trend RMA (9/15 Split) Logic
        private void ExecuteTrendSplitEntry()
        {
            if (currentATR <= 0)
            {
                Print("Cannot execute TREND RMA - ATR not ready");
                return;
            }

            if (ema9 == null || ema15 == null)
            {
                Print("Cannot execute TREND RMA - EMA indicators not ready");
                return;
            }

            try
            {
                // Logic: EMA 9 vs EMA 15 alignment determines trend direction.
                double e9 = Instrument.MasterInstrument.RoundToTickSize(ema9[0]);
                double e15 = Instrument.MasterInstrument.RoundToTickSize(ema15[0]);
                bool isLongTrend = e9 > e15;
                MarketPosition direction = isLongTrend ? MarketPosition.Long : MarketPosition.Short;
                OrderAction entryAction = isLongTrend ? OrderAction.Buy : OrderAction.SellShort;

                // TREND_RMA is risk-sized from MaxRiskAmount (default $200), then split across EMA9/EMA15.
                // V12.1101E [B-1]: Decouple per-leg multipliers — mirror the standard TREND entry logic.
                // E1 (EMA9 leg) uses TRENDEntry1ATRMultiplier; E2 (EMA15 leg) uses TRENDEntry2ATRMultiplier.
                // When isTrendRmaMode is ON, both legs fall back to RMAStopATRMultiplier (same as standard TREND).
                double e1Mult = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry1ATRMultiplier;
                double e2Mult = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry2ATRMultiplier;
                double stop9Dist  = CalculateATRStopDistance(e1Mult);  // EMA9 leg stop distance
                double stop15Dist = CalculateATRStopDistance(e2Mult);  // EMA15 leg stop distance
                double weightedStopDist = (stop9Dist * (1.0 / 3.0)) + (stop15Dist * (2.0 / 3.0));

                int totalQty = CalculatePositionSize(weightedStopDist);
                int qty9 = totalQty <= 1
                    ? 1
                    : Math.Max(1, (int)Math.Round(totalQty / 3.0, MidpointRounding.AwayFromZero));
                int qty15 = Math.Max(0, totalQty - qty9);

                if (totalQty > 1 && qty15 < 1)
                {
                    qty15 = 1;
                    qty9 = Math.Max(1, totalQty - qty15);
                }

                int finalTotalQty = qty9 + qty15;
                string timestamp = DateTime.Now.ToString("HHmmssffff");
                string trendGroupId = "TRMA_" + timestamp;
                string entry1Name = trendGroupId + "_E1";
                string entry2Name = trendGroupId + "_E2";

                double stop1Price = direction == MarketPosition.Long ? e9 - stop9Dist : e9 + stop9Dist;
                PositionInfo pos1 = CreateTRENDPosition(entry1Name, direction, e9, stop1Price, qty9, true, trendGroupId, true);
                activePositions[entry1Name] = pos1;

                List<string> masterEntryNames = new List<string> { entry1Name };

                Order entryOrder1 = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, qty9, e9, 0, "", entry1Name)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, qty9, e9, 0, "", entry1Name);
                entryOrders[entry1Name] = entryOrder1;

                if (qty15 > 0)
                {
                    double stop2Price = direction == MarketPosition.Long ? e15 - stop15Dist : e15 + stop15Dist;
                    PositionInfo pos2 = CreateTRENDPosition(entry2Name, direction, e15, stop2Price, qty15, false, trendGroupId, true);
                    activePositions[entry2Name] = pos2;

                    linkedTRENDEntries[entry1Name] = entry2Name;
                    linkedTRENDEntries[entry2Name] = entry1Name;

                    Order entryOrder2 = direction == MarketPosition.Long
                        ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, qty15, e15, 0, "", entry2Name)
                        : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, qty15, e15, 0, "", entry2Name);
                    entryOrders[entry2Name] = entryOrder2;
                    masterEntryNames.Add(entry2Name);
                }

                double weightedEntryPrice = ((e9 * qty9) + (e15 * qty15)) / Math.Max(1, finalTotalQty);
                weightedEntryPrice = Instrument.MasterInstrument.RoundToTickSize(weightedEntryPrice);

                Print(string.Format("TREND RMA SPLIT: {0} | Qty={1} (EMA9={2}, EMA15={3}) | EMA9={4:F2} EMA15={5:F2} | Anchor={6:F2}",
                    direction == MarketPosition.Long ? "LONG" : "SHORT",
                    finalTotalQty,
                    qty9,
                    qty15,
                    e9,
                    e15,
                    weightedEntryPrice));

                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry(
                        "TREND_RMA",
                        entryAction,
                        finalTotalQty,
                        weightedEntryPrice,
                        OrderType.Limit,
                        masterEntryNames.ToArray());
                }

                DeactivateTRENDMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteTrendSplitEntry: " + ex.Message);
            }
        }

        #region RMA Entry Logic

        // V11: Helper to get price of currently selected RMA Anchor
        private double GetRmaAnchorPrice()
        {
            switch (currentRmaAnchor)
            {
                case RmaAnchorType.Ema30: return ema30[0];
                case RmaAnchorType.Ema65: return ema65[0];
                case RmaAnchorType.Ema200: return ema200[0];
                case RmaAnchorType.OrHigh: return sessionHigh;
                case RmaAnchorType.OrLow: return sessionLow;
                case RmaAnchorType.Manual:
                    // Use thread-safe cache
                    return cachedMnlPrice;
            }
            return ema65[0]; // Default
        }

        private void ExecuteRMAEntry(double clickPrice, MarketPosition? forcedDirection = null)
        {
            if (currentATR <= 0)
            {
                Print(string.Format("[RMA REJECT] ATR not ready. Check if 5-min bars (BarsArray[1]) are loaded and strategy has been running for {0} bars.", RMAATRPeriod));
                return;
            }

            try
            {
                // V11 FIX: Robust Check for Stale Price
                double currentPrice = Close[0];
                if (lastKnownPrice > 0)
                {
                     double diff = Math.Abs(lastKnownPrice - currentPrice);
                     if (currentPrice > 0 && diff / currentPrice < 0.05) currentPrice = lastKnownPrice;
                }

                // V11: Dynamic Anchor Direction Logic (UNUSED for Direction SafeGuard)
                double anchorPrice = GetRmaAnchorPrice();
                double refPrice = anchorPrice > 0 ? anchorPrice : currentPrice;

                MarketPosition direction;

                // V11 SAFEGUARD: Always enforce Limit Order Logic relative to Market
                // If Click > Market -> Short (Sell Limit Above)
                // If Click < Market -> Long (Buy Limit Below)
                // This prevents "Accidental Market Fills" if Anchor logic or stale data gets confused
                if (clickPrice > currentPrice) direction = MarketPosition.Short;
                else direction = MarketPosition.Long;

                // Only use forcedDirection if it MATCHES the Safe Logic (or if prices are super close)
                if (forcedDirection.HasValue && forcedDirection.Value != direction)
                {
                    Print(string.Format("RMA SAFEGUARD: Ignoring forced {0} because Click {1} vs Market {2} implies {3}",
                        forcedDirection.Value, clickPrice, currentPrice, direction));
                }

                Print(string.Format("RMA Entry: Click={0:F2}, Market={1:F2}, Direction={2}",
                    clickPrice, currentPrice, direction));

                // Calculate RMA stop and targets using ATR
                double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded, MaximumStop cap

                double entryPrice = clickPrice;
                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                // v5.13: T1 = Fixed 1 point profit (same as OR, not ATR-based)
                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;

                // v5.13: T2 = 0.5x ATR (using RMA multiplier)
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                    : entryPrice - (currentATR * RMAT1ATRMultiplier);

                // v5.13: T3 = 1.0x ATR (using RMA multiplier)
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                    : entryPrice - (currentATR * RMAT2ATRMultiplier);

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                string signalName = direction == MarketPosition.Long ? "RMALong" : "RMAShort";
                string timestamp = DateTime.Now.ToString("HHmmssffff");
                string entryName = signalName + "_" + timestamp;

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    IsRMATrade = true
                };

                // Submit LIMIT order at clicked price (RMA uses limit entries)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);

                if (entryOrder != null)
                {
                    entryOrders[entryName] = entryOrder;
                    activePositions[entryName] = pos; // Only add to panel if order submitted

                    // DEBUG: Visual Confirmation
                    Draw.Text(this, "Debug_" + entryName, "ORDER SUBMITTED", 0, entryPrice, Brushes.Yellow);
                    Draw.Line(this, "Line_" + entryName, 0, entryPrice, 10, entryPrice, Brushes.Yellow);
                }
                else
                {
                    Print("[ERROR] SubmitOrderUnmanaged returned NULL");
                    Draw.Text(this, "Debug_Fail_" + entryName, "ORDER FAILED", 0, entryPrice, Brushes.Red);
                }

                Print(string.Format("RMA ENTRY ORDER: {0} {1}@{2:F2} | ATR: {3:F2}", signalName, contracts, entryPrice, currentATR));
                Print(string.Format("RMA TARGETS: T1:{0}@{1:F2}(+{2:F2}pt) | T2:{3}@{4:F2} | T3:{5}@{6:F2} | T4:{7}@trail",
                    t1Qty, target1Price, Target1FixedPoints,
                    t2Qty, target2Price, t3Qty, target3Price, t4Qty));

                // V12 SIMA: Dispatch to fleet (replaces legacy slave broadcast)
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("RMA", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Limit);
                }

                DeactivateRMAMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteRMAEntry: " + ex.Message);
            }
        }

        /// <summary>
        /// V10.1: Custom RMA entry for IPC commands - forces direction and uses specified price
        /// </summary>
        private void ExecuteRMAEntryCustom(double price, MarketPosition direction)
        {
            if (currentATR <= 0)
            {
                Print("IPC RMACustom Ignored: ATR not available");
                return;
            }

            try
            {
                double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded, MaximumStop cap

                double entryPrice = Instrument.MasterInstrument.RoundToTickSize(price);
                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;

                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                    : entryPrice - (currentATR * RMAT1ATRMultiplier);

                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                    : entryPrice - (currentATR * RMAT2ATRMultiplier);

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                string signalName = direction == MarketPosition.Long ? "IPCLong" : "IPCShort";
                string entryName = signalName + "_" + DateTime.Now.ToString("HHmmssffff");

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    IsRMATrade = true
                };

                activePositions[entryName] = pos;

                // Execute as MARKET order for IPC commands to ensure immediate fill (V9 style)
                if (direction == MarketPosition.Long)
                    SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, contracts, 0, 0, "", entryName);
                else
                    SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, contracts, 0, 0, "", entryName);

                Print(string.Format("IPC EXEC: {0} {1} contracts at MKT (Ref: {2:F2})", direction, contracts, entryPrice));

                // V12.1: Smart Dispatch to SIMA Fleet
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("RMA_IPC", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Limit);
                }
            }
            catch (Exception ex)
            {
                Print("Error ExecuteRMAEntryCustom: " + ex.Message);
            }
        }

        private void ActivateRMAMode()
        {
            isRMAModeActive = true;
        }

        private void DeactivateRMAMode()
        {
            isRMAModeActive = false;
            isRMAButtonClicked = false;

            // V12.14: Broadcast RMA deactivation to panel
            string deactivateConfig = string.Format(
                "CONFIG|OR|COUNT:{0};T1:{1};T2:{2};T3:{3};STR:{4};MAX:{5};",
                minContracts, Target1FixedPoints, Target2Multiplier, Target3Multiplier,
                StopMultiplier, MaxRiskAmount);
            SendResponseToRemote(deactivateConfig);
            Print("V12.14: DeactivateRMAMode - CONFIG broadcast sent");
        }

        #endregion

        #region MOMO Entry Logic (V8.6)

        /// <summary>
        /// V8.6: Execute MOMO (Momentum) trade using Stop Market orders
        /// OPPOSITE direction from RMA:
        /// - Click ABOVE price = Stop Market LONG (buy when price rises to click level)
        /// - Click BELOW price = Stop Market SHORT (sell when price drops to click level)
        /// Uses same targets/trails as RMA but with fixed 0.5pt stop
        /// </summary>
        private void ExecuteMOMOEntry(double clickPrice)
        {
            if (!MOMOEnabled)
            {
                Print("MOMO mode is disabled");
                return;
            }

            if (currentATR <= 0)
            {
                Print("Cannot execute MOMO entry - ATR not available yet");
                return;
            }

            try
            {
                // Use last known price from OnBarUpdate (Close[0] may be stale in UI events)
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // MOMO Direction: OPPOSITE from RMA!
                // Click ABOVE current price = LONG (stop buy triggers when price rises)
                // Click BELOW current price = SHORT (stop sell triggers when price drops)
                MarketPosition direction;
                if (clickPrice > currentPrice)
                {
                    direction = MarketPosition.Long;
                    Print(string.Format("MOMO: Click above price ({0:F2} > {1:F2}) = LONG stop entry", clickPrice, currentPrice));
                }
                else
                {
                    direction = MarketPosition.Short;
                    Print(string.Format("MOMO: Click below price ({0:F2} < {1:F2}) = SHORT stop entry", clickPrice, currentPrice));
                }

                // MOMO uses FIXED 0.5pt stop (not ATR-based)
                double stopDistance = Math.Min(MOMOStopPoints, MaximumStop); // V8.31: Use MaximumStop

                double entryPrice = clickPrice;
                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                // Same targets as RMA (ATR-based)
                // T1 = Fixed 1 point profit (same as RMA)
                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;

                // T2 = 0.5x ATR (using RMA multiplier)
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                    : entryPrice - (currentATR * RMAT1ATRMultiplier);

                // T3 = 1.0x ATR (using RMA multiplier)
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                    : entryPrice - (currentATR * RMAT2ATRMultiplier);

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                string signalName = direction == MarketPosition.Long ? "MOMOLong" : "MOMOShort";
                string timestamp = DateTime.Now.ToString("HHmmssffff");
                string entryName = signalName + "_" + timestamp;

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    IsRMATrade = false,
                    IsMOMOTrade = true  // V8.6: Mark as MOMO trade
                };

                activePositions[entryName] = pos;

                // V12.Hardening: Use StopMarket (was StopLimit with limitPrice==stopPrice — never fills on fast breakouts)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.StopMarket, contracts, 0, entryPrice, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.StopMarket, contracts, 0, entryPrice, "", entryName);

                entryOrders[entryName] = entryOrder;

                Print(string.Format("MOMO ENTRY ORDER: {0} {1}@{2:F2} STOP MKT | Stop: {3:F2}pt", signalName, contracts, entryPrice, stopDistance));
                Print(string.Format("MOMO TARGETS: T1:{0}@{1:F2}(+{2:F2}pt) | T2:{3}@{4:F2} | T3:{5}@{6:F2} | T4:{7}@trail",
                    t1Qty, target1Price, Target1FixedPoints,
                    t2Qty, target2Price, t3Qty, target3Price, t4Qty));

                // V12 SIMA: Dispatch to fleet (replaces legacy slave broadcast)
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("MOMO", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.StopMarket, entryName);
                }

                // Deactivate MOMO mode after entry (one-shot)
                DeactivateMOMOMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteMOMOEntry: " + ex.Message);
            }
        }

        private void ActivateMOMOMode()
        {
            // Deactivate RMA if active (mutually exclusive)
            if (isRMAModeActive)
            {
                DeactivateRMAMode();
            }
            isMOMOModeActive = true;
        }

        private void DeactivateMOMOMode()
        {
            isMOMOModeActive = false;
        }

        // V12.44: UpdateMOMOModeDisplay() removed — legacy chart UI no-op

        #endregion

        #region TREND Entry Logic (V8.2)

        /// <summary>
        /// V8.2: Execute TREND trade with dual limit orders
        /// Entry 1 (1/3) at 9 EMA with fixed 2pt stop
        /// Entry 2 (2/3) at 15 EMA with 1.1x ATR trailing stop off EMA15
        /// </summary>
        private void ExecuteTRENDEntry()
        {
            // V8.2 FIX: Only execute when on primary series (BarsInProgress=0)
            // This ensures we get correct EMA values from BarsArray[0]
            if (BarsInProgress != 0)
            {
                pendingTRENDEntry = true;
                Print("TREND entry deferred to next primary bar update (BarsInProgress=" + BarsInProgress + ")");
                return;
            }

            // Clear pending flag since we're executing now
            pendingTRENDEntry = false;

            if (!TRENDEnabled)
            {
                Print("TREND mode is disabled");
                return;
            }

            if (currentATR <= 0 || ema9 == null || ema15 == null)
            {
                Print("Cannot execute TREND entry - indicators not ready");
                return;
            }

            // V11: Trend RMA (9/15 Split) Mode
            if (isTrendRmaMode)
            {
                Print(string.Format("V12.20: TREND Multiplier -> Mode=RMA (9/15 Split) ATR={0:F2}", currentATR));
                ExecuteTrendSplitEntry();
                return;
            }

            // V8.2: Ensure we have enough bars for EMA calculation
            if (CurrentBar < 20)
            {
                Print("Cannot execute TREND entry - not enough bars (CurrentBar=" + CurrentBar + ")");
                return;
            }
            try
            {
                // V8.2: Simple check for enough bars
                if (CurrentBar < 20)
                {
                    Print("Cannot execute TREND entry - not enough bars (CurrentBar=" + CurrentBar + ")");
                    return;
                }

                // Get current tick price for direction determination
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // V8.2: Use stored EMA instances (now guaranteed BarsInProgress=0)
                if (ema9 == null || ema15 == null)
                {
                    Print("Cannot execute TREND entry - EMA indicators not initialized");
                    return;
                }

                // V8.10: Use [0] (live tick) for real-time EMA values since Calculate.OnPriceChange updates EMAs on every tick
                double ema9Value = ema9[0];
                double ema15Value = ema15[0];

                // V8.10 DEBUG
                Print(string.Format("TREND DEBUG: ema9[0]={0:F2} ema15[0]={1:F2} Price={2:F2}", ema9Value, ema15Value, currentPrice));
                Print(string.Format("TREND DEBUG: Close[0]={0:F2} CurrentBar={1} BarsInProgress={2}",
                    Close[0], CurrentBar, BarsInProgress));

                // Sanity check: EMAs should be different
                if (Math.Abs(ema9Value - ema15Value) < tickSize * 2)
                {
                    Print(string.Format("WARNING: EMAs very close ({0:F2} vs {1:F2})", ema9Value, ema15Value));
                }

                // Direction: EMA below price = LONG (buying pullback), EMA above = SHORT
                MarketPosition direction;
                if (ema9Value < currentPrice)
                {
                    direction = MarketPosition.Long;
                    Print(string.Format("TREND: EMA9 below price ({0:F2} < {1:F2}) = LONG setup", ema9Value, currentPrice));
                }
                else
                {
                    direction = MarketPosition.Short;
                    Print(string.Format("TREND: EMA9 above price ({0:F2} > {1:F2}) = SHORT setup", ema9Value, currentPrice));
                }

                // V8.31: Both E1 and E2 now use ATR-based stops from live EMAs
                double e1MultTrend = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry1ATRMultiplier;
                double e2MultTrend = isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry2ATRMultiplier;
                Print(string.Format("V12.20: TREND Multiplier -> Mode={0} E1={1:F2}x E2={2:F2}x",
                    isTrendRmaMode ? "RMA" : "STD", e1MultTrend, e2MultTrend));

                double e1StopDist = CalculateATRStopDistance(e1MultTrend); // V12.30: Ceiling-rounded
                double e2StopDist = CalculateATRStopDistance(e2MultTrend); // V12.30: Ceiling-rounded

                // Weighted average stop distance for the group
                double weightedStopDist = (e1StopDist * (1.0/3.0)) + (e2StopDist * (2.0/3.0));

                int totalContracts = CalculatePositionSize(weightedStopDist);

                // Split: 1/3 at 9 EMA, 2/3 at 15 EMA
                int entry1Qty = (int)Math.Ceiling(totalContracts / 3.0);
                int entry2Qty = totalContracts - entry1Qty;

                if (entry1Qty < 1) entry1Qty = 1;
                if (entry2Qty < 1) entry2Qty = 1;

                // Final validation: totalContracts = sum of entries
                totalContracts = entry1Qty + entry2Qty;

                Print(string.Format("TREND RISK: Risk=${0} | E1Stop={1:F2} | E2Stop={2:F2} | WeightedDist={3:F2} | TotalQty={4}",
                    MaxRiskAmount, e1StopDist, e2StopDist, weightedStopDist, totalContracts));
                Print(string.Format("TREND SPLIT: E1Qty={0} (1/3) | E2Qty={1} (2/3)", entry1Qty, entry2Qty));

                string timestamp = DateTime.Now.ToString("HHmmssffff");
                string trendGroupId = "TREND_" + timestamp;
                string entry1Name = trendGroupId + "_E1";
                string entry2Name = trendGroupId + "_E2";

                // V8.31: ENTRY 1: 1/3 at 9 EMA with ATR-based stop from live EMA9
                double entry1Price = ema9Value;
                double e1AtrStop = CalculateATRStopDistance(e1MultTrend);  // V12.30: Ceiling-rounded
                double stop1Price = direction == MarketPosition.Long
                    ? ema9Value - e1AtrStop  // V8.31: Stop is 1.1x ATR below live EMA9
                    : ema9Value + e1AtrStop; // V8.31: Stop is 1.1x ATR above live EMA9

                // ENTRY 2: 2/3 at 15 EMA with ATR trailing stop
                double entry2Price = ema15Value;
                double stop2Price = direction == MarketPosition.Long
                    ? ema15Value - CalculateATRStopDistance(e2MultTrend)
                    : ema15Value + CalculateATRStopDistance(e2MultTrend);

                // Create position info for Entry 1
                PositionInfo pos1 = CreateTRENDPosition(entry1Name, direction, entry1Price, stop1Price,
                    entry1Qty, true, trendGroupId, isTrendRmaMode);
                activePositions[entry1Name] = pos1;

                // Create position info for Entry 2
                PositionInfo pos2 = CreateTRENDPosition(entry2Name, direction, entry2Price, stop2Price,
                    entry2Qty, false, trendGroupId, isTrendRmaMode);
                activePositions[entry2Name] = pos2;

                // Link the entries together
                linkedTRENDEntries[entry1Name] = entry2Name;
                linkedTRENDEntries[entry2Name] = entry1Name;

                // Submit Entry 1 limit order
                Order entryOrder1 = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, entry1Qty, entry1Price, 0, "", entry1Name)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, entry1Qty, entry1Price, 0, "", entry1Name);
                entryOrders[entry1Name] = entryOrder1;

                // Submit Entry 2 limit order
                Order entryOrder2 = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, entry2Qty, entry2Price, 0, "", entry2Name)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, entry2Qty, entry2Price, 0, "", entry2Name);
                entryOrders[entry2Name] = entryOrder2;

                Print(string.Format("TREND ORDERS PLACED: {0} Total={1} contracts",
                    direction == MarketPosition.Long ? "LONG" : "SHORT", totalContracts));
                Print(string.Format("  E1: {0}@{1:F2} (EMA9) | Stop: {2:F2} ({3}xATR from EMA9)",
                    entry1Qty, ema9Value, stop1Price, TRENDEntry1ATRMultiplier));
                Print(string.Format("  E2: {0}@{1:F2} (EMA15) | Stop: {2:F2} ({3}xATR trail)",
                    entry2Qty, ema15Value, stop2Price, TRENDEntry2ATRMultiplier));

                // V12.1: Smart Dispatch to SIMA Fleet
                if (EnableSIMA)
                {
                    // For Trend trades, followers get the full totalContracts qty split by the dispatcher
                    ExecuteSmartDispatchEntry(
                        "TREND",
                        direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort,
                        totalContracts,
                        currentPrice,
                        OrderType.Market,
                        entry1Name,
                        entry2Name);
                }

                // Deactivate TREND mode after placing orders
                DeactivateTRENDMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteTRENDEntry: " + ex.Message);
            }
        }

        private PositionInfo CreateTRENDPosition(string entryName, MarketPosition direction,
            double entryPrice, double stopPrice, int contracts, bool isEntry1, string groupId, bool isRma)
        {
            // V8.2 FIX: TREND uses same multi-target system as RMA
            // T1: 1pt fixed, T2: 0.5x ATR, T3: 1x ATR, T4: Runner
            double target1Price = direction == MarketPosition.Long
                ? entryPrice + Target1FixedPoints
                : entryPrice - Target1FixedPoints;
            double target2Price = direction == MarketPosition.Long
                ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                : entryPrice - (currentATR * RMAT1ATRMultiplier);
            double target3Price = direction == MarketPosition.Long
                ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                : entryPrice - (currentATR * RMAT2ATRMultiplier);

            // V8.2 FIX: Calculate contract distribution (same as RMA)
            int t1Qty, t2Qty, t3Qty, t4Qty;

            if (contracts == 1)
            {
                t1Qty = 1; t2Qty = 0; t3Qty = 0; t4Qty = 0;
            }
            else if (contracts == 2)
            {
                t1Qty = 1; t2Qty = 0; t3Qty = 0; t4Qty = 1;
            }
            else if (contracts == 3)
            {
                t1Qty = 1; t2Qty = 1; t3Qty = 0; t4Qty = 1;
            }
            else if (contracts == 4)
            {
                t1Qty = 1; t2Qty = 1; t3Qty = 1; t4Qty = 1;
            }
            else
            {
                // 5+ contracts: Use percentage split
                t1Qty = (int)Math.Floor(contracts * T1ContractPercent / 100.0);
                t2Qty = (int)Math.Floor(contracts * T2ContractPercent / 100.0);
                t3Qty = (int)Math.Floor(contracts * T3ContractPercent / 100.0);
                t4Qty = contracts - t1Qty - t2Qty - t3Qty;

                if (t1Qty < 1) { t1Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                if (t2Qty < 1) { t2Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                if (t3Qty < 1) { t3Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                if (t4Qty < 1) t4Qty = 1;
            }

            Print(string.Format("TREND POSITION: {0} contracts \u2192 T1:{1} T2:{2} T3:{3} Runner:{4}",
                contracts, t1Qty, t2Qty, t3Qty, t4Qty));

            return new PositionInfo
            {
                SignalName = entryName,
                Direction = direction,
                TotalContracts = contracts,
                T1Contracts = t1Qty,
                T2Contracts = t2Qty,
                T3Contracts = t3Qty,
                T4Contracts = t4Qty,
                RemainingContracts = contracts,
                EntryPrice = entryPrice,
                InitialStopPrice = stopPrice,
                CurrentStopPrice = stopPrice,
                Target1Price = target1Price,
                Target2Price = target2Price,
                Target3Price = target3Price,
                EntryFilled = false,
                T1Filled = false,
                T2Filled = false,
                T3Filled = false,
                BracketSubmitted = false,
                ExtremePriceSinceEntry = entryPrice,
                CurrentTrailLevel = 0,
                IsRMATrade = isRma,
                IsTRENDTrade = true,
                IsTRENDEntry1 = isEntry1,
                IsTRENDEntry2 = !isEntry1,
                LinkedTRENDGroup = groupId
            };
        }

        // V8.4: Execute RETEST entry - auto-detects direction based on price vs OR Mid
        private void ExecuteRetestEntry()
        {
            if (!RetestEnabled)
            {
                Print("RETEST mode is disabled");
                return;
            }

            // V12.1101E [B-2]: Session-scoped latch — one RETEST entry per OR session maximum.
            // Resets automatically in ResetOR() at the start of each new session.
            if (retestFiredThisSession)
            {
                Print("RETEST: Already fired this session — latch active, ignoring duplicate arm");
                return;
            }

            if (!orComplete)
            {
                Print("Cannot execute RETEST - OR not complete yet");
                return;
            }

            if (currentATR <= 0)
            {
                Print("Cannot execute RETEST entry - ATR not available yet");
                return;
            }

            try
            {
                // Use last known price for direction determination
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];

                // Auto-detect direction: Price > OR Mid = LONG, Price < OR Mid = SHORT
                MarketPosition direction;
                double entryPrice;

                if (currentPrice > sessionMid)
                {
                    direction = MarketPosition.Long;
                    entryPrice = sessionHigh;  // Entry at OR High (NO buffer)
                    Print(string.Format("RETEST: Price above OR Mid ({0:F2} > {1:F2}) = LONG at OR High {2:F2}",
                        currentPrice, sessionMid, entryPrice));
                }
                else
                {
                    direction = MarketPosition.Short;
                    entryPrice = sessionLow;   // Entry at OR Low (NO buffer)
                    Print(string.Format("RETEST: Price below OR Mid ({0:F2} < {1:F2}) = SHORT at OR Low {2:F2}",
                        currentPrice, sessionMid, entryPrice));
                }

                // Calculate stop and targets using ATR
                double multToUse = isRetestRmaMode ? RMAStopATRMultiplier : RetestATRMultiplier;
                Print(string.Format("V12.20: RETEST Multiplier -> Mode={0} Using={1:F2}x",
                    isRetestRmaMode ? "RMA" : "STD", multToUse));
                double stopDistance = CalculateATRStopDistance(multToUse); // V12.30: Ceiling-rounded

                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                // T1 = Fixed 1 point profit (same as RMA)
                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;

                // T2 = 0.5x ATR
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                    : entryPrice - (currentATR * RMAT1ATRMultiplier);

                // T3 = 1.0x ATR
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                    : entryPrice - (currentATR * RMAT2ATRMultiplier);

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                string signalName = direction == MarketPosition.Long ? "RetestLong" : "RetestShort";
                string timestamp = DateTime.Now.ToString("HHmmssffff");
                string entryName = signalName + "_" + timestamp;

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    IsRMATrade = isRetestRmaMode,
                    IsTRENDTrade = false,
                    IsRetestTrade = true,              // V8.4: Mark as retest trade
                    RetestTrailActivated = false       // V8.4: Trail not activated yet
                };

                activePositions[entryName] = pos;

                // Submit LIMIT order at OR High/Low (NO buffer)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);

                entryOrders[entryName] = entryOrder;
                retestFiredThisSession = true;  // V12.1101E [B-2]: Arm latch — no further RETEST entries this session

                Print(string.Format("RETEST ENTRY ORDER: {0} {1}@{2:F2} | ATR: {3:F2}", signalName, contracts, entryPrice, currentATR));
                Print(string.Format("RETEST STOP: {0:F2} ({1:F2}x ATR = {2:F2}pts)",
                    stopPrice, RetestATRMultiplier, stopDistance));
                Print(string.Format("RETEST TARGETS: T1:{0}@{1:F2}(+{2:F2}pt) | T2:{3}@{4:F2} | T3:{5}@{6:F2} | T4:{7}@trail",
                    t1Qty, target1Price, Target1FixedPoints,
                    t2Qty, target2Price, t3Qty, target3Price, t4Qty));

                // V12.1: Smart Dispatch to SIMA Fleet
                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry(
                        "RETEST",
                        direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort,
                        contracts,
                        entryPrice,
                        OrderType.Limit,
                        entryName);
                }

                // Deactivate RETEST mode after entry (one-shot)
                DeactivateRetestMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteRetestEntry: " + ex.Message);
            }
        }

        private void ActivateTRENDMode()
        {
            isTRENDModeActive = true;
        }

        private void DeactivateTRENDMode()
        {
            isTRENDModeActive = false;
        }

        // V12.44: UpdateTRENDModeDisplay() removed — legacy chart UI no-op

        private void ActivateRetestMode()
        {
            isRetestModeActive = true;
        }

        private void DeactivateRetestMode()
        {
            isRetestModeActive = false;
        }

        // V12.44: UpdateRetestModeDisplay() removed — legacy chart UI no-op

        #endregion

        #region Manual Entry Methods (V12.27 - Contextual UI)

        /// <summary>
        /// V12.27: TREND manual entry at user-specified price with 100% risk allocation.
        /// Uses full MaxRiskAmount (no 1/3 + 2/3 split like standard TREND).
        /// Submits a single limit order at the manual price.
        /// </summary>
        private void ExecuteTRENDManualEntry(double manualPrice, MarketPosition direction)
        {
            if (currentATR <= 0)
            {
                Print("V12.27 TREND_MANUAL: Ignored - ATR not available");
                return;
            }

            try
            {
                double entryPrice = Instrument.MasterInstrument.RoundToTickSize(manualPrice);

                // V12.27: 100% risk allocation - single position at manual price
                // Stop uses RMA multiplier (Trend RMA Mode forced)
                double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded
                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                    : entryPrice - (currentATR * RMAT1ATRMultiplier);
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                    : entryPrice - (currentATR * RMAT2ATRMultiplier);

                // V12.27: 100% risk - full position size, no split
                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                string signalName = direction == MarketPosition.Long ? "TrendMnlLong" : "TrendMnlShort";
                string entryName = signalName + "_" + DateTime.Now.ToString("HHmmssffff");

                PositionInfo pos = CreateTRENDPosition(entryName, direction, entryPrice, stopPrice,
                    contracts, true, "TMNL_" + DateTime.Now.Ticks, true);
                activePositions[entryName] = pos;

                // Submit LIMIT order at manual price
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);
                entryOrders[entryName] = entryOrder;

                Print(string.Format("V12.27 TREND_MANUAL: {0} {1}@{2:F2} LIMIT | Stop: {3:F2} | 100% Risk",
                    direction, contracts, entryPrice, stopPrice));
                Print(string.Format("V12.27 TREND_MANUAL TARGETS: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2}",
                    t1Qty, target1Price, t2Qty, target2Price, t3Qty, target3Price));

                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry(
                        "TREND_MNL",
                        direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort,
                        contracts,
                        entryPrice,
                        OrderType.Limit,
                        entryName);
                }

            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteTRENDManualEntry: " + ex.Message);
            }
        }

        /// <summary>
        /// V12.27: RETEST manual entry at user-specified price using Limit Order with RMA targets.
        /// Uses RMA stop multiplier regardless of the R toggle state.
        /// </summary>
        private void ExecuteRetestManualEntry(double manualPrice, MarketPosition direction)
        {
            if (currentATR <= 0)
            {
                Print("V12.27 RETEST_MANUAL: Ignored - ATR not available");
                return;
            }

            try
            {
                double entryPrice = Instrument.MasterInstrument.RoundToTickSize(manualPrice);

                // V12.27: Always uses RMA multiplier for manual retest entries
                double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded
                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                    : entryPrice - (currentATR * RMAT1ATRMultiplier);
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                    : entryPrice - (currentATR * RMAT2ATRMultiplier);

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                string signalName = direction == MarketPosition.Long ? "RetestMnlLong" : "RetestMnlShort";
                string entryName = signalName + "_" + DateTime.Now.ToString("HHmmssffff");

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    IsRMATrade = true,  // Uses RMA targets
                    IsRetestTrade = true,
                    RetestTrailActivated = false
                };

                activePositions[entryName] = pos;

                // Submit LIMIT order at manual price
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);
                entryOrders[entryName] = entryOrder;

                Print(string.Format("V12.27 RETEST_MANUAL: {0} {1}@{2:F2} LIMIT | Stop: {3:F2} | RMA Targets",
                    direction, contracts, entryPrice, stopPrice));
                Print(string.Format("V12.27 RETEST_MANUAL TARGETS: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2}",
                    t1Qty, target1Price, t2Qty, target2Price, t3Qty, target3Price));

                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry(
                        "RETEST_MNL",
                        direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort,
                        contracts,
                        entryPrice,
                        OrderType.Limit,
                        entryName);
                }

            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteRetestManualEntry: " + ex.Message);
            }
        }

        /// <summary>
        /// V12.27: FFMA manual entry using Limit Order at user-specified price.
        /// Uses ATR-based stop (same as standard FFMA but with Limit instead of Market).
        /// </summary>
        private void ExecuteFFMALimitEntry(double manualPrice, MarketPosition direction)
        {
            if (currentATR <= 0)
            {
                Print("V12.27 FFMA_LIMIT: Ignored - ATR not available");
                return;
            }

            try
            {
                double entryPrice = Instrument.MasterInstrument.RoundToTickSize(manualPrice);

                // V12.27: ATR-based stop (mirrors standard FFMA but won't use candle high/low since manual)
                double stopDistance = CalculateATRStopDistance(RMAStopATRMultiplier); // V12.30: Ceiling-rounded
                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                if (stopDistance < tickSize * 2)
                {
                    Print(string.Format("V12.27 FFMA_LIMIT: Stop too tight ({0:F2}pts) - using 2 tick minimum", stopDistance));
                    stopPrice = direction == MarketPosition.Long
                        ? entryPrice - (tickSize * 2)
                        : entryPrice + (tickSize * 2);
                    stopDistance = tickSize * 2;
                }

                // V12.44: Final stop-distance guard — prevent CalculatePositionSize(0) → ∞ contracts
                if (stopDistance <= 0)
                {
                    Print("[FFMA_LIMIT REJECT] Stop distance is zero after ATR calc. Aborting entry.");
                    return;
                }

                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                    : entryPrice - (currentATR * RMAT1ATRMultiplier);
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                    : entryPrice - (currentATR * RMAT2ATRMultiplier);

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                string signalName = direction == MarketPosition.Long ? "FFMAMnlLong" : "FFMAMnlShort";
                string entryName = signalName + "_" + DateTime.Now.ToString("HHmmssffff");

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    IsRMATrade = false,
                    IsFFMATrade = true
                };

                activePositions[entryName] = pos;

                // V12.27: Submit LIMIT order (not Market like standard FFMA)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);
                entryOrders[entryName] = entryOrder;

                Print(string.Format("V12.27 FFMA_LIMIT: {0} {1}@{2:F2} LIMIT | Stop: {3:F2} | ATR-based",
                    direction, contracts, entryPrice, stopPrice));
                Print(string.Format("V12.27 FFMA_LIMIT TARGETS: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2}",
                    t1Qty, target1Price, t2Qty, target2Price, t3Qty, target3Price));

                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("FFMA_MNL", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Limit, entryName);
                }

                DeactivateFFMAMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteFFMALimitEntry: " + ex.Message);
            }
        }

        /// <summary>
        /// V12.27: FFMA Manual Market entry — instant market order, direction toward 9 EMA.
        /// Stop at entry candle high/low (same as Auto FFMA).
        /// </summary>
        private void ExecuteFFMAManualMarketEntry()
        {
            if (currentATR <= 0)
            {
                Print("V12.27 FFMA_MANUAL_MARKET: Ignored - ATR not available");
                return;
            }

            if (ema9 == null)
            {
                Print("V12.27 FFMA_MANUAL_MARKET: Ignored - EMA9 not initialized");
                return;
            }

            try
            {
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                double ema9Value = ema9[0];

                // V12.27: Direction always toward 9 EMA
                // Price below EMA9 = LONG (price moving up toward EMA)
                // Price above EMA9 = SHORT (price moving down toward EMA)
                MarketPosition direction;
                if (currentPrice < ema9Value)
                {
                    direction = MarketPosition.Long;
                    Print(string.Format("V12.27 FFMA_MANUAL_MARKET: Price below EMA9 ({0:F2} < {1:F2}) = LONG toward EMA",
                        currentPrice, ema9Value));
                }
                else
                {
                    direction = MarketPosition.Short;
                    Print(string.Format("V12.27 FFMA_MANUAL_MARKET: Price above EMA9 ({0:F2} > {1:F2}) = SHORT toward EMA",
                        currentPrice, ema9Value));
                }

                double entryPrice = currentPrice; // Market order

                // Stop at entry candle high/low (same as Auto FFMA)
                double stopPrice = direction == MarketPosition.Long ? Low[0] : High[0];
                double stopDistance = Math.Min(Math.Abs(entryPrice - stopPrice), MaximumStop);

                if (stopDistance < tickSize * 2)
                {
                    Print(string.Format("V12.27 FFMA_MANUAL_MARKET: Stop too tight ({0:F2}pts) - using 2 tick minimum", stopDistance));
                    stopPrice = direction == MarketPosition.Long
                        ? entryPrice - (tickSize * 2)
                        : entryPrice + (tickSize * 2);
                    stopDistance = tickSize * 2;
                }

                // V12.44: Final stop-distance guard — prevent CalculatePositionSize(0) → ∞ contracts
                if (stopDistance <= 0)
                {
                    Print("[FFMA_MANUAL_MARKET REJECT] Stop distance is zero (doji candle?). Aborting entry.");
                    return;
                }

                double target1Price = direction == MarketPosition.Long
                    ? entryPrice + Target1FixedPoints
                    : entryPrice - Target1FixedPoints;
                double target2Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT1ATRMultiplier)
                    : entryPrice - (currentATR * RMAT1ATRMultiplier);
                double target3Price = direction == MarketPosition.Long
                    ? entryPrice + (currentATR * RMAT2ATRMultiplier)
                    : entryPrice - (currentATR * RMAT2ATRMultiplier);

                int contracts = CalculatePositionSize(stopDistance);
                int t1Qty, t2Qty, t3Qty, t4Qty;
                GetTargetDistribution(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                string signalName = direction == MarketPosition.Long ? "FFMAMnlMktLong" : "FFMAMnlMktShort";
                string entryName = signalName + "_" + DateTime.Now.ToString("HHmmssffff");

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    IsRMATrade = false,
                    IsFFMATrade = true
                };

                activePositions[entryName] = pos;

                // Submit MARKET order (immediate execution)
                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, contracts, 0, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Market, contracts, 0, 0, "", entryName);
                entryOrders[entryName] = entryOrder;

                Print(string.Format("V12.27 FFMA_MANUAL_MARKET: {0} {1}@MARKET | Stop: {2:F2} (candle {3}) | Toward EMA9={4:F2}",
                    direction, contracts, stopPrice, direction == MarketPosition.Long ? "low" : "high", ema9Value));
                Print(string.Format("V12.27 FFMA_MANUAL_MARKET TARGETS: T1:{0}@{1:F2} | T2:{2}@{3:F2} | T3:{4}@{5:F2}",
                    t1Qty, target1Price, t2Qty, target2Price, t3Qty, target3Price));

                if (EnableSIMA)
                {
                    ExecuteSmartDispatchEntry("FFMA_MNL_MKT", direction == MarketPosition.Long ? OrderAction.Buy : OrderAction.SellShort, contracts, entryPrice, OrderType.Market, entryName);
                }

                DeactivateFFMAMode();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteFFMAManualMarketEntry: " + ex.Message);
            }
        }

        #endregion
    }
}
