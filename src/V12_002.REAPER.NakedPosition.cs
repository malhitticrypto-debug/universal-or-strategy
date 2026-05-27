// <copyright file="V12_002.REAPER.NakedPosition.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12 REAPER Naked Position Detection Module
// Build 1111.007-reaper-t1: Extracted from REAPER.Audit.cs and REAPER.NakedStop.cs
// Jane Street Alignment: Atomic state transitions, wait-free progress, bounded latency
using System;
using System.Collections.Concurrent;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12 REAPER Naked Position Detection

        // Grace period tracking (key = account name)
        private ConcurrentDictionary<string, DateTime> _nakedPositionFirstSeen =
            new ConcurrentDictionary<string, DateTime>();

        // In-flight guard (key = expectedKey = accountName_instrumentName)
        private readonly ConcurrentDictionary<string, byte> _reaperNakedStopInFlight =
            new ConcurrentDictionary<string, byte>();

        // Emergency stop queue (marshalled to strategy thread)
        private ConcurrentQueue<(string AccountName, MarketPosition Direction, int Qty)> _reaperNakedStopQueue =
            new ConcurrentQueue<(string, MarketPosition, int)>();

        /// <summary>
        /// Detects naked positions (position without working stop) and enqueues emergency stop after grace period.
        /// Thread-safe: Called from audit thread via TriggerCustomEvent marshalling.
        /// Jane Street Alignment: Atomic state transitions via GetOrAdd (TOCTOU-safe).
        /// </summary>
        /// <param name="acct">Account to check</param>
        /// <param name="pos">Broker position (non-null, non-flat)</param>
        /// <param name="actualQty">Signed quantity (positive=long, negative=short)</param>
        /// <param name="expectedKey">Composite key for in-flight guard (accountName_instrumentName)</param>
        /// <param name="shouldLog">Enable diagnostic logging</param>
        /// <param name="pendingStopReplacements">Stop-replace queue for suppression check</param>
        /// <param name="activePositions">Position info dictionary for stop-replace lookup</param>
        /// <returns>True if emergency stop was enqueued, false otherwise</returns>
        private bool DetectNakedPosition(
            Account acct,
            Position pos,
            int actualQty,
            string expectedKey,
            bool shouldLog,
            ConcurrentDictionary<string, PendingStopReplacement> pendingStopReplacements,
            ConcurrentDictionary<string, PositionInfo> activePositions
        )
        {
            // H17-GUARD: Prevent new enqueues after shutdown initiated
            if (_isTerminating)
            {
                return false;
            }

            // Check for pending stop-replace (suppression logic)
            if (CheckPendingStopReplace(acct, pendingStopReplacements, activePositions))
            {
                _nakedPositionFirstSeen.TryRemove(acct.Name, out _);
                if (shouldLog)
                {
                    Print(string.Format("[REAPER] {0}: Stop replace in flight -- suppressing naked audit.", acct.Name));
                }
                return false;
            }

            // Evaluate grace period
            int graceSeconds = (NakedPositionGraceSec >= 5) ? NakedPositionGraceSec : 5;
            if (!EvaluateNakedPositionGrace(acct.Name, actualQty, graceSeconds, shouldLog))
            {
                return false; // Grace active or just started
            }

            // Grace expired - enqueue emergency stop
            DateTime firstSeen = _nakedPositionFirstSeen[acct.Name]; // Safe: GetOrAdd already called in EvaluateNakedPositionGrace
            double graceElapsed = (DateTime.UtcNow - firstSeen).TotalSeconds;
            return EnqueueEmergencyStop(acct.Name, pos, actualQty, expectedKey, graceElapsed);
        }

        /// <summary>
        /// Checks if account has a pending stop-replace operation (suppresses naked position detection).
        /// </summary>
        /// <param name="acct">Account to check</param>
        /// <param name="pendingStopReplacements">Stop-replace queue</param>
        /// <param name="activePositions">Position info dictionary</param>
        /// <returns>True if stop-replace is in flight for this account</returns>
        private bool CheckPendingStopReplace(
            Account acct,
            ConcurrentDictionary<string, PendingStopReplacement> pendingStopReplacements,
            ConcurrentDictionary<string, PositionInfo> activePositions
        )
        {
            foreach (var psr in pendingStopReplacements.Values)
            {
                PositionInfo psrPos;
                if (
                {
                    activePositions.TryGetValue(psr.EntryName, out psrPos)
                }
                    && psrPos != null
                    && psrPos.ExecutingAccount != null
                    && psrPos.ExecutingAccount.Name == acct.Name
                )
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Evaluates naked position grace period. Records first detection or checks elapsed time.
        /// TOCTOU-safe: Uses GetOrAdd for atomic timestamp initialization.
        /// Jane Street Alignment: Atomic state transition via CAS operation.
        /// </summary>
        /// <param name="accountName">Account name</param>
        /// <param name="actualQty">Signed quantity</param>
        /// <param name="graceSeconds">Configurable grace period (default 5s, min 5s)</param>
        /// <param name="shouldLog">Enable diagnostic logging</param>
        /// <returns>True if grace period has expired, false if still active or just started</returns>
        private bool EvaluateNakedPositionGrace(string accountName, int actualQty, int graceSeconds, bool shouldLog)
        {
            // TOCTOU FIX: GetOrAdd is atomic (CAS operation)
            DateTime firstSeen = _nakedPositionFirstSeen.GetOrAdd(accountName, DateTime.UtcNow);
            double graceElapsed = (DateTime.UtcNow - firstSeen).TotalSeconds;

            if (graceElapsed < graceSeconds)
            {
                // Grace active - log on first detection only (within first 500ms)
                if (graceElapsed < 0.5)
                {
                    Print(
                        string.Format(
                            "[REAPER][NAKED_POSITION] {0}: {1}ct naked -- starting {2}s grace window.",
                            accountName,
                            actualQty,
                            graceSeconds
                        )
                    );
                }
                return false; // Grace active
            }

            return true; // Grace expired
        }

        /// <summary>
        /// Enqueues emergency stop request with in-flight guard (prevents duplicates).
        /// Jane Street Alignment: Wait-free progress via TryAdd (fail-fast on duplicate).
        /// </summary>
        /// <param name="accountName">Account name</param>
        /// <param name="pos">Broker position</param>
        /// <param name="actualQty">Signed quantity</param>
        /// <param name="expectedKey">Composite key for in-flight guard</param>
        /// <param name="graceElapsed">Elapsed grace time (for logging)</param>
        /// <returns>True if enqueued, false if already in-flight</returns>
        private bool EnqueueEmergencyStop(
            string accountName,
            Position pos,
            int actualQty,
            string expectedKey,
            double graceElapsed
        )
        {
            // H16-FIX: Atomic TryAdd check prevents duplicate naked stop submissions.
            if (!_reaperNakedStopInFlight.TryAdd(expectedKey, 0))
            {
                // Already in flight - skip
                return false;
            }

            Print(
                string.Format(
                    "[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
                    accountName,
                    actualQty,
                    graceElapsed
                )
            );

            _reaperNakedStopQueue.Enqueue((accountName, pos.MarketPosition, Math.Abs(actualQty)));
            return true;
        }

        /// <summary>
        /// Calculates ATR-bounded emergency stop price with defensive floor.
        /// Jane Street Alignment: Bounded latency via deterministic calculation (no I/O, no blocking).
        /// </summary>
        /// <param name="direction">Position direction (Long/Short)</param>
        /// <param name="currentClose">Current bar close price</param>
        /// <param name="qty">Position quantity (unsigned)</param>
        /// <returns>Tuple of (stopPrice, closeAction)</returns>
        private (double stopPrice, OrderAction closeAction) CalculateEmergencyStopPrice(
            MarketPosition direction,
            double currentClose,
            int qty
        )
        {
            // Compute emergency stop distance: MaximumStop vs ATR-bound (use tighter)
            double emergencyStopDist = MaximumStop;
            double atrBound = CalculateATRStopDistance(RMAStopATRMultiplier);

            if (atrBound > 0)
            {
                // ATR FLOOR FIX: Prevent stops tighter than MinimumStop
                atrBound = Math.Max(atrBound, MinimumStop);
                emergencyStopDist = Math.Min(emergencyStopDist, atrBound);
            }

            // Fallback: If result is still invalid, use MinimumStop
            if (emergencyStopDist <= 0)
            {
                emergencyStopDist = Math.Max(tickSize, MinimumStop);
            }

            // Calculate stop price and close action based on direction
            double stopPrice;
            OrderAction closeAction;

            if (direction == MarketPosition.Long)
            {
                stopPrice = Instrument.MasterInstrument.RoundToTickSize(currentClose - emergencyStopDist);
                closeAction = OrderAction.Sell;
            }
            else
            {
                stopPrice = Instrument.MasterInstrument.RoundToTickSize(currentClose + emergencyStopDist);
                closeAction = OrderAction.BuyToCover;
            }

            return (stopPrice, closeAction);
        }

        #endregion
    }
}

// Made with Bob
