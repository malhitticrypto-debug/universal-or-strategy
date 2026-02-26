# Zero-Trust Forensic Audit — AUDIT_2026_02_18_CLAUDE

**Date**: 2026-02-18
**Agent**: Claude Sonnet 4.6 (claude-sonnet-4-6)
**Build**: V12.44 post-Phase 6.0 (Phase 8b baseline + Phase 6.0 simplification pass)
**Scope**: All files in `src/`
**Protocol**: Executive Audit — 4 Critical Vectors

---

## Audit Score: **5.5 / 10**

> Concurrency hardening (Phases 6–8b) is solid in the callback path. The distribution math has a sound algorithm but lacks a final assertion gate. The stop-submission path has multiple NULL-acceptance bugs that can silently orphan live positions. Symmetry's duplicate-dispatch guard has a confirmed TOCTOU hole. These issues are addressable with surgical fixes.

---

## Vector 1 — Callback Collision (`Orders.Callbacks.cs`, `Orders.Management.cs`)

**Objective**: Prove double-fills are impossible.

| ID | Severity | File:Line | Description | Status |
|---|---|---|---|---|
| C-07 | HIGH | Orders.Callbacks.cs:267,279,948,955 | Double-removal race: OnOrderUpdate and OnExecutionUpdate can both call ApplyTargetFill for the same fill. First writer wins under stateLock; second writer returns early via dedup guard (lines 949–957). TryRemove is atomic. Race is mitigated but tightly coupled. | Risk (mitigated) |
| R-08 | MEDIUM | Orders.Callbacks.cs:254–261 | TOCTOU: `.Values.Contains(order)` at line 256 is an unprotected O(n) scan. Between line 256 and TryGetValue at line 260, another thread can remove the entry. Consequence: false negative (missed fill) not double-fill. ApplyTargetFill's own stateLock is the real dedup gate. | Risk (low impact) |
| C-08 | MEDIUM | Orders.Management.cs:126–192 | UpdateStopQuantity reads `pos.RemainingContracts` inside stateLock but callers pass `pos` reference without lock. Stale reads possible if pos modified concurrently before lock acquisition. | Risk (design-level) |
| R-09 | LOW | Orders.Callbacks.cs:147–153 | Snapshot `ToArray()` taken outside lock; defensive recheck at line 153 handles concurrent removals. | Clear |

**Verdict**: Double-fills are **effectively prevented** by the three-layer dedup (executionId set, ApplyTargetFill first-writer-wins under stateLock, early-return in OnExecutionUpdate). No confirmed double-fill bug.

---

## Vector 2 — Distribution Math (`UI.Sizing.cs`, `Orders.Management.cs`)

**Objective**: Prove T1+T2+T3+T4+T5 == TotalContracts on every call.

| ID | Severity | File:Line | Description | Status |
|---|---|---|---|---|
| D-008 | HIGH | UI.Sizing.cs:37–201 | **No explicit final assertion.** The function relies on three implicit correction passes (lines 129–137, 141–142, 191–194) but never asserts or logs the final sum before returning. If any edge case escapes all three passes, a contract is silently lost. | Bug |
| D-001 | HIGH | UI.Sizing.cs:49–56 | Integer truncation: all five buckets use `Math.Floor(contracts * percent / 100.0)`. Asymmetric splits (e.g., [15,25,30,20,10]) accumulate remainder. The correction at line 141–142 assigns deficit to `captureIndex`. Works in practice but implicit. | Risk |
| D-009 | HIGH | UI.Sizing.cs:144–177 | Nested invariants (anchor + capture reserve) can modify overlapping buckets in undefined interaction order. If captureIndex == 0, both anchor and capture minKeep point to T1 — anchor invariant and capture invariant can fight. | Risk |
| D-004 | MEDIUM | UI.Sizing.cs:163–177 | Emergency fallback (lines 168–176) silently resets T2..T5 to 0 and gives all remaining contracts to captureIndex. Correct by design but undocumented and surprising. | Risk |
| D-007 | MEDIUM | Orders.Management.cs:100–101 | Bracket audit log prints `nonRunnerLimitQty + runnerQty` but does NOT assert sum == TotalContracts. Audit message misleading if distribution remainder was handled by GetTargetDistribution internally. | Risk |
| D-010 | LOW | UI.Sizing.cs:49–56 | Zero-percent targets (all percents = 0%) correctly route all contracts to T1 via captureIndex fallback. | Clear |

**Verdict**: The algorithm is **structurally sound** but relies entirely on implicit correction passes. D-008 is a real bug — a missing assertion gate. Fix: add `if (finalSum != contracts) Print(...)` before return.

---

## Vector 3 — Stop Coverage (`Orders.Management.cs`, `Trailing.cs`, `REAPER.cs`)

**Objective**: Prove every position has 1:1 stop coverage in all volatility scenarios.

