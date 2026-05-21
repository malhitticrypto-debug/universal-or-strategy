import os
import sys
import json
import firebase_admin
from firebase_admin import credentials
from firebase_admin import firestore

COLLECTION_NAME = 'jane_street_knowledge_base'
CREDENTIALS_PATH = 'firebase-credentials.json'

def init_firestore():
    """Initializes Firebase using local service account credentials."""
    # Resolve absolute path based on the script's location
    root_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    cred_path = os.path.join(root_dir, CREDENTIALS_PATH)
    
    if not os.path.exists(cred_path):
        print(f"[-] Error: Credentials not found at {cred_path}")
        sys.exit(1)
        
    cred = credentials.Certificate(cred_path)
    if not firebase_admin._apps:
        firebase_admin.initialize_app(cred)
    return firestore.client()

def search_kb(db, term):
    """Fetches the collection and performs a case-insensitive RAG substring search."""
    print(f"[*] Querying Jane Street Knowledge Base for: '{term}'...")
    collection_ref = db.collection(COLLECTION_NAME)
    docs = collection_ref.stream()
    
    term_lower = term.lower()
    matches = []
    
    for doc in docs:
        data = doc.to_dict()
        # Combine all relevant text fields for a full-text substring search
        search_text = " ".join([
            str(doc.id), # Include the ID (e.g. carl_cook_...)
            str(data.get('title', '')),
            str(data.get('category', '')),
            " ".join(data.get('takeaways', [])),
            " ".join(data.get('patterns', []))
        ]).lower()
        
        if term_lower and term_lower in search_text:
            matches.append((doc.id, data))
        elif not term_lower:
            matches.append((doc.id, data))
            
    if not matches:
        print(f"[-] No results found for '{term}'.")
        print("[*] Available documents in collection:")
        # Re-fetch stream for discovery
        docs = db.collection(COLLECTION_NAME).stream()
        for doc in docs:
            print(f"  - {doc.id} ({doc.to_dict().get('title', 'No Title')})")
        return
    print(f"[+] Found {len(matches)} matching document(s):\n")
    for doc_id, data in matches:
        print(f"=== {data.get('title', 'Unknown Title')} ===")
        print(f"Document ID : {doc_id}")
        print(f"Category    : {data.get('category', 'N/A')}")
        
        takeaways = data.get('takeaways', [])
        if takeaways:
            print("Takeaways:")
            for t in takeaways:
                print(f"  - {t}")
                
        print("-" * 40)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python query_kb.py \"<search_term>\"")
        sys.exit(1)
        
    query_term = sys.argv[1]
    db = init_firestore()
    search_kb(db, query_term)
