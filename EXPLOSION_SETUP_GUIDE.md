# Explosion Effect Setup Guide

## Changes Made

### 1. Fire System Updates
- **Removed range check from `CanFire()`** - Tanks will now shoot at any distance as long as turret is aligned
- **Bullets explode after traveling max range** - The bullet's range stat controls when it explodes
- **Explosions on impact** - Bullets explode when hitting targets or terrain

### 2. New Explosion Prefab Support
- Added `explosionPrefab` field in TankMan inspector under "Projectile Settings"
- BulletScript now spawns explosion effects on impact and range expiration

## Creating a Simple Explosion Effect

### Option 1: Unity Particle System (Recommended)

1. **Create new GameObject:**
   - Right-click in Hierarchy → Effects → Particle System
   - Name it "BulletExplosion"

2. **Configure Particle System:**
   ```
   Main Module:
   - Duration: 0.5
   - Start Lifetime: 0.3-0.5
   - Start Speed: 5-10
   - Start Size: 0.5-2
   - Start Color: Orange gradient to yellow/red
   - Max Particles: 50
   
   Emission:
   - Rate over Time: 0
   - Bursts: 1 burst at time 0, Count: 30-50
   
   Shape:
   - Shape: Sphere
   - Radius: 0.5
   
   Color over Lifetime:
   - Gradient from bright yellow → orange → red → transparent
   
   Size over Lifetime:
   - Start at 1.0, shrink to 0 over lifetime
   
   Renderer:
   - Render Mode: Billboard
   - Material: Default-Particle
   ```

3. **Add Light (Optional):**
   - Add child Light component
   - Type: Point
   - Color: Orange/Yellow
   - Range: 10
   - Intensity: 2
   - Add simple script to fade intensity over time

4. **Save as Prefab:**
   - Drag to Project window to create prefab
   - Assign to TankMan's `explosionPrefab` field

### Option 2: Simple Expanding Sphere

Create a script called `SimpleExplosion.cs`:

```csharp
using UnityEngine;

public class SimpleExplosion : MonoBehaviour
{
    [SerializeField] private float expandSpeed = 10f;
    [SerializeField] private float maxScale = 3f;
    [SerializeField] private float fadeSpeed = 2f;
    
    private Material material;
    private Color startColor;
    
    void Start()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            material = renderer.material;
            startColor = material.color;
        }
    }
    
    void Update()
    {
        // Expand
        transform.localScale += Vector3.one * expandSpeed * Time.deltaTime;
        
        // Fade out
        if (material != null)
        {
            Color c = material.color;
            c.a -= fadeSpeed * Time.deltaTime;
            material.color = c;
        }
        
        // Destroy when done
        if (transform.localScale.x >= maxScale || (material != null && material.color.a <= 0))
        {
            Destroy(gameObject);
        }
    }
}
```

Then:
1. Create Sphere GameObject
2. Scale to 0.5
3. Add SimpleExplosion script
4. Create material with emission color (orange/yellow)
5. Set material to Transparent rendering mode
6. Save as prefab

### Option 3: Asset Store (Polished Look)

**Free Options:**
- "Unity Particle Pack" (free from Unity)
- "Cartoon FX Free" by Jean Moreno
- "Free Stylized Fire/Explosion VFX" by POLYPERFECT

**Paid Options (High Quality):**
- "Cartoon FX Remaster" ($24.99)
- "Epic Toon FX" ($60)
- "Sci-Fi VFX" collections

## Usage

1. **Assign Explosion Prefab:**
   - Select your tank prefab
   - Find "Projectile Settings" in TankMan component
   - Drag your explosion prefab into the "Explosion Prefab" field

2. **Test:**
   - Tank will fire regardless of range (only needs alignment)
   - Bullet explodes after traveling its max range
   - Explosion spawns on impact with targets/terrain

## Tips

- **Explosion Duration:** Current auto-destroy is set to 2 seconds. Adjust in TankMan.Fire() if needed
- **Multiple Explosion Types:** You can create different explosion prefabs for different turret types
- **Sound:** Add AudioSource component to explosion prefab for sound effects
- **Camera Shake:** Consider adding screen shake on explosions for more impact

## Current Behavior

**Before:**
- Turret wouldn't fire if target was beyond weapon range
- Vision range was irrelevant to firing

**After:**
- Turret fires as long as it's aligned (within 2° of target)
- Vision range determines when tank can see targets
- Weapon range determines when bullet explodes (traveled distance)
- Bullets always explode on impact or max range
