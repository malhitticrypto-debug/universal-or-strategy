using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NinjaTrader.NinjaScript.Strategies
{
    public class ConcurrencyStressTest
    {
        private readonly object stateLock = new object();
        private Dictionary<string, int> activePositions = new Dictionary<string, int>();

        public async Task TestDeadlockRisk()
        {
            // [STRESS-TEST] WaitAsync without finally/Release
            await _simaToggleSem.WaitAsync();
            
            // Logic here...
            
            // Oops, forgot the Release()!
        }

        public void TestUnguardedMutation()
        {
            // [STRESS-TEST] Unguarded mutation
            // This should be inside a lock(stateLock) block
            activePositions["MNQ"] = 5;
        }
    }
}
