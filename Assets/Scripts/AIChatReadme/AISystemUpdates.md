# AI System Updates - Number Entry & Flee Logic

## Overview

This document summarizes the recent improvements to the Cognitanks AI Editor's number entry system and the tank flee logic. Both systems have been simplified and made more modular.

## Number Entry System Updates

### What Changed
- **Context-sensitive input**: Number input fields only appear when nodes are clicked
- **Specific node support**: Only 9 specific node types support numeric input
- **Clean interface**: No permanent input boxes cluttering the UI

### Supported Node Types
1. `If Self HP>#`
2. `If Self HP<#`
3. `If HP < #`
4. `If HP > #`
5. `If Tag = #`
6. `If Tag < #`
7. `If Tag > #`
8. `If Range<#`
9. `If Range>#`

### User Experience
1. Click on a supported node → number input appears
2. Type a number and press Enter → number replaces # or existing number
3. Press Escape or click elsewhere → cancel input

### Technical Files Modified
- `Assets/AiEditor/AISaveFiles/AiEditorFileUI.cs`
- `Assets/AiEditor/Scripts/NodeDeleteUI.cs`

## Flee Logic Updates

### What Changed
- **Simplified flee action**: Only handles movement in opposite direction of nearest enemy
- **Removed hardcoded distance checks**: AI tree now handles all stopping conditions
- **Exact opposite direction**: Mathematical precision in flee direction calculation

### How It Works
1. **Target Selection**: `currentTarget` is always the nearest detected enemy
2. **Direction Calculation**: Tank moves in exact opposite direction using `(transform.position - currentTarget.transform.position).normalized`
3. **No Built-in Stopping**: Flee continues until AI tree transitions to different action

### AI Tree Integration
The AI tree uses nodes to control when to start/stop fleeing:
- **Range nodes**: `If Range<#`, `If Range>#`
- **Vision nodes**: `If Enemy In Vision`, `If No Enemy In Vision`
- **Health nodes**: `If Self HP<#`, `If Self HP>#`

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
