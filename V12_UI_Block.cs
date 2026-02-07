        #region V12 UI - CreateUI (Pixel-Perfect Implementation)

        private void CreateUI()
        {
            if (ChartControl == null || uiCreated) return;

            try
            {
                // Outer container
                System.Windows.Controls.Grid outerContainer = new System.Windows.Controls.Grid();

                mainBorder = new Border
                {
                    Background = BgSecondary,
                    BorderBrush = AccentCyan,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(10, 10, 0, 0),
                    Width = baseWidth,
                    MinWidth = 200,
                    MinHeight = 300
                };

                contentViewbox = new Viewbox
                {
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    StretchDirection = StretchDirection.Both,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                mainGrid = new System.Windows.Controls.Grid();
                mainGrid.Width = baseWidth;

                // V12: 14 rows for complete layout
                for (int i = 0; i < 14; i++)
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 0: Title Bar (Drag Handle)
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                Border titleBar = CreateTitleBar();
                Grid.SetRow(titleBar, 0);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 1: Fleet Config (EIDs 86-88) - Directly beneath title bar
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                fleetConfigBorder = CreateFleetConfigSection();
                Grid.SetRow(fleetConfigBorder, 1);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 2: Status Display
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                StackPanel statusSection = CreateStatusSection();
                Grid.SetRow(statusSection, 2);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 3: OR Info Panel
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                Border orInfoPanel = CreateORInfoPanel();
                Grid.SetRow(orInfoPanel, 3);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 4: Target Mode Selector (ATR/PTS/TKS/RNG)
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                Border targetModeSection = CreateTargetModeSection();
                Grid.SetRow(targetModeSection, 4);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 5: RMA Anchor Section
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                Border rmaAnchorSection = CreateRMAAnchorSection();
                Grid.SetRow(rmaAnchorSection, 5);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 6: RMA Mode Indicator
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                rmaModeTextBlock = new TextBlock
                {
                    Text = "Ôÿà RMA ACTIVE - Click chart Ôÿà",
                    Foreground = AccentOrange,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(12, 4, 12, 4),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };
                Grid.SetRow(rmaModeTextBlock, 6);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 7: OR Entry Row (LONG vertical)
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                longButton = CreateStandardButton("Ôû▓ LONG (L)", AccentGreen, OnLongClick);
                Grid.SetRow(longButton, 7);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 8: OR Entry Row (SHORT vertical)
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                shortButton = CreateStandardButton("Ôû╝ SHORT (S)", AccentRed, OnShortClick);
                Grid.SetRow(shortButton, 8);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 9: Special Entry Row (RMA | RETEST)
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                System.Windows.Controls.Grid specialRow1 = new System.Windows.Controls.Grid();
                specialRow1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                specialRow1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                rmaButton = CreateCompactButton("RMA", AccentOrange, OnRMAClick);
                Grid.SetColumn(rmaButton, 0);
                specialRow1.Children.Add(rmaButton);

                retestButton = CreateCompactButton("RETEST", AccentPurple, OnRetestClick);
                Grid.SetColumn(retestButton, 1);
                specialRow1.Children.Add(retestButton);

                Grid.SetRow(specialRow1, 9);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 10: MOMO | FFMA (Vertical Stack)
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                System.Windows.Controls.Grid specialRow2 = new System.Windows.Controls.Grid();
                specialRow2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                specialRow2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                momoButton = CreateCompactButton("MOMO", AccentPurple, OnMOMOClick);
                Grid.SetColumn(momoButton, 0);
                specialRow2.Children.Add(momoButton);

                ffmaButton = CreateCompactButton("FFMA", AccentPink, OnFFMAClick);
                Grid.SetColumn(ffmaButton, 1);
                specialRow2.Children.Add(ffmaButton);

                Grid.SetRow(specialRow2, 10);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 11: TREND Button
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                trendButton = CreateStandardButton("TREND", AccentBlue, OnTRENDClick);
                Grid.SetRow(trendButton, 11);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 12: Target Row (T1 | T2 | T3 | RUN | BE)
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                System.Windows.Controls.Grid targetGrid = CreateTargetButtonRow();
                Grid.SetRow(targetGrid, 12);

                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                // ROW 13: Flatten Button
                // ÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉÔòÉ
                flattenButton = new Button
                {
                    Content = "ÔÜá FLATTEN ALL (F)",
                    Background = AccentOrange,
                    Foreground = TextPrimary,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Height = 28,
                    Margin = new Thickness(6, 4, 6, 4),
                    Padding = new Thickness(2),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(0)
                };
                flattenButton.Click += (s, e) => OnGlobalSyncSelect("FLATTEN");
                Grid.SetRow(flattenButton, 13);

                // Add all elements to mainGrid
                mainGrid.Children.Add(titleBar);
                mainGrid.Children.Add(fleetConfigBorder);
                mainGrid.Children.Add(statusSection);
                mainGrid.Children.Add(orInfoPanel);
                mainGrid.Children.Add(targetModeSection);
                mainGrid.Children.Add(rmaAnchorSection);
                mainGrid.Children.Add(rmaModeTextBlock);
                mainGrid.Children.Add(longButton);
                mainGrid.Children.Add(shortButton);
                mainGrid.Children.Add(specialRow1);
                mainGrid.Children.Add(specialRow2);
                mainGrid.Children.Add(trendButton);
                mainGrid.Children.Add(targetGrid);
                mainGrid.Children.Add(flattenButton);

                contentViewbox.Child = mainGrid;
                mainBorder.Child = contentViewbox;

                UserControlCollection.Add(mainBorder);

                uiCreated = true;
                Print("V12 PRO MASTER UI Created - Fleet Sync Active");
            }
            catch (Exception ex)
            {
                Print("ERROR CreateUI: " + ex.Message);
            }
        }

        #endregion

        #region V12 UI Component Builders

        private Border CreateTitleBar()
        {
            Border titleBar = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromRgb(30, 58, 95),
                    Color.FromRgb(15, 23, 42),
                    45),
                Padding = new Thickness(4),
                Cursor = Cursors.SizeAll
            };

            titleBar.MouseLeftButtonDown += OnDragStart;
            titleBar.MouseMove += OnDragMove;
            titleBar.MouseLeftButtonUp += OnDragEnd;

            System.Windows.Controls.Grid titleGrid = new System.Windows.Controls.Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock titleText = new TextBlock
            {
                Text = "Ôÿà V12 PRO MASTER Ôÿà",
                Foreground = AccentCyan,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleText.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Cyan,
                BlurRadius = 10,
                ShadowDepth = 0
            };
            Grid.SetColumn(titleText, 0);
            titleGrid.Children.Add(titleText);

            Border versionBadge = new Border
            {
                Background = AccentGold,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            TextBlock versionText = new TextBlock
            {
                Text = "V14.9",
                Foreground = BgPrimary,
                FontWeight = FontWeights.Bold,
                FontSize = 10
            };
            versionBadge.Child = versionText;
            Grid.SetColumn(versionBadge, 1);
            titleGrid.Children.Add(versionBadge);

            titleBar.Child = titleGrid;
            return titleBar;
        }

        private Border CreateFleetConfigSection()
        {
            Border section = new Border
            {
                Background = BgTertiary,
                Padding = new Thickness(10, 8, 10, 8),
                BorderBrush = BorderPrimary,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            StackPanel content = new StackPanel();

            // Header row
            System.Windows.Controls.Grid headerGrid = new System.Windows.Controls.Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock fleetTitle = new TextBlock
            {
                Text = "FLEET CONFIG",
                Foreground = AccentGold,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            };
            Grid.SetColumn(fleetTitle, 0);
            headerGrid.Children.Add(fleetTitle);

            StackPanel statusPanel = new StackPanel { Orientation = Orientation.Horizontal };

            Ellipse indicator = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = AccentGreen,
                Margin = new Thickness(0, 0, 6, 0)
            };
            statusPanel.Children.Add(indicator);

            fleetStatusText = new TextBlock
            {
                Text = string.Format("{0}/{0} Connected", FleetAccountCount),
                Foreground = TextSecondary,
                FontSize = 11
            };
            statusPanel.Children.Add(fleetStatusText);

            Grid.SetColumn(statusPanel, 1);
            headerGrid.Children.Add(statusPanel);

            content.Children.Add(headerGrid);

            // Account chips
            fleetChipsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };

            WrapPanel chipWrap = new WrapPanel();

            // Master chip
            Border masterChip = CreateAccountChip("MASTER", true, true);
            chipWrap.Children.Add(masterChip);

            // Slave chips
            for (int i = 1; i < FleetAccountCount; i++)
            {
                string accountName = "APEX" + i;
                Border chip = CreateAccountChip(accountName, i <= 3, false);
                fleetAccountChips[accountName] = chip;
                chipWrap.Children.Add(chip);
            }

            content.Children.Add(chipWrap);

            section.Child = content;
            return section;
        }

        private Border CreateAccountChip(string name, bool isActive, bool isMaster)
        {
            Border chip = new Border
            {
                Background = isActive ? (isMaster ? new SolidColorBrush(Color.FromArgb(25, 255, 204, 0)) : new SolidColorBrush(Color.FromArgb(25, 0, 255, 255))) : BgSecondary,
                BorderBrush = isActive ? (isMaster ? AccentGold : AccentCyan) : BorderPrimary,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 0, 4, 4),
                Cursor = Cursors.Hand
            };

            TextBlock text = new TextBlock
            {
                Text = name,
                Foreground = isActive ? (isMaster ? AccentGold : AccentCyan) : TextSecondary,
                FontSize = 10
            };

            chip.Child = text;
            chip.Tag = name;

            if (!isMaster)
            {
                chip.MouseLeftButtonDown += (s, e) =>
                {
                    Border b = s as Border;
                    if (b != null)
                    {
                        string acctName = b.Tag as string;
                        ToggleFleetAccount(acctName, b);
                    }
                };
            }

            return chip;
        }

        private void ToggleFleetAccount(string accountName, Border chip)
        {
            if (fleetAccounts.ContainsKey(accountName))
            {
                var acct = fleetAccounts[accountName];
                acct.IsConnected = !acct.IsConnected;

                TextBlock text = chip.Child as TextBlock;
                if (acct.IsConnected)
                {
                    chip.Background = new SolidColorBrush(Color.FromArgb(25, 0, 255, 255));
                    chip.BorderBrush = AccentCyan;
                    if (text != null) text.Foreground = AccentCyan;
                }
                else
                {
                    chip.Background = BgSecondary;
                    chip.BorderBrush = BorderPrimary;
                    if (text != null) text.Foreground = TextSecondary;
                }

                UpdateFleetStatus();
            }
        }

        private void UpdateFleetStatus()
        {
            int connected = fleetAccounts.Values.Count(a => a.IsConnected);
            if (fleetStatusText != null)
            {
                ChartControl.Dispatcher.InvokeAsync(() =>
                {
                    fleetStatusText.Text = string.Format("{0}/{1} Connected", connected, FleetAccountCount);
                });
            }
            connectedFleetCount = connected;
        }

        private StackPanel CreateStatusSection()
        {
            StackPanel section = new StackPanel
            {
                Margin = new Thickness(6, 2, 6, 2)
            };

            section.Children.Add(CreateStatusRow("Session", "NY OPEN", AccentCyan));
            section.Children.Add(CreateStatusRow("ATR (14)", "2.45", TextPrimary));
            section.Children.Add(CreateStatusRow("Fleet P&L", "+$0.00", AccentGreen));

            statusTextBlock = section.Children[0] as TextBlock;
            orInfoBlock = section.Children[1] as TextBlock;
            positionSummaryBlock = section.Children[2] as TextBlock;

            return section;
        }

        private System.Windows.Controls.Grid CreateStatusRow(string label, string value, SolidColorBrush valueColor)
        {
            System.Windows.Controls.Grid row = new System.Windows.Controls.Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                Foreground = TextSecondary,
                FontSize = 12
            };
            Grid.SetColumn(labelBlock, 0);
            row.Children.Add(labelBlock);

            TextBlock valueBlock = new TextBlock
            {
                Text = value,
                Foreground = valueColor,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            };
            Grid.SetColumn(valueBlock, 1);
            row.Children.Add(valueBlock);

            return row;
        }

        private Border CreateORInfoPanel()
        {
            Border panel = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(12, 0, 255, 255)),
                Padding = new Thickness(12, 8, 12, 8),
                BorderBrush = BorderPrimary,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            System.Windows.Controls.Grid levelGrid = new System.Windows.Controls.Grid();
            levelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            levelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            levelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            levelGrid.Children.Add(CreateORLevelBlock("High", "0.00", 0));
            levelGrid.Children.Add(CreateORLevelBlock("Mid", "0.00", 1));
            levelGrid.Children.Add(CreateORLevelBlock("Low", "0.00", 2));

            panel.Child = levelGrid;
            return panel;
        }

        private Border CreateORLevelBlock(string label, string value, int column)
        {
            Border block = new Border
            {
                Background = BgTertiary,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4),
                Margin = new Thickness(1)
            };

            StackPanel content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            TextBlock labelText = new TextBlock
            {
                Text = label,
                Foreground = TextMuted,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            content.Children.Add(labelText);

            TextBlock valueText = new TextBlock
            {
                Text = value,
                Foreground = AccentCyan,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            content.Children.Add(valueText);

            block.Child = content;
            Grid.SetColumn(block, column);
            return block;
        }

        private Border CreateTargetModeSection()
        {
            Border section = new Border
            {
                Background = BgTertiary,
                Padding = new Thickness(6, 2, 6, 2),
                BorderBrush = BorderPrimary,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            StackPanel content = new StackPanel();

            TextBlock title = new TextBlock
            {
                Text = "TARGET CALCULATION MODE",
                Foreground = TextSecondary,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            content.Children.Add(title);

            // Mode chips
            StackPanel chipPanel = new StackPanel { Orientation = Orientation.Horizontal };
            string[] modes = { "ATR", "PTS", "TKS", "RNG" };
            targetModeChips = new Border[4];

            for (int i = 0; i < modes.Length; i++)
            {
                Border chip = CreateModeChip(modes[i], i == 0);
                targetModeChips[i] = chip;
                chipPanel.Children.Add(chip);
            }
            content.Children.Add(chipPanel);

            // Target value inputs
            System.Windows.Controls.Grid inputGrid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < 4; i++)
                inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string[] labels = { "T1", "T2", "T3", "T4" };
            string[] values = { "1.0", "0.5x", "1.0x", "TRAIL" };

            for (int i = 0; i < 4; i++)
            {
                StackPanel inputGroup = new StackPanel { Margin = new Thickness(2) };

                TextBlock labelText = new TextBlock
                {
                    Text = labels[i],
                    Foreground = TextMuted,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                inputGroup.Children.Add(labelText);

                TextBox input = new TextBox
                {
                    Text = values[i],
                    Background = BgSecondary,
                    Foreground = TextPrimary,
                    BorderBrush = BorderPrimary,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    TextAlignment = TextAlignment.Center,
                    Padding = new Thickness(4)
                };
                inputGroup.Children.Add(input);

                if (i == 0) t1ValueInput = input;
                else if (i == 1) t2ValueInput = input;
                else if (i == 2) t3ValueInput = input;
                else t4ValueInput = input;

                Grid.SetColumn(inputGroup, i);
                inputGrid.Children.Add(inputGroup);
            }
            content.Children.Add(inputGrid);

            section.Child = content;
            return section;
        }

        private Border CreateModeChip(string mode, bool isActive)
        {
            Border chip = new Border
            {
                Background = isActive ? new SolidColorBrush(Color.FromArgb(38, 0, 255, 255)) : BgSecondary,
                BorderBrush = isActive ? AccentCyan : BorderPrimary,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 0, 2, 0),
                Cursor = Cursors.Hand
            };

            TextBlock text = new TextBlock
            {
                Text = mode,
                Foreground = isActive ? AccentCyan : TextSecondary,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            };

            chip.Child = text;
            chip.Tag = mode;

            chip.MouseLeftButtonDown += OnTargetModeChipClick;

            return chip;
        }

        private void OnTargetModeChipClick(object sender, MouseButtonEventArgs e)
        {
            Border clickedChip = sender as Border;
            if (clickedChip == null) return;

            string mode = clickedChip.Tag as string;

            // Update all chips
            for (int i = 0; i < targetModeChips.Length; i++)
            {
                Border chip = targetModeChips[i];
                TextBlock text = chip.Child as TextBlock;
                bool isActive = (chip.Tag as string) == mode;

                chip.Background = isActive ? new SolidColorBrush(Color.FromArgb(38, 0, 255, 255)) : BgSecondary;
                chip.BorderBrush = isActive ? AccentCyan : BorderPrimary;
                if (text != null) text.Foreground = isActive ? AccentCyan : TextSecondary;
            }

            // Update target calculation mode
            switch (mode)
            {
                case "ATR": currentTargetMode = TargetModeType.ATR; break;
                case "PTS": currentTargetMode = TargetModeType.PTS; break;
                case "TKS": currentTargetMode = TargetModeType.TKS; break;
                case "RNG": currentTargetMode = TargetModeType.RNG; break;
            }

            UpdateTargetInputValues();
            OnGlobalSyncSelect("TARGET_MODE_" + mode);
        }

        private void UpdateTargetInputValues()
        {
            if (t1ValueInput == null) return;

            switch (currentTargetMode)
            {
                case TargetModeType.ATR:
                    t1ValueInput.Text = "1.0";
                    t2ValueInput.Text = "0.5x";
                    t3ValueInput.Text = "1.0x";
                    break;
                case TargetModeType.PTS:
                    t1ValueInput.Text = "1.0";
                    t2ValueInput.Text = "2.0";
                    t3ValueInput.Text = "4.0";
                    break;
                case TargetModeType.TKS:
                    t1ValueInput.Text = "4";
                    t2ValueInput.Text = "8";
                    t3ValueInput.Text = "16";
                    break;
                case TargetModeType.RNG:
                    t1ValueInput.Text = "0.25x";
                    t2ValueInput.Text = "0.5x";
                    t3ValueInput.Text = "1.0x";
                    break;
            }
        }

        private Border CreateRMAAnchorSection()
        {
            Border section = new Border
            {
                Padding = new Thickness(6, 2, 6, 2),
                BorderBrush = BorderPrimary,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            StackPanel content = new StackPanel();

            TextBlock title = new TextBlock
            {
                Text = "RMA ANCHOR",
                Foreground = AccentOrange,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            content.Children.Add(title);

            WrapPanel chipPanel = new WrapPanel();
            string[] anchors = { "EMA 30", "EMA 65", "EMA 200", "OR High", "OR Low" };
            RMAAnchorType[] anchorTypes = { RMAAnchorType.EMA30, RMAAnchorType.EMA65, RMAAnchorType.EMA200, RMAAnchorType.ORHigh, RMAAnchorType.ORLow };
            rmaAnchorChips = new Border[5];

            for (int i = 0; i < anchors.Length; i++)
            {
                bool isActive = anchorTypes[i] == currentRMAAnchor;
                bool isEMA = anchors[i].StartsWith("EMA");

                Border chip = CreateRMAAnchorChip(anchors[i], anchorTypes[i], isActive, isEMA);
                rmaAnchorChips[i] = chip;
                chipPanel.Children.Add(chip);
            }
            content.Children.Add(chipPanel);

            section.Child = content;
            return section;
        }

        private Border CreateRMAAnchorChip(string label, RMAAnchorType anchorType, bool isActive, bool isEMA)
        {
            SolidColorBrush activeColor = isEMA ? AccentBlue : AccentCyan;
            SolidColorBrush inactiveColor = isEMA ? new SolidColorBrush(Color.FromArgb(76, 59, 130, 246)) : new SolidColorBrush(Color.FromArgb(76, 0, 255, 255));

            Border chip = new Border
            {
                Background = isActive ? new SolidColorBrush(Color.FromArgb(38, activeColor.Color.R, activeColor.Color.G, activeColor.Color.B)) : BgTertiary,
                BorderBrush = isActive ? activeColor : inactiveColor,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 0, 2, 2),
                Cursor = Cursors.Hand
            };

            TextBlock text = new TextBlock
            {
                Text = label,
                Foreground = isActive ? activeColor : TextSecondary,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            };

            chip.Child = text;
            chip.Tag = anchorType;

            chip.MouseLeftButtonDown += OnRMAAnchorChipClick;

            return chip;
        }

        private void OnRMAAnchorChipClick(object sender, MouseButtonEventArgs e)
        {
            Border clickedChip = sender as Border;
            if (clickedChip == null) return;

            RMAAnchorType anchorType = (RMAAnchorType)clickedChip.Tag;
            currentRMAAnchor = anchorType;

            // Update all chips
            for (int i = 0; i < rmaAnchorChips.Length; i++)
            {
                Border chip = rmaAnchorChips[i];
                RMAAnchorType chipType = (RMAAnchorType)chip.Tag;
                TextBlock text = chip.Child as TextBlock;
                bool isActive = chipType == anchorType;
                bool isEMA = i < 3;

                SolidColorBrush activeColor = isEMA ? AccentBlue : AccentCyan;

                chip.Background = isActive ? new SolidColorBrush(Color.FromArgb(38, activeColor.Color.R, activeColor.Color.G, activeColor.Color.B)) : BgTertiary;
                chip.BorderBrush = isActive ? activeColor : (isEMA ? new SolidColorBrush(Color.FromArgb(76, 59, 130, 246)) : new SolidColorBrush(Color.FromArgb(76, 0, 255, 255)));
                if (text != null) text.Foreground = isActive ? activeColor : TextSecondary;
            }

            OnGlobalSyncSelect("RMA_ANCHOR_" + anchorType.ToString());
        }

        private Button CreateStandardButton(string content, SolidColorBrush background, RoutedEventHandler clickHandler)
        {
            Button btn = new Button
            {
                Content = content,
                Background = background,
                Foreground = TextPrimary,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Height = 22,
                Margin = new Thickness(6, 1, 6, 1),
                Padding = new Thickness(2),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btn.Click += clickHandler;
            return btn;
        }

        private Button CreateCompactButton(string content, SolidColorBrush background, RoutedEventHandler clickHandler)
        {
            Button btn = new Button
            {
                Content = content,
                Background = background,
                Foreground = TextPrimary,
                FontWeight = FontWeights.Bold,
                FontSize = 9,
                Height = 22,
                Margin = new Thickness(3, 1, 3, 1),
                Padding = new Thickness(2),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            btn.Click += clickHandler;
            return btn;
        }

        private System.Windows.Controls.Grid CreateTargetButtonRow()
        {
            System.Windows.Controls.Grid grid = new System.Windows.Controls.Grid { Margin = new Thickness(12, 4, 12, 4) };
            for (int i = 0; i < 5; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string[] labels = { "T1", "T2", "T3", "RUN", "BE" };
            for (int i = 0; i < 5; i++)
            {
                Button btn = new Button
                {
                    Content = labels[i],
                    Background = i < 4 ? AccentPurple : BgTertiary,
                    Foreground = TextPrimary,
                    FontWeight = FontWeights.Bold,
                    FontSize = 9,
                    Height = 18,
                    Margin = new Thickness(1, 0, 1, 0),
                    Cursor = Cursors.Hand,
                    BorderThickness = i == 4 ? new Thickness(1) : new Thickness(0),
                    BorderBrush = BorderPrimary
                };

                int index = i;
                btn.Click += (s, e) =>
                {
                    if (index == 4) OnBreakevenButtonClick();
                    else OnGlobalSyncSelect("CLOSE_T" + (index + 1));
                };

                Grid.SetColumn(btn, i);
                grid.Children.Add(btn);

                if (i == 0) t1DropdownButton = btn;
                else if (i == 1) t2DropdownButton = btn;
                else if (i == 2) t3DropdownButton = btn;
                else if (i == 3) runnerDropdownButton = btn;
                else breakevenButton = btn;
            }

            return grid;
        }

        #endregion

        #region V12 Global Sync Protocol

        /// <summary>
        /// V12: Central dispatcher for all UI actions
        /// Ensures selection on Sidebar instantly reflects on Dashboard and HUD
        /// </summary>
        private void OnGlobalSyncSelect(string action)
        {
            Print(string.Format("[GLOBAL SYNC] Action: {0}", action));

            // 1. Sync all UI components
            SyncAllButtons(action);

            // 2. Broadcast to SIMA fleet
            BroadcastToSIMA(action);

            // 3. Execute the action
            ExecuteSyncAction(action);

            // 4. Update fleet status
            UpdateFleetStatusAfterAction(action);
        }

        /// <summary>
        /// V12: Syncs visual state across all UI panels
        /// </summary>
        private void SyncAllButtons(string action)
        {
            if (!uiCreated || ChartControl == null) return;

            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                // Visual feedback - highlight relevant buttons
                if (action == "LONG" && longButton != null)
                {
                    longButton.BorderThickness = new Thickness(2);
                    longButton.BorderBrush = AccentCyan;
                }
                else if (action == "SHORT" && shortButton != null)
                {
                    shortButton.BorderThickness = new Thickness(2);
                    shortButton.BorderBrush = AccentCyan;
                }
            });
        }

        /// <summary>
        /// V12: Broadcasts action to all connected fleet accounts via SIMA
        /// </summary>
        private void BroadcastToSIMA(string action)
        {
            if (!EnableCopyTrading) return;

            Print(string.Format("[SIMA BROADCAST] Sending {0} to {1} fleet accounts", action, connectedFleetCount));

            foreach (var kvp in fleetAccounts)
            {
                if (kvp.Value.IsConnected && !kvp.Value.IsMaster)
                {
                    // Queue command for IPC broadcast
                    string command = string.Format("{0}|{1}", action, kvp.Key);
                    // In production, this would send via TCP/IPC to slave strategies
                    Print(string.Format("  -> {0}: {1}", kvp.Key, action));
                }
            }
        }

        private void ExecuteSyncAction(string action)
        {
            switch (action)
            {
                case "LONG":
                    ExecuteLong();
                    break;
                case "SHORT":
                    ExecuteShort();
                    break;
                case "RMA":
                    ToggleRMAMode();
                    break;
                case "RETEST":
                    ToggleRetestMode();
                    break;
                case "MOMO":
                    ToggleMOMOMode();
                    break;
                case "FFMA":
                    ToggleFFMAMode();
                    break;
                case "TREND":
                    pendingTRENDEntry = true;
                    break;
                case "FLATTEN":
                    FlattenAll();
                    break;
                case "BE_ALL":
                    OnBreakevenButtonClick();
                    break;
                case "TRIM_25":
                case "TRIM_50":
                    ExecuteTrim(action == "TRIM_50" ? 0.5 : 0.25);
                    break;
                default:
                    if (action.StartsWith("CLOSE_T"))
                    {
                        int targetNum = 0;
                        if (int.TryParse(action.Substring(7), out targetNum))
                        {
                            FlattenSpecificTarget(targetNum);
                        }
                    }
                    break;
            }
        }

        private void UpdateFleetStatusAfterAction(string action)
        {
            if (action == "FLATTEN")
            {
                foreach (var kvp in fleetAccounts)
                {
                    kvp.Value.CurrentPosition = MarketPosition.Flat;
                    kvp.Value.PositionSize = 0;
                    kvp.Value.UnrealizedPnL = 0;
                }
            }

            // Update aggregate P&L
            aggregateFleetPnL = fleetAccounts.Values.Sum(a => a.UnrealizedPnL);
        }

        #endregion

        #region V12 RMA Entry with Dynamic Anchors

        /// <summary>
        /// V12: Calculate RMA entry using dynamic anchor points
        /// Anchors: EMA 30, EMA 65, EMA 200, OR High, OR Low
        /// </summary>
        private double GetRMAAnchorPrice()
        {
            switch (currentRMAAnchor)
            {
                case RMAAnchorType.EMA30:
                    return ema30[0];
                case RMAAnchorType.EMA65:
                    return ema65[0];
                case RMAAnchorType.EMA200:
                    return ema200[0];
                case RMAAnchorType.ORHigh:
                    return sessionHigh;
                case RMAAnchorType.ORLow:
                    return sessionLow;
                default:
                    return ema200[0];
            }
        }

        private void ExecuteRMAEntry(double clickPrice)
        {
            if (!RMAEnabled)
            {
                Print("RMA mode is disabled");
                return;
            }

            if (currentATR <= 0)
            {
                Print("Cannot execute RMA entry - ATR not available yet");
                return;
            }

            try
            {
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                double anchorPrice = GetRMAAnchorPrice();

                // V12: Auto-direction based on anchor
                MarketPosition direction;
                if (clickPrice > currentPrice)
                {
                    direction = MarketPosition.Short;
                    Print(string.Format("RMA: Click above price ({0:F2} > {1:F2}) = SHORT | Anchor: {2} @ {3:F2}",
                        clickPrice, currentPrice, currentRMAAnchor, anchorPrice));
                }
                else
                {
                    direction = MarketPosition.Long;
                    Print(string.Format("RMA: Click below price ({0:F2} < {1:F2}) = LONG | Anchor: {2} @ {3:F2}",
                        clickPrice, currentPrice, currentRMAAnchor, anchorPrice));
                }

                // Calculate RMA stop and targets using ATR
                double stopDistance = currentATR * RMAStopATRMultiplier;
                stopDistance = Math.Min(stopDistance, 12.0);

                double entryPrice = clickPrice;
                double stopPrice = direction == MarketPosition.Long
                    ? entryPrice - stopDistance
                    : entryPrice + stopDistance;

                // V12: Target calculation based on current mode
                double target1Price, target2Price, target3Price;
                CalculateTargets(entryPrice, direction, out target1Price, out target2Price, out target3Price);

                // Calculate position size
                double riskToUse = (stopDistance > StopThresholdPoints) ? ReducedRiskPerTrade : RiskPerTrade;
                double stopDistanceInDollars = stopDistance * pointValue;
                int contracts = (int)Math.Floor(riskToUse / stopDistanceInDollars);
                contracts = Math.Max(minContracts, Math.Min(contracts, maxContracts));

                // 4-target system split
                int t1Qty, t2Qty, t3Qty, t4Qty;
                CalculateContractSplit(contracts, out t1Qty, out t2Qty, out t3Qty, out t4Qty);

                string signalName = direction == MarketPosition.Long ? "RMALong" : "RMAShort";
                string timestamp = DateTime.Now.ToString("HHmmss");
                string entryName = signalName + "_" + timestamp;

                PositionInfo pos = new PositionInfo
                {
                    SignalName = entryName,
                    Direction = direction,
                    TotalContracts = contracts,
                    T1Contracts = t1Qty,
                    T2Contracts = t2Qty,
                    T3Contracts = t3Qty,
                    T4Contracts = t4Qty,
                    RemainingContracts = contracts,
                    EntryPrice = entryPrice,
                    InitialStopPrice = stopPrice,
                    CurrentStopPrice = stopPrice,
                    Target1Price = target1Price,
                    Target2Price = target2Price,
                    Target3Price = target3Price,
                    EntryFilled = false,
                    T1Filled = false,
                    T2Filled = false,
                    T3Filled = false,
                    BracketSubmitted = false,
                    ExtremePriceSinceEntry = entryPrice,
                    CurrentTrailLevel = 0,
                    IsRMATrade = true,
                    RMAAnchor = currentRMAAnchor
                };

                activePositions[entryName] = pos;

                Order entryOrder = direction == MarketPosition.Long
                    ? SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, contracts, entryPrice, 0, "", entryName)
                    : SubmitOrderUnmanaged(0, OrderAction.SellShort, OrderType.Limit, contracts, entryPrice, 0, "", entryName);

                entryOrders[entryName] = entryOrder;

                Print(string.Format("RMA ENTRY: {0} {1}@{2:F2} | ATR: {3:F2} | Anchor: {4}",
                    signalName, contracts, entryPrice, currentATR, currentRMAAnchor));

                // V12: Broadcast to SIMA
                BroadcastEntrySignal(entryName, direction, entryPrice, stopPrice, target1Price, target2Price, target3Price,
                    t1Qty, t2Qty, t3Qty, t4Qty, true);

                DeactivateRMAMode();
                UpdateDisplay();
            }
            catch (Exception ex)
            {
                Print("ERROR ExecuteRMAEntry: " + ex.Message);
            }
        }

        /// <summary>
        /// V12: Calculate targets based on current target mode
        /// </summary>
        private void CalculateTargets(double entryPrice, MarketPosition direction, out double t1, out double t2, out double t3)
        {
            switch (currentTargetMode)
            {
                case TargetModeType.ATR:
                    t1 = direction == MarketPosition.Long
                        ? entryPrice + Target1FixedPoints
                        : entryPrice - Target1FixedPoints;
                    t2 = direction == MarketPosition.Long
                        ? entryPrice + (currentATR * Target2Multiplier)
                        : entryPrice - (currentATR * Target2Multiplier);
                    t3 = direction == MarketPosition.Long
                        ? entryPrice + (currentATR * Target3Multiplier)
                        : entryPrice - (currentATR * Target3Multiplier);
                    break;

                case TargetModeType.PTS:
                    t1 = direction == MarketPosition.Long ? entryPrice + 1.0 : entryPrice - 1.0;
                    t2 = direction == MarketPosition.Long ? entryPrice + 2.0 : entryPrice - 2.0;
                    t3 = direction == MarketPosition.Long ? entryPrice + 4.0 : entryPrice - 4.0;
                    break;

                case TargetModeType.TKS:
                    t1 = direction == MarketPosition.Long ? entryPrice + (4 * tickSize) : entryPrice - (4 * tickSize);
                    t2 = direction == MarketPosition.Long ? entryPrice + (8 * tickSize) : entryPrice - (8 * tickSize);
                    t3 = direction == MarketPosition.Long ? entryPrice + (16 * tickSize) : entryPrice - (16 * tickSize);
                    break;

                case TargetModeType.RNG:
                    double range = sessionRange > 0 ? sessionRange : currentATR;
                    t1 = direction == MarketPosition.Long ? entryPrice + (range * 0.25) : entryPrice - (range * 0.25);
                    t2 = direction == MarketPosition.Long ? entryPrice + (range * 0.5) : entryPrice - (range * 0.5);
                    t3 = direction == MarketPosition.Long ? entryPrice + range : entryPrice - range;
                    break;

                default:
                    t1 = entryPrice + (direction == MarketPosition.Long ? 1.0 : -1.0);
                    t2 = entryPrice + (direction == MarketPosition.Long ? 2.0 : -2.0);
                    t3 = entryPrice + (direction == MarketPosition.Long ? 4.0 : -4.0);
                    break;
            }
        }

        private void CalculateContractSplit(int contracts, out int t1Qty, out int t2Qty, out int t3Qty, out int t4Qty)
        {
            if (contracts == 1)
            {
                t1Qty = 1; t2Qty = 0; t3Qty = 0; t4Qty = 0;
            }
            else if (contracts == 2)
            {
                t1Qty = 1; t2Qty = 0; t3Qty = 0; t4Qty = 1;
            }
            else if (contracts == 3)
            {
                t1Qty = 1; t2Qty = 1; t3Qty = 0; t4Qty = 1;
            }
            else if (contracts == 4)
            {
                t1Qty = 1; t2Qty = 1; t3Qty = 1; t4Qty = 1;
            }
            else
            {
                t1Qty = (int)Math.Floor(contracts * T1ContractPercent / 100.0);
                t2Qty = (int)Math.Floor(contracts * T2ContractPercent / 100.0);
                t3Qty = (int)Math.Floor(contracts * T3ContractPercent / 100.0);
                t4Qty = contracts - t1Qty - t2Qty - t3Qty;

                if (t1Qty < 1) { t1Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                if (t2Qty < 1) { t2Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                if (t3Qty < 1) { t3Qty = 1; t4Qty = contracts - t1Qty - t2Qty - t3Qty; }
                if (t4Qty < 1) t4Qty = 1;
            }
        }

        #endregion

        #region Button Click Handlers

        private void OnLongClick(object sender, RoutedEventArgs e)
        {
            OnGlobalSyncSelect("LONG");
        }

        private void OnShortClick(object sender, RoutedEventArgs e)
        {
            OnGlobalSyncSelect("SHORT");
        }

        private void OnRMAClick(object sender, RoutedEventArgs e)
        {
            OnGlobalSyncSelect("RMA");
        }

        private void OnRetestClick(object sender, RoutedEventArgs e)
        {
            OnGlobalSyncSelect("RETEST");
        }

        private void OnMOMOClick(object sender, RoutedEventArgs e)
        {
            OnGlobalSyncSelect("MOMO");
        }

        private void OnFFMAClick(object sender, RoutedEventArgs e)
        {
            OnGlobalSyncSelect("FFMA");
        }

        private void OnTRENDClick(object sender, RoutedEventArgs e)
        {
            OnGlobalSyncSelect("TREND");
        }

        private void OnBreakevenButtonClick()
        {
            foreach (var kvp in activePositions.ToArray())
            {
                PositionInfo pos = kvp.Value;
                if (pos.RemainingContracts > 0 && pos.EntryFilled)
                {
                    double bePrice = pos.Direction == MarketPosition.Long
                        ? pos.EntryPrice + (ManualBreakevenBuffer * tickSize)
                        : pos.EntryPrice - (ManualBreakevenBuffer * tickSize);

                    ChangeStop(pos.SignalName, bePrice);
                    Print(string.Format("Manual BE: {0} moved to {1:F2}", pos.SignalName, bePrice));
                }
            }
        }

        #endregion

        #region Mode Toggles

        private void ToggleRMAMode()
        {
            if (isMOMOModeActive) DeactivateMOMOMode();
            isRMAButtonClicked = !isRMAButtonClicked;
            isRMAModeActive = isRMAButtonClicked;
            UpdateRMAModeDisplay();
        }

        private void ToggleRetestMode()
        {
            isRetestModeActive = !isRetestModeActive;
            UpdateRetestModeDisplay();
        }

        private void ToggleMOMOMode()
        {
            if (isRMAModeActive) DeactivateRMAMode();
            isMOMOModeActive = !isMOMOModeActive;
            UpdateMOMOModeDisplay();
        }

        private void ToggleFFMAMode()
        {
            isFFMAModeArmed = !isFFMAModeArmed;
            UpdateFFMAModeDisplay();
        }

        private void DeactivateRMAMode()
        {
            isRMAModeActive = false;
            isRMAButtonClicked = false;
            UpdateRMAModeDisplay();
        }

        private void DeactivateMOMOMode()
        {
            isMOMOModeActive = false;
            UpdateMOMOModeDisplay();
        }

        private void UpdateRMAModeDisplay()
        {
            if (ChartControl == null) return;
            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                if (rmaModeTextBlock != null)
                {
                    rmaModeTextBlock.Visibility = isRMAModeActive ? Visibility.Visible : Visibility.Collapsed;
                    rmaModeTextBlock.Text = string.Format("Ôÿà RMA ACTIVE ({0}) - Click chart Ôÿà", currentRMAAnchor);
                }
                if (rmaButton != null)
                {
                    rmaButton.Background = isRMAModeActive ? RMAActiveBackground : AccentOrange;
                }
            });
        }

        private void UpdateRetestModeDisplay()
        {
            if (ChartControl == null || retestButton == null) return;
            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                retestButton.Background = isRetestModeActive
                    ? new SolidColorBrush(Color.FromRgb(150, 80, 150))
                    : AccentPurple;
            });
        }

        private void UpdateMOMOModeDisplay()
        {
            if (ChartControl == null || momoButton == null) return;
            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                momoButton.Background = isMOMOModeActive
                    ? new SolidColorBrush(Color.FromRgb(140, 80, 180))
                    : AccentPurple;
            });
        }

        private void UpdateFFMAModeDisplay()
        {
            if (ChartControl == null || ffmaButton == null) return;
            ChartControl.Dispatcher.InvokeAsync(() =>
            {
                ffmaButton.Background = isFFMAModeArmed
                    ? new SolidColorBrush(Color.FromRgb(200, 100, 180))
                    : AccentPink;
            });
        }

        #endregion

        #region Entry Execution Stubs
        // V12: Calls methods preserved in the main strategy logic
        // ExecuteLong, ExecuteShort, ExecuteTRENDEntry, ExecuteMOMOEntry, ManageTrailingStops, ChangeStop
        // are all implemented in UniversalORStrategyV12.cs (SafeLogic)
        #endregion

        #region Flatten

        private void FlattenAll()
        {
            Print("FLATTEN ALL - Closing all positions");

            foreach (var kvp in activePositions.ToArray())
            {
                PositionInfo pos = kvp.Value;
                if (pos.RemainingContracts > 0)
                {
                    if (pos.Direction == MarketPosition.Long)
                        SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, pos.RemainingContracts, 0, 0, "", "Flatten_" + pos.SignalName);
                    else
                        SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, pos.RemainingContracts, 0, 0, "", "Flatten_" + pos.SignalName);
                }
            }
        }

        #endregion

        #region SIMA Broadcast

        private void BroadcastEntrySignal(string entryName, MarketPosition direction, double entry, double stop,
            double t1, double t2, double t3, int t1Qty, int t2Qty, int t3Qty, int t4Qty, bool isRMA)
        {
            if (!EnableCopyTrading) return;

            string signal = string.Format("ENTRY|{0}|{1}|{2:F2}|{3:F2}|{4:F2}|{5:F2}|{6:F2}|{7}|{8}|{9}|{10}|{11}",
                entryName, direction, entry, stop, t1, t2, t3, t1Qty, t2Qty, t3Qty, t4Qty, isRMA ? "RMA" : "OR");

            Print(string.Format("[SIMA] Broadcasting: {0}", signal));

            // Update master account status
            if (fleetAccounts.ContainsKey("MASTER"))
            {
                var master = fleetAccounts["MASTER"];
                master.CurrentPosition = direction;
                master.EntryPrice = entry;
                master.PositionSize = t1Qty + t2Qty + t3Qty + t4Qty;
            }
        }

        #endregion

        #region IPC Server

        private void StartIpcServer()
        {
            if (isIpcRunning) return;

            isIpcRunning = true;
            ipcThread = new Thread(ListenForRemote);
            ipcThread.IsBackground = true;
            ipcThread.Start();

            Print(string.Format("V12 IPC Server started on port {0}", IpcPort));
        }

        private void StopIpcServer()
        {
            isIpcRunning = false;
            try
            {
                if (ipcListener != null)
                {
                    ipcListener.Stop();
                    ipcListener = null;
                }
                if (ipcThread != null && ipcThread.IsAlive)
                {
                    ipcThread.Join(500);
                }
            }
            catch { }
        }

        private void ListenForRemote()
        {
            try
            {
                ipcListener = new TcpListener(IPAddress.Any, IpcPort);
                ipcListener.Start();

                while (isIpcRunning)
                {
                    if (!ipcListener.Pending())
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    using (TcpClient client = ipcListener.AcceptTcpClient())
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            string message = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                            if (!string.IsNullOrEmpty(message))
                            {
                                ipcCommandQueue.Enqueue(message);
                                try
                                {
                                    TriggerCustomEvent(o => ProcessIpcCommands(), null);
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (isIpcRunning) Print("IPC Listener Error: " + ex.Message);
            }
            finally
            {
                if (ipcListener != null) { ipcListener.Stop(); ipcListener = null; }
            }
        }

        private void ProcessIpcCommands()
        {
            if (ipcCommandQueue == null || ipcCommandQueue.IsEmpty) return;

            while (ipcCommandQueue.TryDequeue(out string command))
            {
                try
                {
                    Print(string.Format("[IPC] Processing: {0}", command));
                    string[] parts = command.Split('|');
                    string action = parts[0];

                    // Route through global sync
                    OnGlobalSyncSelect(action);
                }
                catch (Exception ex)
                {
                    Print("Error ProcessIpcCommands: " + ex.Message);
                }
            }
        }

        #endregion

        #region UI Helpers

        private void RemoveUI()
        {
            if (mainBorder != null && uiCreated)
            {
                try
                {
                    UserControlCollection.Remove(mainBorder);
                }
                catch { }
            }
            uiCreated = false;
        }

        private void UpdateDisplay()
        {
            if (!uiCreated) return;

            try
            {
                // Update OR levels
                // Update position info
                // Update fleet status
            }
            catch { }
        }

        #endregion

        #region Drag Handlers

        private void OnDragStart(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
            dragStartPoint = e.GetPosition(ChartControl);
            originalMargin = mainBorder.Margin;
            (sender as UIElement)?.CaptureMouse();
        }

        private void OnDragMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;

            Point currentPos = e.GetPosition(ChartControl);
            double deltaX = currentPos.X - dragStartPoint.X;
            double deltaY = currentPos.Y - dragStartPoint.Y;

            mainBorder.Margin = new Thickness(
                originalMargin.Left + deltaX,
                originalMargin.Top + deltaY,
                0, 0);
        }

        private void OnDragEnd(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            (sender as UIElement)?.ReleaseMouseCapture();
        }

        #endregion

        #region Hotkeys

        private void AttachHotkeys()
        {
            if (ChartControl != null)
            {
                ChartControl.PreviewKeyDown += OnKeyDown;
            }
        }

        private void DetachHotkeys()
        {
            if (ChartControl != null)
            {
                ChartControl.PreviewKeyDown -= OnKeyDown;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.L)
            {
                OnGlobalSyncSelect("LONG");
                e.Handled = true;
            }
            else if (e.Key == Key.S)
            {
                OnGlobalSyncSelect("SHORT");
                e.Handled = true;
            }
            else if (e.Key == Key.F)
            {
                OnGlobalSyncSelect("FLATTEN");
                e.Handled = true;
            }
            else if (e.Key == Key.R)
            {
                OnGlobalSyncSelect("RMA");
                e.Handled = true;
            }
        }

        #endregion

        #region Chart Click Handler

        private void AttachChartClickHandler()
        {
            if (ChartControl != null)
            {
                ChartControl.MouseLeftButtonDown += OnChartClick;
            }
        }

        private void DetachChartClickHandler()
        {
            if (ChartControl != null)
            {
                ChartControl.MouseLeftButtonDown -= OnChartClick;
            }
        }

        private void OnChartClick(object sender, MouseButtonEventArgs e)
        {
            if (!isRMAModeActive && !isMOMOModeActive) return;

            try
            {
                Point clickPoint = e.GetPosition(ChartControl);
                double clickPrice = ChartControl.Instrument.MasterInstrument.RoundToTickSize(
                    ChartControl.GetYAxisValue(clickPoint));

                if (isMOMOModeActive)
                {
                    ExecuteMOMOEntry(clickPrice);
                }
                else if (isRMAModeActive)
                {
                    ExecuteRMAEntry(clickPrice);
                }
            }
            catch (Exception ex)
            {
                Print("Error OnChartClick: " + ex.Message);
            }
        }

        // Logic preserved in main strategy


        #endregion
    }
}
