using UnityEngine;

public enum TurretType
{
    DirectFire,    // Straight-line bullets (rifles, cannons, etc.)
    Artillery,     // Ballistic arc bullets with gravity
    Hammer         // Melee range with AOE damage
}

[CreateAssetMenu(fileName = "NewTurret", menuName = "Components/Turret")]
public class TurretData : ComponentData
{
    [Header("Turret Type")]
    public TurretType turretType = TurretType.DirectFire;
    
    [Header("Animation")]
    [Tooltip("Optional animation prefab for weapons with firing animations (e.g., HammerDown for Hammer weapon)")]
    public GameObject animationPrefab;
    
    [Header("Death Model")]
    [Tooltip("Optional damaged/destroyed model to show when tank dies")]
    public GameObject deathModelPrefab;
    
    [Header("Combat Stats")]
    public int damage;
    public float range;              // Maximum firing range
    public float shotspersec;
    public float bulletSpeed = 50f;  // Speed of fired projectiles
    public string knockback;
    
    [Header("Vision System")]
    public float visionRange = 60f;  // How far the turret can detect enemies (separate from firing range)
    [Range(10f, 180f)]
    public float visionCone = 45f;   // Field of view angle in degrees
}


