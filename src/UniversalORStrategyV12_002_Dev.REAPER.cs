// V12.17 THREADING FIX: Reaper (Safety Hub) Module
// REAPER Module (Extracted)
// FIX: acct.Flatten() calls moved from background thread → strategy thread via TriggerCustomEvent
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class UniversalORStrategyV12_002_Dev : Strategy
    {
        #region V12 REAPER Audit Logic

        // V12.17: Queue for flatten requests marshaled from background thread → strategy thread
        private ConcurrentQueue<string> _reaperFlattenQueue = new ConcurrentQueue<string>();

        /// <summary>
        /// V12 SIMA: Start the Reaper audit background thread
        /// </summary>
        private void StartReaperAudit()
        {
            if (isReaperRunning) return;

            isReaperRunning = true;
            reaperThread = new Thread(ReaperLoop)
            {
                IsBackground = true,
                Name = "V12_Reaper_Audit"
            };
            reaperThread.Start();
            Print("[REAPER] Audit thread STARTED - interval: " + ReaperIntervalMs + "ms");
        }

        /// <summary>
        /// V12 SIMA: Stop the Reaper audit background thread
        /// </summary>
        private void StopReaperAudit()
        {
            if (!isReaperRunning) return;

            isReaperRunning = false;
            try
            {
                if (reaperThread != null && reaperThread.IsAlive)
                {
                    reaperThread.Join(2000); // Wait up to 2 seconds
                }
            }
            catch { }
            Print("[REAPER] Audit thread STOPPED");
        }

        /// <summary>
        /// V12 SIMA: Reaper main loop - audits positions every ReaperIntervalMs
        /// </summary>
        private void ReaperLoop()
        {
            Print("[REAPER] Loop started - monitoring account positions...");

            while (isReaperRunning)
            {
                try
                {
                    Thread.Sleep(ReaperIntervalMs);
                    if (!isReaperRunning) break;

                    // V12.8: Pause auditing while a flatten is actively running to prevent race conditions
                    if (isFlattenRunning) continue;

                    // V12.Hardening: Only audit in live/realtime — skip historical replay
                    if (State != State.Realtime) continue;

                    AuditApexPositions();
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Print("[REAPER] ERROR: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// V12 SIMA: Audit all Apex account positions for desync
        /// If any account has a position that doesn't match expected, log it
        /// </summary>
        private void AuditApexPositions()
        {
            // Throttle logging to once per 30 seconds
            bool shouldLog = (DateTime.Now - lastReaperLog).TotalSeconds >= 30;
            int auditedCount = 0;
            int activeCount = 0;

            foreach (Account acct in Account.All)
            {
                if (acct.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    auditedCount++;

                    // Get actual position on this instrument
                    Position pos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                    int actualQty = 0;

                    if (pos != null && pos.MarketPosition != MarketPosition.Flat)
                    {
                        actualQty = pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;
                    }

                    // Compare with expected
                    int expectedQty = 0;
                    expectedPositions.TryGetValue(acct.Name, out expectedQty);

                    // V12.9: Only log individual accounts when they have non-zero state (reduces spam)
                    if (shouldLog && (expectedQty != 0 || actualQty != 0))
                    {
                        Print($"[REAPER] {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
                        activeCount++;
                    }

                    // Desync detection (V12.1 Path B: Hybrid Recovery)
                    if (expectedQty != actualQty)
                    {
                        // V12.1: Filter Legal Desyncs
                        // If Follower is FLAT but Master is POSITIVE (Expected), this is a "Legal Pull" (Path B target hit).
                        // We do NOT flatten or panic here.
                        if (actualQty == 0 && expectedQty != 0)
                        {
                            if (shouldLog) Print($"[REAPER] {acct.Name} is Flat (Path B Target/Stop hit). Master still active.");
                            continue; 
                        }

                        // CRITICAL: Opposite direction or Ghost position (Active but shouldn't be)
                        bool isCriticalDesync = (actualQty != 0 && expectedQty == 0) || (Math.Sign(actualQty) != Math.Sign(expectedQty) && expectedQty != 0);

                        if (isCriticalDesync)
                        {
                            // V12.8: Throttle CRITICAL DESYNC logging to same shouldLog cadence to prevent output spam
                            if (shouldLog)
                                Print($"[REAPER] 🚨 CRITICAL DESYNC on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");

                            if (AutoFlattenDesync)
                            {
                                if (shouldLog)
                                    Print($"[REAPER] 💀 QUEUING FLATTEN for {acct.Name} - Emergency Re-sync!");
                                // V12.17 FIX: Queue flatten for strategy thread (was: acct.Flatten() on background thread = DEADLOCK)
                                _reaperFlattenQueue.Enqueue(acct.Name);
                                try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); } catch { }
                            }
                        }
                        else
                        {
                            // Minor qty mismatch or other non-critical state
                            if (shouldLog) Print($"[REAPER] Minor Desync on {acct.Name}: Expected={expectedQty}, Actual={actualQty}");
                        }
                    }
                }
            }

            // V12.12: Explicitly audit the Master account if it was NOT covered by the prefix filter.
            // Bug fix: Master "Sim101" with AccountPrefix "Apex" was invisible to the Reaper.
            bool masterAudited = Account.Name.IndexOf(AccountPrefix, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!masterAudited)
            {
                auditedCount++;

                Position masterPos = Account.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                int masterActualQty = 0;
                if (masterPos != null && masterPos.MarketPosition != MarketPosition.Flat)
                {
                    masterActualQty = masterPos.MarketPosition == MarketPosition.Long ? masterPos.Quantity : -masterPos.Quantity;
                }

                int masterExpectedQty = 0;
                expectedPositions.TryGetValue(Account.Name, out masterExpectedQty);

                if (shouldLog && (masterExpectedQty != 0 || masterActualQty != 0))
                {
                    Print($"[REAPER] {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
                    activeCount++;
                }

                if (masterExpectedQty != masterActualQty)
                {
                    if (masterActualQty == 0 && masterExpectedQty != 0)
                    {
                        if (shouldLog) Print($"[REAPER] {Account.Name} (Master) is Flat (Target/Stop hit). Expected was {masterExpectedQty}.");
                    }
                    else
                    {
                        bool isCriticalDesync = (masterActualQty != 0 && masterExpectedQty == 0) || (Math.Sign(masterActualQty) != Math.Sign(masterExpectedQty) && masterExpectedQty != 0);

                        if (isCriticalDesync)
                        {
                            if (shouldLog)
                                Print($"[REAPER] CRITICAL DESYNC on {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");

                            if (AutoFlattenDesync)
                            {
                                if (shouldLog)
                                    Print($"[REAPER] QUEUING FLATTEN for {Account.Name} (Master) - Emergency Re-sync!");
                                // V12.17 FIX: Queue flatten for strategy thread (was: Account.Flatten() on background thread = DEADLOCK)
                                _reaperFlattenQueue.Enqueue(Account.Name);
                                try { TriggerCustomEvent(o => ProcessReaperFlattenQueue(), null); } catch { }
                            }
                        }
                        else
                        {
                            if (shouldLog) Print($"[REAPER] Minor Desync on {Account.Name} (Master): Expected={masterExpectedQty}, Actual={masterActualQty}");
                        }
                    }
                }
            }

            if (shouldLog)
            {
                // V12.9: Single summary line instead of 12 "Expected=0, Actual=0" per cycle
                if (activeCount == 0)
                    Print($"[REAPER] Heartbeat: All {auditedCount} accounts flat.");
                else
                    Print($"[REAPER] Heartbeat: {activeCount}/{auditedCount} accounts with positions.");
                lastReaperLog = DateTime.Now;
            }
        }

        /// <summary>
        /// V12.17 FIX: Processes queued flatten requests on the strategy thread.
        /// Called via TriggerCustomEvent from the Reaper background thread.
        /// This is the SAFE way to call Account.Flatten() — same pattern as IPC.
        /// </summary>
        private void ProcessReaperFlattenQueue()
        {
            string accountName;
            while (_reaperFlattenQueue.TryDequeue(out accountName))
            {
                try
                {
                    // Find the account by name
                    Account targetAcct = null;
                    foreach (Account acct in Account.All)
                    {
                        if (acct.Name == accountName)
                        {
                            targetAcct = acct;
                            break;
                        }
                    }

                    // Also check if it's the Master account
                    if (targetAcct == null && Account.Name == accountName)
                        targetAcct = Account;

                    if (targetAcct != null)
                    {
                        targetAcct.Flatten(new[] { Instrument });
                        expectedPositions[accountName] = 0;
                        Print($"[REAPER] ✓ MARSHAL-FLATTEN executed on strategy thread for {accountName}");
                    }
                    else
                    {
                        Print($"[REAPER] ✗ Could not find account '{accountName}' for marshal-flatten");
                    }
                }
                catch (Exception ex)
                {
                    Print($"[REAPER] ✗ MARSHAL-FLATTEN FAILED for {accountName}: {ex.Message}");
                }
            }
        }

        #endregion
    }
}
