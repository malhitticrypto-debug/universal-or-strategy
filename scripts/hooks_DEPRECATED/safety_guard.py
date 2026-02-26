import sys
import re
import os

def check_file(filepath):
    if not filepath.endswith('.cs'):
        return True
    
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    errors = []

    # 1. Semaphore Guard Check
    if '_simaToggleSem.WaitAsync()' in content:
        if 'finally' not in content or '.Release()' not in content:
            errors.append("[SAFETY] Found '_simaToggleSem.WaitAsync()' but no 'finally { Release() }' block. Potential deadlock risk.")

    # 2. Concurrency Lock Check (Grep-style logic)
    lines = content.splitlines()
    for i, line in enumerate(lines):
        # Broaden the check for any mutation to these collections
        if ('activePositions' in line or 'expectedPositions' in line) and ('=' in line or '.Add' in line or '.Remove' in line):
            # Look back 25 lines for a 'lock'
            lookback = lines[max(0, i-25):i+1]
            if not any('lock' in l for l in lookback):
                errors.append(f"[SAFETY] Mutation at line {i+1} might be unguarded: '{line.strip()}'. No 'lock' found in recent lines.")

    if errors:
        print("\n".join(errors))
        return False
    return True

if __name__ == "__main__":
    file_to_check = os.environ.get('CLAUDE_FILE_PATH')
    if file_to_check and os.path.exists(file_to_check):
        if not check_file(file_to_check):
            sys.exit(0) # We don't want to BLOCK the save (friction), just warn the user.
    sys.exit(0)
