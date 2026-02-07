---
description: Handoff a task from Antigravity IDE to an external CLI agent (e.g., Claude CLI)
---

# CLI Handoff Workflow

Use this workflow when you need to delegate coding tasks from Antigravity IDE to a high-performance external CLI agent.

## Steps

1. **Locate the Mission Context**:
   - Identify the absolute path to your current conversation's "brain" directory.
   - Example: `C:\Users\Mohammed Khalid\.gemini\antigravity\brain\ffa42e18-55f9-4f60-84b6-82230d4ade03`

2. **Generate the Mission Brief**:
   - Ask Antigravity to "Generate a Mission Brief for the CLI agent".
   - Antigravity will create a prompt that includes absolute paths to `implementation_plan.md` and `task.md`.

3. **Copy the Brief**:
   - Ensure the brief contains:
     - Absolute paths to planning artifacts.
     - Specific technical requirements.
     - Files to modify.
     - Coding standards.

4. **Initialize the CLI Session**:
   - Start your CLI agent (e.g., `claude` or `npx @anthropic-ai/claude-code`).
   - Paste the Mission Brief as the first message.

5. **Verify Context Acquisition**:
   - Ensure the CLI agent confirms it has read the planning artifacts from the absolute paths provided.

6. **Mandatory Deployment**:
   - The CLI agent **must** use the `scripts\ninja_deploy.ps1` script after making code changes.
   - Requirement: `powershell -ExecutionPolicy Bypass -File "scripts\ninja_deploy.ps1" -SourceFileName "[FileName]"`
   - This ensures the changes reach the NinjaTrader strategies folder and aren't just left in the repo.

7. **Monitor Execution**:
   - Watch the CLI agent perform the work. If it gets stuck on a path issue, re-provide the absolute path from Step 1.
