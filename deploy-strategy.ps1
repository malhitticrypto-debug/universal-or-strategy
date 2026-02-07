# WSGTA V12 Strategy Deployment Script
# Syncs GitHub Source -> NinjaTrader 8 Execution Directory
# Prevents "Editing Wrong Folder" errors

$ErrorActionPreference = "Stop"

# Source Paths (GitHub Repo)
$RepoRoot = "c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy"
$SrcStrategy = Join-Path $RepoRoot "UniversalORStrategyV12.cs"
$SrcPanel = Join-Path $RepoRoot "V12StandardPanel.cs"

# Destination Paths (NinjaTrader 8)
$NtUserDir = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom"
$DestStrategyDir = Join-Path $NtUserDir "Strategies"
$DestIndicatorDir = Join-Path $NtUserDir "Indicators"

Write-Host "--- Starting WSGTA Strategy Deployment ---" -ForegroundColor Cyan
Write-Host "Source: GitHub Repo ($RepoRoot)" -ForegroundColor Gray
Write-Host "Target: NinjaTrader 8 Custom Folder" -ForegroundColor Gray

# 1. Validate Source Files
if (-not (Test-Path $SrcStrategy)) { Write-Error "Source Strategy not found: $SrcStrategy"; exit }
if (-not (Test-Path $SrcPanel)) { Write-Error "Source Panel not found: $SrcPanel"; exit }

# 1b. Link Detection (One Source of Truth Safeguard)
$NtStrategy = Join-Path $DestStrategyDir "UniversalORStrategyV12.cs"
if (Test-Path $NtStrategy) {
    if ((Get-Item $NtStrategy).Attributes -match "ReparsePoint") {
        Write-Host "LINK DETECTED: System is already in 'One Source of Truth' mode. Skipping copy." -ForegroundColor Gray
        Write-Host "Changes in GitHub are already live in NinjaTrader." -ForegroundColor Green
        exit
    }
}

# 2. Deploy Strategy
Write-Host "`nDeploying Strategy..." -ForegroundColor Yellow
Copy-Item -Path $SrcStrategy -Destination (Join-Path $DestStrategyDir "UniversalORStrategyV12.cs") -Force -Verbose
Write-Host "OK: UniversalORStrategyV12.cs -> Strategies" -ForegroundColor Green

# 3. Deploy Panel
Write-Host "`nDeploying Panel..." -ForegroundColor Yellow
Copy-Item -Path $SrcPanel -Destination (Join-Path $DestIndicatorDir "V12StandardPanel.cs") -Force -Verbose
Write-Host "OK: V12StandardPanel.cs -> Indicators" -ForegroundColor Green

# 4. Cleanup (Remove Panel from Strategies if it exists - common mistake)
$WrongPanelPath = Join-Path $DestStrategyDir "V12StandardPanel.cs"
if (Test-Path $WrongPanelPath) {
    Remove-Item -Path $WrongPanelPath -Force -Verbose
    Write-Host "CLEANUP: Removed V12StandardPanel.cs from Strategies folder (Fixed compilation conflict)" -ForegroundColor Magenta
}

Write-Host "`n--- Deployment Complete! ---" -ForegroundColor Cyan
Write-Host "Action: Please switch to NinjaTrader 8 and press F5 (Tools > Reload NinjaScript) or wait for the chime." -ForegroundColor White
