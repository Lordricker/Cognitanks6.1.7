# Communications (Coms) Feature Implementation

## Overview
The Communications feature allows tanks to share target information with their allies, enabling team-based coordination. Tanks can now access targets that their allies can see, even if those targets are outside their personal vision range.

## How It Works

### 1. Target Broadcasting System
Every tank continuously broadcasts its **target list** which includes:
- **Self** (the tank itself)
- **All detected enemies** within its personal vision cone

This information is shared with all allies (infinite range, no distance limit).

### 2. Ally Target List
Each tank maintains an `allyTargetList` that aggregates:
- All targets from every ally's target list
- Updated every sensor update cycle
- Accessible only when the AI tree has a **Coms node** in its parent chain

### 3. The Coms Node
The `IfComs` node acts as a **flag/marker** in the AI tree:
- Always returns `true` (allows flow to continue)
- Signals to child nodes that they can use the `allyTargetList`
- Does NOT restrict range (infinite range by design)

## AI Tree Usage

### Example Tree Structure
```
Coms → IfEnemy → Chase
```

**Behavior:**
- If this tank OR any ally can see an enemy, the `IfEnemy` check will succeed
- The tank can chase enemies that allies see, even far outside personal vision
- Without the Coms parent, only personal vision targets are used

### Another Example
```
IfEnemy → Chase    (personal vision only)
Coms → IfEnemy → FlankEnemy    (team vision)
Wander    (fallback)
```

**Behavior:**
- First branch: Chase enemies in personal vision
- Second branch: Flank enemies seen by team (even if outside personal vision)
- Third branch: Wander if no enemies found by team

## Updated Condition Nodes

### IfEnemy
- **Without Coms parent:** Uses only personal `detectedEnemies` list
- **With Coms parent:** Checks personal vision first, then searches `allyTargetList` for enemies
- Sets `currentTarget` to the closest enemy found

### IfAlly
- **Without Coms parent:** Uses only personal `detectedAllies` list
- **With Coms parent:** Checks personal vision first, then searches `allyTargetList` for allies
- Sets `currentTarget` to the closest ally found

### IfAny
- **Without Coms parent:** Checks if `currentTarget` exists
- **With Coms parent:** Also checks `allyTargetList` for any target
- Sets `currentTarget` to the closest target found

### IfComs
- Always returns `true`
- Acts as a marker for child nodes to enable communications

## Technical Implementation

### New Fields in TankMan.cs
```csharp
private List<GameObject> allyTargetList = new List<GameObject>();
private List<GameObject> myTargetList = new List<GameObject>();
```

### Key Methods

#### `GetMyTargetList()`
Public method that returns this tank's target list for allies to read.

#### `UpdateSensorData()`
Updated to:
1. Clear and rebuild `myTargetList` (self + detected enemies)
2. Query all detected allies for their target lists
3. Aggregate into `allyTargetList`

#### `HasComsParent(node, tree)`
Helper method that:
1. Traverses the AI tree upward from the given node
2. Checks each parent for `methodName == "IfComs"`
3. Returns `true` if a Coms node is found in the parent chain

## Performance Notes
- Target list sharing happens every sensor update (default: 0.1 seconds)
- No distance limit on communications (by design)
- Ally target lists are deduplicated to avoid duplicates
- Dead tanks are filtered out when selecting targets

## Future Enhancements (Optional)
- Add range limit parameter to Coms node
- Add communication delay/latency
- Add "last known position" for targets that drop out of vision
- Add friendly fire warnings
- Add coordinated attack priorities

## Testing Recommendations
1. Create a simple AI tree with `Coms → IfEnemy → Chase`
2. Place two allied tanks with one enemy between them
3. Ensure the tank without direct vision can still chase the enemy
4. Verify behavior without Coms node uses only personal vision
