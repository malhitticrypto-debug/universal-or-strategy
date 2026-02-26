# Infrastructure Integrity Audit: UniversalORStrategyV12 Properties.cs

**Date:** 2026-02-15  
**Scope:** Properties.cs cross-reference with main .cs, Entries.cs, UI.cs

---

## 1. VARIABLE ALIGNMENT (SetDefaults ↔ Properties.cs)

### ✅ Matched Properties
All SetDefaults values have matching [NinjaScriptProperty] in Properties.cs:
- **Session:** SessionStart, SessionEnd, ORTimeframe, SelectedTimeZone
- **Risk:** RiskPerTrade, ReducedRiskPerTrade, StopThresholdPoints, MESMinimum, MESMaximum, MGCMinimum, MGCMaximum
- **Stops:** StopMultiplier, MinimumStop, MaximumStop
- **Targets:** Target1FixedPoints, Target2Multiplier, Target3Multiplier, T1-T4ContractPercent
- **Trailing:** BreakEvenTriggerPoints, Trail1-3 Trigger/Distance Points
- **Display:** ShowMidLine, BoxOpacity
- **RMA:** RMAEnabled, RMAATRPeriod, RMAStopATRMultiplier, RMAT1ATRMultiplier, RMAT2ATRMultiplier
- **TREND:** TRENDEnabled, TRENDEntry1ATRMultiplier, TRENDEntry2ATRMultiplier
- **RETEST:** RetestEnabled, RetestATRMultiplier
- **MOMO:** MOMOEnabled, MOMOStopPoints
- **FFMA:** FFMAEnabled, FFMAEMADistance, FFMARSIOverbought, FFMARSIOversold
- **SIMA:** AccountPrefix, EnableSIMA, IpcPort, EnablePathB, AutoFlattenDesync, PathBStopPoints, PathBTargetPoints, ChaseIfTouchPoints
- **Compliance:** EnableComplianceHub, ConsistencyThreshold, EnableConsistencyLock, MaxDailyProfitCap, PayoutMinTradingDays, PayoutMinProfit, TrailingDrawdownLimit, TrailingDrawdownWarningBuffer

### 🔧 Fixes Applied
1. **MaxRiskAmount conflict** — Removed `private double MaxRiskAmount = 200.0` from main .cs. The Properties.cs property correctly maps to RiskPerTrade (get/set). Entries.cs sizing uses `MaxRiskAmount` → resolves to RiskPerTrade.
2. **BreakEvenOffsetTicks** — Added to Properties.cs (was MISSING; used in Trailing.cs, UI.cs). Default: 2 ticks.
3. **ReaperAuditEnabled, ReaperIntervalMs** — Moved from private fields to Properties.cs with [NinjaScriptProperty] for NinjaTrader UI exposure.
4. **_accountPrefix** — Removed dead private field; AccountPrefix property in Properties.cs is used everywhere.

---

## 2. ATTRIBUTE CHECK (Display / GroupName)

All properties have [Display(Name = "...", GroupName = "...", Order = N)]:

| GroupName | Order | Properties |
|-----------|-------|------------|
| 1. Session | 1-4 | SessionStart, SessionEnd, ORTimeframe, SelectedTimeZone |
| 2. Risk | 1-8 | RiskPerTrade, ReducedRiskPerTrade, MaxRiskAmount, StopThresholdPoints, MES/MGC Min/Max |
| 3. Targets | 1-7 | Target1FixedPoints, Target2/3Multiplier, T1-T4ContractPercent |
| 4. Stops | 1-3 | StopMultiplier, MinimumStop, MaximumStop |
| 5. Trailing | 1-7 | BreakEvenTriggerPoints, BreakEvenOffsetTicks, Trail1-3 Trigger/Distance |
| 6. Display | 1-2 | ShowMidLine, BoxOpacity |
| 7. RMA | 1-5 | RMAEnabled, RMAATRPeriod, RMAStopATRMultiplier, RMAT1/2ATRMultiplier |
| 8. TREND | 1-3 | TRENDEnabled, TRENDEntry1/2ATRMultiplier |
| 9. RETEST | 1-2 | RetestEnabled, RetestATRMultiplier |
| 10. MOMO | 1-2 | MOMOEnabled, MOMOStopPoints |
| 11. FFMA | 1-4 | FFMAEnabled, FFMAEMADistance, FFMARSIOverbought, FFMARSIOversold |
| 12. SIMA | 1-10 | EnableSIMA, AccountPrefix, IpcPort, EnablePathB, AutoFlattenDesync, PathB Stop/Target, ChaseIfTouchPoints, ReaperAuditEnabled, ReaperIntervalMs |
| 13. Compliance | 1-8 | EnableComplianceHub, ConsistencyThreshold, EnableConsistencyLock, MaxDailyProfitCap, PayoutMinTradingDays, PayoutMinProfit, TrailingDrawdownLimit, TrailingDrawdownWarningBuffer |

---

## 3. LOGIC SYNC: MaxRiskAmount ↔ RiskPerTrade

**Properties.cs:**
```csharp
public double MaxRiskAmount 
{ 
    get { return RiskPerTrade; } 
    set { RiskPerTrade = value; } 
}
```
✅ **Correct.** Entries.cs and UI.cs use `MaxRiskAmount` for sizing; it always reflects RiskPerTrade. SetDefaults sets RiskPerTrade = 200; no separate MaxRiskAmount field needed.

---

## 4. MISSING ENUMS

### ORTimeframeType
✅ **Complete.** Values: Minutes_1(1), Minutes_2(2), Minutes_3(3), Minutes_5(5), Minutes_15(15), Minutes_30(30).
- Main strategy uses `(int)ORTimeframe` for `TimeSpan.FromMinutes((int)ORTimeframe)` — correct.

### RmaAnchorType
✅ Defined in main .cs (line 137): `Ema30, Ema65, Ema200, OrHigh, OrLow, Manual`. Used for RMA anchor selection; not a NinjaScript property (internal logic).

---

## 5. DISCREPANCIES RESOLVED

| Property | Used In | Status |
|----------|---------|--------|
| BreakEvenOffsetTicks | Trailing.cs, UI.cs | ✅ Added to Properties.cs |
| ReaperAuditEnabled | main .cs, REAPER.cs | ✅ Added to Properties.cs |
| ReaperIntervalMs | main .cs, REAPER.cs | ✅ Added to Properties.cs |
| MaxRiskAmount | Entries.cs, UI.cs, LogicAudit.cs | ✅ Passthrough to RiskPerTrade; private field removed |
| _accountPrefix | (unused) | ✅ Removed dead field |

---

## 6. SUMMARY

- **Variable alignment:** All SetDefaults properties have matching Properties.cs entries.
- **Attribute check:** All properties have [Display] with correct GroupName.
- **MaxRiskAmount:** Correctly maps to RiskPerTrade (get/set).
- **ORTimeframeType enum:** Complete and used correctly.
- **Missing properties:** BreakEvenOffsetTicks, ReaperAuditEnabled, ReaperIntervalMs added.
- **Conflicts:** MaxRiskAmount private field removed; _accountPrefix removed.
