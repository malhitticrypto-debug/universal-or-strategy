# [ZERO-TRUST] Build 1101E Forensic Integration Scan
## Cross-Reference of Hardening Layers for Logical Leaks

**Date:** 2025-02-17  
**Scope:** /src directory — SIMA.cs, Symmetry.cs, Orders.Callbacks.cs, Orders.Management.cs, UI.Sizing.cs, Trailing.cs

---

## EXECUTIVE SUMMARY

| Metric | Value |
|--------|-------|
| **Stability Grade** | **6/10** |
| **Critical Findings** | 4 |
| **High Findings** | 3 |
| **Medium Findings** | 2 |

**Ruling:** One potential race (Case 8) and multiple lock-consistency violations prevent a higher grade. No deadlocks identified; concurrency is managed but not uniformly enforced.

---

## FORENSIC TABLE

| ID | Severity | File:Line | Logical Proof of Failure |
|----|----------|-----------|--------------------------|
| **F-01** | CRITICAL | Orders.Callbacks.cs:496 | **Stop Loss Coverage (Case 8):** When T1 and T2 fill simultaneously, the stop-cancel confirmation handler calls `CreateNewStopOrder(entryName, pending.Quantity, ...)`. `pending.Quantity` was captured when T1's UpdateStopQuantity ran. If T2's callback updates `existingPending.Quantity` *after* T1's cancel confirms but *before* we read `pending.Quantity`, we use stale value. **Worse:** if T2 has not run yet, we create stop with T1-era qty (e.g., 3) while `pos.RemainingContracts` is already 1. A stop hit during that window would overclose (3 instead of 1). |
| **F-02** | CRITICAL | Trailing.cs:463 | Same as F-01: Emergency replacement uses `pending.Quantity` instead of live `pos.RemainingContracts`. Stale-pending path (5s timeout) can create oversize stop. |
| **F-03** | HIGH | Symmetry.cs:199 | `followerPos.RemainingContracts = Math.Max(1, followerPos.TotalContracts)` in `SymmetryGuardOnFollowerFill` modifies `pos.RemainingContracts` **without** holding `stateLock`. Concurrent `ApplyTargetFill` or `UpdateStopQuantity` can race. |
| **F-04** | HIGH | Symmetry.cs:500 | `pos.RemainingContracts = Math.Max(1, pos.TotalContracts)` in `SymmetryGuardSkipFollower` — same violation as F-03. |
| **F-05** | HIGH | UI.Sizing.cs:331 | `pos.RemainingContracts = newQty` in `SyncPendingOrders` modified without `stateLock`. Can race with ApplyTargetFill / OnExecutionUpdate. |
| **F-06** | MEDIUM | SIMA.cs (multiple) | `expectedPositions.AddOrUpdate` / direct assignment in ExecuteSmartDispatchEntry, ExecuteMultiAccountMarket, ExecuteMultiAccountBracket, FlattenAllApexAccounts, ClosePositionsOnlyApexAccounts, HydrateExpectedPositionsFromBroker, ExecuteRMAEntryV2 — all without `stateLock` or `_simaToggleSem`. Reaper reads `expectedPositions`; logical consistency with other SIMA state not serialized. |
| **F-07** | MEDIUM | Orders.Callbacks.cs:899–937 | **Order Reference Leak:** Target orders removed from dictionaries via `TryRemove` **before** Cancel, while order state is still `Working`. If Cancel fails or is delayed, reference is lost. Audit spec: remove only after Filled/Cancelled/Rejected. |
| **F-08** | LOW | Orders.Callbacks.cs:841–856 | **Partial-Fill Dedup Gap:** When `executionId` is null/empty, the dedup block is skipped. Duplicate broker callbacks with null `executionId` could over-report fills. |
| **F-09** | INFO | Orders.Callbacks.cs | ApplyTargetFill + IsTargetFilled guard + processedExecutionIds (when non-null) provide defense in depth. **Cannot prove** over-report when executionId is null; no code path removes orders before terminal state in a way that loses fill application. |

---

## TARGET VECTOR ANALYSIS

### 1. Lock Consistency
- **pos.RemainingContracts** — Violations: Symmetry.cs (2), UI.Sizing.cs (1). All three modify without `stateLock`.
- **expectedPositions** — All SIMA modifications are lock-free. ConcurrentDictionary provides atomicity but not logical serialization with Reaper/other SIMA state.

### 2. Order Reference Leaks
- Stop-fill path: `TryRemove` then Cancel. Order is Working at removal. Risk: Cancel failure leaves orphan; low likelihood.
- CleanupPosition: `TryRemove` only for cancel; order state checked for Working/Pending. Acceptable.
- No removal of orders before Filled/Cancelled/Rejected in normal fill path; target dicts updated only after ApplyTargetFill + terminal state handling.

### 3. Stop Loss Coverage (Case 8) — T1+T2 Simultaneous Fill
- **Proof of failure:** `CreateNewStopOrder(entryName, pending.Quantity, ...)` in cancel confirmation and stale-pending path uses cached quantity. Intervening target fill can change `pos.RemainingContracts` without updating the pending object before we read it in the cancel handler.
- **Fix:** Use `pos.RemainingContracts` under `stateLock` when creating replacement, not `pending.Quantity`.

### 4. Partial-Fill Logic — ApplyTargetFill Over-Report
- **Proven safe when executionId present:** `processedExecutionIds` dedup + `IsTargetFilled` guard prevent double-decrement.
- **Gap when executionId null:** Dedup skipped; duplicate OnExecutionUpdate with null id could double-apply. Mitigation: `IsTargetFilled` and `GetTargetFilledQuantity` caps still limit impact; broker typically provides executionId.

---

## HARDENING FIXES (SURGICAL)

See implementation in source files. Summary:

1. **F-01/F-02:** Replace `pending.Quantity` with `pos.RemainingContracts` read under `stateLock` when calling CreateNewStopOrder from cancel-confirm and stale-pending paths.
2. **F-03/F-04:** Wrap Symmetry `pos.RemainingContracts` assignments in `lock (stateLock)`.
3. **F-05:** Wrap UI.Sizing `pos.RemainingContracts` assignment in `lock (stateLock)`.
4. **F-06:** (Optional) Add `_simaToggleSem` or `stateLock` around expectedPositions updates if Reaper consistency is required.
5. **F-07:** (Optional) Defer TryRemove until after Cancel; or document as acceptable trade-off for OCO semantics.
6. **F-08:** (Optional) Add order+quantity-based fallback dedup when executionId is null.

---

*End of Forensic Report*
