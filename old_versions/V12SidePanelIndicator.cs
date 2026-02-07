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
        private ComboBox leaderAccountCombo;
        private ComboBox fleetAccountCombo;
        private ComboBox directionCombo;
        private TextBox priceInput;
        private Button submitButton;

        // UI Components - Section 1: Execution
        private Button orLongButton, orShortButton;
        private Button retestButton, retestRmaToggle;
        private Button rmaButton;
        private Button momoButton, ffmaButton;
        private Button trendButton, trendRmaToggle;
        private Button t1Button, t2Button, t3Button, t4Button, t5Button;
        private Button trim25Button, trim50Button, beButton, tr1Button, tr2Button;
        private Button mButton, flattenButton;
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
        private TextBox svT1Val, svT2Val, svT3Val, svT4Val, svT5Val;
        private ComboBox svT1Type, svT2Type, svT3Type, svT4Type, svT5Type, svStrType;
        private TextBox citVal, strVal, maxVal;
        private StackPanel t2Row, t3Row, t4Row, t5Row;  // For visibility control
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
        private static readonly Brush CyanBg;
        private static readonly SolidColorBrush CyanFg, CyanBorder;
        private static readonly SolidColorBrush PurpleFg;
        private static readonly SolidColorBrush BlueBg, BlueFg, BlueBorder;

        static V12SidePanelIndicator()
        {
            // V12.5 PREMIUM PALETTE: Onyx & Graphite
            BgDeep = Freeze(new SolidColorBrush(Color.FromRgb(8, 8, 8))); // Pitch Black
            BgSlate = Freeze(new SolidColorBrush(Color.FromRgb(18, 18, 18))); // Onyx
            BorderSlate = Freeze(new SolidColorBrush(Color.FromRgb(34, 34, 34))); // Graphite
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

            PinkBorder = Freeze(new SolidColorBrush(Color.FromRgb(162, 28, 175)));

            // Premium Cyan Gradient
            LinearGradientBrush cyanGrad = new LinearGradientBrush(
                Color.FromRgb(8, 51, 68), // Dark Navy
                Color.FromRgb(22, 78, 99), // Deep Cyan
                90.0);
            cyanGrad.Freeze();
            CyanBg = cyanGrad;
            
            CyanFg = Freeze(new SolidColorBrush(Color.FromRgb(34, 211, 238)));
            CyanBorder = Freeze(new SolidColorBrush(Color.FromRgb(8, 145, 178)));

            PurpleFg = Freeze(new SolidColorBrush(Color.FromRgb(168, 85, 247)));

            BlueBg = Freeze(new SolidColorBrush(Color.FromRgb(30, 58, 138)));
            BlueFg = Freeze(new SolidColorBrush(Color.FromRgb(147, 197, 253)));
            BlueBorder = Freeze(new SolidColorBrush(Color.FromRgb(59, 130, 246)));
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
                    ChartControl.Dispatcher.InvokeAsync(() => {
                        InjectIntoChartTrader();
                        PopulateAccountCombos();
                    });
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
            }
            else
            {
                // Hide V12, Show Native
                v12ScrollViewer.Visibility = Visibility.Collapsed;
                
                if (nativeRowChildren != null)
                    foreach (var child in nativeRowChildren) child.Visibility = Visibility.Visible;
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
                Background = Brushes.Transparent, // Let the main stack shadow show through
                Width = 210, 
                HorizontalAlignment = HorizontalAlignment.Right, 
                MinHeight = 400 
            };

            v12MainStack = new StackPanel 
            { 
                Background = BgDeep,
                Opacity = 0.95 // Glassmorphism
            };
            
            // Premium Shadow Effect
            System.Windows.Media.Effects.DropShadowEffect shadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 270,
                ShadowDepth = 4,
                Opacity = 0.6,
                BlurRadius = 15
            };
            v12MainStack.Effect = shadow;

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

            // Initial account population
            PopulateAccountCombos();

            return scroll;
        }

        private void PopulateAccountCombos()
        {
            if (leaderAccountCombo == null || fleetAccountCombo == null) return;

            leaderAccountCombo.Items.Clear();
            fleetAccountCombo.Items.Clear();

            // Add "FLEET: ALL" to fleet combo
            fleetAccountCombo.Items.Add(new ComboBoxItem { Content = "FLEET: ALL" });

            foreach (NinjaTrader.Cbi.Account account in NinjaTrader.Cbi.Account.All)
            {
                // Format with daily realized P/L
                double realizedPL = account.Get(NinjaTrader.Cbi.AccountItem.RealizedProfitLoss, NinjaTrader.Cbi.Currency.UsDollar);
                string plText = realizedPL >= 0 ? $"+${realizedPL:N0}" : $"-${Math.Abs(realizedPL):N0}";
                string displayName = $"{account.Name} [{plText}]";
                
                leaderAccountCombo.Items.Add(new ComboBoxItem { Content = displayName, Tag = account.Name });
                fleetAccountCombo.Items.Add(new ComboBoxItem { Content = displayName, Tag = account.Name });
            }

            if (leaderAccountCombo.Items.Count > 0) leaderAccountCombo.SelectedIndex = 0;
            if (fleetAccountCombo.Items.Count > 0) fleetAccountCombo.SelectedIndex = 0;
            
            Print($"V12 SIDE PANEL: Populated {leaderAccountCombo.Items.Count} accounts with P/L.");
        }

        #endregion

        #region Section 0: Identity

        private Border CreateSection0_Identity()
        {
            Border section = CreateSectionBorder();
            StackPanel stack = new StackPanel { Margin = new Thickness(4, 2, 4, 1) };

            // Section header
            stack.Children.Add(CreateSectionHeader("SECTION 0: IDENTITY"));

            // Row 1: Leader Account (Full Width)
            Grid row1 = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            hubStatusLed = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = TextMuted,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "IPC Status"
            };
            Grid.SetColumn(hubStatusLed, 0);
            row1.Children.Add(hubStatusLed);

            leaderAccountCombo = CreateCombo(0, new[] { "LEADER: APEX_MA", "Sim101" });
            leaderAccountCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(leaderAccountCombo, 1);
            row1.Children.Add(leaderAccountCombo);
            stack.Children.Add(row1);

            // Row 2: Fleet Manager (Full Width)
            fleetAccountCombo = CreateCombo(0, new[] { "FLEET: APEX_ALL", "APEX_GROUP_1" });
            fleetAccountCombo.Margin = new Thickness(0, 1, 0, 1);
            fleetAccountCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            stack.Children.Add(fleetAccountCombo);

            // Row 3: Direction + Price + Submit + Close
            Grid row3 = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) }); // V12: Synchronized width
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row3.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            directionCombo = CreateCombo(0, new[] { "OR LONG", "OR SHORT" });
            directionCombo.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(directionCombo, 0);
            row3.Children.Add(directionCombo);

            priceInput = CreateTextBox(0, "6961.25");
            priceInput.Margin = new Thickness(4, 0, 4, 0);
            Grid.SetColumn(priceInput, 1);
            row3.Children.Add(priceInput);

            submitButton = CreateButton("SUBMIT", 60, GreenBg, GreenFg, GreenBorder);
            submitButton.Height = 22;
            submitButton.Click += Submit_Click;
            Grid.SetColumn(submitButton, 2);
            row3.Children.Add(submitButton);

            // Close button - V12: High Visibility RED (Synchronized)
            Button closeBtn = CreateButton("✕", 28, RedBg, Brushes.White, RedBorder);
            closeBtn.FontSize = 14;
            closeBtn.FontWeight = FontWeights.Bold;
            closeBtn.Margin = new Thickness(4, 0, 0, 0);
            closeBtn.ToolTip = "Restore Native HUD";
            closeBtn.Click += (s, e) => { if (showingV12Panel) ToggleV12Native(); };
            Grid.SetColumn(closeBtn, 3);
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
            StackPanel stack = new StackPanel { Margin = new Thickness(4, 1, 4, 1) };

            stack.Children.Add(CreateSectionHeader("SECTION 1: EXECUTION"));

            Grid mainGrid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // LEFT COLUMN
            StackPanel leftCol = new StackPanel { Margin = new Thickness(0, 0, 1, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            
            orLongButton = CreateDashedButton("OR L", CyanAccent);
            orLongButton.Click += (s, e) => { SendCommand("OR_LONG"); TriggerGlow(CyanAccent); };
            leftCol.Children.Add(orLongButton);

            orShortButton = CreateDashedButton("OR S", PinkFg);
            orShortButton.Margin = new Thickness(0, 2, 0, 0);
            orShortButton.Click += (s, e) => { SendCommand("OR_SHORT"); TriggerGlow(PinkFg); };
            leftCol.Children.Add(orShortButton);

            // Row 2: RETEST [R] | RMA
            // Row 2: RETEST row with R toggle
            Grid retestRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            retestRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            retestRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            retestButton = CreateButton("RETEST", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            retestButton.Click += (s, e) =>
            {
                string cmd = isRetestRmaToggle ? "EXEC_RETEST_RMA" : "EXEC_RETEST";
                SendCommand(cmd);
                TriggerGlow(CyanAccent);
            };
            Grid.SetColumn(retestButton, 0);
            retestRow.Children.Add(retestButton);
            
            retestRmaToggle = CreateButton("R", 24, PurpleFg, TextPrimary, PurpleFg);
            retestRmaToggle.Margin = new Thickness(2, 0, 0, 0);
            retestRmaToggle.Opacity = 0.5;
            retestRmaToggle.Click += (s, e) =>
            {
                isRetestRmaToggle = !isRetestRmaToggle;
                retestRmaToggle.Opacity = isRetestRmaToggle ? 1.0 : 0.5;
            };
            Grid.SetColumn(retestRmaToggle, 1);
            retestRow.Children.Add(retestRmaToggle);
            leftCol.Children.Add(retestRow);

            rmaButton = CreateButton("RMA", double.NaN, PurpleFg, TextPrimary, PurpleFg);
            rmaButton.Margin = new Thickness(0, 2, 0, 0);
            rmaButton.Click += (s, e) => { SendCommand("MODE_RMA"); TriggerGlow(PurpleFg); };
            leftCol.Children.Add(rmaButton);

            momoButton = CreateButton("MOMO", double.NaN, GreenBg, GreenFg, GreenBorder);
            momoButton.Margin = new Thickness(0, 2, 0, 0);
            momoButton.Click += (s, e) => { SendCommand("MODE_MOMO"); TriggerGlow(GreenFg); };
            leftCol.Children.Add(momoButton);

            ffmaButton = CreateButton("FFMA", double.NaN, PinkBg, PinkFg, PinkBorder);
            ffmaButton.Margin = new Thickness(0, 2, 0, 0);
            ffmaButton.Click += (s, e) => { SendCommand("MODE_FFMA"); TriggerGlow(PinkFg); };
            leftCol.Children.Add(ffmaButton);

            mButton = CreateButton("MNL", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            mButton.Margin = new Thickness(0, 2, 0, 0);
            mButton.Click += (s, e) => { SendCommand("MODE_M"); TriggerGlow(OrangeFg); };
            leftCol.Children.Add(mButton);

            // TREND row with R toggle
            Grid trendRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            trendRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            trendRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            trendButton = CreateButton("TREND", double.NaN, BtnBg, TextPrimary, BtnBorder);
            trendButton.Click += (s, e) =>
            {
                string cmd = isTrendRmaToggle ? "EXEC_TREND_RMA" : "EXEC_TREND";
                SendCommand(cmd);
                TriggerGlow(CyanAccent);
            };
            Grid.SetColumn(trendButton, 0);
            trendRow.Children.Add(trendButton);
            
            trendRmaToggle = CreateButton("R", 24, PurpleFg, TextPrimary, PurpleFg);
            trendRmaToggle.Margin = new Thickness(2, 0, 0, 0);
            trendRmaToggle.Opacity = 0.5;
            trendRmaToggle.Click += (s, e) =>
            {
                isTrendRmaToggle = !isTrendRmaToggle;
                trendRmaToggle.Opacity = isTrendRmaToggle ? 1.0 : 0.5;
            };
            Grid.SetColumn(trendRmaToggle, 1);
            trendRow.Children.Add(trendRmaToggle);
            leftCol.Children.Add(trendRow);

            Grid.SetColumn(leftCol, 0);
            mainGrid.Children.Add(leftCol);

            // RIGHT COLUMN
            StackPanel rightCol = new StackPanel { Margin = new Thickness(2, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };

            t1Button = CreateButton("T1", double.NaN, GreenBg, GreenFg, GreenBorder);
            t1Button.Click += (s, e) => { SendCommand("CLOSE_T1"); TriggerGlow(GreenFg); };
            rightCol.Children.Add(t1Button);

            t2Button = CreateButton("T2", double.NaN, YellowBg, YellowFg, YellowBorder);
            t2Button.Margin = new Thickness(0, 2, 0, 0);
            t2Button.Click += (s, e) => { SendCommand("CLOSE_T2"); TriggerGlow(YellowFg); };
            rightCol.Children.Add(t2Button);

            t3Button = CreateButton("T3", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            t3Button.Margin = new Thickness(0, 2, 0, 0);
            t3Button.Click += (s, e) => { SendCommand("CLOSE_T3"); TriggerGlow(OrangeFg); };
            rightCol.Children.Add(t3Button);

            t4Button = CreateButton("T4", double.NaN, RedBg, RedFg, RedBorder);
            t4Button.Margin = new Thickness(0, 2, 0, 0);
            t4Button.Click += (s, e) => { SendCommand("CLOSE_T4"); TriggerGlow(RedFg); };
            rightCol.Children.Add(t4Button);

            trim25Button = CreateButton("25%", double.NaN, YellowBg, YellowFg, YellowBorder);
            trim25Button.Margin = new Thickness(0, 2, 0, 0);
            trim25Button.Click += (s, e) => { SendCommand("TRIM_25"); TriggerGlow(YellowFg); };
            rightCol.Children.Add(trim25Button);

            trim50Button = CreateButton("50%", double.NaN, OrangeBg, OrangeFg, OrangeBorder);
            trim50Button.Margin = new Thickness(0, 2, 0, 0);
            trim50Button.Click += (s, e) => { SendCommand("TRIM_50"); TriggerGlow(OrangeFg); };
            rightCol.Children.Add(trim50Button);

            beButton = CreateButton("BE", double.NaN, BlueBg, BlueFg, BlueBorder);
            beButton.Margin = new Thickness(0, 2, 0, 0);
            beButton.Click += (s, e) => { SendCommand("SET_BE"); TriggerGlow(BlueFg); };
            rightCol.Children.Add(beButton);

            tr1Button = CreateButton("TR1", double.NaN, CyanBg, CyanFg, CyanBorder);
            tr1Button.Margin = new Thickness(0, 2, 0, 0);
            tr1Button.Click += (s, e) => { SendCommand("SET_TRAIL|1"); TriggerGlow(CyanFg); };
            rightCol.Children.Add(tr1Button);

            tr2Button = CreateButton("TR2", double.NaN, CyanBg, CyanFg, CyanBorder);
            tr2Button.Margin = new Thickness(0, 2, 0, 0);
            tr2Button.Click += (s, e) => { SendCommand("SET_TRAIL|2"); TriggerGlow(CyanFg); };
            rightCol.Children.Add(tr2Button);

            flattenButton = CreateButton("FLATTEN", double.NaN, RedBg, RedFg, RedBorder);
            flattenButton.Margin = new Thickness(0, 2, 0, 0);
            flattenButton.FontWeight = FontWeights.Bold;
            flattenButton.Click += (s, e) => { SendCommand("FLATTEN"); TriggerGlow(RedFg); };
            rightCol.Children.Add(flattenButton);

            Grid.SetColumn(rightCol, 1);
            mainGrid.Children.Add(rightCol);

            stack.Children.Add(mainGrid);

            // Price monitor row
            Grid priceRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            priceRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            lastPriceText = new TextBlock
            {
                Text = "6961.25",
                Foreground = GreenFg,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            priceRow.Children.Add(lastPriceText);
            stack.Children.Add(priceRow);

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
            StackPanel stack = new StackPanel { Margin = new Thickness(4, 1, 4, 1) };

            stack.Children.Add(CreateSectionHeader("SECTION 2: TELEMETRY"));

            // OR5 line
            or5Text = new TextBlock
            {
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 1, 0, 1)
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
            StackPanel emaRow2 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 1) };
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
            StackPanel stack = new StackPanel { Margin = new Thickness(4, 1, 4, 1) };

            stack.Children.Add(CreateSectionHeader("SECTION 3: CONFIG"));

            // Mode chips: ORB RMA RETEST MOMO FFMA TREND
            WrapPanel modePanel = new WrapPanel { Margin = new Thickness(0, 1, 0, 2) };

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
            StackPanel countRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
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
            StackPanel svRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
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
            t3Row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            t3Row.Children.Add(new TextBlock { Text = "      T3", Foreground = OrangeFg, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT3Val = CreateTextBox(28, "3.0"); svT3Val.Height = 18; svT3Val.FontSize = 9;
            t3Row.Children.Add(svT3Val);
            svT3Type = CreateCombo(38, new[] { "ATR", "Ticks", "Pts" }); svT3Type.Height = 18; svT3Type.FontSize = 8; svT3Type.Margin = new Thickness(2, 0, 0, 0);
            t3Row.Children.Add(svT3Type);
            stack.Children.Add(t3Row);

            // T4 [4.0] ATR (hidden by default)
            t4Row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2), Visibility = Visibility.Collapsed };
            t4Row.Children.Add(new TextBlock { Text = "      T4", Foreground = PurpleFg, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT4Val = CreateTextBox(28, "4.0"); svT4Val.Height = 18; svT4Val.FontSize = 9;
            t4Row.Children.Add(svT4Val);
            svT4Type = CreateCombo(38, new[] { "ATR", "Ticks", "Pts" }); svT4Type.Height = 18; svT4Type.FontSize = 8; svT4Type.Margin = new Thickness(2, 0, 0, 0);
            t4Row.Children.Add(svT4Type);
            stack.Children.Add(t4Row);

            // T5 [5.0] ATR (hidden by default)
            t5Row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2), Visibility = Visibility.Collapsed };
            t5Row.Children.Add(new TextBlock { Text = "      T5", Foreground = CyanFg, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            svT5Val = CreateTextBox(28, "5.0"); svT5Val.Height = 18; svT5Val.FontSize = 9;
            t5Row.Children.Add(svT5Val);
            svT5Type = CreateCombo(38, new[] { "ATR", "Ticks", "Pts" }); svT5Type.Height = 18; svT5Type.FontSize = 8; svT5Type.Margin = new Thickness(2, 0, 0, 0);
            t5Row.Children.Add(svT5Type);
            stack.Children.Add(t5Row);

            // STR: $150 [ATR]  MAX: $1200  CIT: $150
            StackPanel riskRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };

            riskRow.Children.Add(new TextBlock { Text = "STR:", Foreground = OrangeFg, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            strVal = CreateTextBox(28, "$150"); strVal.Height = 18; strVal.FontSize = 9; strVal.Foreground = OrangeFg;
            riskRow.Children.Add(strVal);
            svStrType = CreateCombo(35, new[] { "ATR", "Ticks", "Pts", "OR" }); svStrType.Height = 18; svStrType.FontSize = 8; svStrType.Margin = new Thickness(2, 0, 4, 0);
            riskRow.Children.Add(svStrType);

            riskRow.Children.Add(new TextBlock { Text = "MAX:", Foreground = OrangeFg, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            maxVal = CreateTextBox(35, "$1200"); maxVal.Height = 18; maxVal.FontSize = 9; maxVal.Foreground = OrangeFg;
            riskRow.Children.Add(maxVal);

            stack.Children.Add(riskRow);

            // CIT Row
            StackPanel citRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            citRow.Children.Add(new TextBlock { Text = "CIT:", Foreground = TextMuted, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) });
            citVal = CreateTextBox(35, "$150"); citVal.Height = 18; citVal.FontSize = 9;
            citRow.Children.Add(citVal);
            stack.Children.Add(citRow);

            // SYNC ALL button
            syncAllButton = CreateButton("SYNC ALL", double.NaN, CyanBg, CyanFg, CyanBorder);
            syncAllButton.Height = 22;
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
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
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
            btn.HorizontalAlignment = HorizontalAlignment.Stretch;
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

            // Send SET_MODE command to strategy - it will reply with that mode's CONFIG
            SendCommand($"SET_MODE|{mode}");
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

            // Update visibility of target input rows based on count
            UpdateTargetVisibility(count);
        }

        private void UpdateTargetVisibility(int count)
        {
            // T1 always visible (count >= 1)
            // T2 controls enabled if count >= 2
            // T3 row visible if count >= 3
            // T4 row visible if count >= 4
            // T5 row visible if count >= 5

            if (svT2Val != null) svT2Val.IsEnabled = count >= 2;
            if (svT2Type != null) svT2Type.IsEnabled = count >= 2;
            if (svT3Val != null) svT3Val.IsEnabled = count >= 3;
            if (svT3Type != null) svT3Type.IsEnabled = count >= 3;
            if (svT4Val != null) svT4Val.IsEnabled = count >= 4;
            if (svT4Type != null) svT4Type.IsEnabled = count >= 4;
            if (svT5Val != null) svT5Val.IsEnabled = count >= 5;
            if (svT5Type != null) svT5Type.IsEnabled = count >= 5;

            // Show/hide rows based on count
            if (t3Row != null) t3Row.Visibility = count >= 3 ? Visibility.Visible : Visibility.Collapsed;
            if (t4Row != null) t4Row.Visibility = count >= 4 ? Visibility.Visible : Visibility.Collapsed;
            if (t5Row != null) t5Row.Visibility = count >= 5 ? Visibility.Visible : Visibility.Collapsed;
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
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI"), // Premium Header Font
                Margin = new Thickness(0, 0, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
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
                Height = 20,
                Padding = new Thickness(4, 0, 4, 0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
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
                Height = 20,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
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
                Height = 18,
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
                Height = 18,
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
            sb.Append($"T4:{svT4Val?.Text ?? "4.0"};T4TYPE:{(svT4Type?.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"T5:{svT5Val?.Text ?? "5.0"};T5TYPE:{(svT5Type?.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"STR:{strVal.Text};STRTYPE:{(svStrType?.SelectedItem as ComboBoxItem)?.Content ?? "ATR"};");
            sb.Append($"MAX:{maxVal.Text};CIT:{citVal?.Text ?? "$150"};");

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

        // V12.3: Cache OR values for display
        private string cachedORH = "0.00";
        private string cachedORL = "0.00";

        private void ParseTelemetry(string data)
        {
            // V12.3: Parse telemetry data from strategy
            // Format: EMA9:val;EMA15:val;EMA30:val;EMA65:val;EMA200:val;ORH:val;ORL:val;ATR:val
            foreach (string pair in data.Split(';'))
            {
                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;

                string key = kv[0].Trim().ToUpper();
                string value = kv[1].Trim();

                switch (key)
                {
                    case "EMA9":
                        UpdateEmaText(ema9Text, "9:", value);
                        break;
                    case "EMA15":
                        UpdateEmaText(ema15Text, "15:", value);
                        break;
                    case "EMA30":
                        UpdateEmaText(ema30Text, "30:", value);
                        break;
                    case "EMA65":
                        UpdateEmaText(ema65Text, "65:", value);
                        break;
                    case "EMA200":
                        UpdateEmaText(ema200Text, "200:", value);
                        break;
                    case "ATR":
                        if (atrText != null) atrText.Text = "ATR: " + value;
                        break;
                    case "ORH":
                        UpdateORText(or5Text, or15Text, "ORH", value);
                        break;
                    case "ORL":
                        UpdateORText(or5Text, or15Text, "ORL", value);
                        break;
                }
            }
        }

        /// <summary>
        /// V12.3: Helper to update EMA TextBlock with label:value format
        /// </summary>
        private void UpdateEmaText(TextBlock tb, string label, string value)
        {
            if (tb == null) return;
            tb.Inlines.Clear();
            tb.Inlines.Add(new System.Windows.Documents.Run(label) { Foreground = TextMuted });
            tb.Inlines.Add(new System.Windows.Documents.Run(value) { Foreground = TextPrimary });
        }

        /// <summary>
        /// V12.3: Helper to update OR TextBlocks with H/L values
        /// </summary>
        private void UpdateORText(TextBlock or5Block, TextBlock or15Block, string key, string value)
        {
            if (key == "ORH") cachedORH = value;
            else if (key == "ORL") cachedORL = value;

            // Calculate range
            double h = 0, l = 0;
            double.TryParse(cachedORH, out h);
            double.TryParse(cachedORL, out l);
            double range = h - l;

            // Update OR5 line (placeholder - actual 5-min OR would need separate tracking)
            if (or5Block != null)
            {
                or5Block.Inlines.Clear();
                or5Block.Inlines.Add(new System.Windows.Documents.Run("OR5: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(cachedORH) { Foreground = OrangeFg });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(cachedORL) { Foreground = OrangeFg });
                or5Block.Inlines.Add(new System.Windows.Documents.Run(string.Format(" (R: {0:F1})", range)) { Foreground = TextMuted });
            }

            // Update OR15 line (uses same data for now - could be enhanced for 15-min OR)
            if (or15Block != null)
            {
                or15Block.Inlines.Clear();
                or15Block.Inlines.Add(new System.Windows.Documents.Run("OR15: ") { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(cachedORH) { Foreground = OrangeFg });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(" | ") { Foreground = TextMuted });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(cachedORL) { Foreground = OrangeFg });
                or15Block.Inlines.Add(new System.Windows.Documents.Run(string.Format(" (R: {0:F1})", range)) { Foreground = TextMuted });
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
                    case "T1": if (svT1Val != null) svT1Val.Text = value; break;
                    case "T1TYPE": SelectComboByValue(svT1Type, value); break;
                    case "T2": if (svT2Val != null) svT2Val.Text = value; break;
                    case "T2TYPE": SelectComboByValue(svT2Type, value); break;
                    case "T3": if (svT3Val != null) svT3Val.Text = value; break;
                    case "T3TYPE": SelectComboByValue(svT3Type, value); break;
                    case "T4": if (svT4Val != null) svT4Val.Text = value; break;
                    case "T4TYPE": SelectComboByValue(svT4Type, value); break;
                    case "T5": if (svT5Val != null) svT5Val.Text = value; break;
                    case "T5TYPE": SelectComboByValue(svT5Type, value); break;
                    case "STR": if (strVal != null) strVal.Text = value; break;
                    case "STRTYPE": SelectComboByValue(svStrType, value); break;
                    case "MAX": if (maxVal != null) maxVal.Text = value; break;
                    case "CIT": if (citVal != null) citVal.Text = value; break;
                    case "COUNT":
                        if (int.TryParse(value, out int count))
                        {
                            selectedTargetCount = count;
                            // Update count button visuals
                            foreach (var btn in new[] { cnt1, cnt2, cnt3, cnt4, cnt5 })
                            {
                                if (btn == null) continue;
                                btn.Background = BtnBg;
                                btn.Foreground = TextPrimary;
                                btn.BorderBrush = BtnBorder;
                            }
                            Button targetBtn = null;
                            switch(count)
                            {
                                case 1: targetBtn = cnt1; break;
                                case 2: targetBtn = cnt2; break;
                                case 3: targetBtn = cnt3; break;
                                case 4: targetBtn = cnt4; break;
                                case 5: targetBtn = cnt5; break;
                            }
                            if (targetBtn != null)
                            {
                                targetBtn.Background = CyanBg;
                                targetBtn.Foreground = CyanFg;
                                targetBtn.BorderBrush = CyanBorder;
                            }
                            UpdateTargetVisibility(count);
                        }
                        break;
                }
            }
        }

        private void SelectComboByValue(ComboBox combo, string value)
        {
            if (combo == null || string.IsNullOrEmpty(value)) return;

            string normalizedValue = value.ToUpper().Trim();

            // Mapping for User Friendliness / Legacy Support
            if (normalizedValue == "OR" || normalizedValue == "OPENRANGE" || normalizedValue == "OPEN RANGE")
                normalizedValue = "OR";
            else if (normalizedValue == "ATR" || normalizedValue == "A")
                normalizedValue = "ATR";
            else if (normalizedValue == "TICKS" || normalizedValue == "T")
                normalizedValue = "Ticks";
            else if (normalizedValue == "PTS" || normalizedValue == "POINTS" || normalizedValue == "P")
                normalizedValue = "Pts";

            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Content.ToString().ToUpper() == normalizedValue.ToUpper() ||
                    (normalizedValue == "OR" && item.Content.ToString() == "OR") ||
                    (normalizedValue == "ATR" && item.Content.ToString() == "ATR"))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }

        #endregion
    }
}
