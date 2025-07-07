# COGNITANKS AI ASSISTANT REFERENCE - INDEX

**Updated Structure (July 2025) - Post JSON Migration + Debug Cleanup**

## RECENT COMPLETION ✅
- **Debug cleanup**: All Debug.Log statements removed from core scripts
- **Tank firing fix**: Component stats now properly copy from ScriptableObjects to JSON
- **Workshop integration**: Components correctly update TankSlot JSON when equipped
- **Compiler fixes**: All type mismatches and property errors resolved

## MAIN REFERENCE FILES

### 🎯 CONSOLIDATED_SYSTEM_MAP.md
**Primary reference for AI assistants**
- Complete architecture tree
- Critical component specifications  
- Data flow diagrams
- File locations and debug commands
- Replaces: CognitanksProjectOverview.md, TechnicalImplementation.md, CompleteScriptInventory.md

### 📊 JSON_MIGRATION_STATUS.md  
**Migration completion status**
- ScriptableObject → JSON conversion details
- Before/after comparisons
- Verification checklist
- Performance impact analysis

### ⚙️ AISystemUpdates.md
**AI Editor features reference**
- Number entry system (9 supported node types)
- Flee logic implementation
- UI interaction patterns

## USAGE FOR AI ASSISTANTS

### Quick System Understanding
1. Read **CONSOLIDATED_SYSTEM_MAP.md** first
2. Check **JSON_MIGRATION_STATUS.md** for current state
3. Reference **AISystemUpdates.md** for AI editor specifics

### Key Information Locations
```
ARCHITECTURE → CONSOLIDATED_SYSTEM_MAP.md
DATA STORAGE → JSON_MIGRATION_STATUS.md  
AI FEATURES → AISystemUpdates.md
```

### Debugging Support
All files include:
- Structured code examples
- File path specifications
- Debug command references
- Troubleshooting patterns

## REMOVED REDUNDANCY

The following files were **consolidated** to eliminate duplication:
- ❌ CognitanksProjectOverview.md → ✅ CONSOLIDATED_SYSTEM_MAP.md
- ❌ TechnicalImplementation.md → ✅ CONSOLIDATED_SYSTEM_MAP.md  
- ❌ CompleteScriptInventory.md → ✅ CONSOLIDATED_SYSTEM_MAP.md
- ❌ NumberEntrySystem.md → ✅ AISystemUpdates.md
- ❌ AIFleeLogic.md → ✅ AISystemUpdates.md
- ❌ IfSelfBugFix.md (empty) → ✅ Deleted

---
**RESULT**: 3 focused files replace 7+ redundant documents with better structure and computer-readable format.
