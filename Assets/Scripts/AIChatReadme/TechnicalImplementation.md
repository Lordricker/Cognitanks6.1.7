# Cognitanks Technical Implementation Guide

## Core Class Relationships

```
TankSlotData (ScriptableObject)
├── References: AiTreeAsset (navAI, turretAI)
├── References: GameObject (turretPrefab, armorPrefab, engineFramePrefab)
├── Contains: Component stats (damage, HP, weight, bulletSpeed, turretType)
└── Used by: TankAssembly.Assemble()

TankAssembly (MonoBehaviour)
├── Instantiates: Tank visuals from TankSlotData
├── Creates: TankMan component
├── Configures: NavMeshAgent
├── Loads: Universal bullet prefab from Resources/Prefabs/BulletObject
├── Finds: FirePoint transform in turret hierarchy
└── Calls: TankMan.SetTankSlotData(), SetBulletPrefab(), SetTurretComponents()

TankMan (MonoBehaviour) - MAIN TANK CONTROLLER
├── Executes: AI trees via coroutines
├── Manages: Sensor data and targeting
├── Controls: Movement via NavMeshAgent
├── Handles: Combat, health, and universal bullet firing
├── Supports: DirectFire and Artillery turret types
└── Uses: TankTeamInfo for team detection

BulletScript (MonoBehaviour) - UNIVERSAL BULLET SYSTEM
├── Inherits: Damage, range, team, and type from firing tank
├── Handles: Distance-based lifetime destruction
├── Applies: Damage only to enemy tanks
├── Supports: Both direct-fire and artillery physics
└── Attached to: BulletObject prefab

TankTeamInfo (MonoBehaviour)
├── Stores: teamId (0=player, 1=enemy, etc.)
├── Methods: IsEnemy(), IsAlly()
└── Attached to: Every tank GameObject

SimpleTeamManager (MonoBehaviour)
├── Assigns: Team IDs to tanks
├── Called by: ArenaManager.Start()
└── Supports: Both singleplayer and multiplayer

ArenaManager (MonoBehaviour)
├── Spawns: Tanks from TankSlotData
├── Calls: SimpleTeamManager.AssignTeams()
└── Manages: Arena lifecycle
```

## AI System Deep Dive

### AI Tree Structure
```
AiTreeAsset
├── nodes: List<AiNodeData> (visual editor data)
├── connections: List<AiConnectionData> (visual connections)
├── executableNodes: List<AiExecutableNode> (runtime execution data)
└── startNodeId: string (entry point)
```

### AI Execution Flow in TankMan
1. **StartAI()** → Creates dual coroutines
2. **ExecuteNavAI()** → Navigation behavior loop
3. **ExecuteTurretAI()** → Turret behavior loop
4. **ExecuteNode()** → Process individual nodes
5. **UpdateSensorData()** → Detect enemies/allies

### AI Node Execution Pattern
```
Condition Node (true) → Follow connection to next highest Y-value node
Condition Node (false) → Backtrack to parent, check next highest Y-value connected node
Action Node → Execute action, follow connection to next highest Y-value node
SubAI Node → Load and execute referenced AI tree, then continue to next highest Y-value node
No more connections → Restart from beginning
```

**Note**: The execution system prioritizes nodes based on their Y-value position in the visual editor. When multiple nodes are connected, it always selects the one with the next highest Y-value. This ensures predictable execution flow based on the visual layout of the AI tree.

### SubAI System Implementation

#### SubAI Node Execution Flow
1. **ExecuteSubAI()** → Validates SubAI node and extracts referenced AI name
2. **LoadSubAITree()** → Searches appropriate folder based on current tree's branch type
3. **ExecuteSubAIChain()** → Executes the referenced AI tree from its start node
4. **Recursive Support** → SubAI trees can reference other SubAI trees (with depth limits)

#### Folder Organization
```
Assets/AiEditor/AISaveFiles/
├── NavFiles/ → Navigation AI trees (referenced by Nav branch SubAI nodes)
├── TurretFiles/ → Turret AI trees (referenced by Turret branch SubAI nodes)
└── [Legacy files] → Old AI trees before folder organization
```

