import os
import subprocess
import sys

def run_hook(script_name):
    script_path = os.path.join("C:/WSGTA/universal-or-strategy/scripts/hooks", script_name)
    if os.path.exists(script_path):
        subprocess.run([sys.executable, script_path], env=os.environ, check=False)

def main():
    file_path = os.environ.get('CLAUDE_FILE_PATH', '')
    
    # 1. Safety Guard (Linter)
    run_hook("safety_guard.py")
    
    # 2. sync to NinjaTrader (The one we previously added)
    # We'll use a python version for consistency
    if file_path.endswith('.cs'):
        import shutil
        dest = os.path.join("C:/Users/Mohammed Khalid/Documents/NinjaTrader 8/bin/Custom/Strategies/", os.path.basename(file_path))
        try:
            shutil.copy2(file_path, dest)
            print("AUTO-SYNC: Deployed to NinjaTrader.")
        except Exception as e:
            print(f"AUTO-SYNC ERROR: {e}")

    # 3. Auto-Git WIP
    try:
        subprocess.run(["git", "add", file_path], cwd="C:/WSGTA/universal-or-strategy", check=False)
        subprocess.run(["git", "commit", "-m", f"WIP: {os.path.basename(file_path)} modification"], cwd="C:/WSGTA/universal-or-strategy", check=False)
        print("AUTO-GIT: Snapshot saved.")
    except:
        pass

    # 4. Doc Sync
    run_hook("sync_settings_doc.py")
    
    # 5. Versioning
    run_hook("bump_version.py")
    
    # 6. Cortex Tasks
    run_hook("update_task_status.py")

if __name__ == "__main__":
    main()
