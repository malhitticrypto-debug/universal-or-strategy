# deploy-sync.ps1
# WSGTA Infrastructure: One Source of Truth Automation
# Establishes Hard Links between Git Repo and NinjaTrader 8

$ErrorActionPreference = "Stop"

# --- CONFIGURATION ---
$RepoRoot = "C:\WSGTA\universal-or-strategy"
$NtCustomDir = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom"
$NtStrategyDir = Join-Path $NtCustomDir "Strategies"
$NtIndicatorDir = Join-Path $NtCustomDir "Indicators"

# File Mappings (Source in Repo -> Target in NT8)
$Mappings = @(
    # Indicators
    @{ src = "V12StandardPanel_V12_001_Dev.cs"; dst = Join-Path $NtIndicatorDir "V12StandardPanel_V12_001_Dev.cs" },
    
    # Strategy (Modularized V12_002 Series)
    @{ src = "UniversalORStrategyV12_002_Dev.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Entries.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Entries.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Entries.FFMA.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Entries.FFMA.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Entries.OR.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Entries.OR.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Entries.RMA.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Entries.RMA.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Entries.MOMO.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Entries.MOMO.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Entries.Trend.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Entries.Trend.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Entries.Retest.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Entries.Retest.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Orders.Callbacks.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Orders.Callbacks.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Orders.Management.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Orders.Management.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.SIMA.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.SIMA.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.REAPER.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.REAPER.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.UI.Callbacks.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.UI.Callbacks.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.UI.Compliance.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.UI.Compliance.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.UI.IPC.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.UI.IPC.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.UI.Sizing.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.UI.Sizing.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Symmetry.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Symmetry.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.LogicAudit.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.LogicAudit.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Trailing.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Trailing.cs" },
    @{ src = "UniversalORStrategyV12_002_Dev.Properties.cs"; dst = Join-Path $NtStrategyDir "UniversalORStrategyV12_002_Dev.Properties.cs" },
    
    # Shared Components
    @{ src = "SignalBroadcaster.cs"; dst = Join-Path $NtStrategyDir "SignalBroadcaster.cs" }
)

Write-Host "`n--- WSGTA DEPLOY SYNC: Hardening Environment ---" -ForegroundColor Cyan

foreach ($map in $Mappings) {
    $srcPath = Join-Path (Join-Path $RepoRoot "src") $map.src
    $dstPath = $map.dst
    
    if (!(Test-Path $srcPath)) {
        Write-Host "SKIP: Source missing -> src/$($map.src)" -ForegroundColor Gray
        continue
    }

    # Ensure Target Directory Exists
    $targetDir = Split-Path $dstPath
    if (!(Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir }

    # Sync Logic
    if (Test-Path $dstPath) {
        $item = Get-Item $dstPath
        if ($item.Attributes -match "ReparsePoint") {
            # Check if it's already linked
            Write-Host "SECURE: $($map.src) is already linked." -ForegroundColor Gray
            continue
        }
        
        # Backup if it's a real file (to avoid losing work)
        $backup = $dstPath + ".bak_" + (Get-Date -Format "yyyyMMdd_HHmm")
        Write-Host "BACKUP: Archiving existing NT file -> $(Split-Path $backup -Leaf)" -ForegroundColor Yellow
        Move-Item $dstPath $backup
    }

    # Create the Link
    Write-Host "LINKING: $($map.src) -> NT8" -ForegroundColor Green
    New-Item -ItemType HardLink -Path $dstPath -Value $srcPath | Out-Null
}

Write-Host "`n--- SYNC COMPLETE: One Source of Truth Established ---" -ForegroundColor Cyan
Write-Host "Tip: Edit files in $RepoRoot. NT8 will update instantly." -ForegroundColor Gray
