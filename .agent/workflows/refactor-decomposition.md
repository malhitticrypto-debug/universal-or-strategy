---
description: How to perform deep architectural refactoring and code decomposition for NinjaTrader strategies
---

# Refactoring & Decomposition Protocol

Use this workflow when a strategy has "morphed" through multiple versions and contains legacy code, redundant variables, or unorganized massive files (e.g., 5000+ lines).

## Phase 1: Dependency Mapping
1. **Identify Entry/Exit Chains**: Map exactly how a trade goes from a UI button click to the `SubmitOrderUnmanaged` call.
2. **Audit Configuration Sources**: List all sources of truth (Panel IPC, Strategy Properties, JSON config, hardcoded defaults).
3. **Trace Common Variables**: Grep for variables like `RiskPerTrade` or `StopMultiplier` to see where evolutionary "split-brain" exists.

## Phase 2: Logic Unification
// turbo
1. **Define Master Source of Truth**: Explicitly declare which system (e.g., V12 Side Panel) is the Master.
2. **Consolidate Risk Logic**: Move all contract sizing to a single `CalculatePositionSize` method.
3. **Remove Redundant Overrides**: Delete logic that uses old variables to override new master settings.

## Phase 3: Structural Decomposition
1. **Identify Logical Departments**:
   - `UI & IPC`: Component drawing and message handling.
   - `SIMA Engine`: Fleet management and dispatch.
   - `Algorithms`: Trade entry math (ORB, RMA, etc.).
   - `Risk & Safety`: Stop/Target management and Reaper logic.
2. **Region Grouping**: Before splitting files, group all related methods into named `#region` blocks.
3. **File Extraction**: Move stable regions into separate partial classes or utility files.

## Phase 4: Verification
1. **Parity Check**: Compare trade execution logs before and after refactoring.
2. **Property Audit**: Ensure legacy [NinjaScriptProperty] attributes are removed to keep the NT8 UI clean.
3. **Recompilation**: Verify no circular dependencies exist after splitting files.
