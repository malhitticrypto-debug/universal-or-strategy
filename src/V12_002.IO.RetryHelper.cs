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
            /// Executes an operation with exponential backoff retry logic.
            /// </summary>
            /// <typeparam name="T">Return type of the operation</typeparam>
            /// <param name="operation">The operation to execute</param>
            /// <param name="isRetryable">Predicate to determine if an exception is retryable</param>
            /// <param name="operationName">Name of the operation for logging</param>
            /// <param name="maxAttempts">Maximum retry attempts (default: 3)</param>
            /// <param name="baseDelayMs">Base delay in milliseconds (default: 50ms)</param>
            /// <returns>Result of the operation</returns>
            public static T ExecuteWithRetry<T>(
                Func<T> operation,
                Func<Exception, bool> isRetryable,
                string operationName,
                int maxAttempts = 3,
                int baseDelayMs = 50
            )
            {
                Exception lastException = null;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        T result = operation();

                        // Track success if this was a retry (not first attempt)
                        if (attempt > 1)
                        {
                            Interlocked.Increment(ref _ioRetrySuccesses);
                        }

                        return result;
                    }
                    catch (Exception ex) when (isRetryable(ex) && attempt < maxAttempts)
                    {
                        lastException = ex;
                        Interlocked.Increment(ref _ioRetryAttempts);

                        // Exponential backoff: 50ms, 100ms, 200ms
                        int delayMs = baseDelayMs * (1 << (attempt - 1));

                        // Log retry attempt (non-critical, best-effort)
                        try
                        {
                            Print(
                                string.Format(
                                    "[IO_RETRY] {0} failed (attempt {1}/{2}), retrying in {3}ms: {4}",
                                    operationName,
                                    attempt,
                                    maxAttempts,
                                    delayMs,
                                    ex.Message
                                )
                            );
                        }
                        catch
                        {
                            // Swallow logging errors - don't let them break retry logic
                        }

                        Thread.Sleep(delayMs);
                    }
                }

                // All retries exhausted - final attempt without catch
                Interlocked.Increment(ref _ioRetryFailures);

                try
                {
                    Print(
                        string.Format(
                            "[IO_RETRY] {0} failed after {1} attempts: {2}",
                            operationName,
                            maxAttempts,
                            lastException?.Message ?? "Unknown error"
                        )
                    );
                }
                catch
                {
                    // Swallow logging errors
                }

                return operation();
            }

            /// <summary>
            /// Executes a void operation with exponential backoff retry logic.
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
                // Do NOT retry if it's a permanent permission issue (e.g., file is read-only, no write access)
                // Heuristic: If the exception message contains "read-only" or "access denied", it's likely permanent
                if (ex is UnauthorizedAccessException uaEx)
                {
                    string msg = uaEx.Message?.ToLowerInvariant() ?? string.Empty;
                    // Don't retry if it's clearly a permanent permission issue
                    if (msg.Contains("read-only") || msg.Contains("access is denied"))
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
