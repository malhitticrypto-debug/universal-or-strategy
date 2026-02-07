# setup-symlinks.ps1
# WSGTA Infrastructure: One Source of Truth Setup
# This script links the GitHub repository to the NinjaTrader 8 Documents folder.

$ErrorActionPreference = "Stop"

# Paths
$RepoRoot = "c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy"
$NtUserDir = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom"
$DestStrategyDir = Join-Path $NtUserDir "Strategies"
$DestIndicatorDir = Join-Path $NtUserDir "Indicators"

# File Names
$StrategyFile = "UniversalORStrategyV12.cs"
$PanelFile = "V12StandardPanel.cs"

# Target Paths
$GitHubStrategy = Join-Path $RepoRoot $StrategyFile
$GitHubPanel = Join-Path $RepoRoot $PanelFile

$NtStrategy = Join-Path $DestStrategyDir $StrategyFile
$NtPanel = Join-Path $DestIndicatorDir $PanelFile

Write-Host "--- WSGTA Symlink Setup (One Source of Truth) ---" -ForegroundColor Cyan

# 1. Backup existing files in NinjaTrader Documents if they aren't links
function Backup-And-Link {
    param($Source, $Destination)
    
    if (Test-Path $Destination) {
        $item = Get-Item $Destination
        if ($item.Attributes -match "ReparsePoint") {
            Write-Host "LINK EXISTS: $Destination is already a link." -ForegroundColor Gray
            return
        }
        
        $BackupPath = $Destination + ".bak_" + (Get-Date -Format "yyyyMMdd_HHmmss")
        Write-Host "BACKING UP: Moving $Destination to $BackupPath" -ForegroundColor Yellow
        Move-Item $Destination $BackupPath
    }
    
    Write-Host "LINKING: $Destination -> $Source" -ForegroundColor Green
    New-Item -ItemType HardLink -Path $Destination -Value $Source
}

# 2. Setup Strategy link
Backup-And-Link -Source $GitHubStrategy -Destination $NtStrategy

# 3. Setup Panel link
Backup-And-Link -Source $GitHubPanel -Destination $NtPanel

Write-Host "`n--- Setup Complete! ---" -ForegroundColor Cyan
Write-Host "NinjaTrader will now read directly from your GitHub folder."
Write-Host "Any change saved in the GitHub folder INSTANTLY updates NinjaTrader."
Write-Host "Switch to NinjaTrader and press F5 if it doesn't auto-reload."
