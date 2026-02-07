
import re
import os

path = r'C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Indicators\V12StandardPanel.cs'

with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Fix trailRow Symmetry
trail_pattern = re.compile(r'// TRAIL row: Input \+ Button.*?rightCol\.Children\.Add\(trailRow\);', re.DOTALL)
trail_replace = """// TRAIL row: [ Button(82) | Input(36) ] -> Outer Symmetry
            Grid trailRow = new Grid { Margin = new Thickness(0, 2, 0, 0), Width = 118 };
            trailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            trailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // OFF-CENTER: Outer Accessory
 
             trailDistInput = CreateTextBox(36, "1.0");
             trailDistInput.Height = 22;
             trailDistInput.FontSize = 10;
             trailDistInput.ToolTip = "Trail distance in points";
             Grid.SetColumn(trailDistInput, 1);
             trailRow.Children.Add(trailDistInput);
 
             trailButton = CreateButton("TRAIL", double.NaN, BtnBg, TextPrimary, BtnBorder);
             trailButton.Margin = new Thickness(0, 0, 0, 0);
             trailButton.Click += (s, e) =>
             {
                 string dist = trailDistInput?.Text ?? "1.0";
                 SendCommand($"SET_TRAIL|{dist}");
                 TriggerGlow(CyanFg);
             };
             Grid.SetColumn(trailButton, 0);
             trailRow.Children.Add(trailButton);

             rightCol.Children.Add(trailRow);"""

content = trail_pattern.sub(trail_replace, content)

# 2. Fix modeCountGrid Symmetry
config_pattern = re.compile(r'// Two-column layout: Modes \(left\) \| Counts \(right\).*?stack\.Children\.Add\(modeCountGrid\);', re.DOTALL)
config_replace = """// Two-column layout: Modes (left) | Counts (right)
            Grid modeCountGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            modeCountGrid.HorizontalAlignment = HorizontalAlignment.Center; 
            modeCountGrid.Width = 242; 
            modeCountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); 
            modeCountGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); 

            // LEFT COLUMN: Modes
            StackPanel modeColumn = new StackPanel { Margin = new Thickness(0, 0, 1, 0), Width = 120, HorizontalAlignment = HorizontalAlignment.Left };
            modeOrbButton = CreateModeChip("ORB", true, -1);
            modeRmaButton = CreateModeChip("RMA", false, -1);
            modeRetestButton = CreateModeChip("RETEST", false, -1);
            modeMomoButton = CreateModeChip("MOMO", false, -1);
            modeFfmaButton = CreateModeChip("FFMA", false, -1);
            modeTrendButton = CreateModeChip("TREND", false, -1);
            
            modeColumn.Children.Add(modeOrbButton);
            modeColumn.Children.Add(modeRmaButton);
            modeColumn.Children.Add(modeRetestButton);
            modeColumn.Children.Add(modeMomoButton);
            modeColumn.Children.Add(modeFfmaButton);
            modeColumn.Children.Add(modeTrendButton);

            Grid.SetColumn(modeColumn, 0);
            modeCountGrid.Children.Add(modeColumn);

            // RIGHT COLUMN: Counts
            StackPanel countColumn = new StackPanel { Margin = new Thickness(1, 0, 0, 0), Width = 120, HorizontalAlignment = HorizontalAlignment.Right };
            cnt1 = CreateCountChip("1", 1);
            cnt2 = CreateCountChip("2", 2);
            cnt3 = CreateCountChip("3", 3);
            cnt4 = CreateCountChip("4", 4);
            cnt5 = CreateCountChip("5", 5);
 
             // Default: 3 selected
             cnt3.Background = CyanBg;
             cnt3.Foreground = CyanFg;
             cnt3.BorderBrush = CyanBorder;
 
             countColumn.Children.Add(cnt1);
             countColumn.Children.Add(cnt2);
             countColumn.Children.Add(cnt3);
             countColumn.Children.Add(cnt4);
             countColumn.Children.Add(cnt5);
             Grid.SetColumn(countColumn, 1);
             modeCountGrid.Children.Add(countColumn);

            stack.Children.Add(modeCountGrid);"""

content = config_pattern.sub(config_replace, content)

# 3. Final widths for any missed T-buttons
content = content.replace('t1Button = CreateButton("T1", double.NaN', 't1Button = CreateButton("T1", 118')
content = content.replace('t2Button = CreateButton("T2", double.NaN', 't2Button = CreateButton("T2", 118')
content = content.replace('t3Button = CreateButton("T3", double.NaN', 't3Button = CreateButton("T3", 118')
content = content.replace('t4Button = CreateButton("T4", double.NaN', 't4Button = CreateButton("T4", 118')

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Replacement complete.")
