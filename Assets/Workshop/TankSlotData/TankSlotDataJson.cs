using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JSON representation of TankSlotData for persistent storage outside the Unity project
/// This allows tank configurations to be saved/loaded in builds where ScriptableObjects cannot be modified
/// </summary>
[System.Serializable]
public class TankSlotDataJson
{
    [Header("Tank Configuration")]
    public bool isActive;
    public int teamId;
    
    [Header("Tank Type")]
    public bool isPlayerControlled = true;
    public string displayName = "";
    
    // DEPRECATED: Prefab GUID references (not used - kept for backward compatibility)
    // Component loading now uses instanceId fields below in "Calculated Stats" section
    public string turretPrefabGuid = "";
    public string armorPrefabGuid = "";
    public string engineFramePrefabGuid = "";
    
    // DEPRECATED: Additional GUID aliases (not used - kept for backward compatibility)
    public string engineFrameGuid { get => engineFramePrefabGuid; set => engineFramePrefabGuid = value; }
    public string armorGuid { get => armorPrefabGuid; set => armorPrefabGuid = value; }
    public string turretGuid { get => turretPrefabGuid; set => turretPrefabGuid = value; }
    
    // AI references (stored as instance IDs that correspond to JSON files)
    public string turretAIInstanceId;
    public string navAIInstanceId;
    
    // Component stats - stored directly to avoid ScriptableObject reference issues
    [Header("Turret Stats")]
    public TurretTypeJson turretType = TurretTypeJson.DirectFire;
    public string turretAnimationPrefabPath; // Path to animation prefab (e.g., HammerDown)
    public string turretDeathModelPrefabPath; // Path to death model prefab (e.g., RifleDamaged)
    public int turretDamage;
    public float turretRange;
    public float turretShotsPerSec;
    public float turretFireRate { get => turretShotsPerSec; set => turretShotsPerSec = value; } // Alias for compatibility
    public float turretBulletSpeed = 50f;
    public string turretKnockback;
    public float turretVisionRange = 60f;
    public float turretVisionCone = 45f;
    
    [Header("Armor Stats")]
    public int armorHP;
    
    [Header("Engine Stats")]
    [HideInInspector] public int engineWeightCapacity;
    [HideInInspector] public int enginePower; // Legacy stat - kept for compatibility
    [HideInInspector] public int engineFrameHP; // Engine frame health points
    
    // Physics-based engine parameters (hidden from inspector - loaded from component data)
    [HideInInspector] public float engineForce = 15000f;         // N (Newtons) - force output for forward movement
    [HideInInspector] public float engineTopSpeed = 15f;         // m/s - maximum forward speed
    [HideInInspector] public float engineTorque = 20000f;         // N·m (Newton-meters) - torque for rotation
    [HideInInspector] public float engineMaxTurnRate = 120f;     // deg/s - maximum turn speed
    [HideInInspector] public float engineTurnRampTime = 1.0f;    // seconds - time to reach full turning power
    [HideInInspector] public float engineTurnStartPercent = 0.5f; // 0-1 - starting power percentage
    
    [Header("Component Weights")]
    public float armorWeight = 20f;     // kg - armor plating weight
    public float turretWeight = 15f;    // kg - turret assembly weight
    public float engineWeight = 15f;    // kg - engine weight
    
    [Header("Physics Settings")]
    public float dragCoefficient = 0.5f;        // Rolling resistance (0.2-1.2)
    public float angularDragCoefficient = 1.0f; // Turn resistance (1.0-5.0)
    
    [Header("Calculated Stats")]
    public float totalWeight;
    
    // ACTIVE COMPONENT REFERENCES: Instance IDs used for loading components
    public string engineFrameInstanceId;  // Used by TankAssembly to load engine frame prefab
    public string armorInstanceId;        // Used by TankAssembly to load armor prefab
    public string turretInstanceId;       // Used by TankAssembly to load turret prefab
    
    // Custom colors for visual customization
    public ColorJson engineFrameColor = new ColorJson(1f, 1f, 1f, 1f);
    public ColorJson armorColor = new ColorJson(1f, 1f, 1f, 1f);
    public ColorJson turretColor = new ColorJson(1f, 1f, 1f, 1f);
    
    // Skin paths for visual customization (material/texture variants)
    [Header("Skin & Decal Customization")]
    public string engineFrameSkinPath = "";  // Path to skin texture in Resources (e.g., "KritaArt/Skins/MetalRust")
    public string armorSkinPath = "";        // Path to skin texture in Resources
    public string turretSkinPath = "";       // Path to skin texture in Resources
    public string turretDecalPath = "";      // Path to decal texture in Resources (turret only, e.g., "KritaArt/Decals/Skull")
    
    // DEPRECATED: Tank-level instanceId and metadata (use slotIndex instead)
    // These were used in old system but aren't needed - each tank slot has a fixed index 0-9
    public string instanceId = "";     // Not actively used
    public string slotName = "";       // Not actively used (can be derived from slotIndex)
    public int slotIndex;         // Active: 0-9 for the 10 tank slots
    public string spawnPointName; // For enemy tanks: name of the spawn point to use (e.g., "SpawnPoint10")
}

/// <summary>
/// JSON representation of TurretType enum
/// </summary>
[System.Serializable]
public enum TurretTypeJson
{
    DirectFire,    // Straight-line bullets (rifles, cannons, etc.)
    Artillery,     // Ballistic arc bullets with gravity
    Hammer         // Melee range with AOE damage
}

/// <summary>
/// JSON representation of Unity's Color struct
/// </summary>
[System.Serializable]
public class ColorJson
{
    public float r;
    public float g;
    public float b;
    public float a;
    
    public ColorJson() { }
    
    public ColorJson(float r, float g, float b, float a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
    }
    
    public ColorJson(Color unityColor)
    {
        this.r = unityColor.r;
        this.g = unityColor.g;
        this.b = unityColor.b;
        this.a = unityColor.a;
    }
    
    public Color ToUnityColor()
    {
        return new Color(r, g, b, a);
    }
}
