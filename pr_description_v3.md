## Summary
Clean src-only PR for Epic 5 null safety hardening.

## Changes
- **File**: `src/V12_002.SIMA.Fleet.cs`
- **Fix**: Added null safety checks in `ShouldSkipFleet_RunHealthCheck` method
- **Details**: Added null guards for `acct` and `acct.Positions` before creating position snapshot to prevent NullReferenceException
- **Impact**: Prevents potential crashes when account or positions collection is null during fleet health checks

## V12.19 Protocol Compliance
✅ Src-Only Push Protocol enforced
✅ Single file change
✅ No non-src contamination

## Previous PR
This replaces PR #5 which was contaminated with 3354 non-src files due to `git add .` violation.

## Protocol Hardening
- Updated `.bob/commands/pr-loop.md` to enforce src-only helper script
- Updated `docs/protocol/PR_LOOP_V2.md` with V12.19 enforcement
- Updated `docs/WORKFLOW_INTEGRATION.md` to ban `git add .`
- Created audit document: `docs/brain/git_add_audit_fix.md`