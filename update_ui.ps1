$targetPath = "UniversalORStrategyV12.cs"
$sourcePath = "V12_UI_Block.cs"
$lines = Get-Content $targetPath
$newContent = Get-Content $sourcePath

$startIndex = -1
$endIndex = -1

for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match "private void CreateUI\(\)") {
        $startIndex = $i
        break
    }
}

for ($i = $startIndex; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match "private void MoveStopsToBreakevenWithOffset") {
        $endIndex = $i
        break
    }
}

if ($startIndex -ne -1 -and $endIndex -ne -1) {
    $before = $lines[0..($startIndex-1)]
    $after = $lines[$endIndex..($lines.Count-1)]
    
    $final = $before + $newContent + $after
    $final | Set-Content $targetPath -Encoding UTF8
    Write-Host "SUCCESS: Replaced lines $startIndex to $endIndex"
} else {
    Write-Error "Could not find start or end markers. Start: $startIndex End: $endIndex"
}
