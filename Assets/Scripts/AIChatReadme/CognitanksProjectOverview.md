# Cognitanks Project Overview - AI Assistant Reference

**Last Updated:** June 2025
**Project Type:** Unity 3D Tank Combat Game with Visual AI Editor

## Project Purpose
Cognitanks is a Unity-based tank combat game where players can:
1. **Build custom tanks** using modular components (Workshop)
2. **Design AI behaviors** using a visual node-based editor
3. **Battle in arenas** with both singleplayer and multiplayer modes
4. **Compete in leagues** with AI-controlled enemy tanks

## Core Architecture

### 1. Tank System (Workshop)
- **TankSlotData.cs**: Core data container for tank configurations
  - Stores component references (turret, armor, engine prefabs)
  - Contains calculated stats (damage, HP, weight, speed)
  - References AI trees for behavior
  - Supports both player and enemy tanks
  - Uses Unity ScriptableObject pattern

- **TankAssembly.cs**: Instantiates tanks from TankSlotData
  - Spawns visual components at runtime
  - Configures NavMeshAgent for movement
  - Sets up TankMan component for AI execution

- **TankMan.cs**: **CRITICAL COMPONENT** - Unified tank management
  - Replaces old Master scripts (NavAIMaster, TurretAIMaster)
  - Executes AI trees for navigation and turret control
  - Handles sensor data (enemy/ally detection)
  - Manages all movement and combat operations
  - Uses team-based detection via TankTeamInfo

### 2. AI System (AiEditor)
- **AiTreeAsset.cs**: Visual AI behavior trees stored as ScriptableObjects
  - Node-based structure (Conditions, Actions, SubAI)
  - Execution flow: top-down, backtrack-on-false, Y-position priority
  - Separate trees for Navigation AI and Turret AI

- **AI Node Types**:
  - **Conditions**: IfEnemy, IfAlly, IfHP, IfRange, IfRifle, etc.
  - **Actions**: Move, Chase, Flee, Fire, Wander, Wait, TrackTarget
  - **SubAI**: Reference to other AI trees (IMPLEMENTED - allows modular AI composition)

- **AI Execution**: TankMan executes dual coroutines for NavAI and TurretAI simultaneously

### 3. Team System
- **TankTeamInfo.cs**: Component for team-based detection
  - Each tank has a teamId (0=player, 1=enemy, etc.)
  - IsEnemy/IsAlly methods for relationship checking
  - Replaces old layer-based detection system

- **SimpleTeamManager.cs**: Manages team assignments
  - Assigns teams for singleplayer (player vs enemies)
  - Handles multiplayer team distribution
  - Called by ArenaManager on scene start

### 4. Arena System
- **ArenaManager.cs**: Core arena/match controller
  - Spawns tanks from TankSlotData configurations
  - Loads enemy tanks dynamically based on league/round
  - Supports both singleplayer and multiplayer modes
  - Integrates with team assignment system

- **LeagueDropdownManager.cs**: UI for selecting enemy difficulty
  - Dynamically loads enemy tanks from Assets/Workshop/TankSlotData/Enemies/
  - Structure: League1/Round1/, League1/Round2/, League2/Round1/, etc.

### 5. Component System
- **BaseClass.cs (ComponentData)**: Base class for all tank components
  - Turret, Armor, EngineFrame, AITree inherit from this
  - Provides unified component management
  - Supports visual customization via colors

### 6. Universal Bullet System
- **BulletScript.cs**: Single bullet prefab handles all projectiles
  - All tanks fire the same BulletObject prefab from Resources/Prefabs/
  - Bullets inherit stats from firing tank (damage, range, speed, team)
  - Distance-based lifetime (destroys after traveling max range)
  - Team-based damage (only harms enemy tanks)
  - Supports both DirectFire and Artillery physics modes

- **Turret Types**:
  - **DirectFire**: Straight-line bullets, no gravity
  - **Artillery**: Ballistic arc bullets with gravity simulation

