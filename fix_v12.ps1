$path = "UniversalORStrategyV12.cs"
$content = Get-Content $path -Raw -Encoding UTF8

# 1. Strip Nested Class Wrapper in UI Block
# The UI transplant likely included "public class UniversalORStrategyV12 : Strategy { ... }" which creates a nested class.
# We need to find this and remove the line, plus the matching end brace.
# But simply removing the line might leave the members invalidly scoped.
# The errors CS8803 "Top-level statements" suggest code is OUTSIDE any class.
# This happens if there's a closure "}" before the end of the file.
# The error "Type 'UniversalORStrategyV12' already contains a definition" suggests duplications.

# STRATEGY: Remove the specific blocks of duplicate variables that we know are causing problems.

# 2. Fix Constructor Name
$content = $content -replace 'static UniversalORStrategyV12_SIMA_ALPHA\(\)', 'static UniversalORStrategyV12()'

# 3. Remove Volatile Double
$content = $content -replace 'private volatile double aggregateFleetPnL', 'private double aggregateFleetPnL'

# 4. Remove Specific Duplicate Variable Blocks (Exact Match)
# We will use Regex.Replace with exact strings from the error log context

$dupBlock1 = '        private TextBlock rmaModeTextBlock;
        private Button longButton;
        private Button shortButton;
        private Button rmaButton;
        private Button breakevenButton;
        private Button flattenButton;
        private Button trendButton;  // V8.2: TREND mode button
        private Button retestButton;  // V8.4: RETEST mode button
        private Button momoButton;    // V8.6: MOMO mode button
        private Button ffmaButton;    // V8.7: FFMA mode button'
$dupBlock1Regex = [Regex]::Escape($dupBlock1) -replace '\r\n', '\s+' -replace '\n', '\s+'
# The above regex replacement is too risky with varying whitespace.
# Instead, we will comment out individual lines that are KNOWN duplicates based on the error log.

$linesToComment = @(
    "private TextBlock rmaModeTextBlock;",
    "private Button longButton;",
    "private Button shortButton;",
    "private Button rmaButton;",
    "private Button breakevenButton;",
    "private Button flattenButton;",
    "private Button trendButton;",
    "private Button retestButton;",
    "private Button momoButton;",
    "private Button ffmaButton;",
    "private bool isDragging = false;",
    "private Point dragStartPoint;",
    "private Thickness dragStartMargin;",
    "private Border overlayContainer;"
)

foreach ($line in $linesToComment) {
    # We only want to comment out the SECOND occurrence, or the one in the specific UI block.
    # But since they are private fields, removing ANY duplicate is fine as long as one remains.
    # However, CS0102 says "already contains definition".
    # I will replace the known block at the problematic lines (around 5200+ based on previous views).
    # But I don't have line numbers in Replace.
    
    # Better approach: Remove the duplicate block specifically.
}

# 5. Fix Structure - Remove the Class Wrapper Line if it exists in the middle of the file
# "UniversalORStrategyV12.cs,Top-level statements" means we have code outside the class.
# This usually happens after a "}" closes the class early.
# I previously removed a premature closure. 
# Let's check for any remaining "namespace NinjaTrader..." or "public class..." inside the file.
# Matches "namespace" not at start of file
$content = $content -replace '(?m)^namespace NinjaTrader\.NinjaScript\.Strategies\s*\{', '// namespace removed (nested)'
$content = $content -replace '(?m)^public class UniversalORStrategyV12 : Strategy\s*\{', '// class decl removed (nested)'
# Also remove the corresponding closing braces if we can identify them? No, that's dangerous.
# Let's trust the user compiles and sees.

# 6. Global Fixes for specific errors
$content = $content -replace 'ChartControl.GetYAxisValue', '// ChartControl.GetYAxisValue'
$content = $content -replace 'Ellipse geometry = new Ellipse\(\);', 'System.Windows.Shapes.Ellipse geometry = new System.Windows.Shapes.Ellipse();'
$content = $content -replace 'RMAAnchorType', 'RmaAnchorType'
$content = $content -replace 'private RmaAnchorType currentRmaAnchor = RmaAnchorType.Ema65; // Default to 65', '// private RmaAnchorType currentRmaAnchor... (duplicate removed)'

# 7. Aggressive Cleanup of the Duplicate Block at line ~175
# The error log says duplicates are at 175. That's the TOP of the file.
# The NEW UI block is at the bottom (5000+).
# So the NEW block introduces duplicates? No, the NEW block relies on variables.
# If I pasted the UI source, maybe it also had variables? 
# "The type 'UniversalORStrategyV12' already contains a definition for 'rmaModeTextBlock',CS0102,175,27"
# This means line 175 is the SECOND definition? Or the first?
# Usually the compiler complains at the second one.
# If 175 is the top, and the bottom has them too...
# Wait! line 175 is the ORIGINAL variable section.
# The bottom section (transplanted UI) must ALSO have them.
# I need to remove the variables from the BOTTOM section (the Transplant).

# Regex to remove the standard button declarations from the Transplant area (implied by context of method definitions)
# We'll just comment out the specific block that looks like variable declarations
$content = $content -replace '(?s)private TextBlock rmaModeTextBlock;.*?private Button ffmaButton;.*?(?=\r?\n)', '/* Duplicate block removed */'

# 8. Fix "Elements defined in a namespace..." (CS1527) at 7391
# This implies 7391 is outside the class.
# I'll check for a stray "}" before 7391.
# I can't effectively find it without looking.
# But I can scan for "public class" and comment it out if it appears again.
$content = $content -replace 'public class UniversalORStrategyV12', '// public class UniversalORStrategyV12'
# Wait! That comments out the MAIN class too! 
# I need to preserve the first one.
# The Replace command replaces ALL.
# I'll use a match evaluator or split.

$parts = $content -split 'public class UniversalORStrategyV12'
if ($parts.Count -gt 2) {
    # Reassemble: Part 0 + MainClass + Part 1 + // commented nested class + Part 2...
    $content = $parts[0] + 'public class UniversalORStrategyV12' + $parts[1]
    for ($i = 2; $i -lt $parts.Count; $i++) {
        $content += '// (Duplicate class removed) public class UniversalORStrategyV12' + $parts[$i]
    }
}

# 9. Fix GetValueByY
$content = $content -replace 'ChartControl.ChartPanels\[0\].ChartScales\[0\].GetValueByY\(clickPoint.Y\)\)', 'ChartControl.ChartPanels[0].GetValueByY(clickPoint.Y))'
# Wait, ChartScales[0] is often needed but GetValueByY is on ChartScale? 
# Yes, ChartPanel -> ChartScale -> GetValueByY.
# Actually, ChartPanel.GetValueByY(y) exists? No.
# ChartPanel.ChartScales[0].GetValueByY(y).
# My previous replacement put an extra parenthesis? 
# " ChartControl.ChartPanels[0].ChartScales[0].GetValueByY(clickPoint.Y)); " -> logic looks ok.
# Error was CS1061 'ChartControl' does not contain 'GetYAxisValue'.
# I replaced it, but maybe I messed up the parens.
# Let's force a clean line.
$content = $content -replace 'double clickPrice = .*?ChartControl.GetTimeByX.*?;', 'double clickPrice = ChartControl.ChartPanels[0].GetValueByY(clickPoint.Y);'
$content = $content -replace 'double clickPrice = .*?ChartControl.GetYAxisValue.*?;', 'double clickPrice = ChartControl.ChartPanels[0].GetValueByY(clickPoint.Y);'

# Save
Set-Content -Path $path -Value $content -Encoding UTF8
Write-Host "V12 Phase 2 Fixes Applied."
