import sys
import os
from datetime import datetime

def bump_version(filepath):
    if not filepath.endswith('.cs'):
        return
    
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    log_file = "C:/WSGTA/universal-or-strategy/docs/BUILD_LOG.txt"
    
    os.makedirs(os.path.dirname(log_file), exist_ok=True)
    with open(log_file, 'a', encoding='utf-8') as f:
        f.write(f"[{timestamp}] Modified: {os.path.basename(filepath)}\n")
    
    print(f"AUTO-VERSION: Logged modification at {timestamp}")

if __name__ == "__main__":
    file_to_bump = os.environ.get('CLAUDE_FILE_PATH')
    if file_to_bump:
        bump_version(file_to_bump)
