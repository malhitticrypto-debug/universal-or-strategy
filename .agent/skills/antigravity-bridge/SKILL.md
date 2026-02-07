---
name: antigravity-bridge
description: Manages context handoff from Antigravity IDE to external CLI agents by resolving "brain" directory paths.
---

# Antigravity Bridge Skill

## Purpose
Enables seamless task delegation from the Antigravity IDE to external CLI agents (like Claude CLI) by providing absolute path resolution to the "brain" artifacts (plans, tasks, etc.).

## The "Brain" Gap
Antigravity stores reasoning context in:
`<appDataDir>\brain\<conversation-id>`

External CLI agents cannot "see" this directory by default. This skill bridges that gap.

## Protocol: The MISSION BRIEF

When handoff is requested, generate a structured prompt for the external agent using absolute paths.

### Template
```markdown
### 🤖 EXPERT CODER MISSION: [Task Name]

**PLANNING REFERENCE (READ THESE FIRST):**
1. **Plan:** [Absolute Path to implementation_plan.md]
2. **Task List:** [Absolute Path to task.md]

**THE MISSION:**
[High-level goal]

**TECHNICAL REQUIREMENTS:**
[Requirement 1]
[Requirement 2]

**CODING STANDARDS:**
- Maintain current versioning.
- No unrelated refactors.
- Include [TAGS] in logs.

**MANDATORY DEPLOYMENT:**
- After editing, you **MUST** run the deployment script to update the NinjaTrader binary.
- Command: `powershell -ExecutionPolicy Bypass -File "scripts\ninja_deploy.ps1" -SourceFileName "[FileName]"`
```

## How to Find Paths
Antigravity agents can see their own artifact paths via the `artifact_formatting_guidelines` and `task_artifact` definitions in their system prompt. Always use these to build the brief.
