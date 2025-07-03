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
    
    // Prefab references (stored as GUIDs or resource paths)
    public string turretPrefabGuid;
    public string armorPrefabGuid;
    public string engineFramePrefabGuid;
    
    // AI references (stored as instance IDs that correspond to JSON files)
    public string turretAIInstanceId;
    public string navAIInstanceId;
    
    // Component stats - stored directly to avoid ScriptableObject reference issues
    [Header("Turret Stats")]
    public TurretTypeJson turretType = TurretTypeJson.DirectFire;
    public int turretDamage;
    public float turretRange;
    public float turretShotsPerSec;
    public float turretBulletSpeed = 50f;
    public string turretKnockback;
    public float turretVisionRange = 60f;
    public float turretVisionCone = 45f;
    
    [Header("Armor Stats")]
    public int armorHP;
    
    [Header("Engine Stats")]
    public int engineWeightCapacity;
    public int enginePower;
    
    [Header("Calculated Stats")]
    public float totalWeight;
    
    // Instance IDs for saving/loading
    public string engineFrameInstanceId;
    public string armorInstanceId;
    public string turretInstanceId;
    
    // Custom colors for visual customization
    public ColorJson engineFrameColor = new ColorJson(1f, 1f, 1f, 1f);
    public ColorJson armorColor = new ColorJson(1f, 1f, 1f, 1f);
    public ColorJson turretColor = new ColorJson(1f, 1f, 1f, 1f);
    
    // Unique identifier for this tank slot configuration
    public string instanceId;
    
    // Metadata
    public string slotName; // e.g., "TankSlot 0"
    public int slotIndex;   // 0-9 for the 10 tank slots
}

/// <summary>
/// JSON representation of TurretType enum
/// </summary>
[System.Serializable]
public enum TurretTypeJson
{
    DirectFire,    // Straight-line bullets (rifles, cannons, etc.)
    Artillery      // Ballistic arc bullets with gravity
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
