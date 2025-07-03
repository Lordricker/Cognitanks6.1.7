# Cognitanks Complete Script Inventory

## Core Tank System

### Tank Management
- **`TankMan.cs`** - **[CRITICAL]** Main tank controller, replaces old Master scripts
  - AI execution (NavAI + TurretAI)
  - Sensor data and targeting
  - Movement via NavMeshAgent
  - Combat and health management
  - Universal bullet firing system
  - Team-based detection

- **`TankSlotData.cs`** - ScriptableObject tank configuration data
  - Component references (prefabs, AI trees)
  - Calculated stats (damage, HP, weight, speed, bulletSpeed, turretType)
  - Player vs enemy tank settings

- **`TankAssembly.cs`** - Tank instantiation and setup
  - Spawns visual components
  - Configures NavMeshAgent
  - Creates and configures TankMan
  - Loads universal bullet prefab from Resources
  - Finds and assigns FirePoint transforms

### Universal Bullet System
- **`BulletScript.cs`** - Universal projectile behavior
  - Distance-based lifetime management
  - Team-based damage application
  - Support for DirectFire and Artillery physics
  - Inherits stats from firing tank

### Team System
- **`TankTeamInfo.cs`** - Component for team-based detection
  - Team ID storage
  - IsEnemy/IsAlly relationship methods

- **`SimpleTeamManager.cs`** - Team assignment manager
  - Singleplayer: player vs enemies
  - Multiplayer: distributed teams

## AI System (AiEditor)

### Core AI
- **`AiTreeAsset.cs`** - AI behavior tree ScriptableObject
  - Node and connection data
  - Execution flow management
  - SubAI node support
  - Clean GUID instance ID generation
  - Legacy TreeName compatibility for editor interface

- **`AiMethodConverter.cs`** - AI data conversion utilities
- **`AiEditorFileUI.cs`** - AI file management interface
  - SubAI node type detection (nodeType 3)
  - Asset save/load with proper node type assignment
  - Title-based save operations (separate from instance ID)

### AI Editor Interface
- **`CanvasPanZoom.cs`** - AI editor camera controls
- **`ContextMenuUI.cs`** - Right-click context menus
  - SubAI file browser with branch-specific folder filtering
  - SubAI node creation and AI file reference system
  - Branch type detection for correct folder selection
- **`NodeDraggable.cs`** - Node movement in editor
- **`NodeDeleteUI.cs`** - Node deletion interface
- **`OutputButtonDrag.cs`** - Connection creation
- **`UILineConnector.cs`** - Visual connection lines
- **`UILineClickDeleter.cs`** - Connection deletion
- **`InlineNumberInput.cs`** - Parameter input fields
- **`TitleName.cs`** - Node title editing
- **`TargetDebugHandler.cs`** - Debug visualization

## Workshop System

### Component Management
- **`BaseClass.cs (ComponentData)`** - Base class for all components
- **`TurretData.cs`** - Turret component data
  - Combat stats (damage, range, bulletSpeed)
  - TurretType enum (DirectFire, Artillery)
  - Vision system (range, cone)
- **`ArmorData.cs`** - Armor component data
- **`EngineFrameData.cs`** - Engine/chassis component data
- **`TankLoadout.cs`** - Complete tank configuration

### Workshop UI
- **`WorkshopUIManager.cs`** - Main workshop interface controller
  - AI inventory loading from disk folders
  - Clean GUID instance ID management
  - Legacy AI file migration on startup
  - Component purchase/sell with robust AI file operations
  - Tank slot assignment with fallback logic
- **`WorkshopStatsPanel.cs`** - Tank statistics display
- **`WorkshopModelPreview.cs`** - 3D tank preview
- **`TankPreview.cs`** - Real-time tank visualization
- **`ComponentEntryUI.cs`** - Component selection interface
- **`ComponentCustomizationUI.cs`** - Component modification UI
- **`MaterialColorUtility.cs`** - Color customization system
- **`RainbowColorSlider.cs`** - Color picker interface

### Tank Slot Management
- **`ActiveTankslots.cs`** - Active tank slot manager
- **`TankSlotButtonUI.cs`** - Tank slot UI buttons
  - Self-healing AI instanceId correction
  - Fallback logic for legacy AI file references
  - Component assignment with GUID validation
- **`TankSlotActiveButton.cs`** - Active slot button behavior
- **`ActiveSlotButtonUI.cs`** - Slot selection interface

### Data Persistence
- **`PlayerDataManager.cs`** - Save/load player data
- **`LeagueDropdownManager.cs`** - Enemy selection UI

## Arena System

### Arena Management
- **`ArenaManager.cs`** - Main arena/match controller
  - Tank spawning
  - Enemy loading
  - Game mode handling

- **`ArenaUIManager.cs`** - Arena interface management
- **`CameraController.cs`** - Arena camera controls

### Scene Management
- **`SceneManager.cs`** - Scene transition management

## Editor Tools

### Workshop Editors
- **`ListComponentAssets.cs`** - Component asset management
- **`ComprehensivePermanentDataTest.cs`** - Data integrity testing
- **`PermanentDataFixer.cs`** - Data repair utilities
- **`TestPlayerDataRestore.cs`** - Data restoration testing

## Script Organization by Directory

### `/Assets/AiEditor/AIScripts/`
- `TankMan.cs` - **[MOST IMPORTANT]**

### `/Assets/AiEditor/AISaveFiles/`
- `AiTreeAsset.cs`
- `AiMethodConverter.cs`
- `AiEditorFileUI.cs`

### `/Assets/AiEditor/Scripts/`
- All AI editor interface scripts (12 files)

