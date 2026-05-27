# Jane Street Deviations from .NET Standards

**Purpose**: Living document tracking all architectural decisions where V12 deviates from standard .NET conventions in favor of Jane Street HFT patterns.

**Approval Authority**: Director + Architect (Bob CLI or Claude Opus 4.7)

**Review Cadence**: Quarterly (or when Codacy grade drops below B+)

---

## Core Jane Street Principles (V12 DNA)

1. **Correctness by Construction**: Make illegal states unrepresentable
2. **Zero-Allocation Hot Paths**: Stack allocation over heap allocation for >100 ops/sec
3. **Lock-Free Concurrency**: FSM/Actor pattern, atomic primitives only
4. **Microsecond Latency**: Every allocation, lock, or virtual call is scrutinized

---

## Decision Log

### Decision #1: Struct-Based Events (Zero-Allocation Hot Path)

**Date**: 2026-05-27  
**PR**: #9  
**Codacy Rule Violated**: CA1003 (Event data should inherit from EventArgs)  
**Severity**: Style (not correctness)

**Context**:
- V12 broadcasts 1000+ signals/second in hot trading paths
- EventArgs inheritance forces heap allocation (reference type)
- Struct-based events use stack allocation (value type)

**Performance Impact**:
- **Before**: 1000 signals/sec × EventArgs = 1000 heap allocations/sec = GC pressure
- **After**: 1000 signals/sec × struct = 0 heap allocations = zero GC pressure

**Implementation**:
```csharp
// STANDARD .NET (heap allocation):
public class TradeSignal : EventArgs { ... }
public static event EventHandler<TradeSignal> OnTradeSignal;

// JANE STREET PATTERN (stack allocation):
public struct TradeSignal { ... }
public static event Action<TradeSignal> OnTradeSignal;
```

**Affected Files**:
- `src/SignalBroadcaster.cs` (9 signal structs)

**Codacy Suppression**:
```yaml
exclude_paths:
  - "src/SignalBroadcaster.cs"  # Jane Street Deviation #1: Struct-based events
```

**Rationale**:
- Jane Street HFT alignment (Priority 1) > Codacy compliance (Priority 2)
- CA1003 is a style guideline, not a correctness requirement
- EventArgs pattern predates modern zero-allocation techniques
- V12 DNA mandates zero-allocation hot paths

**Trade-offs**:
- ✅ Eliminates 1000+ allocations/second
- ✅ Reduces GC pressure in latency-critical paths
- ❌ Reintroduces 9 CA1003 warnings in Codacy
- ❌ Deviates from standard .NET event pattern

**Approval**: Director (2026-05-27)

**References**:
- Protocol: `docs/brain/PR_9_EVENTARGS_REVERSION.md`
- Suppression rationale: `docs/brain/CODACY_PATTERN_SUPPRESSIONS.md`

---

## Decision Template (for future deviations)

### Decision #N: [Title]

**Date**: YYYY-MM-DD  
**PR**: #XXX  
**Codacy Rule Violated**: CAXXXX ([Rule Name])  
**Severity**: [Critical/High/Medium/Low/Style]

**Context**:
[Why this deviation is necessary]

**Performance Impact**:
- **Before**: [Baseline metrics]
- **After**: [Improved metrics]

**Implementation**:
```csharp
// STANDARD .NET:
[code example]

// JANE STREET PATTERN:
[code example]
```

**Affected Files**:
- [List of files]

**Codacy Suppression**:
```yaml
[Suppression config]
```

**Rationale**:
[Detailed explanation of why Jane Street pattern is superior]

**Trade-offs**:
- ✅ [Benefits]
- ❌ [Costs]

**Approval**: [Director/Architect] (YYYY-MM-DD)

**References**:
- [Links to related docs]

---

## Quarterly Review Checklist

- [ ] Verify all deviations still provide measurable performance benefit
- [ ] Check if new .NET versions offer zero-cost alternatives
- [ ] Confirm Codacy suppressions are still necessary
- [ ] Update rationale if Jane Street patterns evolve
- [ ] Archive obsolete deviations

**Last Review**: 2026-05-27  
**Next Review**: 2026-08-27  
**Reviewer**: [Name]