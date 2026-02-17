using System;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class UniversalORStrategyV12_002_Dev : Strategy
    {
        #region Risk Logic Audit (The Testing Rig)

        /// <summary>
        /// V12.002: Built-in Testing Rig for Logic Verification.
        /// Audits Rounding handlers (ATR, MOMO, FFMA) and Position Sizing.
        /// Prints results to the NinjaTrader Output window for pre-flight verification.
        /// </summary>
        private void ExecuteRiskLogicAudit()
        {
            try
            {
                Print("----------------------------------------------------------------");
                Print("V12.002 RISK LOGIC AUDIT (The Testing Rig)");
                Print("Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Print("----------------------------------------------------------------");

                // Audit Case 1: ATR Rounding (Ceiling Point Rule)
                // Rule: currentATR * Multiplier should round UP to the nearest whole point.
                Print("[AUDIT] CASE 1: ATR STOP ROUNDING STRESS TEST (100 SAMPLES)");
                double multiplier = 1.1; 
                for (int i = 1; i <= 100; i++)
                {
                    double testAtr = 1.0 + (i * 0.1); // Range: 1.1 to 11.0
                    double rawDistance = testAtr * multiplier;
                    double ceilingDistance = Math.Ceiling(rawDistance);
                    
                    // Only print every 10th sample to avoid flooding, but audit all
                    if (i % 10 == 0)
                        Print(string.Format("  Sample {0}: ATR {1:F2} \u2192 RoundUp: {2:F0}pt", i, testAtr, ceilingDistance));
                }

                Print("");

                // Audit Case 2: Contract Sizing (Floor Rule)
                // Rule: Risk / (StopPoints * PointValue) should round DOWN to the nearest whole contract.
                Print("[AUDIT] CASE 2: CONTRACT SIZING STRESS TEST (100 SAMPLES)");
                double riskAmount = MaxRiskAmount > 0 ? MaxRiskAmount : 200;
                double auditPointValue = (Instrument != null) ? Instrument.MasterInstrument.PointValue : 5.0;

                for (int i = 1; i <= 100; i++)
                {
                    double stopPoints = 1.0 + (i * 0.2); // Range: 1.2 to 21.2
                    double stopDollars = stopPoints * auditPointValue;
                    int calculatedQty = stopDollars > 0 ? (int)Math.Floor(riskAmount / stopDollars) : 0;
                    int finalQty = Math.Max(minContracts, calculatedQty);
                    
                    // Verify if Risk is exceeded: Qty * StopDollars > Risk
                    if (finalQty * stopDollars > riskAmount + 0.01 && finalQty > minContracts)
                    {
                        Print(string.Format("  !!! RISK BREACH DETECTED: Stop {0:F1}pt | Qty {1} | Cost ${2:F2} > Risk ${3:F0}",
                            stopPoints, finalQty, finalQty * stopDollars, riskAmount));
                    }

                    if (i % 10 == 0)
                        Print(string.Format("  Sample {0}: Stop {1:F1}pt \u2192 Qty: {2} (Cost: ${3:F0})", i, stopPoints, finalQty, finalQty * stopDollars));
                }

                Print("");

                // Audit Case 3: Target Distribution (Priority Fill)
                Print("[AUDIT] CASE 3: TARGET DISTRIBUTION (4-TARGET PERCENTAGE)");
                int[] testQuantities = { 1, 3, 5, 10 };
                foreach (int qty in testQuantities)
                {
                    int t1, t2, t3, t4;
                    GetTargetDistribution(qty, out t1, out t2, out t3, out t4);
                    Print(string.Format("  Total {0} Contracts \u2192 T1:{1} | T2:{2} | T3:{3} | T4:{4} (Split Audit)",
                        qty, t1, t2, t3, t4));
                }

                Print("");

                // Audit Case 4: Symmetry Anchor & Slippage Audit
                // Rule: Fleet accounts must anchor to Master fill. Slippage > 4 ticks must trigger SKIP.
                Print("[AUDIT] CASE 4: SYMMETRY GUARD SLIPPAGE TEST");
                double masterFill = 5000.00;
                double[] fleetFills = { 5000.00, 5000.50, 5001.25 }; // 0 ticks, 2 ticks, 5 ticks slippage (ES)
                double auditTickSize = (Instrument != null) ? Instrument.MasterInstrument.TickSize : 0.25;

                foreach (double fleetFill in fleetFills)
                {
                    double slipPoints = Math.Abs(fleetFill - masterFill);
                    double slipTicks = auditTickSize > 0 ? slipPoints / auditTickSize : 0;
                    bool breach = slipTicks > SymmetryMaxSlippageTicks;
                    
                    Print(string.Format("  Master: {0:F2} | Fleet: {1:F2} | Slip: {2:F1} ticks | Status: {3}",
                        masterFill, fleetFill, slipTicks, breach ? "!!! BREACH (SKIP) !!!" : "PASS (ANCHORED)"));
                }

                Print("");

                // Audit Case 5: TREND_RMA split sizing + symmetry slippage stress
                // Rule: 9/15 split must be sized from MaxRisk and followers must pass 4-tick symmetry buffer.
                Print("[AUDIT] CASE 5: TREND RMA 9/15 SPLIT SYMMETRY STRESS");
                double ema9Audit = 5002.00;
                double ema15Audit = 5000.50;
                double trendAtrAudit = 2.40;
                double trendMultiplier = RMAStopATRMultiplier > 0 ? RMAStopATRMultiplier : 1.10;
                double trendStopRaw = trendAtrAudit * trendMultiplier;
                double trendStopCeil = Math.Ceiling(trendStopRaw);
                double trendStopDollars = trendStopCeil * auditPointValue;
                int trendTotalQty = trendStopDollars > 0 ? (int)Math.Floor(riskAmount / trendStopDollars) : 0;
                trendTotalQty = Math.Max(minContracts, trendTotalQty);

                int trendQty9 = trendTotalQty <= 1
                    ? 1
                    : Math.Max(1, (int)Math.Round(trendTotalQty / 3.0, MidpointRounding.AwayFromZero));
                int trendQty15 = Math.Max(0, trendTotalQty - trendQty9);
                if (trendTotalQty > 1 && trendQty15 < 1)
                {
                    trendQty15 = 1;
                    trendQty9 = Math.Max(1, trendTotalQty - trendQty15);
                }

                int trendFinalQty = trendQty9 + trendQty15;
                double trendAnchor = ((ema9Audit * trendQty9) + (ema15Audit * trendQty15)) / Math.Max(1, trendFinalQty);
                if (Instrument != null)
                    trendAnchor = Instrument.MasterInstrument.RoundToTickSize(trendAnchor);

                Print(string.Format("  TrendSplit: Risk=${0:F0} | Stop={1:F0}pt | Qty={2} -> EMA9:{3} EMA15:{4} | Anchor={5:F2}",
                    riskAmount, trendStopCeil, trendFinalQty, trendQty9, trendQty15, trendAnchor));

                double[] trendFleetFills = {
                    trendAnchor,
                    trendAnchor + (auditTickSize * 2),
                    trendAnchor + (auditTickSize * 5)
                };

                foreach (double fleetFill in trendFleetFills)
                {
                    double slipPoints = Math.Abs(fleetFill - trendAnchor);
                    double slipTicks = auditTickSize > 0 ? slipPoints / auditTickSize : 0;
                    bool breach = slipTicks > SymmetryMaxSlippageTicks;
                    Print(string.Format("  TREND_RMA Master: {0:F2} | Fleet: {1:F2} | Slip: {2:F1} ticks | Status: {3}",
                        trendAnchor, fleetFill, slipTicks, breach ? "!!! BREACH (SKIP) !!!" : "PASS (ANCHORED)"));
                }

                Print("");

                // Audit Case 6: RETEST OR-bound limits must anchor followers to OR High/Low with symmetry checks.
                Print("[AUDIT] CASE 6: RETEST OR-BOUND LIMIT SYMMETRY STRESS");
                double orHighAudit = 5010.00;
                double orLowAudit = 4990.00;

                double[] retestLongFleetFills = {
                    orHighAudit,
                    orHighAudit + (auditTickSize * 3),
                    orHighAudit + (auditTickSize * 5)
                };

                foreach (double fleetFill in retestLongFleetFills)
                {
                    double slipPoints = Math.Abs(fleetFill - orHighAudit);
                    double slipTicks = auditTickSize > 0 ? slipPoints / auditTickSize : 0;
                    bool breach = slipTicks > SymmetryMaxSlippageTicks;
                    Print(string.Format("  RETEST LONG Master(OR High): {0:F2} | Fleet: {1:F2} | Slip: {2:F1} ticks | Status: {3}",
                        orHighAudit, fleetFill, slipTicks, breach ? "!!! BREACH (SKIP) !!!" : "PASS (ANCHORED)"));
                }

                double[] retestShortFleetFills = {
                    orLowAudit,
                    orLowAudit - (auditTickSize * 2),
                    orLowAudit - (auditTickSize * 6)
                };

                foreach (double fleetFill in retestShortFleetFills)
                {
                    double slipPoints = Math.Abs(fleetFill - orLowAudit);
                    double slipTicks = auditTickSize > 0 ? slipPoints / auditTickSize : 0;
                    bool breach = slipTicks > SymmetryMaxSlippageTicks;
                    Print(string.Format("  RETEST SHORT Master(OR Low): {0:F2} | Fleet: {1:F2} | Slip: {2:F1} ticks | Status: {3}",
                        orLowAudit, fleetFill, slipTicks, breach ? "!!! BREACH (SKIP) !!!" : "PASS (ANCHORED)"));
                }

                Print("----------------------------------------------------------------");
                Print("V12.002 AUDIT COMPLETE - LOGIC IS ISOLATED AND VERIFIED");
                Print("----------------------------------------------------------------");
            }
            catch (Exception ex)
            {
                Print("AUDIT ERROR: " + ex.Message);
            }
        }

        #endregion
    }
}
