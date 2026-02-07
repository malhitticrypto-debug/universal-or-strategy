// V12 PRO MASTER Side Panel - NinjaTrader 8 Chart Trader PRECISION INJECTION
// TRINITY Design Authority Blueprint - 42+ Interactive Elements
// Hybrid Toggle Mode: Replaces native Chart Trader, toggle ⚓ to restore
//
// Version: V12.5 PRO MASTER - PRECISION CHART TRADER INJECTION
// Author: Claude Code + SIMA Architecture
//
// INJECTION STRATEGY:
// 1. ChartControl → ChartTab → ChartTrader (via reflection)
// 2. Visual tree type name matching for ChartTrader control
// 3. Fallback: Border detection with button text matching
//
// This indicator INJECTS into the native Chart Trader panel,
// replacing the Buy/Sell buttons with the V12 PRO MASTER interface.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Indicators
{
    public class V12SidePanelIndicator : Indicator
    {
        #region Variables

        // Injection state
        private bool injected = false;
        private Grid chartTraderGrid;
        private Border chartTraderBorder;
        private UIElement nativeContent;
        private ScrollViewer v12ScrollViewer;
        private StackPanel v12MainStack;
        private bool showingV12Panel = true;

        // TCP Connection
        private TcpClient tcpClient;
        private NetworkStream tcpStream;
        private readonly object tcpLock = new object();
        private Thread receiveThread;
        private volatile bool isConnected = false;
        private ConcurrentQueue<string> responseQueue = new ConcurrentQueue<string>();

        // Symbol State
        private string activeSymbol = "MES";
        private double lastPrice = 0;

        // Mode Flags
        private bool isRetestRmaToggle = false;
        private bool isTrendRmaToggle = false;
        private bool isGhostMode = false;

        // Selected Target Count (3 default)
        private int selectedTargetCount = 3;

        // Selected Config Mode
        private string selectedConfigMode = "ORB";

        // UI Components - Section 0: Identity
        private Border hubStatusLed;
        private TextBlock titleLabel;
        private ComboBox instrumentCombo;
        private ComboBox directionCombo;
        private TextBox priceInput;
        private Button submitButton;
        private ComboBox accountCombo;
        private Button fleetButton;
        private CheckBox ghostModeCheck;
        private Button anchorToggleButton;

        // UI Components - Section 1: Execution
        private Button orLongButton, orShortButton;
        private Button retestButton, retestRmaToggle;
        private Button rmaButton;
        private Button momoButton, ffmaButton;
        private Button trendButton, trendRmaToggle;
        private Button t1Button, t2Button, t3Button, t4Button, t5Button;
        private Button trim25Button, trim50Button, beButton, tr1Button, tr2Button;
        private Button flattenButton;
        private TextBlock lastPriceText;

        // UI Components - Section 2: Telemetry
        private TextBlock or5Text, or15Text;
        private TextBlock ema9Text, ema15Text, ema30Text, ema65Text, ema200Text;
        private TextBlock atrText;
        private Button mktSyncButton;
        private Border trendIndicator;
        private TextBlock trendText;

        // UI Components - Section 3: Config
        private Button modeOrbButton, modeRmaButton, modeRetestButton;
        private Button modeMomoButton, modeFfmaButton, modeTrendButton;
        private Button cnt1, cnt2, cnt3, cnt4, cnt5;
        private TextBox svT1Val, svT2Val, svT3Val;
        private ComboBox svT1Type, svT2Type, svT3Type;
        private TextBox glbVal, strVal, maxVal;
        private Button syncAllButton;

        // Glow effect
        private DispatcherTimer glowTimer;
        private Border glowOverlay;

        #endregion

        #region V12 Color Palette (Frozen Brushes)

        private static readonly SolidColorBrush BgDeep;
        private static readonly SolidColorBrush BgSlate;
        private static readonly SolidColorBrush BorderSlate;
        private static readonly SolidColorBrush BtnBg;
        private static readonly SolidColorBrush BtnBorder;
        private static readonly SolidColorBrush TextPrimary;
        private static readonly SolidColorBrush TextMuted;
        private static readonly SolidColorBrush CyanAccent;
        private static readonly SolidColorBrush GreenBg, GreenFg, GreenBorder;
        private static readonly SolidColorBrush RedBg, RedFg, RedBorder;
        private static readonly SolidColorBrush OrangeBg, OrangeFg, OrangeBorder;
        private static readonly SolidColorBrush YellowBg, YellowFg, YellowBorder;
        private static readonly SolidColorBrush PinkBg, PinkFg, PinkBorder;
        private static readonly SolidColorBrush CyanBg, CyanFg, CyanBorder;
        private static readonly SolidColorBrush PurpleFg;

        static V12SidePanelIndicator()
        {
            BgDeep = Freeze(new SolidColorBrush(Color.FromRgb(5, 5, 5)));
            BgSlate = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
            BorderSlate = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
            BtnBg = Freeze(new SolidColorBrush(Color.FromRgb(23, 23, 23)));
            BtnBorder = Freeze(new SolidColorBrush(Color.FromRgb(38, 38, 38)));
            TextPrimary = Freeze(new SolidColorBrush(Color.FromRgb(212, 212, 212)));
            TextMuted = Freeze(new SolidColorBrush(Color.FromRgb(115, 115, 115)));
            CyanAccent = Freeze(new SolidColorBrush(Color.FromRgb(34, 211, 238)));

            GreenBg = Freeze(new SolidColorBrush(Color.FromRgb(6, 78, 59)));
            GreenFg = Freeze(new SolidColorBrush(Color.FromRgb(74, 222, 128)));
            GreenBorder = Freeze(new SolidColorBrush(Color.FromRgb(5, 150, 105)));

            RedBg = Freeze(new SolidColorBrush(Color.FromRgb(127, 29, 29)));
            RedFg = Freeze(new SolidColorBrush(Color.FromRgb(252, 165, 165)));
            RedBorder = Freeze(new SolidColorBrush(Color.FromRgb(220, 38, 38)));

            OrangeBg = Freeze(new SolidColorBrush(Color.FromRgb(124, 45, 18)));
            OrangeFg = Freeze(new SolidColorBrush(Color.FromRgb(251, 146, 60)));
            OrangeBorder = Freeze(new SolidColorBrush(Color.FromRgb(234, 88, 12)));

            YellowBg = Freeze(new SolidColorBrush(Color.FromRgb(66, 32, 6)));
            YellowFg = Freeze(new SolidColorBrush(Color.FromRgb(251, 191, 36)));
            YellowBorder = Freeze(new SolidColorBrush(Color.FromRgb(217, 119, 6)));

            PinkBg = Freeze(new SolidColorBrush(Color.FromRgb(74, 4, 78)));
            PinkFg = Freeze(new SolidColorBrush(Color.FromRgb(244, 114, 182)));
            PinkBorder = Freeze(new SolidColorBrush(Color.FromRgb(162, 28, 175)));

            CyanBg = Freeze(new SolidColorBrush(Color.FromRgb(22, 78, 99)));
            CyanFg = Freeze(new SolidColorBrush(Color.FromRgb(34, 211, 238)));
            CyanBorder = Freeze(new SolidColorBrush(Color.FromRgb(8, 145, 178)));

            PurpleFg = Freeze(new SolidColorBrush(Color.FromRgb(168, 85, 247)));
        }

        private static SolidColorBrush Freeze(SolidColorBrush brush) { brush.Freeze(); return brush; }

        #endregion

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "IPC Port", Description = "TCP Port to connect to V12 Strategy", Order = 1, GroupName = "Connection")]
        public int IpcPort { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Auto Connect", Description = "Automatically connect to IPC on load", Order = 2, GroupName = "Connection")]
        public bool AutoConnect { get; set; }

        #endregion

        #region OnStateChange

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "V12 PRO MASTER Side Panel - Precision Chart Trader Injection";
                Name = "V12 PRO MASTER Panel";
                Calculate = Calculate.OnPriceChange;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = false;
                IsSuspendedWhileInactive = false;

                IpcPort = 5000;
                AutoConnect = true;
            }
            else if (State == State.Historical)
            {
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(InjectIntoChartTrader);
                }
            }
            else if (State == State.Realtime)
            {
                activeSymbol = Instrument.MasterInstrument.Name;
                if (AutoConnect) Task.Run(() => ConnectToStrategy());
            }
            else if (State == State.Terminated)
            {
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(RestoreChartTrader);
                }
                DisconnectFromStrategy();
            }
        }

        #endregion

        #region OnBarUpdate / OnMarketData

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1) return;
            lastPrice = Close[0];

            while (responseQueue.TryDequeue(out string response))
                ProcessStrategyResponse(response);
        }

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            if (e.MarketDataType == MarketDataType.Last)
            {
                lastPrice = e.Price;
                if (ChartControl != null && lastPriceText != null)
                {
                    ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        lastPriceText.Text = lastPrice.ToString("F2");
                    }));
                }
            }
        }

        #endregion

        #region Chart Trader Injection

        // V12 PRO MASTER - Precision Chart Trader Injection
        // Uses ChartControl → ChartTab → ChartTrader hierarchy for reliable injection

        private object chartTraderControl;  // Reference to the ChartTrader UserControl
        private PropertyInfo contentProperty;  // Cached Content property for restore

        private void InjectIntoChartTrader()
        {
            if (injected || ChartControl == null) return;

            try
            {
                // STRATEGY 0: Surgical Strike - Find "Buy Mkt" and grab its container
                // This prevents grabbing the whole window
                object found = FindChartTraderAggressive();
                
                if (found != null)
                {
                    if (found is Grid grid)
                    {
                        InjectIntoGrid(grid);
                        chartTraderControl = found as FrameworkElement;
                        return;
                    }
                    
                    if (found is Border border && border.Child is Grid childGrid)
                    {
                        InjectIntoGrid(childGrid);
                        chartTraderControl = border;
                        return;
                    }
                }

                // STRATEGY 1: Fallback Sibling Search
                object chartTrader = FindChartTraderBySiblingSearch();

                if (chartTrader == null)
                {
                    // STRATEGY 2: Content Area Search
                    chartTraderBorder = FindChartTraderContentArea();

                    if (chartTraderBorder != null && chartTraderBorder.Child is Grid bgGrid)
                    {
                        InjectIntoGrid(bgGrid);
                        return;
                    }

                    Print("V12 PRO MASTER: Auto-injection failed. Using fallback overlay.");
                    CreateFallbackOverlay();
                    return;
                }

                chartTraderControl = chartTrader;

                // STRATEGY 3: Reflection on Content property
                contentProperty = chartTrader.GetType().GetProperty("Content", BindingFlags.Public | BindingFlags.Instance);

                if (contentProperty != null)
                {
                    object content = contentProperty.GetValue(chartTrader);
                    if (content is Grid contentGrid)
                    {
                        InjectIntoGrid(contentGrid);
                        return;
                    }
                }
                
                // Final Fallback
                CreateFallbackOverlay();
            }
            catch (Exception ex)
            {
                Print("V12 PRO Injection Error: " + ex.Message);
                CreateFallbackOverlay();
            }
        }

        private object FindChartTraderAggressive()
        {
            try
            {
                DependencyObject parent = ChartControl;
                while (VisualTreeHelper.GetParent(parent) != null)
                    parent = VisualTreeHelper.GetParent(parent);

                // STRATEGY 0: Surgical Strike - Find "Buy Mkt" and grab its container
                // This prevents grabbing the whole window
                return FindContainerByButtonText(parent, "Buy Mkt");
            }
            catch { return null; }
        }

        /// <summary>
        /// Strategy 1: Traverse up from ChartControl to find ChartTab, then locate ChartTrader
        /// </summary>
        private object FindChartTraderFromChartControl()
        {
            try
            {
                DependencyObject current = ChartControl;
                object chartTab = null;

                while (current != null)
                {
                    string typeName = current.GetType().Name;
                    if (typeName == "ChartTab" || typeName.Contains("ChartTab"))
                    {
                        chartTab = current;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                if (chartTab == null)
                {
                    current = ChartControl;
                    while (current != null)
                    {
                        string typeName = current.GetType().Name;
                        if (typeName == "ChartTab" || typeName.Contains("ChartTab"))
                        {
                            chartTab = current;
                            break;
                        }
                        current = LogicalTreeHelper.GetParent(current);
                    }
                }

                if (chartTab == null) return null;

                Type chartTabType = chartTab.GetType();
                PropertyInfo chartTraderProp = chartTabType.GetProperty("ChartTrader",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (chartTraderProp != null) return chartTraderProp.GetValue(chartTab);

                FieldInfo chartTraderField = chartTabType.GetField("chartTrader", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? chartTabType.GetField("ChartTrader", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? chartTabType.GetField("chartTraderControl", BindingFlags.NonPublic | BindingFlags.Instance);

                if (chartTraderField != null) return chartTraderField.GetValue(chartTab);

                if (chartTab is DependencyObject depObj)
                    return FindChildByTypeName(depObj, "ChartTrader");
            }
            catch (Exception ex)
            {
                Print("V12 PRO MASTER: ChartTab traversal error - " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Strategy 2: Sibling Search - Look for ChartTrader peer in the parent Grid
        /// </summary>
        private object FindChartTraderBySiblingSearch()
        {
            try
            {
                DependencyObject current = ChartControl;
                while (current != null && !(current is Grid))
                {
                    current = VisualTreeHelper.GetParent(current);
                }

                if (current is Grid grid)
                {
                    foreach (UIElement child in grid.Children)
                    {
                        if (child.GetType().Name.Contains("ChartTrader"))
                            return child;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Strategy 2: Search visual tree for control with "ChartTrader" in type name
        /// </summary>
        private object FindChartTraderByTypeName()
        {
            try
            {
                Window chartWindow = Window.GetWindow(ChartControl);
                if (chartWindow == null) return null;

                return FindChildByTypeName(chartWindow, "ChartTrader");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Strategy 0: Bottom-Up "Surgical Strike"
        /// Find the specific "Buy Mkt" button, then grab its parent container.
        /// This ensures we only replace the sidebar, not the whole window.
        /// </summary>
        private object FindContainerByButtonText(DependencyObject parent, string buttonText)
        {
            if (parent == null) return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                // Check if this is the target button
                if (child is Button btn)
                {
                    string content = btn.Content?.ToString() ?? "";
                    if (content.IndexOf(buttonText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Found the button! Return its PARENT (or Grandparent)
                        // The button is usually in a Grid or StackPanel, which is in the ChartTrader View
                        DependencyObject container = VisualTreeHelper.GetParent(child);
                        
                        // Walk up until we find a Border or Grid that is likely the main container
                        // But don't go too high (stop before ChartControl or Window)
                        while (container != null && container != ChartControl && !(container is Window))
                        {
                             if (container.GetType().Name.Contains("ChartTrader")) return container;
                             if (container is Border b && b.Child is Grid) return container; // Common NT8 Pattern
                             
                             container = VisualTreeHelper.GetParent(container);
                        }
                        
                        // If we didn't find a named container, just return the button's immediate parent
                        return VisualTreeHelper.GetParent(child);
                    }
                }

                // Recurse
                var result = FindContainerByButtonText(child, buttonText);
                if (result != null) return result;
            }

            return null;
        }

        /// <summary>
        /// Helper: Find a child element by type name
        /// </summary>
        private object FindChildByTypeName(DependencyObject parent, string typeName)
        {
            if (parent == null) return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                string childTypeName = child.GetType().Name;

                if (childTypeName.Contains(typeName)) return child;

                var result = FindChildByTypeName(child, typeName);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Strategy 3: Find the Chart Trader content area by looking for the button structure
        /// </summary>
        private Border FindChartTraderContentArea()
        {
            try
            {
                Window chartWindow = Window.GetWindow(ChartControl);
                if (chartWindow == null) return null;

                return FindChartTraderBorderRecursive(chartWindow);
            }
            catch
            {
                return null;
            }
        }

        private Border FindChartTraderBorderRecursive(DependencyObject parent)
        {
            if (parent == null) return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                // Check if this is a ChartTrader type
                string typeName = child.GetType().Name;
                if (typeName.Contains("ChartTrader") && child is FrameworkElement fe)
                {
                    // Found ChartTrader - look for its main Border/ContentPresenter
                    Border border = FindFirstBorderChild(fe);
                    if (border != null) return border;
                }

                // Check for Border with Chart Trader buttons
                if (child is Border border2 && IsChartTraderContainer(border2))
                {
                    return border2;
                }

                // Recurse into children
                var result = FindChartTraderBorderRecursive(child);
                if (result != null) return result;
            }

            return null;
        }

        private Border FindFirstBorderChild(DependencyObject parent)
        {
            if (parent == null) return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Border border && border.Child != null)
                {
                    return border;
                }

                // Check one level deeper for ContentPresenter -> Border pattern
                if (child is ContentPresenter cp)
                {
                    int cpChildCount = VisualTreeHelper.GetChildrenCount(cp);
                    for (int j = 0; j < cpChildCount; j++)
                    {
                        var cpChild = VisualTreeHelper.GetChild(cp, j);
                        if (cpChild is Border b && b.Child != null)
                            return b;
                    }
                }
            }

            return null;
        }

        private void InjectIntoGrid(Grid targetGrid)
        {
            // Find which row contains the buttons
            int buttonRow = -1;
            int buttonColumn = 0;
            int buttonColumnSpan = 1;

            foreach (UIElement child in targetGrid.Children)
            {
                if (HasChartTraderButtons(child))
                {
                    buttonRow = Grid.GetRow(child);
                    buttonColumn = Grid.GetColumn(child);
                    buttonColumnSpan = Grid.GetColumnSpan(child);
                    break;
                }
            }

            if (buttonRow == -1)
            {
                // Fallback: If we can't find the specific row, try to inject into the last row or create an overlay
                buttonRow = targetGrid.RowDefinitions.Count > 0 ? targetGrid.RowDefinitions.Count - 1 : 0;
            }

            Print($"V12 PRO MASTER: Injecting into Grid Row {buttonRow}");

            // Store native content of that row (we only hide the buttons, not the header)
            // We need to find all children in that row and hide them
            nativeRowChildren = new List<UIElement>();
            foreach (UIElement child in targetGrid.Children)
            {
                if (Grid.GetRow(child) == buttonRow)
                {
                    nativeRowChildren.Add(child);
                    child.Visibility = Visibility.Collapsed;
                }
            }

            // Create V12 Panel
            v12ScrollViewer = CreateV12Panel();
            
            // Check if we need to add a row definition if the grid is empty (unlikely for ChartTrader)
            if (targetGrid.RowDefinitions.Count == 0)
                targetGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // INJECT: Add V12 Panel to Row 0 (Header Row) to guarantee it stays open
            // We use Margin to push it down below the header content
            Grid.SetRow(v12ScrollViewer, 0); 
            
            // Span all rows to ensure we have vertical space
            int totalRows = targetGrid.RowDefinitions.Count;
            if (totalRows == 0) totalRows = 1;
            Grid.SetRowSpan(v12ScrollViewer, Math.Max(5, totalRows)); // Span generously

            Grid.SetColumn(v12ScrollViewer, 0);
            int totalColumns = targetGrid.ColumnDefinitions.Count;
            if (totalColumns == 0) totalColumns = 1;
            Grid.SetColumnSpan(v12ScrollViewer, totalColumns);
            
            // Alignment & Margin
            v12ScrollViewer.VerticalAlignment = VerticalAlignment.Top;
            v12ScrollViewer.HorizontalAlignment = HorizontalAlignment.Left; // Stick to left of sidebar
            v12ScrollViewer.Width = 230; // Slightly wider than 220 to fill standard width
            // Key Fix: Push down by 30px to uncover the Account Selector Header
            v12ScrollViewer.Margin = new Thickness(0, 30, 0, 0); 

            targetGrid.Children.Add(v12ScrollViewer);

            injected = true;
            showingV12Panel = true;
            this.targetGrid = targetGrid; // Store for restoration
        }

        // Field to store target grid for restoration
        private Grid targetGrid;
        private List<UIElement> nativeRowChildren;

        private void RestoreChartTrader()
        {
            try
            {
                if (targetGrid != null && v12ScrollViewer != null)
                {
                    // Remove V12 Panel
                    if (targetGrid.Children.Contains(v12ScrollViewer))
                        targetGrid.Children.Remove(v12ScrollViewer);

                    // Restore Native Children
                    if (nativeRowChildren != null)
                    {
                        foreach (var child in nativeRowChildren)
                        {
                            child.Visibility = Visibility.Visible;
                        }
                    }
                    
                    Print("V12 PRO MASTER: Chart Trader Row Restored");
                }
                // Fallback for old border method (cleanup)
                else if (chartTraderBorder != null && nativeContent != null)
                {
                     chartTraderBorder.Child = nativeContent;
                     chartTraderBorder.Background = null;
                }

                glowTimer?.Stop();
                injected = false;
                targetGrid = null;
                nativeRowChildren = null;
                chartTraderControl = null;
            }
            catch (Exception ex)
            {
                Print("V12 PRO Restore Error: " + ex.Message);
            }
        }

        private void ToggleV12Native()
        {
            if (targetGrid == null || v12ScrollViewer == null) return;

            showingV12Panel = !showingV12Panel;

            if (showingV12Panel)
            {
                // Show V12, Hide Native Row Children
                if (!targetGrid.Children.Contains(v12ScrollViewer))
                    targetGrid.Children.Add(v12ScrollViewer);
                
                v12ScrollViewer.Visibility = Visibility.Visible;

                if (nativeRowChildren != null)
                    foreach (var child in nativeRowChildren) child.Visibility = Visibility.Collapsed;

                anchorToggleButton.Background = CyanBg;
                anchorToggleButton.Foreground = CyanFg;
                anchorToggleButton.ToolTip = "⚓ V12 PRO Active - Click to show Native Buttons";
            }
            else
            {
                // Hide V12, Show Native
                v12ScrollViewer.Visibility = Visibility.Collapsed;
                
                if (nativeRowChildren != null)
                    foreach (var child in nativeRowChildren) child.Visibility = Visibility.Visible;

                anchorToggleButton.Background = BtnBg;
                anchorToggleButton.Foreground = TextMuted;
                anchorToggleButton.ToolTip = "⚓ Native Buttons Active - Click to show V12 PRO";
            }
        }

        #endregion

        #region Helpers

        private bool IsChartTraderContainer(Border border)
        {
            if (border.Child == null) return false;
            return HasChartTraderButtons(border.Child);
        }

        private bool HasChartTraderButtons(DependencyObject element)
        {
            string[] chartTraderTexts = { "Buy Mkt", "Sell Mkt", "Buy Ask", "Sell Ask", "Buy Bid", "Sell Bid", "Rev", "Close", "Flat" };
            return SearchForButtonText(element, chartTraderTexts);
        }

        private bool SearchForButtonText(DependencyObject element, string[] searchTexts)
        {
            if (element == null) return false;

            if (element is Button btn)
            {
                string content = btn.Content?.ToString() ?? "";
                foreach (var text in searchTexts)
                {
                    if (content.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            int childCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childCount; i++)
            {
                if (SearchForButtonText(VisualTreeHelper.GetChild(element, i), searchTexts))
                    return true;
            }

            return false;
        }

        private void CreateFallbackOverlay()
        {
            Border overlayBorder = new Border
            {
                Background = BgDeep,
                BorderBrush = BorderSlate,
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Width = 220,
                Margin = new Thickness(0, 0, 0, 0)
            };

            overlayBorder.Child = CreateV12Panel();
            UserControlCollection.Add(overlayBorder);
            injected = true;
            showingV12Panel = true;
        }

        #endregion

        #region V12 Panel Creation

        private ScrollViewer CreateV12Panel()
        {
            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = BgDeep,
                Width = 220, // Restore width to prop open sidebar (240 was too wide)
                HorizontalAlignment = HorizontalAlignment.Right, // Stick to right edge
                MinHeight = 400 // Force vertical expansion
            };

            v12MainStack = new StackPanel { Background = BgDeep };

            // Add all sections
            v12MainStack.Children.Add(CreateSection0_Identity());
            v12MainStack.Children.Add(CreateSection1_Execution());
            v12MainStack.Children.Add(CreateSection2_Telemetry());
            v12MainStack.Children.Add(CreateSection3_Config());

            scroll.Content = v12MainStack;

            // Setup glow timer
            glowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            glowTimer.Tick += (s, e) =>
            {
                if (glowOverlay != null) glowOverlay.BorderBrush = Brushes.Transparent;
                glowTimer.Stop();
            };

            return scroll;
        }

        #endregion

        #region Section 0: Identity

        private Border CreateSection0_Identity()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(6) };

            // Section header
            stack.Children.Add(CreateSectionHeader("SECTION 0: IDENTITY"));

            // Row 1: Status LED + Title + Instrument + Account
            StackPanel row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };

            hubStatusLed = new Border
            {
                Width = 10, Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = GreenFg,
                Margin = new Thickness(0, 0, 6, 0),
                ToolTip = "IPC Status"
            };
            row1.Children.Add(hubStatusLed);

            titleLabel = new TextBlock
            {
                Text = "V12 PRO MASTER",
                Foreground = CyanAccent,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            row1.Children.Add(titleLabel);

            instrumentCombo = CreateCombo(40, new[] { "N", "MES", "MGC" });
            row1.Children.Add(instrumentCombo);

            accountCombo = CreateCombo(70, new[] { "APEX_MA" });
            accountCombo.Margin = new Thickness(4, 0, 0, 0);
            row1.Children.Add(accountCombo);

            stack.Children.Add(row1);

            // Row 2: Direction + Price + Submit
            StackPanel row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            directionCombo = CreateCombo(75, new[] { "OR LONG", "OR SHORT" });
            row2.Children.Add(directionCombo);

            priceInput = CreateTextBox(55, "6961.25");
            priceInput.Margin = new Thickness(4, 0, 4, 0);
            row2.Children.Add(priceInput);

            submitButton = CreateButton("SUBMIT", 50, GreenBg, GreenFg, GreenBorder);
            submitButton.Click += Submit_Click;
            row2.Children.Add(submitButton);

            stack.Children.Add(row2);

            // Row 3: Fleet + Ghost + Anchor Toggle
            StackPanel row3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 0) };

            fleetButton = CreateButton("FLEET", 45, BtnBg, TextPrimary, BtnBorder);
            fleetButton.Click += Fleet_Click;
            row3.Children.Add(fleetButton);

            ghostModeCheck = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 2, 0) };
            ghostModeCheck.Checked += (s, e) => { isGhostMode = true; SendCommand("GHOST_MODE|1"); };
            ghostModeCheck.Unchecked += (s, e) => { isGhostMode = false; SendCommand("GHOST_MODE|0"); };
            row3.Children.Add(ghostModeCheck);

            row3.Children.Add(new TextBlock { Text = "GHOST", Foreground = TextMuted, FontSize = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });

            // Anchor Toggle (⚓) - Hybrid mode switch between V12 PRO MASTER and Native Chart Trader
            anchorToggleButton = CreateButton("⚓", 24, CyanBg, CyanFg, CyanBorder);
            anchorToggleButton.FontSize = 12;
            anchorToggleButton.ToolTip = "⚓ V12 PRO MASTER Active - Click to show Native Chart Trader";
            anchorToggleButton.Click += (s, e) => ToggleV12Native();
            row3.Children.Add(anchorToggleButton);

            // Window controls
            row3.Children.Add(new Border { Width = 8 });
            Button closeBtn = CreateButton("✕", 20, BtnBg, TextPrimary, BtnBorder);
            closeBtn.FontSize = 8;
            closeBtn.Padding = new Thickness(0);
            closeBtn.Click += (s, e) => { if (v12ScrollViewer != null) v12ScrollViewer.Visibility = Visibility.Collapsed; };
            row3.Children.Add(closeBtn);

            stack.Children.Add(row3);

            section.Child = stack;
            return section;
        }

        #endregion

        #region Section 1: Execution

        private Border CreateSection1_Execution()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(6) };

            stack.Children.Add(CreateSectionHeader("SECTION 1: EXECUTION"));

            // Row 1: OR L | OR S (dashed buttons)
            Grid orRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            orRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            orRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            orLongButton = CreateDashedButton("OR L", CyanAccent);
            orLongButton.Click += (s, e) => { SendCommand("OR_LONG"); TriggerGlow(CyanAccent); };
            Grid.SetColumn(orLongButton, 0);
            orRow.Children.Add(orLongButton);

            orShortButton = CreateDashedButton("OR S", PinkFg);
            orShortButton.Margin = new Thickness(4, 0, 0, 0);
            orShortButton.Click += (s, e) => { SendCommand("OR_SHORT"); TriggerGlow(PinkFg); };
            Grid.SetColumn(orShortButton, 1);
            orRow.Children.Add(orShortButton);

            stack.Children.Add(orRow);

            // Row 2: RETEST [R] | RMA
            StackPanel row2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            retestButton = CreateButton("RETEST", 55, BtnBg, TextPrimary, BtnBorder);
            retestButton.Click += (s, e) =>
            {
                string cmd = isRetestRmaToggle ? "EXEC_RETEST_RMA" : "EXEC_RETEST";
                SendCommand(cmd);
                TriggerGlow(CyanAccent);
            };
            row2.Children.Add(retestButton);

            retestRmaToggle = CreateButton("R", 22, OrangeBg, OrangeFg, OrangeBorder);
            retestRmaToggle.Opacity = 0.5;
            retestRmaToggle.Margin = new Thickness(2, 0, 6, 0);
            retestRmaToggle.Click += (s, e) =>
            {
                isRetestRmaToggle = !isRetestRmaToggle;
                retestRmaToggle.Opacity = isRetestRmaToggle ? 1.0 : 0.5;
            };
            row2.Children.Add(retestRmaToggle);

            rmaButton = CreateButton("RMA", 40, OrangeBg, OrangeFg, OrangeBorder);
            rmaButton.Click += (s, e) => { SendCommand("MODE_RMA"); TriggerGlow(OrangeFg); };
            row2.Children.Add(rmaButton);

            stack.Children.Add(row2);

            // Row 3: MOMO | FFMA | TREND [R]
            StackPanel row3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            momoButton = CreateButton("MOMO", 45, GreenBg, GreenFg, GreenBorder);
            momoButton.Click += (s, e) => { SendCommand("MODE_MOMO"); TriggerGlow(GreenFg); };
            row3.Children.Add(momoButton);

            ffmaButton = CreateButton("FFMA", 40, PinkBg, PinkFg, PinkBorder);
            ffmaButton.Margin = new Thickness(2, 0, 0, 0);
            ffmaButton.Click += (s, e) => { SendCommand("MODE_FFMA"); TriggerGlow(PinkFg); };
            row3.Children.Add(ffmaButton);

            trendButton = CreateButton("TREND", 50, BtnBg, TextPrimary, BtnBorder);
            trendButton.Margin = new Thickness(2, 0, 0, 0);
            trendButton.Click += (s, e) =>
            {
                string cmd = isTrendRmaToggle ? "EXEC_TREND_RMA" : "EXEC_TREND";
                SendCommand(cmd);
                TriggerGlow(CyanAccent);
            };
            row3.Children.Add(trendButton);

            trendRmaToggle = CreateButton("R", 22, OrangeBg, OrangeFg, OrangeBorder);
            trendRmaToggle.Opacity = 0.5;
            trendRmaToggle.Margin = new Thickness(2, 0, 0, 0);
            trendRmaToggle.Click += (s, e) =>
            {
                isTrendRmaToggle = !isTrendRmaToggle;
                trendRmaToggle.Opacity = isTrendRmaToggle ? 1.0 : 0.5;
            };
            row3.Children.Add(trendRmaToggle);

            stack.Children.Add(row3);

            // Row 4: T1 T2 T3 T4 T5
            StackPanel targetRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            t1Button = CreateTargetButton("T1", GreenBg, GreenFg, GreenBorder, "CLOSE_T1");
            t2Button = CreateTargetButton("T2", YellowBg, YellowFg, YellowBorder, "CLOSE_T2");
            t3Button = CreateTargetButton("T3", OrangeBg, OrangeFg, OrangeBorder, "CLOSE_T3");
            t4Button = CreateTargetButton("T4", RedBg, RedFg, RedBorder, "CLOSE_T4");
            t5Button = CreateTargetButton("T5", OrangeBg, OrangeFg, OrangeBorder, "CLOSE_T5");

            targetRow.Children.Add(t1Button);
            targetRow.Children.Add(t2Button);
            targetRow.Children.Add(t3Button);
            targetRow.Children.Add(t4Button);
            targetRow.Children.Add(t5Button);

            stack.Children.Add(targetRow);

            // Row 5: 25% 50% BE TR1 TR2
            StackPanel scaleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

            trim25Button = CreateButton("25%", 32, YellowBg, YellowFg, YellowBorder);
            trim25Button.Click += (s, e) => { SendCommand("TRIM_25"); TriggerGlow(YellowFg); };
            scaleRow.Children.Add(trim25Button);

            trim50Button = CreateButton("50%", 32, OrangeBg, OrangeFg, OrangeBorder);
            trim50Button.Margin = new Thickness(2, 0, 0, 0);
            trim50Button.Click += (s, e) => { SendCommand("TRIM_50"); TriggerGlow(OrangeFg); };
            scaleRow.Children.Add(trim50Button);

            beButton = CreateButton("BE", 28, CyanBg, CyanFg, CyanBorder);
            beButton.Margin = new Thickness(2, 0, 0, 0);
            beButton.Click += (s, e) => { SendCommand("BE_PLUS_2"); TriggerGlow(CyanFg); };
            scaleRow.Children.Add(beButton);

            tr1Button = CreateButton("TR1", 28, BtnBg, TextPrimary, BtnBorder);
            tr1Button.Margin = new Thickness(2, 0, 0, 0);
            tr1Button.Click += (s, e) => { SendCommand("RUN_1PT"); TriggerGlow(TextPrimary); };
            scaleRow.Children.Add(tr1Button);

            tr2Button = CreateButton("TR2", 28, BtnBg, TextPrimary, BtnBorder);
            tr2Button.Margin = new Thickness(2, 0, 0, 0);
            tr2Button.Click += (s, e) => { SendCommand("RUN_2PT"); TriggerGlow(TextPrimary); };
            scaleRow.Children.Add(tr2Button);

            stack.Children.Add(scaleRow);

            // Row 6: FLATTEN | Price
            Grid flattenRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            flattenRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            flattenRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            flattenButton = CreateButton("FLATTEN", 70, RedBg, RedFg, RedBorder);
            flattenButton.Height = 28;
            flattenButton.FontWeight = FontWeights.Bold;
            flattenButton.Click += (s, e) => { SendCommand("FLATTEN"); TriggerGlow(RedFg); };
            Grid.SetColumn(flattenButton, 0);
            flattenRow.Children.Add(flattenButton);

            lastPriceText = new TextBlock
            {
                Text = "6961.25",
                Foreground = GreenFg,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(lastPriceText, 1);
            flattenRow.Children.Add(lastPriceText);

            stack.Children.Add(flattenRow);

            section.Child = stack;
            return section;
        }

        private Button CreateTargetButton(string text, Brush bg, Brush fg, Brush border, string command)
        {
            Button btn = CreateButton(text, 32, bg, fg, border);
            btn.Margin = new Thickness(0, 0, 2, 0);
            btn.Click += (s, e) => { SendCommand(command); TriggerGlow(fg); };
            return btn;
        }

        #endregion

        #region Section 2: Telemetry

        private Border CreateSection2_Telemetry()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(6) };

            stack.Children.Add(CreateSectionHeader("SECTION 2: TELEMETRY"));

            // OR5 line
            or5Text = new TextBlock
            {
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 4, 0, 2)
            };
            or5Text.Inlines.Add(new System.Windows.Documents.Run("OR5: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
            or5Text.Inlines.Add(new System.Windows.Documents.Run("6981.75") { Foreground = OrangeFg });
            or5Text.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
            or5Text.Inlines.Add(new System.Windows.Documents.Run("6970.25") { Foreground = OrangeFg });
            or5Text.Inlines.Add(new System.Windows.Documents.Run(" (R: 11.5)") { Foreground = TextMuted });
            stack.Children.Add(or5Text);

            // OR15 line
            or15Text = new TextBlock
            {
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            or15Text.Inlines.Add(new System.Windows.Documents.Run("OR15: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
            or15Text.Inlines.Add(new System.Windows.Documents.Run("6985.50") { Foreground = OrangeFg });
            or15Text.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
            or15Text.Inlines.Add(new System.Windows.Documents.Run("6965.00") { Foreground = OrangeFg });
            or15Text.Inlines.Add(new System.Windows.Documents.Run(" (R: 20.5)") { Foreground = TextMuted });
            stack.Children.Add(or15Text);

            // EMA line 1: 9, 15, 30
            StackPanel emaRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            ema9Text = CreateEmaLabel("9:", "6972");
            ema15Text = CreateEmaLabel("15:", "6975");
            ema30Text = CreateEmaLabel("30:", "6959", GreenFg);
            emaRow1.Children.Add(ema9Text);
            emaRow1.Children.Add(ema15Text);
            emaRow1.Children.Add(ema30Text);
            stack.Children.Add(emaRow1);

            // EMA line 2: 65, 200, ATR
            StackPanel emaRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            ema65Text = CreateEmaLabel("65:", "6945");
            ema200Text = CreateEmaLabel("200:", "6912", PurpleFg);
            atrText = new TextBlock
            {
                Text = "ATR: 12.5",
                Foreground = TextMuted,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            emaRow2.Children.Add(ema65Text);
            emaRow2.Children.Add(ema200Text);
            emaRow2.Children.Add(atrText);
            stack.Children.Add(emaRow2);

            // MKT SYNC + BULLISH/BEARISH
            StackPanel syncRow = new StackPanel { Orientation = Orientation.Horizontal };

            mktSyncButton = CreateButton("MKT SYNC", 65, CyanBg, CyanFg, CyanBorder);
            mktSyncButton.Height = 22;
            mktSyncButton.FontSize = 9;
            syncRow.Children.Add(mktSyncButton);

            trendIndicator = new Border
            {
                Background = GreenBg,
                BorderBrush = GreenBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(8, 0, 0, 0),
                Height = 22
            };
            trendText = new TextBlock
            {
                Text = "BULLISH",
                Foreground = GreenFg,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            };
            trendIndicator.Child = trendText;
            syncRow.Children.Add(trendIndicator);

            stack.Children.Add(syncRow);

            section.Child = stack;
            return section;
        }

        private TextBlock CreateEmaLabel(string label, string value, Brush valueColor = null)
        {
            TextBlock tb = new TextBlock
            {
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 8, 0)
            };
            tb.Inlines.Add(new System.Windows.Documents.Run(label) { Foreground = TextMuted });
            tb.Inlines.Add(new System.Windows.Documents.Run(value) { Foreground = valueColor ?? TextPrimary });
            return tb;
        }

        #endregion

        #region Section 3: Config

        private Border CreateSection3_Config()
        {
            Border section = CreateSectionBorder();
            section.BorderThickness = new Thickness(0); // No bottom border for last section
            StackPanel stack = new StackPanel { Margin = new Thickness(6) };

            stack.Children.Add(CreateSectionHeader("SECTION 3: CONFIG"));

            // Mode chips: ORB RMA RETEST MOMO FFMA TREND
            WrapPanel modePanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 6) };

            modeOrbButton = CreateModeChip("ORB", true);
            modeRmaButton = CreateModeChip("RMA", false);
            modeRetestButton = CreateModeChip("RETEST", false);
            modeMomoButton = CreateModeChip("MOMO", false);
            modeFfmaButton = CreateModeChip("FFMA", false);
            modeTrendButton = CreateModeChip("TREND", false);

            modePanel.Children.Add(modeOrbButton);
            modePanel.Children.Add(modeRmaButton);
            modePanel.Children.Add(modeRetestButton);
            modePanel.Children.Add(modeMomoButton);
            modePanel.Children.Add(modeFfmaButton);
            modePanel.Children.Add(modeTrendButton);

            stack.Children.Add(modePanel);

            // T: 1 2 [3] 4 5
            StackPanel countRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            countRow.Children.Add(new TextBlock { Text = "T:", Foreground = TextPrimary, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });

            cnt1 = CreateCountChip("1", 1);
            cnt2 = CreateCountChip("2", 2);
            cnt3 = CreateCountChip("3", 3);
            cnt4 = CreateCountChip("4", 4);
            cnt5 = CreateCountChip("5", 5);

            // Default selection: 3
            cnt3.Background = CyanBg;
            cnt3.Foreground = CyanFg;
            cnt3.BorderBrush = CyanBorder;

            countRow.Children.Add(cnt1);
            countRow.Children.Add(cnt2);
            countRow.Children.Add(cnt3);
            countRow.Children.Add(cnt4);
            countRow.Children.Add(cnt5);

            stack.Children.Add(countRow);

            // SV: T1 [1.0] ATR  T2 [2.0] ATR
            StackPanel svRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            svRow1.Children.Add(new TextBlock { Text = "SV:", Foreground = TextPrimary, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });

            svRow1.Children.Add(new TextBlock { Text = "T1", Foreground = GreenFg, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT1Val = CreateTextBox(28, "1.0"); svT1Val.Height = 18; svT1Val.FontSize = 9;
            svRow1.Children.Add(svT1Val);
            svT1Type = CreateCombo(38, new[] { "ATR", "Ticks", "Pts" }); svT1Type.Height = 18; svT1Type.FontSize = 8; svT1Type.Margin = new Thickness(2, 0, 6, 0);
            svRow1.Children.Add(svT1Type);

            svRow1.Children.Add(new TextBlock { Text = "T2", Foreground = YellowFg, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT2Val = CreateTextBox(28, "2.0"); svT2Val.Height = 18; svT2Val.FontSize = 9;
            svRow1.Children.Add(svT2Val);
            svT2Type = CreateCombo(38, new[] { "ATR", "Ticks", "Pts" }); svT2Type.Height = 18; svT2Type.FontSize = 8;
            svRow1.Children.Add(svT2Type);

            stack.Children.Add(svRow1);

            // T3 [3.0] ATR
            StackPanel svRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            svRow2.Children.Add(new TextBlock { Text = "      T3", Foreground = OrangeFg, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT3Val = CreateTextBox(28, "3.0"); svT3Val.Height = 18; svT3Val.FontSize = 9;
            svRow2.Children.Add(svT3Val);
            svT3Type = CreateCombo(38, new[] { "ATR", "Ticks", "Pts" }); svT3Type.Height = 18; svT3Type.FontSize = 8; svT3Type.Margin = new Thickness(2, 0, 0, 0);
            svRow2.Children.Add(svT3Type);
            stack.Children.Add(svRow2);

            // GLB: $150  STR: $150  MAX: $1200
            StackPanel riskRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };

            riskRow.Children.Add(new TextBlock { Text = "GLB:", Foreground = TextMuted, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            glbVal = CreateTextBox(35, "$150"); glbVal.Height = 18; glbVal.FontSize = 9;
            riskRow.Children.Add(glbVal);

            riskRow.Children.Add(new TextBlock { Text = "STR:", Foreground = OrangeFg, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) });
            strVal = CreateTextBox(35, "$150"); strVal.Height = 18; strVal.FontSize = 9; strVal.Foreground = OrangeFg;
            riskRow.Children.Add(strVal);

            riskRow.Children.Add(new TextBlock { Text = "MAX:", Foreground = OrangeFg, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) });
            maxVal = CreateTextBox(40, "$1200"); maxVal.Height = 18; maxVal.FontSize = 9; maxVal.Foreground = OrangeFg;
            riskRow.Children.Add(maxVal);

            stack.Children.Add(riskRow);

            // SYNC ALL button
            syncAllButton = CreateButton("SYNC ALL", double.NaN, CyanBg, CyanFg, CyanBorder);
            syncAllButton.Height = 26;
            syncAllButton.FontWeight = FontWeights.Bold;
            syncAllButton.Click += SyncAll_Click;
            stack.Children.Add(syncAllButton);

            section.Child = stack;
            return section;
        }

        private Button CreateModeChip(string text, bool isActive)
        {
            Button btn = new Button
            {
                Content = text,
                Background = isActive ? CyanBg : BtnBg,
                Foreground = isActive ? CyanFg : TextMuted,
                BorderBrush = isActive ? CyanBorder : BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Height = 20,
                MinWidth = 30,
                Padding = new Thickness(4, 0, 4, 0),
                Margin = new Thickness(0, 0, 2, 2),
                Cursor = Cursors.Hand
            };

            btn.Click += (s, e) => SelectConfigMode(text, btn);
            return btn;
        }

        private Button CreateCountChip(string text, int count)
        {
            Button btn = CreateButton(text, 20, BtnBg, TextPrimary, BtnBorder);
            btn.Height = 20;
            btn.FontSize = 10;
            btn.Margin = new Thickness(0, 0, 2, 0);
            btn.Click += (s, e) => SelectTargetCount(count, btn);
            return btn;
        }

        private void SelectConfigMode(string mode, Button clickedBtn)
        {
            selectedConfigMode = mode;

            foreach (var btn in new[] { modeOrbButton, modeRmaButton, modeRetestButton, modeMomoButton, modeFfmaButton, modeTrendButton })
            {
                btn.Background = BtnBg;
                btn.Foreground = TextMuted;
                btn.BorderBrush = BtnBorder;
            }

            clickedBtn.Background = CyanBg;
            clickedBtn.Foreground = CyanFg;
            clickedBtn.BorderBrush = CyanBorder;
        }

        private void SelectTargetCount(int count, Button clickedBtn)
        {
            selectedTargetCount = count;

            foreach (var btn in new[] { cnt1, cnt2, cnt3, cnt4, cnt5 })
            {
                btn.Background = BtnBg;
                btn.Foreground = TextPrimary;
                btn.BorderBrush = BtnBorder;
            }

            clickedBtn.Background = CyanBg;
            clickedBtn.Foreground = CyanFg;
            clickedBtn.BorderBrush = CyanBorder;
        }

        #endregion

        #region UI Helpers

        private Border CreateSectionBorder()
        {
            return new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(17, 17, 17)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Background = BgDeep
            };
        }

        private TextBlock CreateSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = CyanAccent,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas")
            };
        }

        private Button CreateButton(string text, double width, Brush bg, Brush fg, Brush border)
        {
            Button btn = new Button
            {
                Content = text,
                Background = bg,
                Foreground = fg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Height = 22,
                Padding = new Thickness(4, 0, 4, 0),
                Cursor = Cursors.Hand
            };

            if (!double.IsNaN(width)) btn.Width = width;
            return btn;
        }

        private Button CreateDashedButton(string text, Brush fg)
        {
            return new Button
            {
                Content = text,
                Background = BgSlate,
                Foreground = fg,
                BorderBrush = fg,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Height = 26,
                Cursor = Cursors.Hand
            };
        }

        private TextBox CreateTextBox(double width, string defaultText)
        {
            return new TextBox
            {
                Text = defaultText,
                Background = BgSlate,
                Foreground = TextPrimary,
                BorderBrush = BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Height = 22,
                Width = width,
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private ComboBox CreateCombo(double width, string[] items)
        {
            ComboBox combo = new ComboBox
            {
                Background = BtnBg,
                Foreground = TextPrimary,
                BorderBrush = BtnBorder,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 9,
                Height = 22,
                Width = width
            };

            foreach (var item in items)
            {
                combo.Items.Add(new ComboBoxItem { Content = item });
            }

            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;

            return combo;
        }

        private void TriggerGlow(Brush color)
        {
            // Visual feedback on main panel border
            if (v12MainStack != null && v12MainStack.Parent is ScrollViewer sv)
            {
                sv.BorderBrush = color;
                sv.BorderThickness = new Thickness(2);

                glowTimer?.Stop();
                glowTimer?.Start();
            }
        }

        #endregion

        #region Event Handlers

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            string direction = (directionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "OR LONG";
            string price = priceInput.Text;

            string cmd = direction.Contains("LONG") ? "OR_LONG" : "OR_SHORT";
            cmd += $"|{activeSymbol}";
            if (!string.IsNullOrEmpty(price) && price != "0.00")
                cmd += $"|{price}";

            SendCommand(cmd);
            TriggerGlow(GreenFg);
        }

        private void Fleet_Click(object sender, RoutedEventArgs e)
        {
            SendCommand("GET_FLEET");
        }

        private void SyncAll_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"CONFIG|{selectedConfigMode}|");
            sb.Append($"COUNT:{selectedTargetCount};");
            sb.Append($"T1:{svT1Val.Text};T1TYPE:{(svT1Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"T2:{svT2Val.Text};T2TYPE:{(svT2Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"T3:{svT3Val.Text};T3TYPE:{(svT3Type.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"STR:{strVal.Text};MAX:{maxVal.Text};");

            SendCommand(sb.ToString());
            TriggerGlow(CyanFg);
        }

        #endregion

        #region IPC Communication

        private void ConnectToStrategy()
        {
            try
            {
                lock (tcpLock)
                {
                    if (isConnected) return;

                    tcpClient = new TcpClient();
                    tcpClient.Connect("127.0.0.1", IpcPort);
                    tcpStream = tcpClient.GetStream();
                    isConnected = true;

                    Print($"V12 PRO MASTER: Connected on port {IpcPort}");

                    if (ChartControl != null)
                    {
                        ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (hubStatusLed != null) hubStatusLed.Background = GreenFg;
                        }));
                    }

                    receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "V12_Receive" };
                    receiveThread.Start();

                    SendCommand("GET_LAYOUT");
                }
            }
            catch (Exception ex)
            {
                Print($"V12 PRO MASTER: Connection failed - {ex.Message}");
                isConnected = false;

                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (hubStatusLed != null) hubStatusLed.Background = TextMuted;
                    }));
                }
            }
        }

        private void DisconnectFromStrategy()
        {
            lock (tcpLock)
            {
                isConnected = false;
                try { tcpStream?.Close(); } catch { }
                try { tcpClient?.Close(); } catch { }
                tcpStream = null;
                tcpClient = null;
            }
        }

        private void SendCommand(string command)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!isConnected) ConnectToStrategy();

                    lock (tcpLock)
                    {
                        if (tcpStream != null && tcpStream.CanWrite)
                        {
                            if (!command.Contains("|"))
                                command = $"{command}|{activeSymbol}";

                            byte[] data = Encoding.UTF8.GetBytes(command + "\n");
                            tcpStream.Write(data, 0, data.Length);
                            tcpStream.Flush();

                            Print($"V12: Sent -> {command}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Print($"V12: Send error - {ex.Message}");
                    isConnected = false;
                }
            });
        }

        private void ReceiveLoop()
        {
            StringBuilder buffer = new StringBuilder();
            byte[] readBuffer = new byte[4096];

            while (isConnected)
            {
                try
                {
                    if (tcpStream == null || !tcpStream.CanRead) break;

                    int bytesRead = tcpStream.Read(readBuffer, 0, readBuffer.Length);
                    if (bytesRead == 0) break;

                    buffer.Append(Encoding.UTF8.GetString(readBuffer, 0, bytesRead));

                    string data = buffer.ToString();
                    int newlineIdx;
                    while ((newlineIdx = data.IndexOf('\n')) >= 0)
                    {
                        string message = data.Substring(0, newlineIdx).Trim();
                        data = data.Substring(newlineIdx + 1);

                        if (!string.IsNullOrEmpty(message))
                            responseQueue.Enqueue(message);
                    }
                    buffer.Clear();
                    buffer.Append(data);
                }
                catch
                {
                    break;
                }
            }
        }

        private void ProcessStrategyResponse(string response)
        {
            string[] parts = response.Split('|');
            if (parts.Length == 0) return;

            if (ChartControl != null)
            {
                ChartControl.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (parts[0] == "TELEMETRY" && parts.Length > 1)
                            ParseTelemetry(parts[1]);
                        else if (parts[0] == "CONFIG" && parts.Length > 1)
                            ParseConfig(parts[1]);
                    }
                    catch (Exception ex)
                    {
                        Print($"V12: Parse error - {ex.Message}");
                    }
                }));
            }
        }

        private void ParseTelemetry(string data)
        {
            foreach (string pair in data.Split(';'))
            {
                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;

                string key = kv[0].Trim().ToUpper();
                string value = kv[1].Trim();

                // Update telemetry displays based on key
                // (Implementation depends on strategy response format)
            }
        }

        private void ParseConfig(string data)
        {
            foreach (string pair in data.Split(';'))
            {
                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;

                string key = kv[0].Trim().ToUpper();
                string value = kv[1].Trim();

                switch (key)
                {
                    case "T1": svT1Val.Text = value; break;
                    case "T2": svT2Val.Text = value; break;
                    case "T3": svT3Val.Text = value; break;
                    case "STR": strVal.Text = value; break;
                    case "MAX": maxVal.Text = value; break;
                }
            }
        }

        #endregion
    }
}
