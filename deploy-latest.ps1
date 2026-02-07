# WSGTA V12 Deployment & Launcher
# Automates: Build -> Shortcut Update -> Relaunch

$ErrorActionPreference = "Stop"
$ProjectRoot = "c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy"
$ProjectFile = Join-Path $ProjectRoot "V12_ExternalRemote\V12_ExternalRemote.csproj"
$BuildOutput = Join-Path $ProjectRoot "V12_ExternalRemote\bin\Release\net6.0-windows\V12_ExternalRemote.exe"
$UserDesktop = [Environment]::GetFolderPath('Desktop')
$ShortcutPath = Join-Path $UserDesktop "V12_Clean_Master.lnk"
$LogFile = Join-Path $UserDesktop "deploy_debug_log.txt"

Start-Transcript -Path $LogFile -Append

Write-Host "--- Starting Deployment Phase (Logged) ---" -ForegroundColor Cyan

# 1. Terminate existing instances
Write-Host "Stopping existing V12_ExternalRemote processes..." -ForegroundColor Gray
try {
    Get-Process -Name "V12_ExternalRemote" -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process -Name "V9_ExternalRemote" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
}
catch {
    Write-Host "No running instances found." -ForegroundColor DarkGray
}

# 2. Build Project
Write-Host "Cleaning previous build..." -ForegroundColor Gray
dotnet clean "$ProjectFile" -c Release
if ($LASTEXITCODE -ne 0) { Write-Error "Clean Failed"; exit }

Write-Host "Building latest version (Release)..." -ForegroundColor Gray
dotnet build "$ProjectFile" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build Failed! Check for code errors in NinjaTrader or VS."
    Read-Host "Press Enter to see error details and exit..."
    exit
}

# Verify Build Exists
if (-not (Test-Path $BuildOutput)) {
    Write-Error "Build Artifact not found at: $BuildOutput"
    exit
}

# 3. Update Shortcut
Write-Host "Updating desktop shortcut: $ShortcutPath" -ForegroundColor Gray
try {
    $WshShell = New-Object -ComObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
    $Shortcut.TargetPath = $BuildOutput
    $Shortcut.WorkingDirectory = [System.IO.Path]::GetDirectoryName($BuildOutput)
    $Shortcut.Save()
}
catch {
    Write-Warning "Failed to update shortcut: $_"
}

# 4. Deploy NinjaScripts (Delegated to Single Source of Truth)
Write-Host "Calling deploy-strategy.ps1 to sync NinjaScripts..." -ForegroundColor Cyan
$DeployScript = Join-Path $ProjectRoot "deploy-strategy.ps1"
if (Test-Path $DeployScript) {
    powershell -ExecutionPolicy Bypass -File $DeployScript
}
else {
    Write-Warning "deploy-strategy.ps1 not found! Skipping NinjaScript deployment."
}

# 5. Relaunch
Write-Host "--- Launching Latest Version ---" -ForegroundColor Green
try {
    # Use Explorer to launch to ensure it detaches properly from the script session
    Invoke-Item $BuildOutput
    Write-Host "Launch command sent."
}
catch {
    Write-Error "Failed to launch app: $_"
    # Fallback
    Start-Process -FilePath $BuildOutput -WorkingDirectory ([System.IO.Path]::GetDirectoryName($BuildOutput))
}

Write-Host "Deployment Complete! ✅" -ForegroundColor Cyan
Start-Sleep -Seconds 2