- **Integration**: 
  - TankAssembly loads bullet prefab and assigns to TankMan
  - FirePoint transforms found automatically in turret hierarchies
  - AI nodes control firing logic (CanFire checks 2-degree aim accuracy)

## Key File Locations

### Core Systems
- `Assets/AiEditor/AIScripts/TankMan.cs` - **Most important script**
- `Assets/Scripts/TankTeamInfo.cs` - Team detection system
- `Assets/Scripts/SimpleTeamManager.cs` - Team assignment
- `Assets/Workshop/TankSlotData/TankSlotData.cs` - Tank data containers

### Workshop/Tank Building
- `Assets/Workshop/TankSlotData/TankAssembly.cs` - Tank instantiation
- `Assets/Workshop/ComponentData/ScriptableObjects/` - Component definitions
- `Assets/Workshop/UI/` - Workshop interface scripts

### Arena/Combat
- `Assets/Arenas/Scripts/ArenaManager.cs` - Match controller
- `Assets/Workshop/UI/LeagueDropdownManager.cs` - Enemy selection

### AI System
- `Assets/AiEditor/AISaveFiles/AiTreeAsset.cs` - AI behavior trees
- `Assets/AiEditor/Scripts/` - Visual AI editor interface

## Recent Major Changes (June 2025)

### AI Instance ID System Overhaul (June 2025)
- **CLEANED**: AI instance IDs now use clean GUIDs only (no title prefixes)
- **FIXED**: Purchase system generates clean GUID instance IDs: `System.Guid.NewGuid().ToString()`
- **SEPARATED**: Instance ID (permanent GUID) from title (user-renamable display name)
- **ENHANCED**: Self-healing system with migration for legacy AI files with old instanceId format
- **IMPROVED**: Sell button robustly deletes AI files by GUID instanceId with title fallback
- **REMOVED**: TreeName redundancy - now only uses title for display and editor interface
- **ADDED**: `CleanupLegacyAIFiles()` automatically migrates old files to new GUID format on startup
- **STRENGTHENED**: Fallback logic in TankSlotButtonUI corrects instanceId mismatches automatically

### SubAI System Implementation (December 2024)
- **IMPLEMENTED**: Modular SubAI nodes allowing AI trees to reference other AI trees
- **ADDED**: ContextMenuUI SubAI file browser with branch-specific folder filtering
- **ENHANCED**: TankMan with SubAI execution logic (LoadSubAITree, ExecuteSubAI, ExecuteSubAIChain)
- **FIXED**: Node type detection for SubAI nodes in AiEditorFileUI
- **SEPARATED**: AI assets into NavFiles/ and TurretFiles/ folders for organization
- **DEBUGGED**: Folder search logic to correctly locate referenced AI assets by branch type

### Team System Overhaul
- **REMOVED**: Layer-based enemy detection (old system)
- **ADDED**: TankTeamInfo component for team-based detection
- **UPDATED**: All FindObjectsOfType calls to FindObjectsByType (Unity API update)
- **ENHANCED**: TankMan with robust team detection and debugging

### AI System Improvements
- **CONSOLIDATED**: Master scripts functionality into TankMan
- **IMPROVED**: AI execution flow with proper backtracking
- **ENHANCED**: Sensor data with vision cone calculations
- **ADDED**: Comprehensive debugging and logging

### Git Repository
- **URL**: https://github.com/Lordricker/Cognitanks
- **Latest Commit**: "updated team creation" (team system overhaul)

## Development Patterns

### Adding New AI Conditions
1. Add case to `TankMan.ExecuteCondition()`
2. Use existing sensor data (detectedEnemies, currentTarget, etc.)
3. Follow naming pattern: "If[Condition]"

### Adding New AI Actions
1. Add case to `TankMan.ExecuteAction()`
2. Implement as coroutine if time-based (StartCoroutine)
3. Handle NavMeshAgent state checking
 