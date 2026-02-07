# Architectural Audit Skill

This skill provides the intelligence required to identify and resolve "evolutionary debt" in complex trading systems that have transitioned across multiple architectural versions.

## Core Principles
1. **Source of Truth (SoT)**: Every system setting must have exactly one point of entry and one variable that represents its current state.
2. **Logic Purity**: Math should not be hidden behind UI attributes. Calculation methods should be "clean" and independent of display logic.
3. **No Ghosts**: Logic that was written for a previous version (e.g., V8) but superseded by a new engine (e.g., V12 SIMA) must be removed, not just commented out.

## Audit Checkpoints

### 1. Risk Logic Audit
- **Check for**: Duplicate risk variables (e.g., `RiskPerTrade` vs `MaxRiskAmount`).
- **Resolution**: Unify into the most modern variable and update all references.
- **Verification**: Ensure the `CalculatePositionSize` method uses the unified variable exclusively.

### 2. Stop/Target Mapping
- **Check for**: Hardcoded offsets or ATR multipliers that ignore user Panel inputs.
- **Resolution**: Direct all entries to a centralized `CalculateStopPriceForMode()` or similar factory method.

### 3. IPC/UI Sync
- **Check for**: Split-brain scenarios where the local Strategy Property differs from the Panel value.
- **Resolution**: Use `ProcessIpcCommands` to update internal state immediately and prioritize those values in trading logic.

## Usage
Trigger this skill when:
- Preparing for a major version upgrade.
- Decomposing a large strategy file.
- Resolving "inexplicable" bugs where the strategy behaves differently than the Panel inputs suggest.
