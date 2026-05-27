// <copyright file="V12_002.Data.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        private int _uiSnapshotTickCounter;

        // V12.EPIC-7-QUALITY-006: IPC Error Handling Diagnostic Counters
        private long _ipcCleanupFailures = 0;
        private long _ipcZombieConnections = 0;

        // V12.EPIC-7-QUALITY-007: State Persistence Error Handling Diagnostic Counters
        private long _stateCorruptionDetected = 0;
        private long _stateTempCleanupFailures = 0;

        // V12.EPIC-7-QUALITY-008: UI Callbacks Error Handling Diagnostic Counter
        private long _uiCallbackFailures = 0;

        // V12.EPIC-7-QUALITY-011: File I/O Retry Logic Diagnostic Counters
        // Note: Actual counters are in RetryHelper.cs (static class)
        // These properties provide access for monitoring/reporting
        private static long IoRetryAttempts
        {
            get
            {
                RetryHelper.GetRetryMetrics(out long attempts, out _, out _);
                return attempts;
            }
        }

        private static long IoRetrySuccesses
        {
            get
            {
                RetryHelper.GetRetryMetrics(out _, out long successes, out _);
                return successes;
            }
        }

        private static long IoRetryFailures
        {
            get
            {
                RetryHelper.GetRetryMetrics(out _, out _, out long failures);
                return failures;
            }
        }

        // Placeholder for missing Data logic.
        public static class Data
        {
            // Add static data members here if needed.
        }
    }
}
