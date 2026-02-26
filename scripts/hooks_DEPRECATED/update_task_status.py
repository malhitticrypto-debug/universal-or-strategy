import sys
import os
import re

def update_tasks(filepath):
    if not filepath.endswith('.cs'):
        return
    
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Look for [FIX-DONE: Task Name]
    matches = re.findall(r'\[FIX-DONE:\s*(.*?)\]', content)
    if not matches:
        return

    task_file = "C:/WSGTA/universal-or-strategy/task.md"
    if not os.path.exists(task_file):
        return

    with open(task_file, 'r', encoding='utf-8') as f:
        task_content = f.read()

    original_content = task_content
    for task_name in matches:
        # Simplistic markdown checkbox update
        # Find - [ ] task_name and change to - [x] task_name
        task_pattern = re.compile(rf'- \[ \]\s*.*{re.escape(task_name)}.*', re.IGNORECASE)
        task_content = task_pattern.sub(lambda m: m.group(0).replace('[ ]', '[x]'), task_content)

    if task_content != original_content:
        with open(task_file, 'w', encoding='utf-8') as f:
            f.write(task_content)
        print(f"CORTEX: Automatically checked off tasks matching: {', '.join(matches)}")

if __name__ == "__main__":
    file_path = os.environ.get('CLAUDE_FILE_PATH')
    if file_path:
        update_tasks(file_path)