### `/Assets/Scripts/`
- `TankTeamInfo.cs`
- `SimpleTeamManager.cs`
- `SceneManager.cs`

### `/Assets/Arenas/Scripts/`
- `ArenaManager.cs`
- `ArenaUIManager.cs`
- `CameraController.cs`

### `/Assets/Workshop/`
- **`PlayerDataManager.cs`** - Root level
- **`TankSlotData/`** - Tank configuration scripts (5 files)
- **`UI/`** - Workshop interface scripts (9 files)
- **`ComponentData/ScriptableObjects/`** - Component data scripts (5 files)
- **`Editor/`** - Development tools (4 files)

## Script Criticality Levels

### Level 1 - Critical (Core Game Function)
- **`TankMan.cs`** - Tank AI and behavior
- **`TankSlotData.cs`** - Tank data management
- **`TankAssembly.cs`** - Tank instantiation
- **`ArenaManager.cs`** - Match management
- **`AiTreeAsset.cs`** - AI behavior storage

### Level 2 - Important (Major Features)
- **`TankTeamInfo.cs`** - Team detection
- **`SimpleTeamManager.cs`** - Team assignment
- **`PlayerDataManager.cs`** - Data persistence
- **`WorkshopUIManager.cs`** - Workshop interface
- **`LeagueDropdownManager.cs`** - Enemy selection

### Level 3 - Supporting (Feature Components)
- Component data classes (TurretData, ArmorData, etc.)
- Workshop UI components
- AI editor interface scripts
- Camera and scene management

### Level 4 - Utility (Development/Polish)
- Editor tools and testing scripts
- Color and material utilities
- Debug and visualization helpers

## Recent Changes (June 2025)

### SubAI System Implementation (December 2025)
- **`ContextMenuUI.cs`** - Added SubAI file browser and node creation
  - Branch-specific folder filtering (NavFiles/ vs TurretFiles/)
  - SubAI node creation with proper AI file referencing
  - Display of AI tree titles instead of filenames
- **`TankMan.cs`** - Implemented SubAI execution logic
  - ExecuteSubAI() method for loading and executing referenced AI trees
  - LoadSubAITree() with branch-type-based folder searching
  - ExecuteSubAIChain() for recursive SubAI execution
  - Comprehensive debug logging for SubAI operations
- **`AiEditorFileUI.cs`** - Enhanced node type detection
  - SubAI nodes properly identified as nodeType 3
  - GameObject name-based detection for SubAI nodes

### Modified Scripts
- **`TankMan.cs`** - Major overhaul for team system integration
- **`SimpleTeamManager.cs`** - Updated API usage, team assignment logic
- **`ArenaManager.cs`** - Team system integration
- **`LeagueDropdownManager.cs`** - Dynamic enemy loading
- **`TankTeamInfo.cs`** - New team detection system

### API Updates Applied
- All `FindObjectsOfType<T>()` → `FindObjectsByType<T>(FindObjectsSortMode.None)`
- Layer-based detection → Team-based detection
- Deprecated Unity API calls updated

## Development Priority for AI Assistants

1. **First understand**: TankMan.cs - contains 90% of game logic
2. **Then understand**: TankSlotData.cs and TankAssembly.cs - data and instantiation
3. **Next**: Team system (TankTeamInfo + SimpleTeamManager)
4. **Finally**: Workshop and UI systems for feature additions

## Common Modification Points

### Adding AI Behaviors
- Modify `TankMan.ExecuteCondition()` or `TankMan.ExecuteAction()`
- For SubAI: Create modular AI trees in NavFiles/ or TurretFiles/ folders
- Reference SubAI trees using SubAI nodes for modular composition

### Adding Tank Components
- Create new ComponentData subclass
- Add to workshop UI selection
- Handle in TankAssembly.Assemble()

### Modifying Arena Logic
- ArenaManager.cs for spawning and rules
- ArenaUIManager.cs for interface changes

### Changing Team Mechanics
- TankTeamInfo.cs for team relationships
- SimpleTeamManager.cs for team assignment logic

## SubAI System Organization

### Asset Structure
```
Assets/AiEditor/AISaveFiles/
├── NavFiles/               → Navigation AI trees (for Nav branch SubAI references)
│   ├── Traveler.asset     → Example: SubAI node referencing "at.asset"
│   ├── at.asset           → Simple navigation behavior
│   └── [other nav AIs]
├── TurretFiles/           → Turret AI trees (for Turret branch SubAI references)
│   ├── clone.asset        → Example: SubAI node referencing "Ferdinand.asset"
│   ├── Ferdinand.asset    → Turret combat behavior
│   └── [other turret AIs]
└── [Legacy files]         → AI files before folder organization
```

### SubAI Node Properties
- **nodeType**: 3 (AiNodeType.SubAI)
- **originalLabel**: Contains the filename of the referenced AI tree
- **methodName**: Same as originalLabel for display purposes
- **Visual Display**: Shows the referenced AI tree's title/TreeName, not filename

### SubAI Execution Flow
1. **Node Detection**: ContextMenuUI detects current branch type
2. **File Browser**: Populates list from appropriate folder (NavFiles/ or TurretFiles/)
3. **Node Creation**: SubAI node stores reference to selected AI file
4. **Runtime Execution**: TankMan loads and executes referenced AI based on current tree's branch type
5. **Recursive Support**: SubAI trees can reference other SubAI trees

### Key Implementation Files
- **`ContextMenuUI.cs`** - SubAI file browser and node creation
- **`TankMan.cs`** - SubAI execution logic (ExecuteSubAI, LoadSubAITree, ExecuteSubAIChain)
- **`AiEditorFileUI.cs`** - SubAI node type detection and serialization
