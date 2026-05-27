---
description: Manual TDD-mode Epic Execution. Provides a structured path for manual ticket execution while maintaining autonomous /pr-loop hardening after every commit.
argument-hint: <epic-slug> <ticket-number> <pr-number>
---
# EPIC TDD -- MANUAL EXECUTION
**Epic Slug:** $1
**Ticket:** $2
**Target PR:** $3
**Mode:** Engineer (Manual-parity)
**Protocol:** V12 High-Precision TDD Gate

You are the V12 TDD Engineer. You are executing ticket $2 of epic $1 manually. 
Your goal is to implement the changes and drive them to 100/100 PHS on PR #$3.

---

## EXECUTION WORKFLOW

### Step 1: Implementation
Execute the changes described in `docs/brain/$1/ticket-$2.md`.
Ensure:
- ✅ Zero locks.
- ✅ ASCII only.
- ✅ FSM/Actor pattern followed.

### Step 2: Local Verification
Run the FULL validation suite:
1. `powershell -File .\deploy-sync.ps1`
2. `powershell -File .\scripts\pre_push_validation.ps1`

Expected output: ALL blocking checks PASS (8/8 required, 5/5 warnings informational).

If ANY blocking check fails: **HALT and fix.**

### Step 3: F5 Verification
1. Press F5 in NinjaTrader.
2. Verify the BUILD_TAG banner.
3. Confirm the Logic Audit passes.

### Step 4: Commit & Push
1. `git add .`
2. `git commit -m "[$1] ticket-$2: manual TDD implementation"`
3. `git push`

### Step 5: Autonomous Perfection Gate
**Switch to: Orchestrator mode (/pr-loop)**

Immediately after pushing, hand off to the perfection loop:
```
EPIC: $1
TASK: Run /pr-loop $3
GOAL: Drive the ticket to 100/100 PHS.
STOP when /pr-loop outputs [PHS-PERFECT].
```

---

## COMPLETION
Once /pr-loop is 100/100, report status to Director:
"Ticket $2 complete. 100/100 PHS achieved. Proceed to next ticket?"
