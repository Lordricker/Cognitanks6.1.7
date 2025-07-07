# COGNITANKS SYSTEM MAP - CONSOLIDATED REFERENCE
*Last Updated: July 2025 - Post JSON Migration*

## ARCHITECTURE TREE
```
Cognitanks
├── Workshop (Tank Building)
│   ├── TankSlotJsonManager [SINGLETON] → manages 10 JSON slots in AppData
│   ├── TankAssembly → spawns tanks from JSON data
│   └── UI → WorkshopUIManager, TankSlotButtonUI, ComponentEntryUI
├── Arena (Combat)
│   ├── ArenaManager → loads tanks from JSON, spawns via TankAssembly
│   ├── TankMan [CORE] → AI execution, combat, movement from JSON data
│   └── Bullets → BulletScript (universal projectile system)
├── AI System
│   ├── JSON Storage → AppData/AiTrees/{NavFiles,TurretFiles}/*.json
│   ├── Editor → AiEditorFileUI, ContextMenuUI, visual node editor
│   └── Execution → TankMan.ExecuteNavAI(), ExecuteTurretAI()
└── Data Storage
    ├── Tank Slots → AppData/TankSlotData/TankSlot X.json (0-9)
    ├── AI Files → AppData/AiTrees/{Nav,Turret}Files/*.json
    └── Enemies → Resources/Workshop/TankSlotData/Enemies/League#/Round#/
```

## CRITICAL COMPONENTS

### TankMan.cs [PRIMARY CONTROLLER]
```csharp
// DATA SOURCE: TankSlotDataJson (from persistent storage)
// FUNCTION: Unified tank management (replaces old Master scripts)
class TankMan {
    TankSlotDataJson tankSlotData;           // JSON data reference
    AiTreeAsset runtimeNavAI, runtimeTurretAI; // Loaded from JSON by instanceId
    
    // CORE METHODS
    SetTankSlotData(TankSlotDataJson)        // Called by TankAssembly
    LoadAIFromInstanceId(string)             // Loads AI from AppData JSON
    ExecuteNavAI(), ExecuteTurretAI()        // Parallel AI execution
    UpdateSensorData()                       // Enemy/ally detection
    Fire()                                   // Universal bullet system
}
```

### TankSlotJsonManager.cs [DATA MANAGER]
```csharp
// STORAGE: AppData/LocalLow/DefaultCompany/Cognitanks 1_7f/TankSlotData/
// FUNCTION: JSON-based tank slot management (replaces ScriptableObjects)
class TankSlotJsonManager : MonoBehaviour {
    static TankSlotJsonManager Instance;     // Singleton pattern
    
    // AUTO-CREATES 10 default slots if missing
    // CALCULATES component stats on assignment
    // PERSISTS all changes to AppData immediately
    
    LoadTankSlot(int slotIndex) -> TankSlotDataJson
    SaveTankSlot(TankSlotDataJson, int slotIndex)
    AssignComponentToSlot(string instanceId, ComponentType, int slot)
}
```

### TankSlotDataJson [DATA STRUCTURE]
```json
{
  "slotNumber": 0,
  "isActive": true,
  "tankName": "Default Tank",
  "teamId": 0,
  "turretInstanceId": "turret_123",          // Component references
  "armorInstanceId": "armor_456",
  "engineInstanceId": "engine_789",
  "navAIInstanceId": "nav_ai_abc",           // AI references
  "turretAIInstanceId": "turret_ai_def",
  "totalWeight": 150.5,                      // Calculated stats
  "enginePower": 75,
  "armorHP": 200,
  "turretDamage": 50,
  // ... more calculated stats
  "slotColor": {"r": 1.0, "g": 0.0, "b": 0.0, "a": 1.0}
}
```

## DATA FLOW