| ID | Severity | File:Line | Description | Status |
|---|---|---|---|---|
| S-001 | CRITICAL | Orders.Management.cs:47–51 | **NULL stop stored unconditionally.** `stopOrders[entryName] = stopOrder` executes even if `SubmitOrderUnmanaged` returns NULL. Position is marked `BracketSubmitted = true` but stop is NULL. No re-attempt or flatten triggered. | **Confirmed Bug** |
| S-002 | CRITICAL | Orders.Management.cs:51 | `ContainsKey` returns true for a NULL-valued entry. Downstream checks that call `TryGetValue` and get `null` will silently skip stop management, masking the unprotected condition. | **Confirmed Bug** |
| S-004 | CRITICAL | Orders.Management.cs:263–272 | `CreateNewStopOrder` has a NULL return check (calls FlattenPositionByName on NULL). `SubmitBracketOrders` at lines 47–51 does NOT. Two inconsistent behaviors for the same failure mode. | **Confirmed Bug** |
| S-015 | CRITICAL | Orders.Management.cs:82–84 | Target limit orders stored in `targetDict[entryName] = limitOrder` without NULL check. If broker rejects limit order, NULL is stored; target fill will never be matched; stop reduction will never fire. | **Confirmed Bug** |
| S-010 | CRITICAL | REAPER.cs:78–82 + Orders.Callbacks.cs:424–449 | Reaper startup grace skips first audit cycle. Pending stop replacement waits for broker cancel confirm before submitting new stop. Combined window: position can be unprotected for up to 30 seconds (Reaper detection cycle) after a target partial fill triggers stop reduce. | **Confirmed Risk** |
| S-003 | HIGH | Orders.Management.cs:89–102 | Stop quantity audit runs AFTER `pos.BracketSubmitted = true`. Mismatch only logs warning; position is treated as protected regardless. | **Confirmed Bug** |
| S-005 | HIGH | Orders.Management.cs:153–160 | C-05 post-check logs "deficit will be corrected by final invariant" but does NOT re-correct in-place if stop audit fails. | Risk |
| S-006 | HIGH | Orders.Callbacks.cs:277 | Between T1 fill triggering stop reduce and broker confirming stop cancel, position holds an oversized stop. Stop not immediately corrected — relies on async cancel/replace cycle. | Confirmed Risk |
| S-008 | HIGH | Orders.Callbacks.cs:424–449 | Ghost stop resurrection: stop cancel callback (line 424) creates replacement if `replacementQty > 0`. But between lock acquisition and creation, a simultaneous target fill can remove the position from `activePositions`. Replacement stop submitted for a flat account. | Confirmed Race |
| S-014 | HIGH | Trailing.cs:445–473 | Stale pending replacement timeout = 5 seconds. During cleanup, old pending is removed and emergency stop created. If another callback fires between removal and creation, the stop is lost momentarily. | Risk |
| S-012 | HIGH | Orders.Callbacks.cs:478–496 | Manual user stop cancel removes reference without auto-resubmit. Position permanently unprotected by design, but no UI warning. | By Design (risk) |
| S-007 | MEDIUM | Trailing.cs:478–639 | No absolute guard preventing trailing stop from degrading on ATR expansion if EMA moves against position. Direction check at line 194 is present but not absolute. | Theoretical Risk |
| S-009 | MEDIUM | Trailing.cs:366–369 | Micro-update throttle (1-tick filter) may cause stop to lag fast-moving markets by 100ms+. | Theoretical Risk |
| S-013 | MEDIUM | Orders.Management.cs:141–155 | TOCTOU in pending replacement dedup: `ContainsKey` at line 146 then `TryAdd` at line 169 — window for second thread to insert duplicate. | Confirmed Race |

**Verdict**: **5 confirmed bugs in the stop submission path.** S-001/S-002/S-015 are the most dangerous — NULL orders are silently accepted into tracking dictionaries, masking unprotected positions. S-010 confirms a 30-second Reaper blind window.

---

## Vector 4 — SIMA Sync (`SIMA.cs`, `Symmetry.cs`, `SignalBroadcaster.cs`)

**Objective**: Prove broadcast collisions cannot cause follower account desync.