#### SubAI Node Creation (ContextMenuUI)
```csharp
// Branch-specific file browser
void PopulateSubAIFiles()
{
    string folderName = currentBranch == BranchType.Turret ? "TurretFiles" : "NavFiles";
    // Load and display available AI files from appropriate folder
    // Exclude currently open file to prevent self-reference
}
```

#### SubAI Execution Logic (TankMan)
```csharp
void ExecuteSubAI(AiExecutableNode subAiNode, AiEditor.AiTreeAsset currentTree)
{
    // Load referenced AI tree from correct folder based on currentTree.branchType
    var referencedAI = LoadSubAITree(subAiNode.originalLabel, subAiNode, currentTree);
    
    // Execute the referenced AI tree based on its branch type
    if (referencedAI.branchType == AiEditor.AiBranchType.Nav) {
        // Execute as navigation AI
    } else if (referencedAI.branchType == AiEditor.AiBranchType.Turret) {
        // Execute as turret AI
    }
}
```

## Team System Implementation

### Team Assignment Logic
```csharp
// In SimpleTeamManager.AssignTeams()
if (gameMode == GameMode.Singleplayer)
{
    // Player tanks: teamId = 0
    // Enemy tanks: teamId = 1
}
else if (gameMode == GameMode.Multiplayer)
{
    // Distribute teams evenly
    // teamId = tankIndex % 2
}
```

### Team Detection Logic
```csharp
// In TankTeamInfo
public bool IsEnemy(TankTeamInfo other)
{
    return other != null && other.teamId != this.teamId;
}

public bool IsAlly(TankTeamInfo other)
{
    return other != null && other.teamId == this.teamId && other != this;
}
```

## Movement System

### NavMeshAgent Configuration (in TankAssembly)
```csharp
navAgent.speed = Mathf.Max(1f, enginePower - (totalWeight * 0.1f));
navAgent.angularSpeed = Mathf.Max(30f, 90f - (totalWeight * 0.5f));
navAgent.updateRotation = false; // Manual rotation for terrain following
navAgent.obstacleAvoidanceType = HighQualityObstacleAvoidance;
```

### Terrain Following System (in TankMan)
```csharp
// AlignToTerrain() in LateUpdate
// 4-point raycast for pitch/roll calculation
// Manual Y-axis rotation based on NavAgent velocity
// Rotation limits: ±30° on X and Z axes
```

## Sensor System

### Detection Process (UpdateSensorData in TankMan)
1. **Physics.OverlapSphere()** → Find objects in vision range
2. **Filter by TankTeamInfo** → Only detect tanks
3. **Vision cone check** → Angle calculation vs visionCone
4. **Team classification** → IsEnemy/IsAlly via TankTeamInfo
5. **Target selection** → Closest enemy becomes currentTarget

### Vision Cone Calculation
```csharp
Vector3 visionForward = turretTransform != null ? turretTransform.forward : transform.forward;
float angleToTarget = Vector3.Angle(visionForward, directionToTarget);
bool inVisionCone = angleToTarget <= visionCone * 0.5f; // Half-angle check
```

## Data Management

### ScriptableObject Pattern
- **TankSlotData**: Tank configurations
- **AiTreeAsset**: AI behavior trees  
- **ComponentData**: Base class for all components
- **ArmorData, TurretData, EngineFrameData**: Specific component types

### Player Data Persistence
- **PlayerDataManager**: Handles save/load of player tanks
- **ActiveTankslots**: Manages active tank selections
- Uses Unity's JsonUtility for serialization

## Workshop System

### Component Customization
- **ComponentCustomizationUI**: Handles component selection
- **TankPreview**: Real-time tank visualization
- **WorkshopUIManager**: Coordinates workshop interface

### Tank Building Flow
1. Select components via UI
2. TankSlotData updated with selections
3. TankPreview shows real-time changes
4. Save to PlayerDataManager

## Arena Loading System

