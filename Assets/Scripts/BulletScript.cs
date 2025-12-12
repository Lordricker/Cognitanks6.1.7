using UnityEngine;

/// <summary>
/// Universal bullet script for all tank projectiles
/// Handles damage, lifetime based on range, team-based collision detection, and artillery physics
/// </summary>
public class BulletScript : MonoBehaviour
{
    [Header("Explosion Effect")]
    [SerializeField] private GameObject explosionPrefab; // Assign explosion prefab in bullet prefab inspector
    
    [Header("Combat Stats")]
    [SerializeField] private int damage;
    [SerializeField] private float maxRange;
    [SerializeField] private int firingTeamId;
    [SerializeField] private bool isArtillery = false;
    
    [Header("Runtime Data")]
    [SerializeField] private Vector3 startPosition;
    [SerializeField] private bool isInitialized = false;
    
    /// <summary>
    /// Initialize bullet with combat stats from the firing tank
    /// Explosion prefab is assigned directly in the bullet prefab inspector
    /// </summary>
    public void Initialize(int bulletDamage, float bulletRange, int teamId, bool artilleryMode = false)
    {
        damage = bulletDamage;
        maxRange = bulletRange;
        firingTeamId = teamId;
        isArtillery = artilleryMode;
        startPosition = transform.position;
        isInitialized = true;
        
        // Safety cleanup - destroy bullet after reasonable time even if range isn't reached
        // Artillery bullets get more time due to their longer flight time
        float maxLifetime = isArtillery ? 15f : 10f;
        Destroy(gameObject, maxLifetime);
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        // Check if bullet has traveled its maximum range
        float distanceTraveled = Vector3.Distance(startPosition, transform.position);
        if (distanceTraveled >= maxRange)
        {
            Explode();
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
                    hitTank.TakeDamage(damage);
                    Debug.Log($"[BulletScript] Hit enemy tank {hitTank.name} for {damage} damage");
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
    /// Handle bullet explosion/destruction
    /// </summary>
    void Explode()
    {
        Debug.Log($"[BulletScript] Bullet exploded at {transform.position}");
        
        // Spawn explosion effect if available
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            explosion.transform.localScale = new Vector3(10f, 5f, 10f); // Scale explosion - width 10x, height 5x for rounder shape
            
            // Start fade out coroutine and destroy after 0.3 seconds
            BulletScript tempScript = explosion.AddComponent<BulletScript>();
            tempScript.StartCoroutine(tempScript.FadeOutExplosion(explosion, 0.3f));
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
        }
    }
}
