# PR #4 Fix Queue
Generated: 2026-05-25 14:04:01
Updated: 2026-05-25 14:07:00 (Post-Fix)

## Instructions for v12-engineer

Process these issues in priority order. Mark each as FIXED after applying the fix.

### Fix #1 - [P0] CRITICAL
[x] **Bot:** cubic-dev-ai  
[x] **File:** src/V12_002.SIMA.Fleet.cs:482  
[x] **Issue:** Thread-safety regression: removing the `ToArray()` snapshot reintroduces the broker-thread mutation race

**Fix Applied:** Restored `.ToArray()` snapshot on line 481 per Build 939-P0 requirement. Updated comment to document bot correction.

---

### Fix #2 - [P0] CRITICAL
[x] **Bot:** amazon-q-developer  
[x] **File:** src/V12_002.SIMA.Fleet.cs:479-486  
[x] **Issue:** Thread-Safety Violation: The change removes defensive `.ToArray()` snapshot that protected against broker-thread mutations during iteration.

**Fix Applied:** Same as Fix #1 - restored snapshot.

---

### Fix #3 - [P0] CRITICAL
[x] **Bot:** codacy-production  
[x] **File:** src/V12_002.SIMA.Fleet.cs  
[x] **Issue:** Re-introduces critical thread-safety risk. The removal of the defensive snapshot on `acct.Positions` makes the iteration susceptible to `InvalidOperationException`.

**Fix Applied:** Same as Fix #1 - restored snapshot.

---

### Fix #4 - [P0] CRITICAL
[x] **Bot:** sourcery-ai  
[x] **File:** src/V12_002.SIMA.Fleet.cs  
[x] **Issue:** Removing the ToArray() snapshot reintroduces potential concurrent-modification issues and InvalidOperationExceptions.

**Fix Applied:** Same as Fix #1 - restored snapshot.

---

### Fix #5 - [P0] CRITICAL
[x] **Bot:** gemini-code-assist  
[x] **File:** src/V12_002.SIMA.Fleet.cs  
[x] **Issue:** Introduces critical thread-safety risk because the `Positions` collection is updated by the broker thread.

**Fix Applied:** Same as Fix #1 - restored snapshot.

---

### Fix #6 - [P1] REVIEW
[x] **Bot:** coderabbitai  
[x] **File:** src/V12_002.SIMA.Fleet.cs:479-486  
[x] **Issue:** Replace the direct foreach over acct.Positions with a stable snapshot.

**Fix Applied:** Same as Fix #1 - restored snapshot.

---

### Fix #7 - [P2] PERFORMANCE
[x] **Bot:** sourcery-ai  
[x] **File:** N/A  
[x] **Issue:** Reviewer's guide - informational only, no action required.

**Fix Applied:** N/A - informational comment.

---

### Fix #8 - [P2] PERFORMANCE
[x] **Bot:** coderabbitai  
[x] **File:** N/A  
[x] **Issue:** Walkthrough summary - informational only, no action required.

**Fix Applied:** N/A - informational comment.

---

## Summary

**All P0 CRITICAL issues resolved**: Restored `.ToArray()` snapshot on `acct.Positions` (line 481) to prevent broker-thread mutation race condition per Build 939-P0 requirement.

**Remaining optimizations preserved**: The `_followerBrackets` and `activePositions` for-loop optimizations (lines 493-522) remain intact - these are thread-safe because ConcurrentDictionary supports concurrent enumeration.

**Net Result**: 2 LINQ allocations eliminated (lines 493-522), thread-safety maintained (line 481).
