using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Controls; // For Button
using System.Windows; // For Thickness, etc.

namespace V12_ExternalRemote
{
    public partial class MainWindow : Window
    {
        private string hubIp = "127.0.0.1";
        private int hubPort = 5000;
        private TcpClient client;
        private TosRtdClient _rtdClient;

        // Persistent TCP Client (prevents port exhaustion)
        private TcpClient _hubClient;
        private NetworkStream _hubStream;
        private readonly System.Threading.SemaphoreSlim _tcpLock = new System.Threading.SemaphoreSlim(1, 1);
        private Task _responseListenerTask;
        private System.Threading.CancellationTokenSource _responseCts;
        
        // Multi-Symbol Logic
        private Dictionary<string, SymbolData> _symbolCache = new Dictionary<string, SymbolData>();
        private string _activeSymbol = "MES"; // Currently DISPLAYED symbol
        
        public class SymbolData
        {
            public string Last { get; set; } = "...";
            public string Ema9 { get; set; } = "---";
            public string Ema15 { get; set; } = "---";
            public string Ema30 { get; set; } = "---";
            public string Ema65 { get; set; } = "---";
            public string Ema200 { get; set; } = "---";
            public string OrHigh { get; set; } = "---";
            public string OrLow { get; set; } = "---";
            public string Or15High { get; set; } = "---";
            public string Or15Low { get; set; } = "---";
            public string Flag5m { get; set; } = "---";
            public string Flag15m { get; set; } = "---";
            public string Flag1h { get; set; } = "---";
            public double LastVal { get; set; } = 0;
            public Button TabButton { get; set; }
        }

        private string _logPath = "v9_remote_log.txt";
        private V9_ExternalRemote_TCP_Server _server;

        // V12.1: RMA Mode Toggles
        private bool _isRetestRma = false;
        private bool _isTrendRma = false;

        // V12.2: Dynamic Symbol Chips
        private bool _suppressEvents = false; // Prevents feedback loops
        private List<string> _savedSymbols = new List<string> { "MES", "MGC" }; // Defaults

        // V12.3: Debounce timer for config changes (prevents IPC spam)
        private System.Windows.Threading.DispatcherTimer _configDebounceTimer;
        private const int DEBOUNCE_MS = 500;

        // V12.3: Async logging queue to prevent UI blocking
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _logQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();
        private bool _isLogging = false;

        // V12.2: SIMA Fleet Tracking
        private System.Collections.ObjectModel.ObservableCollection<AccountFleetItem> _fleetAccounts = new System.Collections.ObjectModel.ObservableCollection<AccountFleetItem>();

        public MainWindow()
        {
            InitializeComponent();
            
            // V12.1: Restore local state (last selected mode/count) BEFORE anything else
            LoadLocalState();

            // Global Safety
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                MessageBox.Show("V9 Remote Error: " + e.ExceptionObject.ToString());
            };

            // V9_010 FINAL: NinjaTrader is now the Server. WPF app is the Client.
            // Disabling internal server to prevent port conflict.
            try
            {
                // V12: Listen on Port 5001 for Strategy Config
                _server = new V9_ExternalRemote_TCP_Server(5001);
                _server.OnConfigReceived += (msg) => this.Dispatcher.Invoke(() => ProcessConfigMessage(msg));
                _server.Start();
                LogToFile($"TCP Server started on port 5001");
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to start TCP Server: {ex.Message}");
            }

            InitializeTosRtd();
            HubStatusLed.Background = Brushes.Gray;

            // V12.3: Initialize debounce timer for config changes
            _configDebounceTimer = new System.Windows.Threading.DispatcherTimer();
            _configDebounceTimer.Interval = TimeSpan.FromMilliseconds(DEBOUNCE_MS);
            _configDebounceTimer.Tick += (s, args) => {
                _configDebounceTimer.Stop();
                SendConfigUpdate("DEBOUNCED");
            };

            // V12.3: Wire up Config Change Events ONCE here (was incorrectly in LogToFile causing memory leak)
            this.Loaded += (s, e) => {
                WireConfigChangeEvents();
                // Request Fleet List on load
                Task.Run(async () => await SendCommand("GET_FLEET"));
            };
        }

