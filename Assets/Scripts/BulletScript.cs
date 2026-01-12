using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Universal bullet script for all tank projectiles
/// Handles damage, lifetime based on range, team-based collision detection, and artillery physics
/// </summary>
public class BulletScript : MonoBehaviour
{
    [Header("Explosion Effects")]
    [SerializeField] private GameObject bulletSpawnExplosionPrefab; // Muzzle flash effect when bullet is fired
    [SerializeField] private GameObject bulletDeathExplosionPrefab; // Impact/explosion effect when bullet hits or expires
    [SerializeField] private Vector3 explosionScale = new Vector3(10f, 5f, 10f); // Scale modifier for explosions
    [SerializeField] private float explosionFadeDuration = 0.3f; // How long explosions last
    [SerializeField] private float aoeRadius = 50f; // Area of effect radius for explosion damage
    
    [Header("Trail Effect")]
    [SerializeField] private GameObject trailEmitterPrefab; // Particle trail for artillery bullets
    
    [Header("Combat Stats")]
    [SerializeField] private int damage;
    [SerializeField] private float maxRange;
    [SerializeField] private int firingTeamId;
    [SerializeField] private bool isArtillery = false;
    
    [Header("Runtime Data")]
    [SerializeField] private Vector3 startPosition;
    [SerializeField] private bool isInitialized = false;
    private float knockback;
    private TankMan firingTank; // Reference to the tank that fired this bullet (for damage tracking)
    
    /// <summary>
    /// Get the knockback value for manual force application
    /// </summary>
    public float GetKnockbackValue() => knockback;
    
    /// <summary>
    /// Initialize bullet with combat stats from the firing tank
    /// Explosion prefab is assigned directly in the bullet prefab inspector (used for muzzle flash and impact)
    /// </summary>
    public void Initialize(int bulletDamage, float bulletRange, int teamId, bool artilleryMode = false, float bulletKnockback = 1f, float customAoeRadius = -1f, TankMan shooter = null)
    {
        damage = bulletDamage;
        maxRange = bulletRange;
        firingTeamId = teamId;
        isArtillery = artilleryMode;
        knockback = bulletKnockback;
        startPosition = transform.position;
        isInitialized = true;
        firingTank = shooter;
        
        // Apply custom AOE radius if provided (for Hammer weapon)
        if (customAoeRadius > 0f)
        {
            aoeRadius = customAoeRadius;
        }
        
        // Set bullet mass to near-zero - knockback is handled manually via forces
        Rigidbody bulletRb = GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            // Minimal mass to prevent any physics-based tipping
            bulletRb.mass = 0.01f;
            
            // Use continuous collision detection to prevent bullets from passing through ground
            bulletRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
        
        // Play gunshot sound at muzzle position with proper volume
        if (SoundManager.Instance != null && SoundManager.Instance.gunshotSound != null)
        {
            float volume = SoundManager.Instance.masterVolume * SoundManager.Instance.sfxVolume;
            AudioSource.PlayClipAtPoint(SoundManager.Instance.gunshotSound, transform.position, volume);
        }
        
        // Spawn muzzle explosion effect (gunpowder flash)
        if (bulletSpawnExplosionPrefab != null)
        {
            GameObject muzzleExplosion = Instantiate(bulletSpawnExplosionPrefab, transform.position, Quaternion.identity);
            muzzleExplosion.transform.localScale = explosionScale;
            
            // Start fade out coroutine and destroy after specified duration
            BulletScript tempScript = muzzleExplosion.AddComponent<BulletScript>();
            tempScript.StartCoroutine(tempScript.FadeOutExplosion(muzzleExplosion, explosionFadeDuration));
        }
        
        // Spawn trail emitter for artillery bullets only
        if (isArtillery && trailEmitterPrefab != null)
        {
            GameObject trailEmitter = Instantiate(trailEmitterPrefab, transform.position, Quaternion.identity);
            trailEmitter.transform.parent = transform; // Attach to bullet so it follows
            
            // Speed up the trail particles by 5x for better visual effect
            ParticleSystem ps = trailEmitter.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.simulationSpeed = 5f; // 5x faster simulation
                main.startLifetime = 2f; // Auto-destroy particles after 2 seconds
                
                var emission = ps.emission;
                emission.rateOverTime = emission.rateOverTime.constant * 20f; // 5x more particles
            }
        }
        
        // Safety cleanup - destroy bullet after reasonable time even if range isn't reached
        // Artillery bullets get more time due to their longer flight time
        float maxLifetime = isArtillery ? 15f : 10f;
        Destroy(gameObject, maxLifetime);
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        // Rotate bullet to face its velocity direction (especially important for artillery arcs)
        Rigidbody bulletRb = GetComponent<Rigidbody>();
        if (bulletRb != null && bulletRb.linearVelocity.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(bulletRb.linearVelocity.normalized);
        }
        
