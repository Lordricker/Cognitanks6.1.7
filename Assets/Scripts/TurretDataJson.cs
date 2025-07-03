using UnityEngine;

/// <summary>
/// JSON-serializable version of TurretData
/// Contains all the same data as the ScriptableObject version but can be saved/loaded at runtime
/// </summary>
[System.Serializable]
public class TurretDataJson : ComponentDataJson
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
    public float visionCone = 45f;   // Field of view angle in degrees
    
    /// <summary>
    /// Constructor with default values
    /// </summary>
    public TurretDataJson()
    {
        category = ComponentCategoryJson.Turret;
        cost = 100;
        weight = 10;
        damage = 10;
        range = 50f;
        shotspersec = 1f;
        bulletSpeed = 50f;
        knockback = "Low";
        visionRange = 60f;
        visionCone = 45f;
    }
    
    /// <summary>
    /// Convert from existing TurretData ScriptableObject
    /// </summary>
    public static TurretDataJson FromScriptableObject(TurretData original)
    {
        TurretDataJson json = new TurretDataJson();
        
        // Copy base properties
        json.title = original.title;
        json.description = original.description;
        json.cost = original.cost;
        json.weight = original.weight;
        json.modelPrefabPath = GetPrefabResourcePath(original.modelPrefab);
        json.category = (ComponentCategoryJson)original.category;
        json.instanceId = original.instanceId;
        json.customColor = original.customColor;
        
        // Copy turret-specific properties
        json.turretType = original.turretType;
        json.damage = original.damage;
        json.range = original.range;
        json.shotspersec = original.shotspersec;
        json.bulletSpeed = original.bulletSpeed;
        json.knockback = original.knockback;
        json.visionRange = original.visionRange;
        json.visionCone = original.visionCone;
        
        return json;
    }
    
    /// <summary>
    /// Load turret data from JSON file
    /// </summary>
    public static TurretDataJson LoadFromJson(string fileName)
    {
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "Components", fileName + ".json");
        
        if (!System.IO.File.Exists(filePath))
        {
            Debug.LogError($"Turret component file not found: {filePath}");
            return null;
        }
        
        try
        {
            string json = System.IO.File.ReadAllText(filePath);
            TurretDataJson data = JsonUtility.FromJson<TurretDataJson>(json);
            Debug.Log($"Loaded turret component from: {filePath}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load turret component: {e.Message}");
            return null;
        }
    }
}
