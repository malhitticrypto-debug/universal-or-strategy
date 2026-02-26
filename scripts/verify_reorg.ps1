# verify_reorg.ps1
# WSGTA Infrastructure: Reorganization Verification Suite

$RepoRoot = "C:\WSGTA\universal-or-strategy"
$SrcDir = Join-Path $RepoRoot "src"
$DocsDir = Join-Path $RepoRoot "docs\audits"
$BinDir = Join-Path $RepoRoot "bin"

Write-Host "`n--- PROJECT REORG VERIFICATION ---" -ForegroundColor Cyan

$Success = $true

# 1. Check src/ contents
$csFiles = Get-ChildItem -Path $SrcDir -Filter *.cs
if ($csFiles.Count -ge 5) {
    Write-Host "[PASS] Core logic identified in src/ ($($csFiles.Count) files)" -ForegroundColor Green
}
else {
    Write-Host "[FAIL] Missing core files in src/" -ForegroundColor Red
    $Success = $false
}

# 2. Check bin/ contents
if (Test-Path (Join-Path $BinDir "acli.exe")) {
    Write-Host "[PASS] Auditor binary moved to bin/" -ForegroundColor Green
}
else {
    Write-Host "[FAIL] acli.exe missing from bin/" -ForegroundColor Red
    $Success = $false
}

# 3. Check docs/audits/ contents
if (Test-Path (Join-Path $DocsDir "CONSOLIDATED_AUDIT.md")) {
    Write-Host "[PASS] Audit logs moved to docs/audits/" -ForegroundColor Green
}
else {
    Write-Host "[FAIL] Audit logs missing from docs/audits/" -ForegroundColor Red
    $Success = $false
}

# 4. Check for root clutter
$rootCS = Get-ChildItem -Path $RepoRoot -Filter *.cs
if ($rootCS.Count -eq 0) {
    Write-Host "[PASS] Root directory is clean of .cs files" -ForegroundColor Green
}
else {
    Write-Host "[FAIL] .cs files still lingering in root: $($rootCS.Name)" -ForegroundColor Yellow
    $Success = $false
}

if ($Success) {
    Write-Host "`n--- VERIFICATION SUCCESS: PROJECT HARDENED ---" -ForegroundColor Cyan
}
else {
    Write-Host "`n--- VERIFICATION FAILED: MANUAL FIX REQUIRED ---" -ForegroundColor Red
    exit 1
}
