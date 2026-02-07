# Sequential Logic Injection Protocol (SLIP)

A standardized multi-agent workflow for implementing complex features in large-scale codebases. Designed to prevent truncation, logic drift, and context loss when working with files exceeding 5,000 lines.

## 👥 Roles
*   **The Architect (Master Agent)**: High-level planning, requirement analysis, artifact management, and visual parity verification. (Antigravity)
*   **The Builder (Execution Agent)**: Intensive code generation, unit-level logic implementation, and syntax validation. (Opus 4.5 / Sonnet 3.7)

## 🔄 The Cycle (Prompt-Verify-Commit)

### Phase 1: Planning & Blueprinting
The Architect creates a detailed **Implementation Plan** and a **Mockup** (if UI is involved). This is reviewed by the User before any code is touched.

### Phase 2: Sequential Logic Injection (The SLIP)
Instead of one "Giant Prompt," the work is split into **Logical Modules** (Prompts) fed into the *same conversation* with the Builder.

1.  **Prompt 1: Data & Interface**: Wiring the underlying variables, event handlers, and data flow.
2.  **Prompt 3: Execution Logic**: Implementing the core functional behavior and overrides.
3.  **Prompt 3: Edge Cases & Polish**: Handling error states, edge cases, and final refinements.

### Phase 3: Verification & Integration
The User provides the Builder's output (or a summary of successful compilation) back to the Architect. The Architect then updates the **Walkthrough** and **Task Checklist**.

## 🛠️ Prompting Best Practices
*   **Context Inclusion**: Every prompt should remind the Builder of the specific file, key variables, and previous steps in the chain.
*   **Truncation Prevention**: Ask the Builder to return only the modified sections OR the full file, depending on the IDE's capability (e.g., Cursor vs. CLI).
*   **Constraint Enforcement**: List "Strict Rules" (e.g., "Do not touch Trend Logic") in every prompt to ensure compliance.

## ⚖️ Default Safety Rules for Trading Strategies
*   **Rounding**: Force all price entries to the nearest instrument tick (e.g., 0.25).
*   **Immediate Fill Warnings**: Log/Print a warning if a limit price is already through the market.
*   **Universal Kill-Switch**: Clearing the manual input must always return the system to default behavior.