| ID | Severity | File:Line | Description | Status |
|---|---|---|---|---|
| Q3-002 | HIGH | SIMA.cs:201 | **`activeFleetAccounts` toggled by UI/IPC thread without `_simaToggleSem`.** Dispatcher reads `isActive=true`, account toggled to false before Submit returns. Trade executed to inactive account — violates fleet invariant. | **Confirmed Bug** |
| Q4-001 | HIGH | Symmetry.cs:55–98 | **TOCTOU in duplicate dispatch guard.** Loop reads `symmetryDispatchById` to find existing dispatch (lines 65–76). No lock. Two threads can both read "no existing dispatch" and both insert new contexts. H-11 fix from Phase 7 is incomplete. | **Confirmed Bug** |
| Q4-002 | HIGH | Symmetry.cs:100–119 | Same as Q4-001 — read-check-write cycle on `symmetryDispatchById` is not atomic. Two dispatch contexts created for same (TradeType, Direction) signal. | **Confirmed Bug** |
| Q2-001 | HIGH | SIMA.cs:136, 372–386 | `_simaToggleSem` serializes against ApplySimaState only. Two concurrent `ExecuteSmartDispatchEntry` calls for different instruments race on `activePositions`/`entryOrders` dict writes (ConcurrentDictionary item writes are individually atomic, but the full multi-dict transaction is not). | Risk |
| Q2-002 | HIGH | SIMA.cs:372–386 | Atomicity loss: `expectedPositions` reserved under `stateLock` at line 372; `activePositions` committed outside lock at line 377. Reaper/Symmetry can observe expectedPositions > 0 but empty activePositions. | Risk |
| Q3-001 | HIGH | SIMA.cs:133–436 | SIMA toggle race: if ApplySimaState unsubscribes fleet handlers while ExecuteSmartDispatchEntry is mid-flight, the submitted orders are orphaned from notification handlers (orders ARE at broker, but NinjaTrader loses callbacks). | Risk |
| H-10 | HIGH | SIMA.cs:479–486 | `_simaToggleSem.Wait(500ms)` in ApplySimaState can timeout during slow dispatch. On timeout: warns and returns, leaving SIMA state machine in ambiguous condition. No retry, no escalation, no error flag. | Risk |
| ORPHAN-02 | MEDIUM | SIMA.cs:407–408 | Functional update rollback (`AddOrUpdateExpectedPositionLocked` with lambda) can be lost if expectedPositions was concurrently modified by another thread between Submit and catch. Safer: `SetExpectedPositionLocked` with direct value. | Risk |
| Q1-001 | — | SignalBroadcaster.cs:205–219 | SafeInvoke isolation CONFIRMED: per-handler try/catch prevents single subscriber exception from breaking fan-out. | **Clear** |
| Q5-001 | — | SIMA.cs:154, 597, 663 | `isFlattenRunning` guard checked early (no lock held) — preferred pattern. Finally-block semantics preserved on early return. | **Clear** |

**Verdict**: **3 confirmed bugs** (Q3-002, Q4-001, Q4-002). SafeInvoke fan-out is solid. The Symmetry duplicate-dispatch guard added in Phase 7 [H-11] has a TOCTOU hole that was not previously caught.

---

## Consolidated Priority Fix List

| Priority | ID(s) | Fix |
|---|---|---|
| P0 — Immediate | S-001, S-002, S-004, S-015 | Null-guard stop/target order submissions. After `SubmitOrderUnmanaged`, check `== null` → call `FlattenPositionByName` or log + skip dict write. Never store NULL in `stopOrders` or `targetXOrders`. |
| P0 — Immediate | Q4-001, Q4-002 | Add `lock(stateLock)` (or a dedicated `_symmetryLock`) around the read-check-write cycle in `SymmetryGuardBeginDispatch` (Symmetry.cs:55–96). |
| P1 — High | Q3-002 | Guard `activeFleetAccounts` reads inside dispatcher with `_simaToggleSem.Wait` or a dedicated reader-writer lock. |
| P1 — High | S-003 | Move `pos.BracketSubmitted = true` to AFTER the stop quantity audit passes, not before. |
| P1 — High | D-008 | Add explicit final sum assertion in `GetTargetDistribution` before returning: `if (t1+t2+t3+t4+t5 != contracts) Print(...)`. |
| P2 — Medium | S-008 | In stop-cancel replacement callback, re-verify position still exists in `activePositions` under lock before creating replacement stop. |
| P2 — Medium | Q2-002 | Wrap expectedPositions increment AND activePositions/entryOrders/stopOrders/targets commit in a single `lock(stateLock)` block in `ExecuteSmartDispatchEntry`. |
| P2 — Medium | ORPHAN-02 | Replace functional update rollback with `SetExpectedPositionLocked` direct set under lock in catch block. |
| P3 — Low | H-10 | Increase `_simaToggleSem` wait timeout or add retry loop with exponential backoff. Add boolean `_simaToggleFailedFlag` to surface state inconsistency to UI. |
| P3 — Low | C-08 | Document (or enforce via assertion) that `UpdateStopQuantity` callers must pass a `pos` reference that is stable (read inside stateLock before the call). |
| P3 — Low | D-007 | Update bracket audit log to assert `nonRunnerLimitQty + runnerQty == pos.TotalContracts` and emit an error if not. |

---

## Audit Metadata

- **Vectors scanned**: 4 / 4
- **Confirmed bugs**: 8 (S-001, S-002, S-003, S-004, S-015, Q3-002, Q4-001, Q4-002)
- **High-risk design issues**: 12
- **Clear / no action**: 6
- **Files audited**: Orders.Callbacks.cs, Orders.Management.cs, UI.Sizing.cs, Properties.cs, Trailing.cs, REAPER.cs, SIMA.cs, Symmetry.cs, SignalBroadcaster.cs
