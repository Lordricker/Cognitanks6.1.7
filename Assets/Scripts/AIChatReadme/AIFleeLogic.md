# AI Flee Logic Documentation

## Overview  

The AI flee system in Cognitanks is designed to be simple and modular. The flee action itself only handles movement in the opposite direction of the nearest enemy, while stopping conditions are managed by the AI tree using vision and range nodes.

## How It Works

### 1. Target Selection

The tank's `currentTarget` is always set to the **nearest detected enemy**:

```csharp
// In TankMan.cs - Target selection logic
foreach (GameObject tank in enemyTanks)
{
    float distance = Vector3.Distance(transform.position, tank.transform.position);
    if (distance < closestDistance)
    {
        closestDistance = distance;
        currentTarget = tank; // Always the closest enemy
    }
}
```

### 2. Flee Direction Calculation

The flee action moves in the **exact opposite direction** of the nearest enemy:

```csharp
// Calculate flee direction (away from target)
Vector3 fleeDirection = (transform.position - currentTarget.transform.position).normalized;
```

This mathematical formula ensures the tank moves directly away from the enemy, not at an angle.

### 3. Simple Flee Action

The `FleeFromTarget()` coroutine is intentionally simple:

```csharp
IEnumerator FleeFromTarget()
{
    while (currentTarget != null)
    {
        // Calculate direction away from enemy
        Vector3 fleeDirection = (transform.position - currentTarget.transform.position).normalized;
        
        // Move a reasonable distance in that direction
        float fleeDistance = 100f;
        Vector3 fleePosition = transform.position + fleeDirection * fleeDistance;
        
        // Clamp to map boundaries and set destination
        fleePosition.x = Mathf.Clamp(fleePosition.x, 30f, 770f);
        fleePosition.z = Mathf.Clamp(fleePosition.z, 30f, 770f);
        navAgent.SetDestination(fleePosition);
        
        // Update direction every 0.3 seconds
        yield return new WaitForSeconds(0.3f);
    }
}
```

**Key Points:**
- No hardcoded distance checks
- No "smart flee distance" logic
- Continues until `currentTarget` becomes null or AI tree transitions

### 4. Stopping Conditions (AI Tree Responsibility)

The flee action does **NOT** handle stopping conditions. Instead, the AI tree uses nodes like:

**Range Detection:**
- `If Range<#` - Check if enemy is within a certain distance
- `If Range>#` - Check if enemy is beyond a certain distance

**Vision Detection:**
- `If Enemy In Vision` - Check if any enemy is visible
- `If No Enemy In Vision` - Check if no enemies are detected

**Health-Based Decisions:**
- `If Self HP>#` - Continue fleeing if health is above threshold
- `If Self HP<#` - Stop fleeing if health is below threshold

## Example AI Tree Logic

Here's how a typical flee scenario would work:

```
1. Tank detects enemy → currentTarget set to nearest enemy
2. Health check: "If Self HP<50" → true, transition to "Flee"
3. Flee action starts → tank moves away from currentTarget
4. AI tree checks: "If Range>100" → false, continue fleeing
5. AI tree checks: "If Range>100" → true, transition to different action
6. Flee action ends, new action begins
```

## Benefits of This Design

### 1. **Separation of Concerns**
- Flee action: Only handles movement direction
- AI tree: Handles all decision-making and stopping conditions

### 2. **Flexibility**
- AI designers can create complex flee behaviors using combinations of nodes
- Different tanks can have different flee strategies without code changes

### 3. **Predictable Behavior**
- Tank always moves in exact opposite direction of nearest enemy
- No complex pathfinding or "smart" distance calculations

### 4. **Easy Debugging**
- Simple flee logic is easy to understand and debug
- AI tree decisions are visible in the editor

## AI Tree Examples

### Basic Flee Until Safe Distance
```
If Self HP<30 → Flee
└─ If Range>80 → Patrol
```

### Flee Until Out of Vision
```
If Enemy In Vision AND If Self HP<50 → Flee
└─ If No Enemy In Vision → Wait
```

### Advanced Flee with Multiple Conditions
```
If Self HP<40 → Flee
├─ If Range>120 AND If Self HP>60 → Attack
└─ If Range>120 AND If Self HP<60 → Patrol
```

## Technical Implementation Details

### File Location
- Main flee logic: `Assets/AiEditor/AIScripts/TankMan.cs`
- Method: `IEnumerator FleeFromTarget()`

### Key Components
- **NavMeshAgent**: Handles pathfinding and movement
- **currentTarget**: Always points to nearest detected enemy
- **Map boundaries**: Flee positions clamped to (30,30) to (770,770)

### Update Frequency
- Flee direction recalculated every 0.3 seconds
- Ensures tank maintains proper direction as enemy moves

## Migration Notes

Previous versions of the flee system included hardcoded distance checks. These have been removed to ensure the AI tree has full control over stopping conditions.

**Old behavior (removed):**
```csharp
// This hardcoded logic was removed
if (distanceToTarget >= range * 2f) {
    break; // Stop fleeing
}
```

**New behavior:**
The flee action continues until the AI tree decides to transition to a different action based on its node conditions.