        private void WireConfigChangeEvents()
        {
            // V12.3: Wire up config change events (runs ONCE at startup, not every log call)
            if (ConfCitVal != null) ConfCitVal.TextChanged += OnConfigTextChanged;
            if (ConfCitActive != null) ConfCitActive.Click += (s, e) => DebouncedConfigSync();
            if (ConfOrderType != null) ConfOrderType.SelectionChanged += (s, e) => DebouncedConfigSync();

            // Wire types to ensure persistence
            if (ConfT1Type != null) ConfT1Type.SelectionChanged += (s, e) => DebouncedConfigSync();
            if (ConfT2Type != null) ConfT2Type.SelectionChanged += (s, e) => DebouncedConfigSync();
            if (ConfT3Type != null) ConfT3Type.SelectionChanged += (s, e) => DebouncedConfigSync();
            if (ConfT4Type != null) ConfT4Type.SelectionChanged += (s, e) => DebouncedConfigSync();
            if (ConfT5Type != null) ConfT5Type.SelectionChanged += (s, e) => DebouncedConfigSync();
            if (ConfStrType != null) ConfStrType.SelectionChanged += (s, e) => DebouncedConfigSync();

            // V12.3: Wire text boxes with debounce
            if (ConfT1Val != null) ConfT1Val.TextChanged += OnConfigTextChanged;
            if (ConfT2Val != null) ConfT2Val.TextChanged += OnConfigTextChanged;
            if (ConfT3Val != null) ConfT3Val.TextChanged += OnConfigTextChanged;
            if (ConfT4Val != null) ConfT4Val.TextChanged += OnConfigTextChanged;
            if (ConfT5Val != null) ConfT5Val.TextChanged += OnConfigTextChanged;
            if (ConfStrVal != null) ConfStrVal.TextChanged += OnConfigTextChanged;
            if (ConfMaxVal != null) ConfMaxVal.TextChanged += OnConfigTextChanged;
        }
        private void OnConfigTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressEvents) return;
            DebouncedConfigSync();
        }

        private void DebouncedConfigSync()
        {
            if (_suppressEvents) return;

            // Save to local config immediately (no delay)
            SaveConfigUI(_selectedConfigMode, _targetCount);

            // Reset debounce timer - only send IPC after 500ms of inactivity
            _configDebounceTimer.Stop();
            _configDebounceTimer.Start();
        }

        private void LoadLocalState()
        {
            try
            {
                string path = "v9_remote_state.txt";
                if (System.IO.File.Exists(path))
                {
                    string content = System.IO.File.ReadAllText(path);
                    var parts = content.Split('|');
                    if (parts.Length >= 2)
                    {
                        _selectedConfigMode = parts[0];
                        int.TryParse(parts[1], out _targetCount);
                    }
                    if (parts.Length >= 3)
                    {
                        var syms = parts[2].Split(',');
                        _savedSymbols.Clear();
                        foreach (var s in syms) 
                            if (!string.IsNullOrWhiteSpace(s)) _savedSymbols.Add(s);
                    }
                    LogToFile($"RESTORED STATE: Mode={_selectedConfigMode}, Count={_targetCount}, Syms={_savedSymbols.Count}");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Failed to load local state: {ex.Message}");
            }
            // Render chips after load
            RenderSymbolButtons();
        }

        private void SaveLocalState()
        {
            try
            {
                string path = "v9_remote_state.txt";
                string symList = string.Join(",", _savedSymbols);
                System.IO.File.WriteAllText(path, $"{_selectedConfigMode}|{_targetCount}|{symList}");
            }
            catch { }
        }

        private void SymbolInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string newSym = SymbolInput.Text.Trim().ToUpper();
                if (!string.IsNullOrEmpty(newSym))
                {
                    AddSymbol(newSym);
                    SetSymbol_Click(null, null); // Actually switch to it
                    SymbolInput.Text = ""; // Clear for next entry
                }
            }
        }

        private void AddSymbol(string sym)
        {
            if (!_savedSymbols.Contains(sym))
            {
                _savedSymbols.Add(sym);
                RenderSymbolButtons();
                SaveLocalState();
            }
        }

        private void RemoveSymbol(string sym)
        {
            if (_savedSymbols.Contains(sym))
            {
                _savedSymbols.Remove(sym);
                RenderSymbolButtons();
                SaveLocalState();
            }
        }

        private void RenderSymbolButtons()
        {
            DynamicSymbolPanel.Children.Clear();
            foreach (var sym in _savedSymbols)
            {
                bool isActive = sym == _activeSymbol;
                
                // Chip Container
                Grid grid = new Grid { Margin = new Thickness(0,0,4,0) };
                
                // Main Button (Selects Symbol)
                Button btn = new Button
                {
                    Content = sym,
                    Height = 22,
                    Foreground = isActive ? Brushes.White : Brushes.Gray,
                    Background = new SolidColorBrush(Color.FromRgb(23, 23, 23)), // #171717
                    BorderThickness = new Thickness(1),
                    BorderBrush = isActive ? Brushes.Cyan : new SolidColorBrush(Color.FromRgb(38, 38, 38)) // #262626
                };
                
                // Custom Template for Button to match Style but dynamically
                // Applying Style via code is tricky with StaticResource, setting properties directly is safer here
                if (isActive) 
                {
                    btn.FontWeight = FontWeights.Bold;
                    btn.Background = new SolidColorBrush(Color.FromRgb(30,30,30));
                }

                btn.Click += (s, e) => 
                {
                    SymbolInput.Text = sym;
                    SetSymbol_Click(null, null); // Switch logic
                    RenderSymbolButtons(); // Re-render to update highlights
                };

                // Close 'X' Button
                Button closeBtn = new Button
                {
                    Content = "×", // Multiplication sign looks better than X
                    Width = 12, Height = 12,
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -4, -4, 0),
                    Background = Brushes.Red,
                    Foreground = Brushes.White,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0)
                };
                // Make it circular roughly
                closeBtn.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"
                    <ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>
                        <Border Background='{TemplateBinding Background}' CornerRadius='6'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                    </ControlTemplate>");

                closeBtn.Click += (s, e) => RemoveSymbol(sym);

                grid.Children.Add(btn);
                grid.Children.Add(closeBtn);
                DynamicSymbolPanel.Children.Add(grid);
            }
        }

        private void InitializeTosRtd()
        {
            LogToFile($"--- REMOTE APP START ({DateTime.Now}) ---");
            LogToFile($"APP DIRECTORY: {AppDomain.CurrentDomain.BaseDirectory}");
            _rtdClient = new TosRtdClient(this.Dispatcher);
            
            _rtdClient.OnDataUpdate += (key, value) => {
                UpdatePriceDisplay(key, value);
            };

            _rtdClient.OnConnectionStatusChanged += (connected) => {
                this.Dispatcher.Invoke(() => {
                    TosStatusLed.Background = connected ? Brushes.Lime : Brushes.Red;
                    LogToFile($"TOS STATUS: {(connected ? "CONNECTED" : "DISCONNECTED")}");
                });
            };

            _rtdClient.Start();
            
            // Initial subscription
            SubscribeToSymbol(_activeSymbol);
        }

        private void SubscribeToSymbol(string symbol)
        {
            // Normalize
            symbol = symbol.ToUpper();
            if (_symbolCache.ContainsKey(symbol))
            {
                SwitchToSymbol(symbol);
                return;
            }

            LogToFile($"Adding symbol: {symbol}");

            // Create Data Entry
            var data = new SymbolData();
            
            // Create UI Tab
            var btn = new Button
            {
                Content = symbol,
                FontSize = 9,
                Margin = new Thickness(0, 0, 2, 0),
                Padding = new Thickness(5, 2, 5, 2),
                Background = Brushes.Transparent,
                Foreground = Brushes.Gray,
                BorderThickness = new Thickness(0)
            };
            
            // Style hack: apply basic props, we'll handle active state manually
            btn.Click += (s, e) => SwitchToSymbol(symbol);
            
            data.TabButton = btn;
            _symbolCache[symbol] = data;
            WatchlistPanel.Children.Add(btn);

            // Subscribe
            string exchange = GetExchange(symbol);
            string fullSymbol = $"/{symbol}:{exchange}";
            
            LogToFile($"Subscribing all studies for {symbol}. Full={fullSymbol}");

            // Correct Mapping based on Shotgun Discovery:
            // CUSTOM4  -> EMA9
            // CUSTOM6  -> EMA15
            // CUSTOM10 -> OR HIGH
            // CUSTOM12 -> OR LOW (Assumed based on pattern, though not seen in snippets yet)

            // ALL subscriptions must use fullSymbol (e.g., /MES:XCME)
            // The "loading" issue is now handled by the "Sticky Data" filter in UpdatePriceDisplay.

            _rtdClient.Subscribe($"{symbol}:LAST", new object[] { "LAST", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:EMA9", new object[] { "Custom1", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:EMA15", new object[] { "Custom2", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:EMA30", new object[] { "CUSTOM8", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:EMA65", new object[] { "CUSTOM19", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:EMA200", new object[] { "CUSTOM18", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:ORHIGH", new object[] { "CUSTOM9", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:ORLOW", new object[] { "CUSTOM11", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:OR15HIGH", new object[] { "CUSTOM13", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:OR15LOW", new object[] { "CUSTOM15", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:FLAG5M", new object[] { "CUSTOM14", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:FLAG15M", new object[] { "CUSTOM16", fullSymbol });
            _rtdClient.Subscribe($"{symbol}:FLAG1H", new object[] { "CUSTOM20", fullSymbol });

            // Safety catch: Try XNYM for metals if XCEC fails (Gold/Silver have odd routing sometimes)
            // But log indicated MGC worked on XCEC for Discovery. Sticking to plan.

            SwitchToSymbol(symbol);
        }

        private void SwitchToSymbol(string symbol)
        {
            _activeSymbol = symbol;
            
            // Update Tabs UI
            foreach (var kvp in _symbolCache)
            {
                if (kvp.Value.TabButton != null)
                {
                    bool isActive = kvp.Key == symbol;
                    kvp.Value.TabButton.Foreground = isActive ? Brushes.Cyan : Brushes.Gray;
                    kvp.Value.TabButton.FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal;
                }
            }

            // Refresh Main Display from Cache
            if (_symbolCache.TryGetValue(symbol, out var data))
            {
                LastPriceTxt.Text = data.Last;
                Ema9Txt.Text = data.Ema9;
                Ema15Txt.Text = data.Ema15;
                Ema30Txt.Text = data.Ema30;
                Ema65Txt.Text = data.Ema65;
                Ema200Txt.Text = data.Ema200;
                OrHighTxt.Text = data.OrHigh;
                OrLowTxt.Text = data.OrLow;
                Flag5mVal.Text = data.Flag5m;
                Flag15mVal.Text = data.Flag15m;
                Flag1hVal.Text = data.Flag1h;
            }

            // Request Configuration from Strategy for this symbol
            // V12.1 FIX: Use GET_LAYOUT to force Strategy to match our Local Mode/Count
            // This prevents "Default ORB" data from overwriting our RMA view on connection
            string mode = _selectedConfigMode ?? "ORB";
            Task.Run(async () => await SendCommand($"GET_LAYOUT|{_activeSymbol}|{mode}|{_targetCount}"));
        }

        private void UpdatePriceDisplay(string key, object value)
        {
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (value == null) return;
                    string valStr = value.ToString();
                    
                    // Trace every single update at the UI level
                    LogToFile($"UI RECV: {key} = {valStr}"); 
                    
                    if (key.Contains("DISCO"))
                    {
                         LogToFile($"DISCOVERY: {key} = {valStr}");
                    }

                    if (valStr == "#N/A" || string.IsNullOrWhiteSpace(valStr) || valStr.ToLower().Contains("loading")) return;

                    // Parse Key: SYMBOL:FIELD
                    string[] parts = key.Split(':');
                    if (parts.Length < 2) return;
                    
                    string symbol = parts[0];
                    string field = parts[1];

                    if (!_symbolCache.ContainsKey(symbol)) return;
                    var data = _symbolCache[symbol];

                    if (!double.TryParse(valStr, out double val)) return;
                    
                    // Specific check for 0.00 - indicators like EMA/OR rarely hit exactly zero.
                    // If it's 0.00, it might be an uninitialized state in TOS.
                    if (val == 0 && (field != "LAST")) return; 

                    string fmtVal = val.ToString("F2");

                    // Update Cache
                    if (field == "LAST") { data.Last = fmtVal; data.LastVal = val; }
                    else if (field == "EMA9") data.Ema9 = fmtVal;
                    else if (field == "EMA15") data.Ema15 = fmtVal;
                    else if (field == "EMA30") data.Ema30 = fmtVal;
                    else if (field == "EMA65") data.Ema65 = fmtVal;
                    else if (field == "EMA200") data.Ema200 = fmtVal;
                    else if (field == "ORHIGH") data.OrHigh = fmtVal;
                    else if (field == "ORLOW") data.OrLow = fmtVal;
                    else if (field == "OR15HIGH") data.Or15High = fmtVal;
                    else if (field == "OR15LOW") data.Or15Low = fmtVal;
                    else if (field == "FLAG5M") data.Flag5m = fmtVal;
                    else if (field == "FLAG15M") data.Flag15m = fmtVal;
                    else if (field == "FLAG1H") data.Flag1h = fmtVal;

                    // Update UI ONLY if active
                    if (symbol == _activeSymbol)
                    {
                         if (field == "LAST") LastPriceTxt.Text = fmtVal;
                         else if (field == "EMA9") Ema9Txt.Text = fmtVal;
                          else if (field == "EMA15") Ema15Txt.Text = fmtVal;
                          else if (field == "EMA30") Ema30Txt.Text = fmtVal;
                          else if (field == "EMA65") Ema65Txt.Text = fmtVal;
                          else if (field == "EMA200") Ema200Txt.Text = fmtVal;
                          else if (field == "ORHIGH") { OrHighTxt.Text = fmtVal; UpdateOrRange(); }
                          else if (field == "ORLOW") { OrLowTxt.Text = fmtVal; UpdateOrRange(); }
                          else if (field == "OR15HIGH") Or15HighTxt.Text = fmtVal;
                          else if (field == "OR15LOW") Or15LowTxt.Text = fmtVal;
                          else if (field == "FLAG5M") UpdateFlag(Flag5m, Flag5mVal, val);
                          else if (field == "FLAG15M") UpdateFlag(Flag15m, Flag15mVal, val);
                          else if (field == "FLAG1H") Update1HTrend(val);
                    }
                    
                    // MTF Logic (simplified for now, attached to active symbol mostly or global)
                    // For now, let's skip complex MTF flag parsing unless it's strictly required
                }
                catch { }
            });
        }
        
        private static readonly Brush RedFlag = new SolidColorBrush(Color.FromArgb(60, 255, 0, 0));
        private static readonly Brush GreenFlag = new SolidColorBrush(Color.FromArgb(60, 0, 255, 0));

        private void Update1HTrend(double level)
        {
            try
            {
                double lastPrice = 0;
                double.TryParse(LastPriceTxt.Text, out lastPrice);

                // Update the Flag Cluster (Right Sidebar)
                Flag1hVal.Text = level.ToString("F2");
                Flag1h.Background = (lastPrice > level) ? GreenFlag : RedFlag;

                // Update the Central Multi-Row Indicator
                if (lastPrice > level)
                {
                    Trend1hVal.Text = "BULL";
                    Trend1hVal.Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128)); // #4ade80
                    Trend1hBorder.Background = new SolidColorBrush(Color.FromArgb(40, 6, 78, 59)); // #064e3b with alpha
                }
                else
                {
                    Trend1hVal.Text = "BEAR";
                    Trend1hVal.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)); // #f87171
                    Trend1hBorder.Background = new SolidColorBrush(Color.FromArgb(40, 69, 10, 10)); // #450a0a with alpha
                }
            }
            catch { }
        }

        private void UpdateOrRange()
        {
            try
            {
                double high = 0, low = 0;
                double.TryParse(OrHighTxt.Text, out high);
                double.TryParse(OrLowTxt.Text, out low);

                if (high > 0 && low > 0)
                {
                    double range = high - low;
                    OrRangeTxt.Text = range.ToString("F2");
                }
            }
            catch { }
        }

        private void UpdateFlag(System.Windows.Controls.Border flagBorder, System.Windows.Controls.TextBlock flagText, double level)
        {
            try
            {
                double lastPrice = 0;
                double.TryParse(LastPriceTxt.Text, out lastPrice);
                
                flagText.Text = level.ToString("F2");
                
                // Color logic: Green if price is above, Red if below
                flagBorder.Background = (lastPrice > level) ? GreenFlag : RedFlag;
            }
            catch { }
        }

        private void SetSymbol_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(SymbolInput.Text)) return;
                
                string newSym = SymbolInput.Text.Trim().ToUpper();
                SubscribeToSymbol(newSym);
                TriggerGlow(Brushes.Cyan);
                SymbolInput.Text = ""; // Clear input after adding
            }
            catch { }
        }
        
        private string GetExchange(string symbol)
        {
            // Map common futures roots to their exchanges based on successful Shotgun Test V3 results
            string root = symbol.Length >= 2 ? symbol.Substring(0, 2).ToUpper() : symbol.ToUpper();
            
            // CME (Equities like S&P, Nasdaq)
            if (root == "ES" || root == "ME" || root == "NQ" || root == "MN" || root == "RT") 
                return "XCME";
            
            // COMEX (Gold, Silver, Copper)
            if (root == "GC" || root == "MG" || root == "SI" || root == "HG")
                return "XCEC";
            
            // NYMEX (Crude Oil)
            if (root == "CL" || root == "QM")
                return "XNYM";

            // CBOT (Dow, Treasuries, Grains)
            if (root == "YM" || root == "ZN" || root == "ZB" || root == "ZC" || root == "ZS" || root == "ZW")
                return "XCBT";
            
            // Default to CME for unknown
            return "XCME";
        }

        private async Task<bool> TestHubConnection()
        {
            try
            {
                using (var testClient = new TcpClient())
                {
                    var connectTask = testClient.ConnectAsync(hubIp, hubPort);
                    if (await Task.WhenAny(connectTask, Task.Delay(500)) == connectTask)
                    {
                        await connectTask;
                        return true;
                    }
                    return false;
                }
            }
            catch { return false; }
        }

        private async Task EnsureConnectionAsync()
        {
            // Check if we need to (re)connect
            if (_hubClient != null && _hubClient.Connected && _hubStream != null)
                return;

            // Cleanup any dead connection
            if (_hubClient != null)
            {
                try { _responseCts?.Cancel(); } catch { }
                try { _hubStream?.Dispose(); } catch { }
                try { _hubClient?.Close(); } catch { }
                _hubStream = null;
                _hubClient = null;
            }

            // Establish new connection
            _hubClient = new TcpClient();
            var connectTask = _hubClient.ConnectAsync(hubIp, hubPort);
            if (await Task.WhenAny(connectTask, Task.Delay(2000)) != connectTask)
            {
                _hubClient?.Close();
                _hubClient = null;
                LogToFile("HUB CONNECT: Timeout after 2s");
                throw new Exception("Connection timeout");
            }

            await connectTask;
            _hubStream = _hubClient.GetStream();
            LogToFile($"HUB CONNECT: Established persistent connection to {hubIp}:{hubPort}");

            // V12.0: Start the response listener for persistent symmetry
            StartResponseListener();
        }

        private void StartResponseListener()
        {
            if (_responseListenerTask != null && !_responseListenerTask.IsCompleted) return;

            _responseCts = new System.Threading.CancellationTokenSource();
            _responseListenerTask = Task.Run(() => ListenForHubResponses(_responseCts.Token));
            LogToFile("IPC Response Listener STARTED");
        }

        private async Task ListenForHubResponses(System.Threading.CancellationToken ct)
        {
            byte[] buffer = new byte[4096];
            StringBuilder sb = new StringBuilder();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (_hubStream == null || !_hubClient.Connected)
                    {
                        await Task.Delay(500, ct);
                        continue;
                    }

                    if (_hubStream.DataAvailable)
                    {
                        int bytesRead = await _hubStream.ReadAsync(buffer, 0, buffer.Length, ct);
                        if (bytesRead > 0)
                        {
                            string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            sb.Append(data);

                            // Process lines (delimited by \n)
                            string content = sb.ToString();
                            int lastNewline = content.LastIndexOf('\n');
                            if (lastNewline >= 0)
                            {
                                string completeLines = content.Substring(0, lastNewline);
                                sb.Clear();
                                sb.Append(content.Substring(lastNewline + 1));

                                foreach (string line in completeLines.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    string cleanLine = line.Trim();
                                    if (string.IsNullOrEmpty(cleanLine)) continue;

                                    if (cleanLine.StartsWith("CONFIG|"))
                                    {
                                        this.Dispatcher.Invoke(() => ProcessConfigMessage(cleanLine));
                                    }
                                    // V12.2: Handle mode sync from chart panel
                                    else if (cleanLine.StartsWith("SYNC_MODE|"))
                                    {
                                        string mode = cleanLine.Substring("SYNC_MODE|".Length).Trim().ToUpper();
                                        this.Dispatcher.Invoke(() => SyncConfigMode(mode));
                                        LogToFile($"MODE SYNC RX: {mode}");
                                    }
                                }

                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(100, ct);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogToFile($"IPC Listener Error: {ex.Message}");
            }
            finally
            {
                LogToFile("IPC Response Listener STOPPED");
            }
        }

        private async Task SendCommand(string cmd)
        {
            await _tcpLock.WaitAsync();
            try
            {
                LogToFile($"OUT: {cmd}"); // V12.1: Audit trail

                // Ensure we have a live connection (reuses existing or reconnects)
                await EnsureConnectionAsync();

                // Write command to persistent stream (do NOT dispose)
                byte[] data = Encoding.UTF8.GetBytes(cmd + "\n"); // Add newline delimiter
                await _hubStream.WriteAsync(data, 0, data.Length);
                await _hubStream.FlushAsync();

                this.Dispatcher.Invoke(() => HubStatusLed.Background = Brushes.Lime);
            }
            catch (Exception ex)
            {
                LogToFile($"CMD FAIL: {cmd} | {ex.Message}");

                // Connection is broken - dispose and null so EnsureConnectionAsync reconnects next time
                try { _hubStream?.Dispose(); } catch { }
                try { _hubClient?.Close(); } catch { }
                _hubStream = null;
                _hubClient = null;

                this.Dispatcher.Invoke(() => HubStatusLed.Background = Brushes.Red);
            }
            finally
            {
                _tcpLock.Release();
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            try { _responseCts?.Cancel(); } catch { }
            _server?.Stop();
            Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DirectionCombo.SelectedItem is ComboBoxItem item)
                {
                    string action = item.Content.ToString();
                    string cmd = "";
                    
                    // Parse optional price
                    double price = 0;
                    double.TryParse(PriceInput.Text, out price);
                    string priceArg = price > 0 ? $"|{price}" : "";

                    switch (action)
                    {
                        case "OR LONG":
                            cmd = $"OR_LONG|{_activeSymbol}";
                            TriggerGlow(Brushes.Cyan);
                            SyncConfigMode("ORB");
                            break;
                        case "OR SHORT":
                            cmd = $"OR_SHORT|{_activeSymbol}";
                            TriggerGlow(Brushes.Magenta);
                            SyncConfigMode("ORB");
                            break;
                        case "RETEST":
                            cmd = _isRetestRma ? $"EXEC_RETEST_RMA|{_activeSymbol}" : $"EXEC_RETEST|{_activeSymbol}";
                            TriggerGlow(Brushes.Yellow);
                            SyncConfigMode("RETEST");
                            break;
                        case "RMA":
                            // If price is provided, execute RMA at price
                            if (price > 0)
                                cmd = $"EXEC_RMA|{_activeSymbol}{priceArg}";
                            else
                                cmd = $"MODE_RMA_NOSYNC|{_activeSymbol}"; 
                            TriggerGlow(Brushes.Orange);
                            SyncConfigMode("RMA");
                            break;
                        case "MOMO":
                            // MOMO needs price usually, or uses Close
                            cmd = $"EXEC_MOMO|{_activeSymbol}{priceArg}";
                            TriggerGlow(Brushes.Lime);
                            SyncConfigMode("MOMO");
                            break;
                        case "FFMA LIMIT":
                            cmd = $"EXEC_FFMA|{_activeSymbol}{priceArg}";
                            TriggerGlow(Brushes.Magenta);
                            SyncConfigMode("FFMA");
                            break;
                        case "TREND 9":
                            cmd = _isTrendRma ? $"TREND_9_RMA|{_activeSymbol}" : $"TREND_9|{_activeSymbol}";
                            TriggerGlow(Brushes.Cyan);
                            SyncConfigMode("TREND");
                            break;
                        case "TREND 15":
                            cmd = _isTrendRma ? $"TREND_15_RMA|{_activeSymbol}" : $"TREND_15|{_activeSymbol}";
                            TriggerGlow(Brushes.Blue);
                            SyncConfigMode("TREND");
                            break;
                    }

                    if (!string.IsNullOrEmpty(cmd))
                    {
                        await SendCommand(cmd);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Submit_Click Error: {ex.Message}");
            }
        }

        private async void Flatten_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"FLATTEN|{_activeSymbol}");
            TriggerGlow(Brushes.White);
        }

        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"CANCEL_ALL|{_activeSymbol}");
            TriggerGlow(Brushes.Red);
        }

        private async void Trim25_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"TRIM_25|{_activeSymbol}");
            TriggerGlow(Brushes.Yellow);
        }

        private async void Trim50_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"TRIM_50|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void BEPlus1_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"BE_PLUS_1|{_activeSymbol}");
            TriggerGlow(Brushes.Cyan);
        }

        // V12.2: Configurable Trail Button (replaces TR1/TR2)
        private async void Trail_Click(object sender, RoutedEventArgs e)
        {
            string trailValue = TrailInput?.Text ?? "1.0";
            await SendCommand($"EXEC_TRAIL|{_activeSymbol}|{trailValue}");
            TriggerGlow(Brushes.Yellow);
        }

        // V10.3: OR Breakout Entry Handlers
        private async void OrLong_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"OR_LONG|{_activeSymbol}");
            TriggerGlow(Brushes.Cyan);
            SyncConfigMode("ORB");
        }

        private async void OrShort_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"OR_SHORT|{_activeSymbol}");
            TriggerGlow(Brushes.Magenta);
            SyncConfigMode("ORB");
        }

        // V10.5: Button clicks open context menu
        private void T1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void T2_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void T3_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        // V10.5: T1 Target Actions
        private async void T1_Market_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T1_MARKET|{_activeSymbol}");
            TriggerGlow(Brushes.LimeGreen);
        }

        private async void T1_1pt_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T1_1PT|{_activeSymbol}");
            TriggerGlow(Brushes.LimeGreen);
        }

        private async void T1_2pt_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T1_2PT|{_activeSymbol}");
            TriggerGlow(Brushes.LimeGreen);
        }

        private async void T1_Now_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T1_NOW|{_activeSymbol}");
            TriggerGlow(Brushes.LimeGreen);
        }

        private async void T1_BE_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T1_BE|{_activeSymbol}");
            TriggerGlow(Brushes.LimeGreen);
        }

        private async void T1_Cancel_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T1_CANCEL|{_activeSymbol}");
            TriggerGlow(Brushes.Gray);
        }

        // V10.5: T2 Target Actions
        private async void T2_Market_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T2_MARKET|{_activeSymbol}");
            TriggerGlow(Brushes.Yellow);
        }

        private async void T2_1pt_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T2_1PT|{_activeSymbol}");
            TriggerGlow(Brushes.Yellow);
        }

        private async void T2_2pt_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T2_2PT|{_activeSymbol}");
            TriggerGlow(Brushes.Yellow);
        }

        private async void T2_Now_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T2_NOW|{_activeSymbol}");
            TriggerGlow(Brushes.Yellow);
        }

        private async void T2_BE_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T2_BE|{_activeSymbol}");
            TriggerGlow(Brushes.Yellow);
        }

        // ═══════════════════════════════════════════════════════════════════
        // V12 CONFIGURATION ROW LOGIC (ROW 3)
        // ═══════════════════════════════════════════════════════════════════
        
        private string _selectedConfigMode = "ORB"; // Default
        private int _targetCount = 4; // Default to 4

        public class StrategyConfig
        {
            public int TargetCount { get; set; } = 4;
            public string T1 { get; set; } = "10";
            public string T1Type { get; set; } = "Ticks";
            public string T2 { get; set; } = "20";
            public string T2Type { get; set; } = "Ticks";
            public string T3 { get; set; } = "30";
            public string T3Type { get; set; } = "Ticks";
            public string T4 { get; set; } = "40";
            public string T4Type { get; set; } = "Ticks";
            public string T5 { get; set; } = "50";
            public string T5Type { get; set; } = "Ticks";
            public string Str { get; set; } = "10";
            public string StrType { get; set; } = "Ticks";
            public string Max { get; set; } = "$1200";
            // V12.2 New Fields
            public string CitVal { get; set; } = "1";
            public bool CitActive { get; set; } = false;
            public string OrderType { get; set; } = "Limit";
        }
        
        private Dictionary<string, StrategyConfig> _configs = new Dictionary<string, StrategyConfig>();
        
        // V12.1: Store preferred Target Count per Mode
        private Dictionary<string, int> _modeTargetCounts = new Dictionary<string, int>()
        {
            { "ORB", 4 },
            { "RMA", 4 },
            { "RETEST", 4 },
            { "MOMO", 4 },
            { "FFMA", 4 },
            { "TREND", 4 }
        };

        private StrategyConfig GetConfig(string mode, int count)
        {
            string key = $"{mode}_{count}";
            if (!_configs.ContainsKey(key)) _configs[key] = new StrategyConfig { TargetCount = count };
            return _configs[key];
        }

        private void SaveConfigUI(string mode, int count)
        {
            var cfg = GetConfig(mode, count);
            cfg.TargetCount = count;
            if (ConfT1Val != null) cfg.T1 = ConfT1Val.Text;
            if (ConfT1Type != null && ConfT1Type.SelectedItem is ComboBoxItem t1c) cfg.T1Type = t1c.Content.ToString();
            
            if (ConfT2Val != null) cfg.T2 = ConfT2Val.Text;
            if (ConfT2Type != null && ConfT2Type.SelectedItem is ComboBoxItem t2c) cfg.T2Type = t2c.Content.ToString();
            
            if (ConfT3Val != null) cfg.T3 = ConfT3Val.Text;
            if (ConfT3Type != null && ConfT3Type.SelectedItem is ComboBoxItem t3c) cfg.T3Type = t3c.Content.ToString();
            
            if (ConfT4Val != null) cfg.T4 = ConfT4Val.Text;
            if (ConfT4Type != null && ConfT4Type.SelectedItem is ComboBoxItem t4c) cfg.T4Type = t4c.Content.ToString();
            
            if (ConfT5Val != null) cfg.T5 = ConfT5Val.Text;
            if (ConfT5Type != null && ConfT5Type.SelectedItem is ComboBoxItem t5c) cfg.T5Type = t5c.Content.ToString();
            
            if (ConfStrVal != null) cfg.Str = ConfStrVal.Text;
            if (ConfStrType != null && ConfStrType.SelectedItem is ComboBoxItem strc) cfg.StrType = strc.Content.ToString();
            
            if (ConfMaxVal != null) cfg.Max = ConfMaxVal.Text;
            
            // V12.2 Save
            if (ConfCitVal != null) cfg.CitVal = ConfCitVal.Text;
            if (ConfCitActive != null) cfg.CitActive = ConfCitActive.IsChecked == true;
            if (ConfOrderType != null && ConfOrderType.SelectedItem is ComboBoxItem cbo) cfg.OrderType = cbo.Content.ToString();
        }

        private void ProcessConfigMessage(string msg)
        {
            try 
            {
                LogToFile($"CONFIG RX RAW: {msg}");
                var parts = msg.Split('|');
                if (parts.Length < 3) return;
                
                string mode = parts[1].Trim().ToUpper(); 

                // V12.2: Handle Fleet List
                if (mode == "FLEET")
                {
                    _fleetAccounts.Clear();
                    for (int i = 2; i < parts.Length; i++)
                    {
                        var acctName = parts[i].Trim();
                        if (!string.IsNullOrEmpty(acctName))
                            _fleetAccounts.Add(new AccountFleetItem { Name = acctName, IsActive = true });
                    }
                    this.Dispatcher.Invoke(() => {
                        FleetItemsList.ItemsSource = null;
                        FleetItemsList.ItemsSource = _fleetAccounts;
                    });
                    return;
                }

                string data = parts[2].Trim(); 
                
                LogToFile($"IPC CONFIG RX: {mode} -> {data}");
                
                var kvps = data.Split(';');
                int incomingCount = _targetCount; // Default to current known count

                // 1. Pass: Find COUNT first to get the correct config object
                foreach (var item in kvps)
                {
                    var pair = item.Split(':');
                    if (pair.Length == 2 && pair[0].Trim().ToUpper() == "COUNT")
                    {
                        if (int.TryParse(pair[1].Trim(), out int c)) incomingCount = c;
                    }
                }

                var cfg = GetConfig(mode, incomingCount);
                cfg.TargetCount = incomingCount;

                // 2. Pass: Fill remaining properties
                foreach (var item in kvps)
                {
                    var pair = item.Split(':');
                    if (pair.Length == 2)
                    {
                        string k = pair[0].Trim().ToUpper();
                        string v = pair[1].Trim();
                        
                        if (k == "T1") cfg.T1 = v;
                        else if (k == "T1TYPE") cfg.T1Type = v;
                        else if (k == "T2") cfg.T2 = v;
                        else if (k == "T2TYPE") cfg.T2Type = v;
                        else if (k == "T3") cfg.T3 = v;
                        else if (k == "T3TYPE") cfg.T3Type = v;
                        else if (k == "T4") cfg.T4 = v;
                        else if (k == "T4TYPE") cfg.T4Type = v;
                        else if (k == "T5") cfg.T5 = v;
                        else if (k == "T5TYPE") cfg.T5Type = v;
                        else if (k == "STR") cfg.Str = v;
                        else if (k == "STRTYPE") cfg.StrType = v;
                        else if (k == "MAX") 
                        {
                            string vClean = v.Replace("$", "").Trim();
                            cfg.Max = "$" + vClean;
                        }
                        // V12.2 New Fields
                        else if (k == "CIT") cfg.CitVal = v;
                        else if (k == "CITACT") cfg.CitActive = bool.Parse(v);
                        else if (k == "OT") cfg.OrderType = v;
                    }
                }
                
                this.Dispatcher.Invoke(() => {
                    _suppressEvents = true; // Prevent loop during IPC update
                    // Update current UI if it matches the incoming mode+count
                    if (_selectedConfigMode.ToUpper() == mode && _targetCount == incomingCount)
                    {
                         LoadConfigUI(mode, incomingCount);
                         UpdateTargetButtonVisibility(incomingCount); // V12.1: Management Row Sync
                         TriggerGlow(Brushes.Cyan);
                    }
                    else
                    {
                        // If the incoming message is for the currently selected mode but a different count
                        if (_selectedConfigMode.ToUpper() == mode && _targetCount != incomingCount)
                        {
                            _targetCount = incomingCount; 
                            LoadConfigUI(mode, incomingCount);
                            UpdateTargetButtonVisibility(incomingCount); // V12.1: Management Row Sync
                            TriggerGlow(Brushes.Cyan);
                        }
                        else if (_selectedConfigMode.ToUpper() != mode)
                        {
                            // Auto-switch to the received mode
                            Button targetBtn = null;
                            if (mode == "ORB") targetBtn = ModeOrb;
                            else if (mode == "RMA") targetBtn = ModeRma;
                            else if (mode == "RETEST") targetBtn = ModeRetest;
                            else if (mode == "MOMO") targetBtn = ModeMomo;
                            else if (mode == "FFMA") targetBtn = ModeFfma;
                            else if (mode == "TREND") targetBtn = ModeTrend;
                            
                            if (targetBtn != null)
                            {
                                // V12.1 FIX: Update state SILENTLY. Do not call ConfigMode_Click (loop prevention)
                                _selectedConfigMode = mode;
                                _targetCount = incomingCount;
                                
                                ResetConfigButtons();
                                HighlightConfigButton(targetBtn);
                                LoadConfigUI(mode, incomingCount);
                                UpdateTargetButtonVisibility(incomingCount); 
                                TriggerGlow(Brushes.Cyan);
                                
                                LogToFile($"STATE SYNCED (Incoming): {mode} {incomingCount}");
                            }
                        }
                    }
                    _suppressEvents = false; // Restore events
                });
            }
            catch (Exception ex)
            {
                LogToFile($"ERROR ProcessConfigMessage: {ex.Message}");
            }
        }

        private void LoadConfigUI(string mode, int count)
        {
            _suppressEvents = true;
            var cfg = GetConfig(mode, count);
            _targetCount = count;
            _selectedConfigMode = mode;
            
            // Restore Inputs
            // Restore Inputs
            if (ConfT1Val != null) ConfT1Val.Text = cfg.T1;
            if (ConfT1Type != null) ConfT1Type.Text = cfg.T1Type;
            
            if (ConfT2Val != null) ConfT2Val.Text = cfg.T2;
            if (ConfT2Type != null) ConfT2Type.Text = cfg.T2Type;
            
            if (ConfT3Val != null) ConfT3Val.Text = cfg.T3;
            if (ConfT3Type != null) ConfT3Type.Text = cfg.T3Type;
            
            if (ConfT4Val != null) ConfT4Val.Text = cfg.T4;
            if (ConfT4Type != null) ConfT4Type.Text = cfg.T4Type;
            
            if (ConfT5Val != null) ConfT5Val.Text = cfg.T5;
            if (ConfT5Type != null) ConfT5Type.Text = cfg.T5Type;
            
            if (ConfStrVal != null) ConfStrVal.Text = cfg.Str;
            if (ConfStrType != null) ConfStrType.Text = cfg.StrType;
            
            if (ConfMaxVal != null) ConfMaxVal.Text = cfg.Max;
            
            // Restore ComboBoxes correctly
            SelectByContent(ConfT1Type, cfg.T1Type);
            SelectByContent(ConfT2Type, cfg.T2Type);
            SelectByContent(ConfT3Type, cfg.T3Type);
            SelectByContent(ConfT4Type, cfg.T4Type);
            SelectByContent(ConfT5Type, cfg.T5Type);
            SelectByContent(ConfT5Type, cfg.T5Type);
            SelectByContent(ConfStrType, cfg.StrType);
            
            // V12.2 Restore
            if (ConfCitVal != null) ConfCitVal.Text = cfg.CitVal;
            if (ConfCitActive != null) ConfCitActive.IsChecked = cfg.CitActive;
            SelectByContent(ConfOrderType, cfg.OrderType);
            
            // Restore UI State
            UpdateTargetInputs(_targetCount);
            UpdateTargetButtonVisibility(_targetCount); // V12.1: Management Row Sync
            
            // Restore Count Buttons visual
            ResetCountButtons();
            
            Button targetBtn = null;
            if (_targetCount == 1) targetBtn = Cnt1;
            else if (_targetCount == 2) targetBtn = Cnt2;
            else if (_targetCount == 3) targetBtn = Cnt3;
            else if (_targetCount == 4) targetBtn = Cnt4;
            else if (_targetCount == 5) targetBtn = Cnt5;
            
            if (targetBtn != null) HighlightCountButton(targetBtn);
            _suppressEvents = false;
        }

        
        private void SyncConfigMode(string mode)
        {
            Button targetBtn = null;
            if (mode == "ORB") targetBtn = ModeOrb;
            else if (mode == "RMA") targetBtn = ModeRma;
            else if (mode == "RETEST") targetBtn = ModeRetest;
            else if (mode == "MOMO") targetBtn = ModeMomo;
            else if (mode == "FFMA") targetBtn = ModeFfma;
            else if (mode == "TREND") targetBtn = ModeTrend;

            if (targetBtn != null && _selectedConfigMode.ToUpper() != mode)
            {
                ConfigMode_Click(targetBtn, null);
            }
        }

        private async void ConfigMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // 1. Save Previous State
                SaveConfigUI(_selectedConfigMode, _targetCount);

                // 2. Update Selected Mode
                string content = btn.Content.ToString().ToUpper();
                _selectedConfigMode = content;

                // V12.1: Restore persisted count for this mode
                if (_modeTargetCounts.ContainsKey(_selectedConfigMode))
                {
                    _targetCount = _modeTargetCounts[_selectedConfigMode];
                }
                else
                {
                    _targetCount = 4; // Default safety
                    _modeTargetCounts[_selectedConfigMode] = 4;
                }

                // 3. Visual Feedback (Highlight Active, Dim others)
                ResetConfigButtons();
                HighlightConfigButton(btn);

                // 4. Load New State
                LoadConfigUI(_selectedConfigMode, _targetCount);

                // V12.5: Send SET_MODE to trigger broadcast to ALL clients (Panel + External App)
                await SendCommand($"SET_MODE|{_selectedConfigMode}");

                LogToFile($"CONFIG SELECT: {_selectedConfigMode}");
                SaveLocalState(); // Persist change

                // V12.2: Push restored config to Strategy immediately upon switch
                // This prevents Strategy from overwriting our local config with its previous state
                SyncActivePropertiesToTemplate();
            }
        }

        private async void TargetCount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // 1. Save previous state
                SaveConfigUI(_selectedConfigMode, _targetCount);

                // 2. Determine Count
                string txt = btn.Content.ToString();
                if (int.TryParse(txt, out int count))
                {
                    _targetCount = count;

                    // V12.1: Persist this count for the current mode
                    if (_modeTargetCounts.ContainsKey(_selectedConfigMode))
                    {
                        _modeTargetCounts[_selectedConfigMode] = count;
                    }
                    else
                    {
                        _modeTargetCounts.Add(_selectedConfigMode, count);
                    }

                    // 3. Visual Feedback for Count Buttons
                    ResetCountButtons();
                    HighlightCountButton(btn);

                    // 4. Load from memory
                    LoadConfigUI(_selectedConfigMode, count);

                    // V12.5: Send SET_TARGETS to trigger broadcast to ALL clients (Panel + External App)
                    await SendCommand($"SET_TARGETS|{count}");
                    UpdateTargetButtonVisibility(_targetCount); // V12.1: Management Row Sync

                    LogToFile($"CONFIG COUNT: {_selectedConfigMode} -> {count} Targets");
                    SaveLocalState(); // Persist change
                }
            }
        }

        private void ResetConfigButtons()
        {
            RestoreBtnStyle(ModeOrb, "CyanBtn");
            RestoreBtnStyle(ModeRma, "RmaBtn"); 
            RestoreBtnStyle(ModeRetest, "V12Btn");
            RestoreBtnStyle(ModeMomo, "MomoBtn"); 
            RestoreBtnStyle(ModeFfma, "FfmaBtn"); 
            RestoreBtnStyle(ModeTrend, "V12Btn");
        }

        private void RestoreBtnStyle(Button btn, string styleKey)
        {
             if (btn == null) return;
             btn.Opacity = 0.5; 
        }

        private void HighlightConfigButton(Button btn)
        {
             if (btn == null) return;
             btn.Opacity = 1.0; 
        }

        private void ResetCountButtons()
        {
            if (Cnt1 != null) Cnt1.Style = (Style)FindResource("V12Btn");
            if (Cnt2 != null) Cnt2.Style = (Style)FindResource("V12Btn");
            if (Cnt3 != null) Cnt3.Style = (Style)FindResource("V12Btn");
            if (Cnt4 != null) Cnt4.Style = (Style)FindResource("V12Btn");
            if (Cnt5 != null) Cnt5.Style = (Style)FindResource("V12Btn");
        }

        private void HighlightCountButton(Button btn)
        {
            if (btn != null) btn.Style = (Style)FindResource("CyanBtn");
        }

        private void UpdateTargetInputs(int count)
        {
            SetInputState(LblT1, ConfT1Val, ConfT1Type, count >= 1);
            SetInputState(LblT2, ConfT2Val, ConfT2Type, count >= 2);
            SetInputState(LblT3, ConfT3Val, ConfT3Type, count >= 3);
            SetInputState(LblT4, ConfT4Val, ConfT4Type, count >= 4);
            SetInputState(LblT5, ConfT5Val, ConfT5Type, count >= 5);
        }

        private void UpdateTargetButtonVisibility(int count)
        {
            this.Dispatcher.Invoke(() => {
                if (BtnT1 != null) BtnT1.Visibility = count >= 1 ? Visibility.Visible : Visibility.Collapsed;
                if (BtnT2 != null) BtnT2.Visibility = count >= 2 ? Visibility.Visible : Visibility.Collapsed;
                if (BtnT3 != null) BtnT3.Visibility = count >= 3 ? Visibility.Visible : Visibility.Collapsed;
                if (BtnT4 != null) BtnT4.Visibility = count >= 4 ? Visibility.Visible : Visibility.Collapsed;
                if (BtnT5 != null) BtnT5.Visibility = count >= 5 ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void SetInputState(TextBlock lbl, TextBox box, ComboBox combo, bool enabled)
        {
            // Safety Check
            if (lbl == null || box == null || combo == null) return;

            if (enabled)
            {
                lbl.Opacity = 1.0;
                box.Opacity = 1.0;
                box.IsEnabled = true;
                combo.Opacity = 1.0;
                combo.IsEnabled = true;
            }
            else
            {
                lbl.Opacity = 0.3;
                box.Opacity = 0.3;
                box.IsEnabled = false;
                combo.Opacity = 0.3;
                combo.IsEnabled = false;
            }
        }


        private async void T2_Cancel_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T2_CANCEL|{_activeSymbol}");
            TriggerGlow(Brushes.Gray);
        }

        // V10.5: T3 Target Actions
        private async void T3_Market_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T3_MARKET|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void T3_1pt_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T3_1PT|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void T3_2pt_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T3_2PT|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void T3_Now_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T3_NOW|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void T3_BE_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T3_BE|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void T3_Cancel_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T3_CANCEL|{_activeSymbol}");
            TriggerGlow(Brushes.Gray);
        }

        // V10.5: T4/RUN Handlers
        private async void CloseT4_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"CLOSE_T4|{_activeSymbol}");
            TriggerGlow(Brushes.OrangeRed);
        }

        private async void Run_Close_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"CLOSE_T4|{_activeSymbol}");
            TriggerGlow(Brushes.OrangeRed);
        }

        // V10.4: Advanced Runner Control Handlers
        private async void RunBE_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RUN_BE|{_activeSymbol}");
            TriggerGlow(Brushes.Cyan);
        }

        private async void Run1pt_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RUN_1PT|{_activeSymbol}");
            TriggerGlow(Brushes.Lime);
        }

        private async void Run2pt_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RUN_2PT|{_activeSymbol}");
            TriggerGlow(Brushes.Green);
        }

        private async void Run50_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RUN_50|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void RunOff_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RUN_OFF|{_activeSymbol}");
            TriggerGlow(Brushes.Gray);
        }

        private async void Rma_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"MODE_RMA_NOSYNC|{_activeSymbol}");
            TriggerGlow(Brushes.SandyBrown);
            SyncConfigMode("RMA");
        }

        private async void Momo_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"MODE_MOMO_NOSYNC|{_activeSymbol}");
            TriggerGlow(Brushes.MediumPurple);
            SyncConfigMode("MOMO");
        }

        private async void Trend_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"EXEC_TREND_NOSYNC|{_activeSymbol}");
            TriggerGlow(Brushes.Teal);
            SyncConfigMode("TREND");
        }

        private async void Ffma_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"MODE_FFMA_NOSYNC|{_activeSymbol}");
            TriggerGlow(Brushes.DeepSkyBlue);
            SyncConfigMode("FFMA");
        }

        private async void Retest_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"EXEC_RETEST_NOSYNC|{_activeSymbol}");
            TriggerGlow(Brushes.DodgerBlue);
            SyncConfigMode("RETEST");
        }

        private async void RetestRmaToggle_Click(object sender, RoutedEventArgs e)
        {
            _isRetestRma = !_isRetestRma;
            BtnRetestRmaToggle.Opacity = _isRetestRma ? 1.0 : 0.5;
            // Send Toggle Command
            await SendCommand(_isRetestRma ? $"MODE_RETEST_RMA_NOSYNC|{_activeSymbol}" : $"MODE_RETEST_STD_NOSYNC|{_activeSymbol}");
            TriggerGlow(Brushes.DarkOrange);
        }

        private async void TrendRmaToggle_Click(object sender, RoutedEventArgs e)
        {
            _isTrendRma = !_isTrendRma;
            BtnTrendRmaToggle.Opacity = _isTrendRma ? 1.0 : 0.5;
            // Send Toggle Command
            await SendCommand(_isTrendRma ? $"MODE_TREND_RMA_NOSYNC|{_activeSymbol}" : $"MODE_TREND_STD_NOSYNC|{_activeSymbol}");
            TriggerGlow(Brushes.DarkOrange);
        }

        // V12: EMA Button Handlers (for RMA anchor selection)
        private async void Ema9_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RMA_ANCHOR_EMA9|{_activeSymbol}");
            TriggerGlow(Brushes.Lime);
        }

        private async void Ema15_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RMA_ANCHOR_EMA15|{_activeSymbol}");
            TriggerGlow(Brushes.Orange);
        }

        private async void Ema30_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RMA_ANCHOR_EMA30|{_activeSymbol}");
            TriggerGlow(Brushes.Yellow);
        }

        private async void Ema65_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RMA_ANCHOR_EMA65|{_activeSymbol}");
            TriggerGlow(Brushes.Cyan);
        }

        private async void Ema200_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"RMA_ANCHOR_EMA200|{_activeSymbol}");
            TriggerGlow(Brushes.Magenta);
        }

        private void GhostMode_Changed(object sender, RoutedEventArgs e)
        {
            if (GhostModeCheck.IsChecked == true)
            {
                this.Opacity = 0.5;
            }
            else
            {
                this.Opacity = 1.0;
            }
        }

        private void SelectByContent(ComboBox cb, string val)
        {
            if (cb == null || string.IsNullOrWhiteSpace(val)) return;
            foreach (ComboBoxItem item in cb.Items)
            {
                if (item.Content.ToString().ToUpper() == val.ToUpper())
                {
                    cb.SelectedItem = item;
                    return;
                }
            }
        }

        private async void SyncAll_Click(object sender, RoutedEventArgs e)
        {
            await SendCommand($"T1_SET|{_activeSymbol}|{ConfT1Val.Text}|{(ConfT1Type.SelectedItem as ComboBoxItem).Content}");
            await SendCommand($"T2_SET|{_activeSymbol}|{ConfT2Val.Text}|{(ConfT2Type.SelectedItem as ComboBoxItem).Content}");
            await SendCommand($"T3_SET|{_activeSymbol}|{ConfT3Val.Text}|{(ConfT3Type.SelectedItem as ComboBoxItem).Content}");
            await SendCommand($"T4_SET|{_activeSymbol}|{ConfT4Val.Text}|{(ConfT4Type.SelectedItem as ComboBoxItem).Content}");
            await SendCommand($"T5_SET|{_activeSymbol}|{ConfT5Val.Text}|{(ConfT5Type.SelectedItem as ComboBoxItem).Content}");
            await SendCommand($"STR_SET|{_activeSymbol}|{ConfStrVal.Text}|{(ConfStrType.SelectedItem as ComboBoxItem).Content}");
            await SendCommand($"MAX_SET|{_activeSymbol}|{ConfMaxVal.Text}"); // V12: Sync Max Loss ($)
            
            LogToFile("SYNC ALL: Sent T1-T5 and STR settings with units");
            TriggerGlow(Brushes.Cyan);
        }

        private async void TriggerGlow(Brush color)
        {
            this.Dispatcher.Invoke(() => GlowBorder.BorderBrush = color);
            await Task.Delay(500);
            this.Dispatcher.Invoke(() => GlowBorder.BorderBrush = Brushes.Transparent);
        }

        private void LogToFile(string msg)
        {
            // V12.3: Queue-based async logging to prevent UI thread blocking
            // CRITICAL FIX: Removed event wiring that was here (moved to WireConfigChangeEvents)
            _logQueue.Enqueue($"{DateTime.Now:HH:mm:ss.fff} | {msg}");
            ProcessLogQueueAsync();
        }

        private async void ProcessLogQueueAsync()
        {
            // Prevent multiple concurrent writes
            if (_isLogging) return;
            _isLogging = true;

            try
            {
                var sb = new StringBuilder();
                while (_logQueue.TryDequeue(out string line))
                {
                    sb.AppendLine(line);
                }

                if (sb.Length > 0)
                {
                    await Task.Run(() => {
                        try
                        {
                            System.IO.File.AppendAllText(_logPath, sb.ToString());
                        }
                        catch { }
                    });
                }
            }
            finally
            {
                _isLogging = false;
            }
        }

        // V12.2 Helper Methods
        private void SyncActivePropertiesToTemplate()
        {
            // V12.3: Now uses debounced sync via DebouncedConfigSync()
            // This method is kept for compatibility but uses debounce internally
            DebouncedConfigSync();
        }

        private async void SendConfigUpdate(string trigger)
        {
            // Build the config string
            var cfg = GetConfig(_selectedConfigMode, _targetCount);
            // CONFIG|MODE|T1:10;...;CIT:1;CITACT:True;OT:Limit;
            StringBuilder sb = new StringBuilder();
            sb.Append($"CONFIG|{_selectedConfigMode}|");
            sb.Append($"COUNT:{cfg.TargetCount};");
            sb.Append($"T1:{cfg.T1};T1TYPE:{cfg.T1Type};");
            sb.Append($"T2:{cfg.T2};T2TYPE:{cfg.T2Type};");
            sb.Append($"T3:{cfg.T3};T3TYPE:{cfg.T3Type};");
            sb.Append($"T4:{cfg.T4};T4TYPE:{cfg.T4Type};");
            sb.Append($"T5:{cfg.T5};T5TYPE:{cfg.T5Type};");
            sb.Append($"STR:{cfg.Str};STRTYPE:{cfg.StrType};");
            sb.Append($"MAX:{cfg.Max.Replace("$","")};");
            sb.Append($"CIT:{cfg.CitVal};CITACT:{cfg.CitActive};");
            sb.Append($"OT:{cfg.OrderType};");
            
            await SendCommand(sb.ToString());
        }
        private void AccountActive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is string acctName)
            {
                bool active = cb.IsChecked == true;
                Task.Run(async () => await SendCommand($"TOGGLE_ACCOUNT|{acctName}|{(active ? "1" : "0")}"));
            }
        }

        private void Fleet_Click(object sender, RoutedEventArgs e)
        {
            if (FleetPopup != null) FleetPopup.IsOpen = !FleetPopup.IsOpen;
            Task.Run(async () => await SendCommand("GET_FLEET"));
        }

        #region V12.2 SIMA Support Classes

        public class AccountFleetItem : System.ComponentModel.INotifyPropertyChanged
        {
            private bool _isActive = true;
            private double _pl;
            public string Name { get; set; }
            public bool IsActive 
            { 
                get => _isActive; 
                set { _isActive = value; OnPropertyChanged("IsActive"); } 
            }
            public double PL 
            { 
                get => _pl; 
                set { _pl = value; OnPropertyChanged("PL"); OnPropertyChanged("PLColor"); } 
            }
            public Brush PLColor => PL >= 0 ? Brushes.Lime : Brushes.Red;

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }

        #endregion

    }
}