### Tank Spawning Sequence
```
1. ArenaManager.Start()
2. → TankSlotJsonManager.LoadTankSlot(0) // Active slot
3. → TankAssembly.Assemble(TankSlotDataJson)
4. → GameObject tank = Instantiate(tankPrefab)
5. → TankMan tankMan = tank.AddComponent<TankMan>()
6. → tankMan.SetTankSlotData(slotData)
7. → tankMan.LoadAIFromInstanceId(slotData.navAIInstanceId)
8. → tankMan.StartAI()
```

### Component Assignment Flow
```
1. Player clicks component in Workshop UI
2. → WorkshopUIManager.OnComponentClick()
3. → TankSlotJsonManager.AssignComponentToSlot()
4. → Component stats written to JSON
5. → JSON saved to AppData immediately
6. → UI refreshed to show assignment
```

### AI Assignment Flow
```
1. Player selects AI in Workshop UI
2. → PlayerDataManager.LoadAIFromFile()
3. → AI instanceId stored in tank slot JSON
4. → Tank spawning loads AI by instanceId from AppData
```

## FILE LOCATIONS

### Persistent Data (AppData)
```
AppData/LocalLow/DefaultCompany/Cognitanks 1_7f/
├── TankSlotData/
│   ├── TankSlot 0.json ... TankSlot 9.json    // 10 tank slots
├── AiTrees/
│   ├── NavFiles/*.json                         // Navigation AI
│   └── TurretFiles/*.json                      // Turret AI
```

### Project Assets
```
Assets/
├── Workshop/TankSlotData/
│   ├── TankSlotJsonManager.cs                  // JSON manager
│   ├── TankAssembly.cs                         // Tank spawning
│   └── TankSlotButtonUI.cs                     // UI components
├── AiEditor/AIScripts/
│   └── TankMan.cs                              // Main controller
├── Scripts/
│   ├── TankTeamInfo.cs                         // Team detection
│   └── SimpleTeamManager.cs                   // Team assignment
└── Resources/Workshop/TankSlotData/Enemies/    // Enemy configs
```

## AI SYSTEM

### Node Types
```
CONDITION: IfEnemy, IfAlly, IfHP, IfRange, IfRifle, IfArmor, IfTag
ACTION: Move, Chase, Flee, Fire, Wander, Wait, TrackTarget, Stop
SUBAI: References other AI trees by name
```

### Execution Pattern
```
1. Top-down evaluation of conditions
2. Y-position priority for connected nodes
3. Backtrack on false conditions
4. Parallel execution: NavAI + TurretAI
5. 0.1s update interval per tree
```

## MIGRATION STATUS ✅ COMPLETE

### From ScriptableObjects to JSON
- ✅ Tank slot data → AppData JSON files
- ✅ AI assignment → instanceId references
- ✅ Component stats → calculated in JSON
- ✅ UI refactored → JSON-based operations
- ✅ Arena spawning → JSON data source
- ✅ All legacy SO code removed

### Key Benefits
- User data persists across Unity updates
- 10 tank slots always available
- AI files in user-accessible location
- Cross-platform compatibility
- No Unity dependencies for data

## DEBUG COMMANDS

### Verify JSON Data
```powershell
# View tank slot
cat "$env:APPDATA\..\LocalLow\DefaultCompany\Cognitanks 1_7f\TankSlotData\TankSlot 0.json"

# List AI files
ls "$env:APPDATA\..\LocalLow\DefaultCompany\Cognitanks 1_7f\AiTrees\NavFiles\"
```

### Common Issues
```
PROBLEM: Tank doesn't spawn
→ CHECK: TankSlot 0.json exists and isActive=true

PROBLEM: AI not executing  
→ CHECK: instanceId exists in AppData/AiTrees/

PROBLEM: Component stats zero
→ CHECK: Component assignment wrote stats to JSON

PROBLEM: Firing doesn't work
→ CHECK: bulletPrefab assigned, firePoint found, turret stats valid
```

---
*This consolidated reference replaces: CognitanksProjectOverview.md, TechnicalImplementation.md, CompleteScriptInventory.md, AISystemUpdates.md*
