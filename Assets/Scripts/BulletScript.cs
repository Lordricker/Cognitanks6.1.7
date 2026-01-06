using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Universal bullet script for all tank projectiles
/// Handles damage, lifetime based on range, team-based collision detection, and artillery physics
/// </summary>
public class BulletScript : MonoBehaviour
{
    [Header("Explosion Effect")]
    [SerializeField] private GameObject explosionPrefab; // Assign explosion prefab in bullet prefab inspector
    [SerializeField] private Vector3 explosionScale = new Vector3(10f, 5f, 10f); // Scale modifier for explosions
    [SerializeField] private float explosionFadeDuration = 0.3f; // How long explosions last
    [SerializeField] private float aoeRadius = 50f; // Area of effect radius for explosion damage
    
    [Header("Combat Stats")]
    [SerializeField] private int damage;
    [SerializeField] private float maxRange;
    [SerializeField] private int firingTeamId;
    [SerializeField] private bool isArtillery = false;
    
    [Header("Runtime Data")]
    [SerializeField] private Vector3 startPosition;
    [SerializeField] private bool isInitialized = false;
    private float knockback;
    
    /// <summary>
    /// Get the knockback value for manual force application
    /// </summary>
    public float GetKnockbackValue() => knockback;
    
    /// <summary>
    /// Initialize bullet with combat stats from the firing tank
    /// Explosion prefab is assigned directly in the bullet prefab inspector (used for muzzle flash and impact)
    /// </summary>
    public void Initialize(int bulletDamage, float bulletRange, int teamId, bool artilleryMode = false, float bulletKnockback = 1f)
    {
        damage = bulletDamage;
        maxRange = bulletRange;
        firingTeamId = teamId;
        isArtillery = artilleryMode;
        knockback = bulletKnockback;
        startPosition = transform.position;
        isInitialized = true;
        
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
        if (explosionPrefab != null)
        {
            GameObject muzzleExplosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            muzzleExplosion.transform.localScale = explosionScale;
            
            // Start fade out coroutine and destroy after specified duration
            BulletScript tempScript = muzzleExplosion.AddComponent<BulletScript>();
            tempScript.StartCoroutine(tempScript.FadeOutExplosion(muzzleExplosion, explosionFadeDuration));
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
        // Apply AOE damage and knockback to all tanks in radius
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, aoeRadius);
        HashSet<TankMan> damagedTanks = new HashSet<TankMan>(); // Track tanks already damaged to prevent multiple hits
        
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
        
        // Spawn explosion effect if available
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            explosion.transform.localScale = explosionScale;
            
            // Start fade out coroutine and destroy after specified duration
            BulletScript tempScript = explosion.AddComponent<BulletScript>();
            tempScript.StartCoroutine(tempScript.FadeOutExplosion(explosion, explosionFadeDuration));
        }
        
        // Destroy the bullet
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Fade out explosion effect over specified duration
    /// </summary>
    public System.Collections.IEnumerator FadeOutExplosion(GameObject explosion, float duration)
    {
        // Get all renderers in the explosion (particles, meshes, etc.)
        Renderer[] renderers = explosion.GetComponentsInChildren<Renderer>();
        ParticleSystem[] particleSystems = explosion.GetComponentsInChildren<ParticleSystem>();
        
        // Store original colors/alphas
        var originalColors = new System.Collections.Generic.Dictionary<Material, Color>();
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    originalColors[mat] = mat.color;
                }
            }
        }
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration); // 1 to 0 over duration
            
            // Fade all materials
            foreach (var kvp in originalColors)
            {
                Color color = kvp.Value;
                color.a = alpha;
                kvp.Key.color = color;
            }
            
            // Fade particle systems
            foreach (var ps in particleSystems)
            {
                var main = ps.main;
                Color startColor = main.startColor.color;
                startColor.a = alpha;
                main.startColor = startColor;
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
