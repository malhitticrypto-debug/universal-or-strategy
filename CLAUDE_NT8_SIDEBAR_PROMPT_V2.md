# Prompt for Claude (Sage): NinjaTrader V12 Side Panel (42+ Interactive Items)

**Objective**: Implement a vertical **SIMA Side Panel** inside the NinjaTrader 8 Chart Trader UI for the `UniversalORStrategyV12`. This panel MUST replace the standard Chart Trader buttons and map **all 42+ interactive elements** from the V12 Design Authority blueprint.

---

## 🏗️ Technical Requirements
1. **Container**: Inject a WPF `UserControl` into the `ChartTrader` container. 
   - Use the `OnStateChange` pattern with `State.CustomUsage` to find the `ChartTrader` parent and its `Grid` container.
   - The UI should be a **vertical sidebar** that sits on the right of the chart area.
   - Use `State.Active` to attach and `State.Terminated` to detach.
2. **Design Fidelity**: Strictly follow the V12 "TRINITY" Dashboard Mockup.
   - **Background**: `#050505` (Deep Black)
   - **Primary Accent (Cyan)**: `#22d3ee`
   - **Execution Accents**: Emerald (#059669) for Buy, Crimson (#dc2626) for Sell.
   - **Typography**: Semi-bold, readable fonts (e.g., Segoe UI or Roboto).

## 📊 Feature Mapping (42+ Interactive Items)
Ensure the following sections are vertically stacked and functional:

### 1. [IDENTITY] (Items 1-11)
- **Status LEDS**: `HubStatusLed` (IPC) and `TosStatusLed` (RTD).
- **Symbol Chip**: Dynamic symbol display (MES/MGC) + `SymbolInput`.
- **Account**: `AccountCombo` + `FleetBtn` (Popup for Multi-Account toggles).
- **Controls**: `GhostModeCheck`, Window Minimize/Close.

### 2. [EXECUTION] (Items 12-28)
- **Primary Buttons**: `OR LONG` and `OR SHORT` (Large styled buttons).
- **Strategy Chips**: `RETEST`, `RMA` (Anchor toggle), `MOMO`, `FFMA`, `TREND`.
- **Target Distribution**: Buttons for `T1` through `T5` selection.
- **Instant Actions**: `25%`, `50%` (Trim), `BE` (Breakeven + 2), `TR 1`, `TR 2`.
- **Kill Switch**: Large `FLATTEN` button.

### 3. [TELEMETRY] (Items 30-33)
- **Opening Range**: Display H/L/[R] for 5M and 15M windows.
- **EMA Stack**: Values for 9, 15, 30, 65, 200 EMAs (Color-coded).
- **Trend Tags**: Real-time direction (BULLISH/BEARISH) + `MKT SYNC` status.

### 4. [CONFIG & MODES] (Items 34-42) - NEW ADDITIONS
- **Mode Toggle**: Selector for `ORB`, `RMA`, `RETEST`, `MOMO`, `FFMA`, `TREND`.
- **Target Count**: Numerical selector (1, 2, 3, 4, 5).
- **Order Type (OT)**: **[NEW]** Dropdown for `Limit`, `Market`, `StopMarket`.
- **Chase If Touch (CIT)**: **[NEW]** `ConfCitVal` (Ticks) + `ConfCitActive` checkbox.
- **Risk Setup**: Fields for `STR` (Stop Multiplier), `MAX` (Risk Amount), and `SYNC ALL`.

---

## 🧩 Technical Hooks (Already in Project)
- **Strategy File**: `UniversalORStrategyV12.SafeLogic.cs`
- **Inbound**: `ProcessIpcCommands()` handles strings like `SET_OT|Market` or `SET_CIT|2`.
- **Outbound**: `SendResponseToRemote(string msg)` for UI telemetry sync.
- **State Handshake**: `isTosSyncMode`, `isLongArmed`, `isShortArmed`.

**Task for Claude**:
Please provide the C# code for the `ChartTrader` UI injection class and the XAML for the `V12SidePanel`. Every single item mentioned above must have a corresponding WPF element with an event handler or binding that triggers the strategy's IPC logic.
