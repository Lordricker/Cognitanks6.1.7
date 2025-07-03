using UnityEngine;

public enum TurretType
{
    DirectFire,    // Straight-line bullets (rifles, cannons, etc.)
    Artillery      // Ballistic arc bullets with gravity
}

[CreateAssetMenu(fileName = "NewTurret", menuName = "Components/Turret")]
public class TurretData : ComponentData
{
    [Header("Turret Type")]
    public TurretType turretType = TurretType.DirectFire;
    
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


