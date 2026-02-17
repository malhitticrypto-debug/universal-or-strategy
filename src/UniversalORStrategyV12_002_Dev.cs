// V12.12 FLEET SYMMETRY & SAFETY HARDENING - Single-Instance Multi-Account Copy Trading Engine
// Based on UniversalORStrategyV10_3.cs (BUILD 1702)
// SIMA Architecture: One strategy instance on Master account broadcasts to all Apex accounts
//
// SAFETY: This file was auto-generated. Original V10_3 file unchanged.
//
// Key Features:
//   - Account Loop execution (Account.All iteration)
//   - IPC command distribution to multiple accounts
//   - Reaper Audit thread for position verification
//   - [SIMA] logging prefix for all multi-account operations
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;  // V8.30: Thread-safe collections
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;  // V8.30: For .Values.Contains() on ConcurrentDictionary
using System.Text;
using System.Globalization;
using System.Threading;  // V8.30: For Interlocked operations
using System.Threading.Tasks; // V12.2: For Task.Run in async operations
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;  // V11: For UniformGrid
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;  // V11: For Ellipse in header
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using System.Net;
using System.Net.Sockets;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class UniversalORStrategyV12_002_Dev : Strategy
    {
        #region Variables

        // OR tracking
        private double sessionHigh;
        private double sessionLow;
        private double sessionMid;
        private double sessionRange;
        private bool isInORWindow;
        private bool orComplete;
        private bool retestFiredThisSession;  // V12.1101E [B-2]: Latch — prevent multiple RETEST entries per session
        private DateTime orStartDateTime;
        private DateTime orEndDateTime;
        private DateTime sessionStartDateTime;
        private DateTime lastResetDate;
        private int orStartBarIndex;
        private int orEndBarIndex;

        // Instrument info
        private double tickSize;
        private double pointValue;
        private int minContracts;
        private int maxContracts;  // V12.1101E [B-9]: Upper bound from MESMaximum/MGCMaximum — prevents runaway ATR sizer

        // ATR Indicator for RMA
        private ATR atrIndicator;
        private double currentATR;
        private double lastKnownPrice;  // Track current price for UI events

        // V8.2: EMA indicators for TREND trades
        private EMA ema9;
        private EMA ema15;
        // V11: Additional EMAs for Telemetry & RMA Anchors
        private EMA ema30;
        private EMA ema65;
        private EMA ema200;

        // V11: Thread-safe Value Cache for UI Telemetry
        private double _ema9Val;
        private double _ema15Val;
        private double _ema30Val;
        private double _ema65Val;
        private double _ema200Val;
        private double _orHighVal;
        private double _orLowVal;

        // V8.7: RSI indicator for FFMA trades
        private RSI rsiIndicator;

        // V12.2: ATR Sizing & Risk Management (MaxRiskAmount is Properties.cs passthrough to RiskPerTrade)
        private ConcurrentDictionary<string, bool> activeFleetAccounts = new ConcurrentDictionary<string, bool>();

        // Position tracking - multi-target system
        // V8.30: Replaced Dictionary with ConcurrentDictionary for thread-safe access
        private ConcurrentDictionary<string, PositionInfo> activePositions;
        private ConcurrentDictionary<string, Order> entryOrders;
        private ConcurrentDictionary<string, Order> stopOrders;
        private ConcurrentDictionary<string, Order> target1Orders;
        private ConcurrentDictionary<string, Order> target2Orders;
        private ConcurrentDictionary<string, Order> target3Orders;  // v5.13: New T3 orders
        private ConcurrentDictionary<string, Order> target4Orders;  // v5.13: New T4 orders (Runner)

        // V8.11: Track pending stop replacements to fix duplicate stop bug
        // V8.30: Replaced Dictionary with ConcurrentDictionary for thread-safe access
        private ConcurrentDictionary<string, PendingStopReplacement> pendingStopReplacements;

        // V12.Hardening: Execution dedup guard — prevents double-decrement from OnOrderUpdate + OnExecutionUpdate
        private readonly HashSet<string> processedExecutionIds = new HashSet<string>();
        private readonly Queue<string> processedExecutionIdQueue = new Queue<string>(); // For bounded pruning
        private readonly object executionDeduplicateLock = new object();
        private const int MaxProcessedExecutionIds = 500;

        // RMA Mode tracking
        private volatile bool isRMAModeActive;
        private volatile bool isRMAButtonClicked;  // One-shot mode from button

        // V8.2: TREND Mode tracking
        private volatile bool isTRENDModeActive;
        private bool pendingTRENDEntry;  // V8.2 FIX: Flag to execute TREND in OnBarUpdate when BarsInProgress=0
        private ConcurrentDictionary<string, string> linkedTRENDEntries;  // V8.30: Thread-safe - Links E1 and E2 by group ID

        // V8.4: RETEST Mode tracking
        private volatile bool isRetestModeActive;

        // V8.6: MOMO Mode tracking
        private volatile bool isMOMOModeActive;

        // V8.7: FFMA Mode tracking (Far From Moving Average)
        private volatile bool isFFMAModeArmed;
        private double ffmaEntryBarHigh;   // Store entry candle high for stop (short)
        private double ffmaEntryBarLow;    // Store entry candle low for stop (long)

        // V11 Logic State
        private volatile bool isTrendRmaMode = false; // False = STD (All-in), True = RMA (9/15 Split)
        private volatile bool isRetestRmaMode = false; // V12: RETEST RMA toggle state

        // V12.2 Hybrid Sync: Logic State
        private volatile bool isTosSyncMode = false;
        private bool isLongArmed = false;
        private bool isShortArmed = false;
        private DateTime lastArmedTime = DateTime.MinValue;

        // V11: RMA Anchor Logic
        public enum RmaAnchorType { Ema30, Ema65, Ema200, OrHigh, OrLow, Manual }
        private RmaAnchorType currentRmaAnchor = RmaAnchorType.Ema65; // Default to 65
        private double lastMnlPrice = 0;
        private double cachedMnlPrice = 0; // Thread-safe cache
        private bool isMnlArmed = false;

        private DateTime lastStopManagementTime; // V8.13: Stop management throttling (100ms)

        // V8.30: Circuit breaker state - prevents cascade when too many pending replacements
        private volatile int pendingReplacementCount = 0;
        private const int CIRCUIT_BREAKER_THRESHOLD = 5;
        private volatile bool circuitBreakerActive = false;
        private DateTime circuitBreakerActivatedTime = DateTime.MinValue;

        // V8.30: DrawORBox throttling - prevents chart update saturation
        private DateTime lastDrawORBoxTime = DateTime.MinValue;
        private const int DRAW_ORBOX_THROTTLE_MS = 200;

        // V8.30: Adaptive throttling based on tick frequency
        private int tickCountInLastSecond = 0;
        private DateTime lastTickCountReset = DateTime.MinValue;
        private int adaptiveThrottleMs = 100;


        // V9.1.8 IPC Integration
        private TcpListener ipcListener;
        private Thread ipcThread;
        private volatile bool isIpcRunning;
        private readonly object ipcLock = new object();
        private readonly object stateLock = new object();  // V12.20: Atomic mode transitions
        private ConcurrentQueue<string> ipcCommandQueue;
        // V12.2: Multi-Client Support
        private ConcurrentBag<TcpClient> connectedClients;

        // V12 SIMA: Multi-Account Execution Engine
        private Thread reaperThread;
        private volatile bool isReaperRunning;
        private volatile bool isFlattenRunning; // V12.8: Guard to pause Reaper during flatten
        private ConcurrentDictionary<string, int> expectedPositions; // AccountName -> Expected Quantity (+ long, - short)
        private int simaAccountCount = 0; // Cached count of detected Apex accounts
        private DateTime lastReaperLog = DateTime.MinValue;

        // V12.1 SIMA Internal (ReaperAuditEnabled, ReaperIntervalMs now in Properties.cs)

        // V12.1: Apex Compliance Tracking
        private ConcurrentDictionary<string, double> accountDailyProfit = new ConcurrentDictionary<string, double>();
        private ConcurrentDictionary<string, double> accountTotalProfit = new ConcurrentDictionary<string, double>();
        private string complianceLogPath;
        private DateTime lastComplianceLog = DateTime.MinValue;
        private ConcurrentDictionary<string, int> accountTradeCount = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, int> accountDailyTradeCount = new ConcurrentDictionary<string, int>();
        private ConcurrentDictionary<string, double> accountEquityPeak = new ConcurrentDictionary<string, double>();
        private ConcurrentDictionary<string, double> accountMaxDrawdown = new ConcurrentDictionary<string, double>();
        private ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> accountTradingDays = new ConcurrentDictionary<string, ConcurrentDictionary<int, byte>>();
        private ConcurrentDictionary<string, DateTime> accountLastSummaryDate = new ConcurrentDictionary<string, DateTime>();
        private string dailySummaryCsvPath;
        private DateTime lastDailySummaryCheck = DateTime.MinValue;
        private readonly object dailySummaryLock = new object();

        // CIT (Chase If Touch) â€” uses ChaseIfTouchPoints property (NinjaScriptProperty)

        #endregion

        #region Position Info Class

        private class PositionInfo
        {
            public string SignalName;
            public MarketPosition Direction;
            public int TotalContracts;
            public int T1Contracts;   // v5.13: 20% - Fixed 1pt quick profit
            public int T2Contracts;   // v5.13: 30% - 0.5x ATR
            public int T3Contracts;   // v5.13: 30% - 1.0x ATR
            public int T4Contracts;   // v5.13: 20% - Runner/Trail
            public volatile int RemainingContracts; // V12.1101E [SK-08]: volatile — written from OnOrderUpdate, OnExecutionUpdate, OnBarUpdate threads
            public double EntryPrice;
            public double InitialStopPrice;
            public double CurrentStopPrice;
            public double Target1Price;  // v5.13: Fixed 1pt
            public double Target2Price;  // v5.13: 0.5x ATR
            public double Target3Price;  // v5.13: 1.0x ATR
            public bool EntryFilled;
            public bool T1Filled;
            public bool T2Filled;
            public bool T3Filled;       // v5.13: New flag
            public bool BracketSubmitted;
            public double ExtremePriceSinceEntry;
            public int CurrentTrailLevel;
            public bool IsRMATrade;  // Flag to identify RMA trades
            public bool ManualBreakevenArmed;  // Manual breakeven button clicked
            public bool ManualBreakevenTriggered;  // Manual breakeven has executed
            public int TicksSinceEntry;  // v5.13: Tick counter for frequency-based trailing

            // V8.2: TREND trade tracking
            public bool IsTRENDTrade;           // Flag for TREND trades
            public bool IsTRENDEntry1;          // True if this is the 9 EMA entry (1/3)
            public bool IsTRENDEntry2;          // True if this is the 15 EMA entry (2/3)
            public string LinkedTRENDGroup;    // Links Entry1 and Entry2 together
            public bool Entry1TrailActivated;  // V8.2: True when E1 switches from fixed stop to EMA9 trail

            // V8.4: RETEST trade tracking
            public bool IsRetestTrade;          // Flag for RETEST trades
            public bool RetestTrailActivated;   // V8.4: True when retest switches from fixed stop to 9 EMA trail

            // V8.6: MOMO trade tracking
            public bool IsMOMOTrade;            // Flag for MOMO trades

            // V8.7: FFMA trade tracking
            public bool IsFFMATrade;            // Flag for FFMA trades

            // V12.1: SIMA Multi-Account tracking
            public Account ExecutingAccount;    // The account this position belongs to (null = Master)
            public bool IsFollower;             // True if this is a SIMA follower position
        }

        // V8.11: Class to track pending stop replacements
        // V8.30: Added CreatedTime for timeout support
        private class PendingStopReplacement
        {
            public string EntryName;
            public int Quantity;
            public double StopPrice;
            public MarketPosition Direction;
            public Order OldOrder;  // Track the old order being cancelled
            public DateTime CreatedTime;  // V8.30: Timeout support - clean up stale replacements
        }

        // V8.22: Thread-Safe UI Snapshot Struct
        // Decouples UI thread from Strategy thread to prevent "Collection moved" or race conditions
        public struct PositionDisplayInfo
        {
            public string TradeType;
            public string Direction;
            public double EntryPrice;
            public double StopPrice;
            public int RemainingContracts;
            public bool EntryFilled;
            public bool ManualBreakevenArmed;
            public bool ManualBreakevenTriggered;
        }

        // V12.12: Compliance snapshot for UI thread
        private struct ComplianceSnapshot
        {
            public bool Enabled;
            public bool HasAccounts;
            public string AccountName;
            public int TradeCount;
            public int UniqueDays;
            public double MaxDrawdown;
            public string ConsistencyText;
            public int ConsistencySeverity;
            public string PayoutText;
            public int PayoutSeverity;
            public string DrawdownText;
            public int DrawdownSeverity;
        }

        #endregion

        // V12.46: Enums, Properties, and TimeZoneConverter moved to Properties.cs

        #region OnStateChange

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Universal OR Strategy V12.12 - Modular Build";
                Name = "UniversalORStrategyV12_002";
                Calculate = Calculate.OnPriceChange;  // CRITICAL FIX: Updates on every price tick for real-time trailing
                EntriesPerDirection = 10;
                EntryHandling = EntryHandling.UniqueEntries;
                IsExitOnSessionCloseStrategy = false;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                StartBehavior = StartBehavior.ImmediatelySubmit;
                TimeInForce = TimeInForce.Gtc;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                IsUnmanaged = true;

                // Session defaults (NY Open)
                SessionStart = DateTime.Parse("09:30");
                SessionEnd = DateTime.Parse("16:00");
                ORTimeframe = ORTimeframeType.Minutes_5;
                SelectedTimeZone = "Eastern";

                // Risk defaults
                RiskPerTrade = 200;
                ReducedRiskPerTrade = 200;
                StopThresholdPoints = 5.0;
                MESMinimum = 1;
                MESMaximum = 30;
                MGCMinimum = 1;
                MGCMaximum = 15;

                // Stop defaults
                StopMultiplier = 0.5;
                MinimumStop = 1.0;
                MaximumStop = 15.0;  // V8.31: Increased from 8.0
                IpcPort = 5001;


                // v5.13: 4-Target System - T1=Fixed 1pt, T2-T4=ATR-based
                Target1FixedPoints = 1.0;   // T1 = Fixed 1 point quick scalp
                Target2Multiplier = 0.5;    // T2 = 0.5x ATR
                Target3Multiplier = 1.0;    // T3 = 1.0x ATR
                T1ContractPercent = 20;     // 20% quick scalp
                T2ContractPercent = 30;     // 30%
                T3ContractPercent = 30;     // 30%
                T4ContractPercent = 20;     // 20% runner

                // Trailing stop defaults
                BreakEvenTriggerPoints = 2.0;
                BreakEvenOffsetTicks = 2;              // BE stop offset in ticks (0 = exact entry)
                Trail1TriggerPoints = 3.0;
                Trail1DistancePoints = 2.0;
                Trail2TriggerPoints = 4.0;
                Trail2DistancePoints = 1.5;
                Trail3TriggerPoints = 5.0;
                Trail3DistancePoints = 1.0;

                // Display
                ShowMidLine = true;
                BoxOpacity = 20;

                // RMA defaults
                RMAEnabled = true;
                RMAATRPeriod = 14;
                RMAStopATRMultiplier = 1.1;
                RMAT1ATRMultiplier = 0.5;
                RMAT2ATRMultiplier = 1.0;

                // V8.2: TREND defaults (V8.31: E1 now uses ATR from live EMA9)
                TRENDEnabled = true;
                TRENDEntry1ATRMultiplier = 1.1;   // V8.31: 1.1x ATR stop from live 9 EMA (was fixed 2pt)
                TRENDEntry2ATRMultiplier = 1.1;   // 1.1x ATR trailing for 15 EMA entry

                // V8.4: RETEST defaults
                RetestEnabled = true;
                RetestATRMultiplier = 1.1;        // 1.1x ATR for both stop and trail

                // V8.6: MOMO defaults
                MOMOEnabled = true;
                MOMOStopPoints = 0.5;             // Fixed 0.5pt stop for MOMO trades

                // V8.7: FFMA defaults
                FFMAEnabled = true;
                FFMAEMADistance = 10.0;           // 10 points from 9 EMA
                FFMARSIOverbought = 80;
                FFMARSIOversold = 20;

                // V12 SIMA defaults
                AccountPrefix = "Apex";
                EnableSIMA = false; // SAFETY: Default to OFF
                ReaperAuditEnabled = true;
                ReaperIntervalMs = 1000;          // 1 second audit cycle
                EnablePathB = false;
                AutoFlattenDesync = false;
                PathBStopPoints = 10.0;
                PathBTargetPoints = 15.0;
                ChaseIfTouchPoints = "0";

                // Apex Compliance defaults
                EnableComplianceHub = true;
                ConsistencyThreshold = 30;
                EnableConsistencyLock = false;
                MaxDailyProfitCap = 1500; // Default $1500 cap for consistency
                PayoutMinTradingDays = 10;
                PayoutMinProfit = 2600; // Common Apex 50K payout threshold (adjust per account)
                TrailingDrawdownLimit = 2500; // Common Apex 50K trailing DD
                TrailingDrawdownWarningBuffer = 200;
            }
            else if (State == State.Configure)
            {
                // V8.30: Initialize thread-safe collections
                // ConcurrentDictionary(concurrencyLevel, initialCapacity)
                activePositions = new ConcurrentDictionary<string, PositionInfo>(2, 4);
                entryOrders = new ConcurrentDictionary<string, Order>(2, 4);
                stopOrders = new ConcurrentDictionary<string, Order>(2, 4);
                target1Orders = new ConcurrentDictionary<string, Order>(2, 4);
                target2Orders = new ConcurrentDictionary<string, Order>(2, 4);
                target3Orders = new ConcurrentDictionary<string, Order>(2, 4);  // v5.13
                target4Orders = new ConcurrentDictionary<string, Order>(2, 4);  // v5.13

                // V8.2: TREND linked entries tracking
                // V8.30: Thread-safe dictionary
                linkedTRENDEntries = new ConcurrentDictionary<string, string>(2, 4);

                // V8.11: Initialize pending stop replacements tracking
                // V8.30: Thread-safe dictionary
                pendingStopReplacements = new ConcurrentDictionary<string, PendingStopReplacement>(2, 4);


                // IPC Queue
                ipcCommandQueue = new ConcurrentQueue<string>();

                // V12 SIMA: Initialize expected positions tracking
                expectedPositions = new ConcurrentDictionary<string, int>(2, 20); // Up to 20 accounts

                // V12.1: Initialize Compliance Hub
                string logsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8", "SIMA_Logs");
                if (!System.IO.Directory.Exists(logsDir)) System.IO.Directory.CreateDirectory(logsDir);
                complianceLogPath = System.IO.Path.Combine(logsDir, "ApexPerformance.json");
                dailySummaryCsvPath = System.IO.Path.Combine(logsDir, "DailySummaries.csv");
                EnsureDailySummaryCsv();

                // Add 5-min data series for ATR (index 1)
                AddDataSeries(BarsPeriodType.Minute, 5);

                // V12.002: Run Risk Logic Audit (The Testing Rig) on startup
                ExecuteRiskLogicAudit();
            }
            else if (State == State.DataLoaded)
            {
                tickSize = Instrument.MasterInstrument.TickSize;
                pointValue = Instrument.MasterInstrument.PointValue;
                lastKnownPrice = 0; // V11 FIX: Reset price on load to prevent stale data (e.g. MES->MGC switch)

                string symbol = Instrument.MasterInstrument.Name;
                if (symbol.Contains("MES") || symbol.Contains("ES"))
                {
                    minContracts = MESMinimum;
                    maxContracts = MESMaximum; // V12.1101E [B-9]: Upper bound for ATR sizer
                }
                else if (symbol.Contains("MGC") || symbol.Contains("GC"))
                {
                    minContracts = MGCMinimum;
                    maxContracts = MGCMaximum; // V12.1101E [B-9]
                }
                else
                {
                    minContracts = 1;
                    maxContracts = 20; // V12.1101E [B-9]: Conservative default for unknown instruments
                }

                // Initialize ATR indicator on 5-min bars (BarsArray[1])
                atrIndicator = this.ATR(BarsArray[1], RMAATRPeriod);

                // V8.2: Initialize EMA indicators for TREND trades
                // Using simple form - default is primary bars series
                ema9 = this.EMA(9);
                ema15 = this.EMA(15);
                // V11: Telemetry & Multi-Anchor EMAs
                ema30 = this.EMA(30);
                ema65 = this.EMA(65);
                ema200 = this.EMA(200);
                
                // V8.7: Initialize RSI for FFMA trades
                rsiIndicator = this.RSI(14, 3);
                
                // V8.2 DEBUG: Verify EMA periods are correct
                Print(string.Format("EMA INIT DEBUG: ema9.Period={0} ema15.Period={1}", ema9.Period, ema15.Period));

                ResetOR();

                Print(string.Format("UniversalORStrategy V12.14 | {0} | Tick: {1} | PV: ${2}", symbol, tickSize, pointValue));
                Print(string.Format("Session: {0} - {1} {2} | OR: {3} min",
                    SessionStart.ToString("HH:mm"), SessionEnd.ToString("HH:mm"), SelectedTimeZone, (int)ORTimeframe));
                Print(string.Format("OR Targets: T1={0}pt T2={1}xOR T3={2}xOR | Stop={3}xOR", Target1FixedPoints, Target2Multiplier, Target3Multiplier, StopMultiplier));
                Print(string.Format("RMA: Enabled={0} ATR({1}) Stop={2}xATR T1={3}xATR T2={4}xATR",
                    RMAEnabled, RMAATRPeriod, RMAStopATRMultiplier, RMAT1ATRMultiplier, RMAT2ATRMultiplier));
                Print("V12.9 REPAIRED: Definitive Chart-Click Fix + Logic Refresh");
                Print(string.Format("TREND: Enabled={0} E1Stop={1}xATR E2Trail={2}xATR", TRENDEnabled, TRENDEntry1ATRMultiplier, TRENDEntry2ATRMultiplier));
                Print(string.Format("FFMA: Enabled={0} Distance={1}pt RSI={2}/{3}", FFMAEnabled, FFMAEMADistance, FFMARSIOversold, FFMARSIOverbought));
                Print(string.Format("V12 SIMA: {0} | AccountPrefix: \"{1}\"", EnableSIMA ? "ENABLED - Fleet mode" : "DISABLED - Single account", AccountPrefix));

            }
            else if (State == State.Realtime)
            {
                // V12.2 HEADLESS SAFETY: Start core services even if ChartControl is null (for background execution)
                // EMERGENCY SAFE MODE (V12.32): Disabling background services to allow platform login
                StartIpcServer();

                if (EnableSIMA)
                {
                    EnumerateApexAccounts();
                    if (ReaperAuditEnabled)
                        StartReaperAudit();
                }

                // V10.3: Subscribe to external signals for multi-chart sync
                // SignalBroadcaster.OnExternalCommand += HandleExternalSignal;

                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        AttachHotkeys();
                        AttachChartClickHandler();
                        Print("REALTIME - Hotkeys: L=Long, S=Short, Shift+Click=RMA, F=Flatten");
                    });
                }
            }
            else if (State == State.Terminated)
            {
                if (ChartControl != null)
                {
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        DetachHotkeys();
                        DetachChartClickHandler();
                    });
                }

                // Stop IPC Server
                StopIpcServer();
                
                // V12 SIMA: Stop Reaper audit thread
                StopReaperAudit();
                
                // V12.7: Always unsubscribe from account updates (subscribed for fleet bracket management)
                // V12.1101E [A-4]: Use shared UnsubscribeFromFleetAccounts() — unconditional (no EnableSIMA guard)
                // to handle cases where flag was toggled OFF mid-session while handlers were still subscribed.
                UnsubscribeFromFleetAccounts();
                
                // V10.3: Unsubscribe
                SignalBroadcaster.OnExternalCommand -= HandleExternalSignal;

                // Clear references
                activePositions?.Clear();
                entryOrders?.Clear();
                stopOrders?.Clear();
                target1Orders?.Clear();
                target2Orders?.Clear();
                target3Orders?.Clear();  // v5.13
                target4Orders?.Clear();  // v5.13
                accountDailyProfit?.Clear();
                accountTotalProfit?.Clear();
                accountTradeCount?.Clear();
                accountDailyTradeCount?.Clear();
                accountEquityPeak?.Clear();
                accountMaxDrawdown?.Clear();
                accountTradingDays?.Clear();
                accountLastSummaryDate?.Clear();

            }
        }

        #endregion

        #region OnMarketData - V10.1: Process IPC on every tick for real-time responsiveness

        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            // Only process on primary instrument
            if (marketDataUpdate.MarketDataType == MarketDataType.Last)
            {
                // Update last known price for real-time tracking
                lastKnownPrice = marketDataUpdate.Price;
                
                // Process IPC commands immediately on every tick
                // This ensures Remote App buttons work even outside session time
                ProcessIpcCommands();
            }
        }

        #endregion

        #region OnBarUpdate

        protected override void OnBarUpdate()
        {
            // Only process primary series
            if (BarsInProgress != 0) return;
            if (CurrentBar < 5) return;

            try
            {
                // Update last known price for UI events
                lastKnownPrice = Close[0];

                // V12.12: Daily summary roll-over (throttled)
                if (EnableComplianceHub)
                {
                    DateTime nowInZone = GetComplianceNow();
                    if ((nowInZone - lastDailySummaryCheck).TotalSeconds >= 30)
                    {
                        List<Account> complianceAccounts = GetComplianceAccounts();
                        if (complianceAccounts.Count > 0)
                            MaybeFinalizeDailySummaries(nowInZone, complianceAccounts);
                    }
                }

                // V8.21: Reduced log volume - OR buildings and updates are handled via DrawORBox and UpdateDisplay

                // Process IPC Commands
                ProcessIpcCommands();

                // CIT Logic
                ManageCIT();

                // V8.2 FIX: Process pending TREND entry (deferred from button click)
                if (pendingTRENDEntry)
                {
                    ExecuteTRENDEntry();
                }

                // Update ATR value from 5-min bars
                if (BarsArray[1] != null && BarsArray[1].Count > RMAATRPeriod)
                {
                    currentATR = atrIndicator[0];
                }

                // V11: Update Telemetry Cache (Thread-safe for UI)
                _ema9Val = ema9[0];
                _ema15Val = ema15[0];
                _ema30Val = ema30[0];
                _ema65Val = ema65[0];
                _ema200Val = ema200[0];
                _orHighVal = sessionHigh;
                _orLowVal = sessionLow;

                // CRITICAL FIX: Convert from LOCAL timezone (PC) to selected timezone
                DateTime barTimeInZone = ConvertToSelectedTimeZone(Time[0]);
                TimeSpan currentTime = barTimeInZone.TimeOfDay;
                TimeSpan sessionStartTime = SessionStart.TimeOfDay;
                TimeSpan sessionEndTime = SessionEnd.TimeOfDay;

                // Calculate OR end time based on session start + timeframe
                TimeSpan orEndTime = sessionStartTime.Add(TimeSpan.FromMinutes((int)ORTimeframe));

                // Detect if session crosses midnight (e.g. 21:00 to 07:00)
                bool sessionCrossesMidnight = sessionEndTime < sessionStartTime;

                // V11: Draw MNL Anchor Line if active
                if (currentRmaAnchor == RmaAnchorType.Manual && cachedMnlPrice > 0)
                {
                    NinjaTrader.NinjaScript.DrawingTools.Draw.HorizontalLine(this, "MNL_Line", cachedMnlPrice, Brushes.Magenta, DashStyleHelper.Dash, 2);
                }
                else
                {
                    RemoveDrawObject("MNL_Line");
                }
                
                // Smart reset logic - only reset at NEW SESSION START
                bool shouldReset = false;

                if (sessionCrossesMidnight)
                {
                    // For overnight sessions: only reset at session start
                    if (currentTime >= sessionStartTime && currentTime < sessionStartTime.Add(TimeSpan.FromMinutes(10)))
                    {
                        if (barTimeInZone.Date != lastResetDate)
                        {
                            shouldReset = true;
                        }
                    }
                }
                else
                {
                    // For regular sessions: reset when date changes AFTER session ends
                    if (barTimeInZone.Date != lastResetDate && currentTime >= sessionStartTime)
                    {
                        shouldReset = true;
                    }
                }

                if (shouldReset)
                {
                    ResetOR();
                    lastResetDate = barTimeInZone.Date;
                    Print(string.Format("Session Reset: {0} at {1} {2}",
                        barTimeInZone.Date.ToShortDateString(), currentTime, SelectedTimeZone));
                }

                // Build OR during window
                if (currentTime > sessionStartTime && currentTime <= orEndTime)
                {
                    if (!isInORWindow)
                    {
                        Print(string.Format("OR WINDOW START: {0} (Bar time in {1})",
                            barTimeInZone.ToString("MM/dd/yyyy HH:mm:ss"), SelectedTimeZone));
                    }

                    isInORWindow = true;
                    sessionHigh = Math.Max(sessionHigh, High[0]);
                    sessionLow = Math.Min(sessionLow, Low[0]);
                    sessionRange = sessionHigh - sessionLow;
                    sessionMid = (sessionHigh + sessionLow) / 2.0;

                    if (orStartDateTime == DateTime.MinValue)
                    {
                        orStartDateTime = Time[0];
                        sessionStartDateTime = Time[0];
                        orStartBarIndex = CurrentBar;
                        Print(string.Format("OR Start tracked - Bar {0}", CurrentBar));
                    }
                }

                // Mark OR complete when the last bar of the window closes
                if (currentTime >= orEndTime && !orComplete && orStartBarIndex > 0)
                {
                    isInORWindow = false;
                    orComplete = true;
                    orEndDateTime = Time[0];
                    orEndBarIndex = CurrentBar;

                    Print(string.Format("OR COMPLETE at {0}: H={1:F2} L={2:F2} M={3:F2} R={4:F2}",
                        barTimeInZone.ToString("HH:mm:ss"), sessionHigh, sessionLow, sessionMid, sessionRange));
                    Print(string.Format("OR Targets: T1=+{0:F2} T2=+{1:F2} Stop=-{2:F2}",
                        Target1FixedPoints, sessionRange * Target2Multiplier, CalculateORStopDistance()));

                    // V8.30: Always draw immediately when OR completes (important event)
                    DrawORBox();
                    lastDrawORBoxTime = DateTime.Now;
                }

                // Update box if OR complete
                bool inActiveSession = false;
                if (sessionCrossesMidnight)
                {
                    inActiveSession = (currentTime >= sessionStartTime || currentTime <= sessionEndTime);
                }
                else
                {
                    inActiveSession = (currentTime >= sessionStartTime && currentTime <= sessionEndTime);
                }

                // V8.30: Throttle DrawORBox updates to prevent chart saturation
                if (orComplete && sessionHigh != double.MinValue && inActiveSession)
                {
                    if ((DateTime.Now - lastDrawORBoxTime).TotalMilliseconds >= DRAW_ORBOX_THROTTLE_MS)
                    {
                        DrawORBox();
                        lastDrawORBoxTime = DateTime.Now;
                    }
                }

                // Position sync check
                SyncPositionState();
                SymmetryGuardProcessPendingFollowerFills();

                // Manage trailing stops - NOW CALLED ON EVERY PRICE CHANGE!
                if (activePositions.Count > 0)
                {
                    ManageTrailingStops();
                    ManageCIT();
                }

                // V8.7: Check FFMA conditions when armed
                if (isFFMAModeArmed && FFMAEnabled)
                {
                    CheckFFMAConditions();
                }

                SyncPendingOrders();  // V12.30: Real-time sizing synchronization
            }
            catch (Exception ex)
            {
                Print("ERROR OnBarUpdate: " + ex.Message);
            }
        }

        #endregion

        // V12.16: FFMA entry logic moved to Entries.cs


        #region Drawing - Box Instead of Rays

        private void DrawORBox()
        {
            if (sessionHigh == double.MinValue || sessionLow == double.MaxValue) return;
            if (orStartDateTime == DateTime.MinValue || orEndDateTime == DateTime.MinValue) return;

            try
            {
                int areaOpacity = BoxOpacity;

                DateTime orStartInZone = ConvertToSelectedTimeZone(orStartDateTime);
                TimeSpan sessionStartTime = SessionStart.TimeOfDay;
                TimeSpan sessionEndTime = SessionEnd.TimeOfDay;

                // Detect overnight session (e.g., 21:00 to 16:00)
                bool sessionCrossesMidnight = sessionEndTime < sessionStartTime;

                // Calculate session end date
                DateTime sessionEndInZone;
                if (sessionCrossesMidnight)
                {
                    // Overnight session: end time is NEXT day
                    sessionEndInZone = new DateTime(
                        orStartInZone.Year,
                        orStartInZone.Month,
                        orStartInZone.Day,
                        sessionEndTime.Hours,
                        sessionEndTime.Minutes,
                        sessionEndTime.Seconds
                    ).AddDays(1);  // ADD ONE DAY for overnight sessions!
                }
                else
                {
                    // Same-day session: end time is same day
                    sessionEndInZone = new DateTime(
                        orStartInZone.Year,
                        orStartInZone.Month,
                        orStartInZone.Day,
                        sessionEndTime.Hours,
                        sessionEndTime.Minutes,
                        sessionEndTime.Seconds
                    );
                }

                TimeZoneInfo targetZone;
                switch (SelectedTimeZone)
                {
                    case "Eastern":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        break;
                    case "Central":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                        break;
                    case "Mountain":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
                        break;
                    case "Pacific":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                        break;
                    default:
                        targetZone = TimeZoneInfo.Local;
                        break;
                }

                DateTime boxEndTime = TimeZoneInfo.ConvertTime(sessionEndInZone, targetZone, TimeZoneInfo.Local);

                    Draw.Rectangle(this, "ORBox", false,
                    orStartDateTime, sessionHigh,
                    boxEndTime, sessionLow,
                    Brushes.DodgerBlue, Brushes.DodgerBlue, areaOpacity);

                if (ShowMidLine)
                {
                    Draw.Line(this, "ORMid", false,
                        orStartDateTime, sessionMid,
                        boxEndTime, sessionMid,
                        Brushes.Yellow, DashStyleHelper.Dash, 1);
                }
            }
            catch (Exception ex)
            {
                Print("ERROR DrawORBox: " + ex.Message);
            }
        }

        private void ResetOR()
        {
            sessionHigh = double.MinValue;
            sessionLow = double.MaxValue;
            sessionMid = 0;
            sessionRange = 0;
            isInORWindow = false;
            orComplete = false;
            retestFiredThisSession = false;  // V12.1101E [B-2]: Reset RETEST latch at session start
            orStartDateTime = DateTime.MinValue;
            orEndDateTime = DateTime.MinValue;
            sessionStartDateTime = DateTime.MinValue;
            orStartBarIndex = 0;
            orEndBarIndex = 0;

            RemoveDrawObjects();
        }

        #endregion

        #region Helpers

        private DateTime ConvertToSelectedTimeZone(DateTime localTime)
        {
            try
            {
                TimeZoneInfo targetZone;
                switch (SelectedTimeZone)
                {
                    case "Eastern":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                        break;
                    case "Central":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                        break;
                    case "Mountain":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
                        break;
                    case "Pacific":
                        targetZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                        break;
                    case "UTC":
                        targetZone = TimeZoneInfo.Utc;
                        break;
                    default:
                        return localTime;
                }

                return TimeZoneInfo.ConvertTime(localTime, TimeZoneInfo.Local, targetZone);
            }
            catch (Exception ex)
            {
                Print("ERROR ConvertToSelectedTimeZone: " + ex.Message);
                return localTime;
            }
        }


        private void RemoveDrawObjects()
        {
            RemoveDrawObject("ORBox");
            RemoveDrawObject("ORMid");
        }

        #endregion

        // V12.16: OR, RMA, MOMO, TREND, RETEST entry logic moved to Entries.cs


        // V12.16: Order Management, Trailing Stops, Position Sync moved to Orders.cs


        // V12.16: UI handlers moved to UI.cs


        // V12.16: Stop Management Helpers moved to Orders.cs


        // V12.16: IPC, Compliance, Position Sizing moved to UI.cs

    }
}
// V12.9 REPAIRED - Single-Instance Multi-Account Copy Trading Engine
