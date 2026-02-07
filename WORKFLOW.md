# WSGTA Development Workflow: One Source of Truth

## The Problem
Historically, NinjaTrader 8 looks at files in `Documents/NinjaTrader 8/bin/Custom/`. However, agents and Git work out of the `Github/universal-or-strategy/` folder. This leads to confusion and bugs where fixes are applied to one folder but not the other.

## The Solution: One Source of Truth
We use **Hard Links** to connect the folders. This means the file exists in ONE place on the hard drive, but appears in BOTH folders.
- **Editing GitHub** = Instantly updates NinjaTrader.
- **Git Commit** = Saves exactly what NinjaTrader is running.

## Gold Standard Process
1. **Always edit in the GitHub folder.** (This is where the IDE is opened).
2. **Never edit in the Documents folder.**
3. **If you lose sync**, run:
   ```powershell
   .\setup-symlinks.ps1
   ```
4. **Deploying to a new machine?**
   - Clone the repo.
   - Run `.\setup-symlinks.ps1`.
   - Start trading.

## Troubleshooting
- **NinjaTrader doesn't see changes:** Press `F5` in NinjaTrader (Tools > Reload NinjaScript).
- **Compilation error "Multiple definitions":** Look for backup files (.bak) in the `Documents` folder and remove them if they end in `.cs`.
