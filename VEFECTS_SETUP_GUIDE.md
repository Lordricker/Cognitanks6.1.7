# Vefects Fire VFX URP Setup Guide

## Package Overview

You have the **Vefects Free Fire VFX URP** package installed, which provides high-quality fire and explosion effects optimized for Unity's Universal Render Pipeline (URP).

### What's Included

**Location:** `Assets/Vefects/Free Fire VFX URP/`

- **Particles/** - Ready-to-use explosion/fire prefabs
- **Materials/** - Pre-configured materials for effects
- **Textures/** - Texture maps for particles
- **Shaders/** - Custom URP shaders
- **Lights/** - Point lights for glow effects
- **Audio/** - Sound effects (optional)

## Available Explosion Prefabs

### Small Explosions (Perfect for Bullets)
- `VFX_Fire_01_Small.prefab` - Small fire burst with flames
- `VFX_Fire_01_Small_Simple.prefab` - Lightweight version
- `VFX_Fire_01_Small_Smoke.prefab` - With smoke trail

### Medium Explosions (Good for Larger Projectiles)
- `VFX_Fire_01_Medium.prefab` - Medium explosion
- `VFX_Fire_01_Medium_Simple.prefab` - Lightweight
- `VFX_Fire_01_Medium_Smoke.prefab` - With smoke

### Large Explosions (Tank Destruction)
- `VFX_Fire_01_Big.prefab` - Large explosion
- `VFX_Fire_01_Big_Simple.prefab` - Lightweight
- `VFX_Fire_01_Big_Smoke.prefab` - With smoke

### Floor Effects (Environmental)
- `VFX_Fire_Floor_01/02.prefab` - Ground-based fire
- Various smoke variants

## Setup Instructions

### Step 1: Configure Your Bullet Prefab

1. **Select your bullet prefab** in Project window
2. **Add/Configure BulletScript component:**
   - The script should already be attached
3. **Assign Explosion Effect:**
   - Find the "Explosion Effect" section at the top
   - Drag one of the Vefects prefabs (recommend `VFX_Fire_01_Small_Simple.prefab`)
   - Path: `Assets/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Small_Simple.prefab`

### Step 2: Test the System

1. **Play your scene**
2. **Bullet behavior:**
   - Fires when turret is aligned (no range restriction on firing)
   - Travels at bullet speed
   - **Explodes after traveling max range** (from turret stats)
   - **Explodes on impact** with any object
   - Spawns Vefects explosion effect at explosion point

### Step 3: Recommended Explosion by Weapon Type

```
Rifle/Direct Fire:
- VFX_Fire_01_Small_Simple.prefab (fast, clean)

Artillery:
- VFX_Fire_01_Medium.prefab (bigger impact)

Tank Death:
- VFX_Fire_01_Big.prefab (dramatic)
```

## How the System Works Now

### Architecture Changes

**Before:**
- TankMan held explosion prefab reference
- Explosion was passed to bullet at runtime

**After:**
- Bullet prefab has explosion reference (inspector assignable)
- TankMan only passes combat stats (damage, range, team, artillery mode)
- Bullet manages its own explosion effect

### Bullet Lifecycle

1. **Tank fires** → passes damage, range, teamId, artilleryMode
2. **Bullet spawns** → reads assigned explosion prefab from its own prefab
3. **Bullet travels** → tracks distance from start position
4. **Bullet explodes** when:
   - Distance traveled >= maxRange, OR
   - Hits any object (enemy, terrain, etc.)
5. **Explosion spawns** → Vefects effect instantiates at bullet position
6. **Auto-cleanup** → Explosion destroys after 2 seconds

## Customization Tips

### Adjusting Explosion Duration

In `BulletScript.cs`, line ~118:
```csharp
Destroy(explosion, 2f); // Change this value
```

- Vefects Small effects: 1-1.5 seconds
- Vefects Medium effects: 1.5-2 seconds
- Vefects Big effects: 2-3 seconds

### Multiple Explosion Types

You can create multiple bullet prefab variants:
1. Duplicate your bullet prefab
2. Assign different Vefects explosion to each
3. Use different prefabs for different turret types

Example:
- `Bullet_Rifle.prefab` → Small explosion
- `Bullet_Artillery.prefab` → Medium explosion
- `Bullet_Heavy.prefab` → Big explosion

### Performance Optimization

**For better performance, use "_Simple" variants:**
- Less particles
- Simpler shaders
- Better for many simultaneous explosions

**Example:**
- Instead of: `VFX_Fire_01_Small.prefab`
- Use: `VFX_Fire_01_Small_Simple.prefab`

## Testing Checklist

- [ ] Bullet prefab has BulletScript component
- [ ] Explosion prefab is assigned in bullet inspector
- [ ] Tank can fire (turret aligns within 2°)
- [ ] Bullet explodes after traveling range distance
- [ ] Bullet explodes on impact with targets
- [ ] Vefects explosion spawns at correct position
- [ ] Explosion auto-destroys after duration
- [ ] No console errors

## Troubleshooting

### Explosion doesn't appear
- Check bullet prefab inspector - is explosion assigned?
- Verify Vefects prefab exists at path
- Check console for missing prefab errors

### Explosion doesn't disappear
- Adjust Destroy timer in BulletScript.Explode()
- Check if Vefects prefab has auto-destroy scripts

### Explosion looks wrong in URP
- Verify project uses URP
- Check Vefects shaders are URP versions
- Reimport Vefects package if needed

### Multiple explosions at once cause lag
- Use "_Simple" prefab variants
- Reduce particle count in Vefects prefabs
- Consider object pooling for explosions

## Additional Features to Add (Optional)

### Sound Effects
Vefects includes audio files. To add sound:
1. Add AudioSource to bullet prefab
2. Assign Vefects audio clips
3. Play sound in `BulletScript.Explode()`

### Camera Shake
For more impact, add screen shake when explosions happen:
```csharp
// In Explode() method
Camera.main.GetComponent<CameraShake>()?.Shake(0.2f, 0.3f);
```

### Explosion Damage Radius
Modify `Explode()` to damage nearby tanks:
```csharp
Collider[] hits = Physics.OverlapSphere(transform.position, 5f);
foreach (var hit in hits) {
    TankMan tank = hit.GetComponent<TankMan>();
    if (tank != null) tank.TakeDamage(splashDamage);
}
```

## Summary

✅ **Vefects explosions ready to use**
✅ **Bullet prefab manages its own explosion**
✅ **Range controls bullet lifetime, not firing**
✅ **Explosions on impact and range expiration**
✅ **Professional-quality fire effects**

**Quick Start:** Drag `VFX_Fire_01_Small_Simple.prefab` into your bullet prefab's "Explosion Prefab" field and you're done!
