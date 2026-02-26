---
description: Multi-Agent "Red Team" Auditing Workflow
---

# Multi-Agent Auditing Workflow (The "Reaper Scan")

This workflow coordinates multiple AI agents (Claude, Cursor, Gemini, Grok) to cross-audit strategy logic, identify bugs, and reach a consensus on hardening fixes.

## 1. Preparation
1. Ensure `UniversalORStrategyV12_002_Dev` is in a clean baseline state.
2. Create/Initialize `C:\WSGTA\universal-or-strategy\docs\audits\CONSOLIDATED_AUDIT.md`.

## 2. Round 1: Redundant Discovery (Independent Deep Scan)
*All agents perform the same comprehensive 'Full Spectrum' scan to ensure maximum coverage.*

1. **Prompt for ALL agents**: "Independent Audit Task: Perform a 360-degree deep-scan of the UniversalORStrategyV12 codebase. Look for: (1) Division-by-zero risk in sizing math, (2) MOMO Stop-Limit slippage risks, (3) Thread-safety in SIMA broadcasting, (4) State-persistence bugs, and (5) Logic clashing between modes. Propose specific hardening fixes. **DIRECTIVE: Write your findings directly into C:\WSGTA\universal-or-strategy\docs\audits\CONSOLIDATED_AUDIT.md under your designated section.** Do NOT read other AI findings yet."
2. **Execution**:
    - **Gemini (Lead)**: Analyze and write to `## ROUND_1: GEMINI`.
    - **Claude Code CLI**: Analyze and write to `## ROUND_1: CLAUDE`.
    - **Codex 5.3**: Analyze and write to `## ROUND_1: CODEX_5.3`.
    - **Cursor**: Analyze and write to `## ROUND_1: CURSOR`.

## 3. Round 2: The Consensus Debate (Rebuttal & Synthesis)
1. Feed the completed Round 1 logs back to ALL agents.
2. **Prompt**: "Review findings from the other 3 agents. Identify any 'hallucinations', 'false positives', or 'overlooked risks'. Debate the differences. Refine your final risk assessment."
3. Update `## ROUND_2: THE DEBATE` with the cross-agent synthesis.

## 4. Round 3: Final Hardening Plan (Agreement)
1. **Gemini** synthesizes the debate into a single, unified `## FINAL_HARDENING_CONSENSUS`.
2. **USER Approval**: User grants permission for surgical edits.
3. **Execution**: Cursor (Codex) implements the final, multi-agent verified hardening.

## 4. Round 3: Consensus & Implementation Plan
1. **Gemini** (or a designated Project Director) synthesizes all verified risks into a single `## FINAL_HARDENING_PLAN`.
2. **USER Approval**: User reviews the plan and gives the "GO" signal.
3. **Execution**: Use Cursor (Codex) to apply the approved surgery across all files.

## 5. Verification
1. Run `ExecuteRiskLogicAudit` (Case 5/6) to ensure symmetry guard and slippage caps remain intact.
2. Sunday Open Readiness Check.
