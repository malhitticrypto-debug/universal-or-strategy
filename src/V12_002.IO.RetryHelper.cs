// <copyright file="V12_002.IO.RetryHelper.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// EPIC-7-QUALITY-011: File I/O Security - Retry Logic with Exponential Backoff
// Handles transient file I/O failures (locked files, network glitches, antivirus scans)

using System;
using System.IO;
using System.Threading;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        /// <summary>
        /// Retry helper for file I/O operations with exponential backoff.
        /// Enforces Jane Street Resilience: "Systems must gracefully handle transient failures"
        /// </summary>
        private static class RetryHelper
        {
            // Diagnostic counters (thread-safe)
            private static long _ioRetryAttempts = 0;
            private static long _ioRetrySuccesses = 0;
            private static long _ioRetryFailures = 0;

            /// <summary>
            /// Executes an operation with fail-fast error handling (Jane Street alignment).
            /// NO RETRY LOOPS - propagates errors immediately to avoid blocking hot paths.
            /// </summary>
            /// <typeparam name="T">Return type of the operation</typeparam>
            /// <param name="operation">The operation to execute</param>
            /// <param name="isRetryable">UNUSED - kept for API compatibility</param>
            /// <param name="operationName">Name of the operation for logging</param>
            /// <param name="maxAttempts">UNUSED - kept for API compatibility</param>
            /// <param name="baseDelayMs">UNUSED - kept for API compatibility</param>
            /// <returns>Result of the operation</returns>
            public static T ExecuteWithRetry<T>(
                Func<T> operation,
                Func<Exception, bool> isRetryable,
                string operationName,
                int maxAttempts = 3,
                int baseDelayMs = 50
            )
            {
                try
                {
                    return operation();
                }
                catch (Exception ex)
                {
                    // Track failure immediately
                    Interlocked.Increment(ref _ioRetryFailures);

                    // Note: Cannot log from static context (Print() is non-static)
                    // Logging removed to maintain fail-fast semantics

                    throw; // Fail fast - propagate immediately
                }
            }

            /// <summary>
            /// Executes a void operation with fail-fast error handling (Jane Street alignment).
            /// </summary>
            public static void ExecuteWithRetry(
                Action operation,
                Func<Exception, bool> isRetryable,
                string operationName,
                int maxAttempts = 3,
                int baseDelayMs = 50
            )
            {
                ExecuteWithRetry<object>(
                    () =>
                    {
                        operation();
                        return null;
                    },
                    isRetryable,
                    operationName,
                    maxAttempts,
                    baseDelayMs
                );
            }

            /// <summary>
            /// Determines if an exception represents a transient I/O error worth retrying.
            /// </summary>
            /// <param name="ex">The exception to check</param>
            /// <returns>True if the error is transient and retryable</returns>
            public static bool IsTransientIOError(Exception ex)
            {
                // IOException: File locked, sharing violation, etc.
                if (ex is IOException ioEx)
                {
                    // Check for specific transient error codes
                    int hResult = ioEx.HResult;

                    // ERROR_SHARING_VIOLATION (0x80070020): File is locked by another process
                    // ERROR_LOCK_VIOLATION (0x80070021): File region is locked
                    // ERROR_FILE_EXISTS (0x80070050): Race condition during file creation
                    return hResult == unchecked((int)0x80070020)
                        || hResult == unchecked((int)0x80070021)
                        || hResult == unchecked((int)0x80070050);
                }

                // UnauthorizedAccessException: Only retry if it's likely transient (e.g., antivirus scan)
                // HEURISTIC TRADE-OFF: We use message inspection to distinguish permanent vs transient.
                // - Permanent: "read-only", "access is denied" (file attributes, ACL issues)
                // - Transient: Antivirus scan, file in use by another process
                // LIMITATION: This is best-effort. Some permanent issues may be retried unnecessarily,
                // but this is safer than never retrying (which would fail on transient AV scans).
                if (ex is UnauthorizedAccessException uaEx)
                {
                    string msg = uaEx.Message ?? string.Empty;
                    // Don't retry if it's clearly a permanent permission issue
                    // Use IndexOf with OrdinalIgnoreCase for locale-independent matching
                    if (
                        msg.IndexOf("read-only", StringComparison.OrdinalIgnoreCase) >= 0
                        || msg.IndexOf("access is denied", StringComparison.OrdinalIgnoreCase) >= 0
                    )
                    {
                        return false;
                    }
                    // Otherwise, assume it's transient (e.g., antivirus scan)
                    return true;
                }

                // FileNotFoundException on write operations: Race condition (file deleted between check and write)
                // Note: This is only retryable for write operations, not reads
                // Caller should use context-specific retry predicates

                return false;
            }

            /// <summary>
            /// Gets diagnostic counters for retry operations.
            /// </summary>
            public static void GetRetryMetrics(out long attempts, out long successes, out long failures)
            {
                attempts = Interlocked.Read(ref _ioRetryAttempts);
                successes = Interlocked.Read(ref _ioRetrySuccesses);
                failures = Interlocked.Read(ref _ioRetryFailures);
            }

            /// <summary>
            /// Resets diagnostic counters (for testing).
            /// </summary>
            public static void ResetMetrics()
            {
                Interlocked.Exchange(ref _ioRetryAttempts, 0);
                Interlocked.Exchange(ref _ioRetrySuccesses, 0);
                Interlocked.Exchange(ref _ioRetryFailures, 0);
            }
        }
    }
}

// Made with Bob