### Dynamic Enemy Loading
```csharp
// Path pattern: Assets/Workshop/TankSlotData/Enemies/{league}/{round}/
// Example: Assets/Workshop/TankSlotData/Enemies/League1/Round1/TankSlot 10.asset
```

### Enemy Tank Creation
1. Create TankSlotData in enemy folder
2. Set `isPlayerControlled = false`
3. Assign enemy AI trees
4. Set `teamId = 1`

## Performance Considerations

### AI Update Frequency
- AI coroutines update every `aiUpdateInterval` (default 0.1s)
- Sensor data updates every frame but logs every 60 frames
- Vision calculations cached per update cycle

### NavMesh Optimization
- Obstacle avoidance: High quality for tanks
- Area masks: Can be used for tank-specific navigation
- Update frequency: Unity's built-in NavMesh update rate

## Debug Systems

### TankMan Logging
- Frame-based logging (every 60 frames for regular data)
- Action-specific logging for AI execution
- Sensor detection with detailed team information
- Condition evaluation with reasoning

### Visual Debug
- Scene view raycasts for terrain detection
- Gizmos for vision cones (planned feature)
- NavMesh path visualization (Unity built-in)

## Common Development Patterns

### Adding New Component Types
1. Inherit from ComponentData
2. Set appropriate ComponentCategory
3. Add to workshop UI selection
4. Handle in TankAssembly.Assemble()

### Extending AI Conditions
1. Add case to TankMan.ExecuteCondition()
2. Use existing sensor data variables
3. Return boolean result
4. Add debug logging

### Creating SubAI Trees
1. Create AI trees in appropriate branch folders (NavFiles/ or TurretFiles/)
2. Reference them using SubAI nodes in other AI trees
3. SubAI nodes display the referenced AI's title, not filename
4. Execution automatically loads from correct folder based on current branch context

### Creating Custom Actions
1. Add case to TankMan.ExecuteAction()
2. Implement as coroutine for time-based actions
3. Handle NavMeshAgent state
4. Store in currentActionCoroutine for proper cleanup

## Testing & Debugging

### AI Testing
- Use Debug.Log statements in TankMan (already extensive)
- Monitor sensor detection in console
- Check AI tree connections in visual editor
- Verify team assignments in inspector
- Test SubAI references and folder search logic

### SubAI System Testing
- Verify SubAI nodes reference correct AI files
- Check folder organization (NavFiles/ vs TurretFiles/)
- Test recursive SubAI execution (SubAI referencing other SubAI)
- Monitor debug logs for SubAI loading and execution
- Ensure branch type determines correct folder search

### Movement Testing  
- Ensure NavMesh is baked in arena scenes
- Check spawn point positions
- Verify boundary clamping (30f to 770f)
- Test obstacle avoidance

### Team System Testing
- Verify TankTeamInfo on all tanks
- Check team ID assignments
- Test enemy detection in various scenarios
- Monitor vision cone calculations

## Universal Bullet System

### Overview
All tanks use the same bullet prefab (BulletObject) with stats inherited from the firing tank.

### Bullet Prefab Setup
- **Location**: `Assets/Resources/Prefabs/BulletObject.prefab`
- **Components**: Rigidbody, Collider, BulletScript
- **Physics**: Configured at runtime (gravity on/off based on turret type)

### Firing Process
1. **TankAssembly loads** BulletObject prefab via Resources.Load()
2. **TankMan.Fire()** instantiates bullet at FirePoint
3. **Stats inherited**: damage, range, bulletSpeed, team from TankSlotData
4. **Physics configured**: Direct-fire (no gravity) vs Artillery (with gravity)
5. **BulletScript.Initialize()** sets up bullet behavior

### Turret Types
```csharp
public enum TurretType
{
    DirectFire,    // Straight-line bullets (rifles, cannons)
    Artillery      // Ballistic arc bullets with gravity
}
```

### Artillery Trajectory Calculation
```csharp
// CalculateArtilleryDirection() in TankMan
// Calculates launch angle for ballistic trajectory
// Applies velocity with vertical component for arc
// Uses gravity for realistic projectile physics
```

