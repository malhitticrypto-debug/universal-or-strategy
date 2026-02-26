import sys
import re
import os

def sync_docs():
    repo_root = "C:/WSGTA/universal-or-strategy"
    properties_file = os.path.join(repo_root, "src/UniversalORStrategyV12_002_Dev.Properties.cs")
    registry_file = os.path.join(repo_root, "docs/SETTINGS_REGISTRY.md")
    
    if not os.path.exists(properties_file):
        return

    with open(properties_file, 'r', encoding='utf-8') as f:
        content = f.read()

    # Regex to find NinjaScript properties
    # [NinjaScriptProperty] ... public [Type] [Name] { get; set; }
    matches = re.findall(r'\[NinjaScriptProperty\].*?public\s+\w+\s+(\w+)\s+\{', content, re.DOTALL)
    
    if not matches:
        return

    doc_content = "# NinjaScript Properties Registry\n\n*Automatically updated by Antigravity Hooks*\n\n"
    doc_content += "| Property Name | Category | Description |\n"
    doc_content += "| :--- | :--- | :--- |\n"
    
    for name in sorted(matches):
        doc_content += f"| {name} | ... | ... |\n"

    os.makedirs(os.path.dirname(registry_file), exist_ok=True)
    with open(registry_file, 'w', encoding='utf-8') as f:
        f.write(doc_content)
    print("AUTO-DOC: Updated SETTINGS_REGISTRY.md")

if __name__ == "__main__":
    file_changed = os.environ.get('CLAUDE_FILE_PATH', '')
    if "Properties.cs" in file_changed:
        sync_docs()
