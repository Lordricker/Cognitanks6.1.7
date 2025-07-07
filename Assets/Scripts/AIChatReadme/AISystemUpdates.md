# AI EDITOR FEATURES - QUICK REFERENCE

## RECENT UPDATES (July 2025) ✅
- **Tank Firing**: Fixed component stats not copying from Workshop to JSON
- **Debug Cleanup**: Removed Debug.Log statements from TankMan.cs, TankHPBar.cs
- **Compiler Fixes**: Resolved all type mismatches and property name errors
- **JSON Integration**: Components now properly update tank combat stats in real-time

## NUMBER ENTRY SYSTEM

### Supported Nodes (9 types)
```
If Self HP>#    If Self HP<#    If HP < #    If HP > #
If Tag = #      If Tag < #      If Tag > #   
If Range<#      If Range>#
```

### Usage
```
1. Click node → input field appears
2. Type number + Enter → replaces # in label
3. Esc/click away → cancel
```

### Implementation
- `AiEditorFileUI.cs` → context-sensitive input
- `NodeDeleteUI.cs` → node click detection

## FLEE LOGIC

### Behavior
```csharp
// Simplified flee: move opposite to nearest enemy
Vector3 fleeDirection = (transform.position - currentTarget.position).normalized;
navAgent.SetDestination(transform.position + fleeDirection * distance);
```

### AI Tree Control
```
FLEE CONDITIONS (via AI nodes):
- If Range<10 → Flee (start fleeing when enemy close)
- If Range>50 → Stop/Wander (stop fleeing when safe)
- If HP<25 → Flee (flee when low health)
```

### No Built-in Stopping
- Flee continues until AI tree changes action
- AI designer controls all flee logic via nodes

### Technical Files Modified
- `Assets/AiEditor/AIScripts/TankMan.cs` (FleeFromTarget method)

## Benefits

### Number Entry System
- **Cleaner UI**: No visual clutter from permanent input fields
- **Explicit control**: Only specific node types can have numbers
- **Intuitive interaction**: Click-to-edit pattern

### Flee Logic
- **Separation of concerns**: Flee action handles movement, AI tree handles decisions
- **Flexibility**: AI designers can create complex flee behaviors using node combinations
- **Predictable behavior**: Always moves in exact opposite direction of nearest enemy

## Example Usage

### AI Tree for Smart Fleeing
```
Root
├─ If Self HP<30 → Flee
│  └─ If Range>100 → Patrol
└─ If Enemy In Vision → Attack
```

### Number Entry Example
1. Click on "If Range<#" node
2. Type "50" and press Enter
3. Node becomes "If Range<50"

## Documentation Location

All documentation is located in `Assets/Scripts/AIChatReadme/`:
- `NumberEntrySystem.md`: Detailed number entry documentation
- `AIFleeLogic.md`: Detailed flee logic documentation
- `AISystemUpdates.md`: This overview document

## Backward Compatibility

- Existing AI files continue to work
- Old number input components are automatically hidden
- Previous flee behaviors are preserved through AI tree logic
- No breaking changes to existing tanks or AI trees