### Bullet Behavior (BulletScript)
- **Distance tracking**: Destroys bullet after traveling max range
- **Team detection**: Only damages enemy tanks (different teamId)
- **Damage application**: Reduces target tank's health
- **Physics modes**: Gravity on/off based on turret type

### AI Integration
- **CanFire()**: Checks if turret aimed within 2 degrees of target
- **Fire AI node**: Only executes if CanFire() returns true
- **Ferdinand.asset**: Example AI tree with IfEnemy → Fire/CenterTarget logic

### Resource Loading
```csharp
// In TankAssembly.Assemble()
GameObject bulletPrefab = Resources.Load<GameObject>("Prefabs/BulletObject");
tankMan.SetBulletPrefab(bulletPrefab);
```

### FirePoint Detection
```csharp
// FindFirePointRecursive() in TankAssembly
// Searches turret hierarchy for "FirePoint" transform
// Assigns to TankMan for bullet spawn location
```

## AI Instance ID System

### Clean GUID Instance IDs
All AI assets now use clean GUID-only instance IDs for robust file management:

```csharp
// Purchase system generates clean GUID
newComp.instanceId = System.Guid.NewGuid().ToString();

// Example: "f47ac10b-58cc-4372-a567-0e02b2c3d479"
// No title prefixes like "MyAI_f47ac10b-58cc-4372-a567-0e02b2c3d479"
```

### AI File Operations
```csharp
// Purchase: Create file with GUID filename
string assetPath = aiFolder + newComp.instanceId + ".asset";
UnityEditor.AssetDatabase.CreateAsset(newComp, assetPath);

// Load inventory: Set instanceId to filename
string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
aiTreeAsset.instanceId = fileName;

// Sell: Delete by GUID instanceId with title fallback
string assetPath = aiFolder + component.instanceId + ".asset";
UnityEditor.AssetDatabase.DeleteAsset(assetPath);
```

### Legacy Migration System
Automatic cleanup of old instanceId format on startup:

```csharp
void CleanupLegacyAIFiles()
{
    // Find files with old format (contains underscore, not valid GUID)
    if (fileName.Contains("_") && !System.Guid.TryParse(fileName, out _))
    {
        // Generate new clean GUID
        string newInstanceId = System.Guid.NewGuid().ToString();
        
        // Update asset instanceId and rename file
        asset.instanceId = newInstanceId;
        UnityEditor.AssetDatabase.MoveAsset(oldPath, newPath);
        
        // Update all tank slot references
        UpdateTankSlotReferences(oldInstanceId, newInstanceId, asset.branchType);
    }
}
```

### Self-Healing Fallback Logic
Tank slot loading automatically corrects instanceId mismatches:

```csharp
// If GUID lookup fails, search by title and update references
if (foundByTitle)
{
    Debug.LogWarning("Found AI by title fallback - updating instanceId");
    slotData.turretAIInstanceId = correctInstanceId;
    PlayerDataManager.Instance.SavePlayerData();
}
```

### Instance ID vs Title Separation
- **Instance ID**: Permanent GUID for system operations (never changes)
- **Title**: User-friendly display name (can be renamed anytime)
- **TreeName**: Legacy compatibility field (kept in sync with title)

## AI Editor Number Entry System

**Recent Major Improvement**: Redesigned the number entry system to be context-sensitive and explicit.

**Key Features**:
- **Explicit Node Types**: Maintains a predefined list of node types that can have numbers
- **Context-Sensitive Input**: Global TMP_InputField that only appears when a supported node is clicked
- **Smart Label Replacement**: Replaces # placeholders or existing numbers in node labels
- **Clean Interface**: Removed permanent number input boxes for a cleaner UI

**Files**:
- `AiEditorFileUI.cs`: Main implementation with node click handling and global input management
- `InlineNumberInput.cs`: Simplified component for storing numeric values on nodes
- `NumberEntrySystem.md`: Detailed documentation of the system

**Supported Node Types**: Condition nodes (If HP, If Range, etc.) and action nodes (Move, Chase, Fire, etc.)
