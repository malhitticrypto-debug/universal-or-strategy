# TASK: Build "Standard Edition" V12 Control Surface

**Context:**
We have been attempting a complex "Injection" based panel that replaces the Chart Trader sidebar. It is visually superior but technically unstable (disappearing panels, UI overlap).
The user needs a **working trading interface IMMEDIATELY** and wants to revert to the "Standard Way" of doing things in NinjaTrader 8.

**Goal:**
Create a standard, reliable button interface for the `UniversalORStrategyV12`.
Do NOT use deep visual tree injection (finding grids, borders, etc.).
Use the documented, standard NinjaTrader API methods for adding controls.

**Key Requirements:**
1.  **Reliability First:** It must load 100% of the time. No disappearing act.
2.  **Functionality:** Port the existing buttons from `V12SidePanelIndicator.cs` (Logic is already written, just need a new container).
    *   **Identity:** Label "V12 PRO", Local/SIMA toggle.
    *   **Execution:** OR LONG/SHORT, RETEST, RMA, MOMO, FFMA, TREND.
    *   **Targets:** T1-T5, 25%/50% Trim, BE, RUN 1pt/2pt.
    *   **Telemetry:** Labels for OR High/Low, EMA values, PnL (if possible).
3.  **The "Standard Way":**
    *   **Option A (Preferred):** A Standard `UserControl` added to the *Top* or *Bottom* of the chart (like a toolbar).
    *   **Option B:** Using `ChartControl.Dispatcher.Invoke` to add a simple `StackPanel` to `UserControlCollection` that is strictly docked (DockPanel) and doesn't rely on "hijacking" native containers.
    *   **Option C:** `AddChartTraderButton` calls (though we have too many buttons for this to be clean, so a Toolbar/Panel is better).

**Reference Files:**
*   `UniversalORStrategyV12.SafeLogic.cs`: The strategy logic (Keep the SIMA "Safety First" defaults!).
*   `V12SidePanelIndicator.Injection.Backup.cs`: The code containing all the button logic/event handlers we need to port.

**Immediate Instruction:**
Write a new `V12StandardPanel.cs` (or modify `V12SidePanelIndicator.cs`) to strip out the "Injection/Surgical Strike" logic and replace it with a standard WPF DockPanel/Toolbar implementation that simply docks to the top or bottom of the chart window.
ensure it does NOT conflict with the native Chart Trader.

**Deliverable:**
A compile-ready C# file that gives the user their buttons back so they can trade.