        // Check if bullet has traveled its maximum range
        float distanceTraveled = Vector3.Distance(startPosition, transform.position);
        if (distanceTraveled >= maxRange)
        {
            Explode();
        }
    }
    
    void FixedUpdate()
    {
        if (!isInitialized || !isArtillery) return;
        
        // For artillery bullets, manually advance physics 2 extra times per frame (3x total speed)
        // This keeps the same arc shape but makes the bullet travel along it faster
        Rigidbody bulletRb = GetComponent<Rigidbody>();
        if (bulletRb != null)
        {
            // Apply 2 extra physics steps worth of gravity and movement
            for (int i = 0; i < 2; i++)
            {
                // Apply extra gravity
                bulletRb.linearVelocity += Physics.gravity * Time.fixedDeltaTime;
                
                // Apply extra movement
                bulletRb.MovePosition(bulletRb.position + bulletRb.linearVelocity * Time.fixedDeltaTime);
            }
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[BulletScript] Bullet collided with: {collision.gameObject.name}");
        
        // Check if we hit a tank
        TankTeamInfo hitTankTeam = collision.gameObject.GetComponent<TankTeamInfo>();
        
        // If no TankTeamInfo on the collider, check the parent (tank parts)
        if (hitTankTeam == null)
        {
            hitTankTeam = collision.gameObject.GetComponentInParent<TankTeamInfo>();
            if (hitTankTeam != null)
            {
                Debug.Log($"[BulletScript] Found TankTeamInfo on parent: {hitTankTeam.name}");
            }
        }
        else
        {
            Debug.Log($"[BulletScript] Found TankTeamInfo directly on: {hitTankTeam.name}");
        }
        
        if (hitTankTeam != null)
        {
            Debug.Log($"[BulletScript] Hit tank team {hitTankTeam.teamId}, bullet fired by team {firingTeamId}");
            
            // Only damage enemies (different team)
            if (hitTankTeam.teamId != firingTeamId)
            {
                // Apply damage to the tank
                TankMan hitTank = hitTankTeam.GetComponent<TankMan>();
                if (hitTank == null)
                {
                    hitTank = hitTankTeam.GetComponentInParent<TankMan>();
                }
                
                if (hitTank != null)
                {
                    // For artillery, skip direct damage - only use AOE damage from Explode()
                    if (!isArtillery)
                    {
                        hitTank.TakeDamage(damage);
                        
                        // Record damage dealt for match stats
                        RecordDamageDealt(damage);
                        
                        Debug.Log($"[BulletScript] Hit enemy tank {hitTank.name} for {damage} direct damage (bullet mass: {knockback})");
                    }
                    else
                    {
                        Debug.Log($"[BulletScript] Artillery hit enemy tank {hitTank.name} - using AOE damage only");
                    }
                    
                    // Play bullet hit sound at collision point
                    if (SoundManager.Instance != null)
                        SoundManager.Instance.PlayBulletHitAtPosition(collision.contacts[0].point);
                }
                else
                {
                    Debug.Log($"[BulletScript] Could not find TankMan component on {hitTankTeam.name}");
                }
            }
            else
            {
                Debug.Log($"[BulletScript] Hit friendly tank {hitTankTeam.name} - no damage");
            }
        }
        else
        {
            Debug.Log($"[BulletScript] Hit non-tank object: {collision.gameObject.name}");
        }
        
        // Explode on any collision
        Explode();
    }
    
    /// <summary>
    /// Handle bullet explosion/destruction with AOE damage and knockback
    /// </summary>
    void Explode()
    {
        Debug.Log($"[BulletScript] Bullet exploded at {transform.position}");
        
        // Only apply AOE damage for artillery bullets
        if (isArtillery)
        {
            // Apply AOE damage and knockback to all tanks in radius
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, aoeRadius);
            HashSet<TankMan> damagedTanks = new HashSet<TankMan>(); // Track tanks already damaged to prevent multiple hits
            int totalAoeDamageDealt = 0; // Track total AOE damage for stats
            
            foreach (Collider hitCollider in hitColliders)
            {
                // Check if we hit a tank
                TankTeamInfo hitTankTeam = hitCollider.GetComponent<TankTeamInfo>();
                if (hitTankTeam == null)
                {
                    hitTankTeam = hitCollider.GetComponentInParent<TankTeamInfo>();
                }
                
                if (hitTankTeam != null && hitTankTeam.teamId != firingTeamId)
                {
                    // Found enemy tank - apply damage
                    TankMan hitTank = hitTankTeam.GetComponent<TankMan>();
                    if (hitTank == null)
                    {
                        hitTank = hitTankTeam.GetComponentInParent<TankMan>();
                    }
                    
                    if (hitTank != null && !damagedTanks.Contains(hitTank))
                    {
                        damagedTanks.Add(hitTank); // Mark this tank as damaged
                        
                        // Calculate distance-based damage falloff (full damage at center, 50% at edge)
                        float distance = Vector3.Distance(transform.position, hitTank.transform.position);
                        float damageMultiplier = 1f - (distance / aoeRadius) * 0.5f; // 100% to 50% based on distance
                        int aoeDamage = Mathf.RoundToInt(damage * damageMultiplier);
                        
                        Debug.Log($"[BulletScript] Artillery AOE hit {hitTank.name} at distance {distance:F1}, damage: {aoeDamage} (base: {damage}, multiplier: {damageMultiplier:F2})");
                        hitTank.TakeDamage(aoeDamage);
                        totalAoeDamageDealt += aoeDamage;
                        
                        // Apply knockback force away from explosion center
                        if (hitTank.Rb != null)
                        {
                            Vector3 knockbackDirection = (hitTank.transform.position - transform.position).normalized;
                            float knockbackMultiplier = 1f - (distance / aoeRadius); // Stronger at center
                            float knockbackForce = knockback * knockbackMultiplier;
                            hitTank.Rb.AddForce(knockbackDirection * knockbackForce, ForceMode.Impulse);
                        }
                    }
                }
            }
            
            // Record total AOE damage dealt for stats
            if (totalAoeDamageDealt > 0)
            {
                RecordDamageDealt(totalAoeDamageDealt);
            }
        }
        
        // Spawn explosion effect if available
        if (bulletDeathExplosionPrefab != null)
        {
            GameObject explosion = Instantiate(bulletDeathExplosionPrefab, transform.position, Quaternion.identity);
            explosion.transform.localScale = isArtillery ? explosionScale * 5f : explosionScale;
            
            // Start fade out coroutine and destroy after specified duration
            BulletScript tempScript = explosion.AddComponent<BulletScript>();
            tempScript.StartCoroutine(tempScript.FadeOutExplosion(explosion, explosionFadeDuration));
        }
        
        // Destroy the bullet
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Records damage dealt by the firing tank for match stats
    /// </summary>
    void RecordDamageDealt(float damageAmount)
    {
        if (firingTank != null && MatchStatsManager.Instance != null)
        {
            MatchStatsManager.Instance.RecordDamageDealt(firingTank, damageAmount);
        }
    }
    
    /// <summary>
    /// Fade out explosion effect over specified duration by manually fading each particle
    /// </summary>
    public System.Collections.IEnumerator FadeOutExplosion(GameObject explosion, float duration)
    {
        // Get all particle systems in the explosion
        ParticleSystem[] particleSystems = explosion.GetComponentsInChildren<ParticleSystem>();
        
        // Store references for particle manipulation
        ParticleSystem.Particle[][] particleArrays = new ParticleSystem.Particle[particleSystems.Length][];
        Color[][] originalColors = new Color[particleSystems.Length][];
        
        // Initialize particle arrays for each system
        for (int i = 0; i < particleSystems.Length; i++)
        {
            int maxParticles = particleSystems[i].main.maxParticles;
            particleArrays[i] = new ParticleSystem.Particle[maxParticles];
            originalColors[i] = new Color[maxParticles];
        }
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration); // 1 to 0 over duration
            
            // Fade particles in each particle system
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null) continue;
                
                // Get current active particles
                int numParticles = ps.GetParticles(particleArrays[i]);
                
                // Store original colors on first frame
                if (elapsed <= Time.deltaTime)
                {
                    for (int j = 0; j < numParticles; j++)
                    {
                        originalColors[i][j] = particleArrays[i][j].startColor;
                    }
                }
                
                // Fade each particle
                for (int j = 0; j < numParticles; j++)
                {
                    Color color = originalColors[i][j];
                    color.a *= alpha; // Multiply original alpha by fade alpha
                    particleArrays[i][j].startColor = color;
                }
                
                // Apply modified particles back to the system
                ps.SetParticles(particleArrays[i], numParticles);
            }
            
            yield return null;
        }
        
        // Destroy after fade completes
        Destroy(explosion);
    }
    
    void OnDrawGizmos()
    {
        if (isInitialized)
        {
            // Draw line showing distance traveled vs max range
            Gizmos.color = Color.red;
            Gizmos.DrawLine(startPosition, transform.position);
            
            // Draw sphere at max range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(startPosition, maxRange);
            
            // Draw AOE radius
            Gizmos.color = Color.orange;
            Gizmos.DrawWireSphere(transform.position, aoeRadius);
        }
    }
}
