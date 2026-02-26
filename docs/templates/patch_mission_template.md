# 🔱 PATCH MISSION BRIEF: [Short Mission Name]

## 1. MISSION CONTEXT
- **Target Version**: [e.g., Build 1101E.7.1]
- **Focus Area**: [e.g., SIMA Sync / UI Layout / Reaper Hardening]
- **Risk Level**: [Low / Medium / High - Zero-Trust Required]

## 2. THE FINDING (Forensic Data)
[Briefly describe the bug, logic leak, or friction point identified in the audit. Link to relevant logs or screenshots.]

## 3. STRATEGIC OBJECTIVES
1.  **[Repair]**: [Primary fix requested]
2.  **[Harden]**: [Secondary safety guard to prevent regression]
3.  **[Audit]**: [Verification requirement]

## 4. EXECUTION CONSTRAINTS (Zero-Trust)
- **Files Affected**: [Strict list of paths in src/]
- **Model Role**: Act as the **Senior Executive Auditor**.
- **Rule**: Do NOT modify unrelated files. Do NOT refactor for style unless requested.
- **WPF Guard**: Halt if UI layout fails > 2 times.

## 5. VERIFICATION REQUIREMENTS
- [ ] Logic Stress Test (Simulate High Volatility)
- [ ] Regression Test (Verify existing features)
- [ ] Documentation Update (Log in audit history)
