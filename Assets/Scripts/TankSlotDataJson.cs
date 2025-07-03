using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JSON-serializable version of TankSlotData
/// Contains all the same data as the ScriptableObject version but can be saved/loaded at runtime
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
    
    // Prefab references - stored as resource paths for JSON compatibility
    public string turretPrefabPath;
    public string armorPrefabPath;
    public string engineFramePrefabPath;
    
    // AI references - stored as file paths for JSON compatibility
    public string turretAIPath;
    public string navAIPath;
    
    [Header("Turret Stats")]
    public TurretType turretType = TurretType.DirectFire;
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
    public string turretAIInstanceId;
    public string navAIInstanceId;
    
    // Custom colors for visual customization
    public Color engineFrameColor = Color.white;
    public Color armorColor = Color.white;
    public Color turretColor = Color.white;

    /// <summary>
    /// Convert from existing TankSlotData ScriptableObject
    /// </summary>
    public static TankSlotDataJson FromScriptableObject(TankSlotData original)
    {
        TankSlotDataJson json = new TankSlotDataJson();
        
        // Copy all basic data
        json.isActive = original.isActive;
        json.teamId = original.teamId;
        json.isPlayerControlled = original.isPlayerControlled;
        json.displayName = original.displayName;
        
        // Convert prefab references to paths
        json.turretPrefabPath = GetPrefabResourcePath(original.turretPrefab);
        json.armorPrefabPath = GetPrefabResourcePath(original.armorPrefab);
        json.engineFramePrefabPath = GetPrefabResourcePath(original.engineFramePrefab);
        
        // Convert AI references to paths
        json.turretAIPath = original.turretAI != null ? original.turretAI.instanceId : "";
        json.navAIPath = original.navAI != null ? original.navAI.instanceId : "";
        
        // Copy all stats
        json.turretType = original.turretType;
        json.turretDamage = original.turretDamage;
        json.turretRange = original.turretRange;
        json.turretShotsPerSec = original.turretShotsPerSec;
        json.turretBulletSpeed = original.turretBulletSpeed;
        json.turretKnockback = original.turretKnockback;
        json.turretVisionRange = original.turretVisionRange;
        json.turretVisionCone = original.turretVisionCone;
        json.armorHP = original.armorHP;
        json.engineWeightCapacity = original.engineWeightCapacity;
        json.enginePower = original.enginePower;
        json.totalWeight = original.totalWeight;
        
        // Copy instance IDs
        json.engineFrameInstanceId = original.engineFrameInstanceId;
        json.armorInstanceId = original.armorInstanceId;
        json.turretInstanceId = original.turretInstanceId;
        json.turretAIInstanceId = original.turretAIInstanceId;
        json.navAIInstanceId = original.navAIInstanceId;
        
        // Copy colors
        json.engineFrameColor = original.engineFrameColor;
        json.armorColor = original.armorColor;
        json.turretColor = original.turretColor;
        
        return json;
    }
    
    /// <summary>
    /// Save this tank slot data to JSON file
    /// </summary>
    public void SaveToJson(string fileName)
    {
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "TankSlots");
        
        // Create directory if it doesn't exist
        if (!System.IO.Directory.Exists(filePath))
        {
            System.IO.Directory.CreateDirectory(filePath);
        }
        
        string fullPath = System.IO.Path.Combine(filePath, fileName + ".json");
        string json = JsonUtility.ToJson(this, true);
        System.IO.File.WriteAllText(fullPath, json);
        
        Debug.Log($"Saved tank slot to: {fullPath}");
    }
    
    /// <summary>
    /// Load tank slot data from JSON file
    /// </summary>
    public static TankSlotDataJson LoadFromJson(string fileName)
    {
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "TankSlots", fileName + ".json");
        
        if (!System.IO.File.Exists(filePath))
        {
            Debug.LogError($"Tank slot file not found: {filePath}");
            return null;
        }
        
        try
        {
            string json = System.IO.File.ReadAllText(filePath);
            TankSlotDataJson data = JsonUtility.FromJson<TankSlotDataJson>(json);
            Debug.Log($"Loaded tank slot from: {filePath}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load tank slot: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Get all available tank slot files
    /// </summary>
    public static List<string> GetAvailableFiles()
    {
        List<string> files = new List<string>();
        string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "TankSlots");
        
        if (System.IO.Directory.Exists(folderPath))
        {
            string[] jsonFiles = System.IO.Directory.GetFiles(folderPath, "*.json");
            foreach (string file in jsonFiles)
            {
                files.Add(System.IO.Path.GetFileNameWithoutExtension(file));
            }
        }
        
        return files;
    }
    
    private static string GetPrefabResourcePath(GameObject prefab)
    {
        if (prefab == null) return "";
        
        #if UNITY_EDITOR
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(prefab);
        
        // If it's in Resources folder, return relative path
        if (assetPath.Contains("/Resources/"))
        {
            int resourcesIndex = assetPath.IndexOf("/Resources/") + "/Resources/".Length;
            string resourcePath = assetPath.Substring(resourcesIndex);
            // Remove .prefab extension for Resources.Load
            if (resourcePath.EndsWith(".prefab"))
                resourcePath = resourcePath.Substring(0, resourcePath.Length - 7);
            return resourcePath;
        }
        
        // For non-Resources assets, store full path (editor only)
        return assetPath;
        #else
        // In build, we can only work with Resources
        return prefab.name;
        #endif
    }
    
    /// <summary>
    /// Load a prefab from the stored path
    /// </summary>
    public GameObject LoadPrefab(string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath)) return null;
        
        // Try loading from Resources first
        GameObject prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab != null) return prefab;
        
        #if UNITY_EDITOR
        // In editor, try loading from full asset path
        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        #endif
        
        return prefab;
    }
}
