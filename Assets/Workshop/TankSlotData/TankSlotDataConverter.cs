using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AiEditor;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Utility class for converting TankSlotData ScriptableObjects to JSON format
/// and managing tank slot configurations for builds
/// </summary>
public class TankSlotDataConverter : MonoBehaviour
{
    private static string GetTankSlotJsonFolder()
    {
        return Path.Combine(Application.persistentDataPath, "TankSlots");
    }
    
    /// <summary>
    /// Converts a TankSlotData ScriptableObject to TankSlotDataJson
    /// </summary>
    public static TankSlotDataJson ConvertToJson(TankSlotData tankSlotData, string slotName, int slotIndex)
    {
        if (tankSlotData == null) return null;
        
        var jsonData = new TankSlotDataJson
        {
            // Basic configuration
            isActive = tankSlotData.isActive,
            teamId = tankSlotData.teamId,
            isPlayerControlled = tankSlotData.isPlayerControlled,
            displayName = tankSlotData.displayName,
            
            // Prefab GUIDs (we'll need to get these from the prefab references)
            turretPrefabGuid = GetPrefabGuid(tankSlotData.turretPrefab),
            armorPrefabGuid = GetPrefabGuid(tankSlotData.armorPrefab),
            engineFramePrefabGuid = GetPrefabGuid(tankSlotData.engineFramePrefab),
            
            // AI instance IDs
            turretAIInstanceId = tankSlotData.turretAIInstanceId,
            navAIInstanceId = tankSlotData.navAIInstanceId,
            
            // Turret stats
            turretType = (TurretTypeJson)tankSlotData.turretType,
            turretDamage = tankSlotData.turretDamage,
            turretRange = tankSlotData.turretRange,
            turretShotsPerSec = tankSlotData.turretShotsPerSec,
            turretBulletSpeed = tankSlotData.turretBulletSpeed,
            turretKnockback = tankSlotData.turretKnockback,
            turretVisionRange = tankSlotData.turretVisionRange,
            turretVisionCone = tankSlotData.turretVisionCone,
            
            // Armor stats
            armorHP = tankSlotData.armorHP,
            
            // Engine stats
            engineWeightCapacity = tankSlotData.engineWeightCapacity,
            enginePower = tankSlotData.enginePower,
            
            // Calculated stats
            totalWeight = tankSlotData.totalWeight,
            
            // Instance IDs
            engineFrameInstanceId = tankSlotData.engineFrameInstanceId,
            armorInstanceId = tankSlotData.armorInstanceId,
            turretInstanceId = tankSlotData.turretInstanceId,
            
            // Colors
            engineFrameColor = new ColorJson(tankSlotData.engineFrameColor),
            armorColor = new ColorJson(tankSlotData.armorColor),
            turretColor = new ColorJson(tankSlotData.turretColor),
            
            // Metadata
            slotName = slotName,
            slotIndex = slotIndex,
            instanceId = System.Guid.NewGuid().ToString()
        };
        
        return jsonData;
    }
    
    /// <summary>
    /// Gets the GUID of a prefab reference for JSON storage
    /// </summary>
    private static string GetPrefabGuid(GameObject prefab)
    {
        if (prefab == null) return "";
        
#if UNITY_EDITOR
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        if (!string.IsNullOrEmpty(assetPath))
        {
            return AssetDatabase.AssetPathToGUID(assetPath);
        }
#endif
        return "";
    }
    
    /// <summary>
    /// Converts all TankSlot assets in the project to JSON files in persistent data path
    /// </summary>
    [ContextMenu("Convert All Tank Slots to JSON")]
    public void ConvertAllTankSlotsToJson()
    {
#if UNITY_EDITOR
        string tankSlotFolder = "Assets/Workshop/TankSlotData/";
        string jsonFolder = GetTankSlotJsonFolder();
        
        // Create the JSON folder if it doesn't exist
        if (!Directory.Exists(jsonFolder))
        {
            Directory.CreateDirectory(jsonFolder);
        }
        
        // Find all TankSlot assets
        string[] assetGuids = AssetDatabase.FindAssets("t:TankSlotData", new[] { tankSlotFolder });
        
        foreach (string guid in assetGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            TankSlotData tankSlotData = AssetDatabase.LoadAssetAtPath<TankSlotData>(assetPath);
            
            if (tankSlotData != null)
            {
                // Extract slot name and index from the asset name
                string assetName = Path.GetFileNameWithoutExtension(assetPath);
                int slotIndex = ExtractSlotIndex(assetName);
                
                // Convert to JSON
                TankSlotDataJson jsonData = ConvertToJson(tankSlotData, assetName, slotIndex);
                
                if (jsonData != null)
                {
                    // Save as JSON file
                    string jsonPath = Path.Combine(jsonFolder, $"{assetName}.json");
                    string jsonContent = JsonUtility.ToJson(jsonData, true);
                    File.WriteAllText(jsonPath, jsonContent);
                    
                    Debug.Log($"Converted {assetName} to JSON: {jsonPath}");
                }
            }
        }
        
        Debug.Log($"Tank slot conversion complete. JSON files saved to: {jsonFolder}");
#else
        Debug.LogWarning("Tank slot conversion only available in the Unity Editor");
#endif
    }
    
    /// <summary>
    /// Extracts the slot index from asset names like "TankSlot 0", "TankSlot 1", etc.
    /// </summary>
    private int ExtractSlotIndex(string assetName)
    {
        if (assetName.StartsWith("TankSlot "))
        {
            string indexStr = assetName.Substring("TankSlot ".Length);
            if (int.TryParse(indexStr, out int index))
            {
                return index;
            }
        }
        return -1; // Invalid index
    }
    
    /// <summary>
    /// Loads all tank slot configurations from JSON files in persistent data path
    /// </summary>
    public static List<TankSlotDataJson> LoadAllTankSlotsFromJson()
    {
        var tankSlots = new List<TankSlotDataJson>();
        string jsonFolder = GetTankSlotJsonFolder();
        
        if (!Directory.Exists(jsonFolder))
        {
            Debug.LogWarning($"Tank slot JSON folder does not exist: {jsonFolder}");
            return tankSlots;
        }
        
        string[] jsonFiles = Directory.GetFiles(jsonFolder, "*.json");
        
        foreach (string filePath in jsonFiles)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                TankSlotDataJson tankSlotData = JsonUtility.FromJson<TankSlotDataJson>(jsonContent);
                
                if (tankSlotData != null)
                {
                    tankSlots.Add(tankSlotData);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load tank slot JSON from {filePath}: {ex.Message}");
            }
        }
        
        // Sort by slot index for consistent ordering
        tankSlots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
        
        return tankSlots;
    }
    
    /// <summary>
    /// Saves a tank slot configuration to JSON
    /// </summary>
    public static void SaveTankSlotToJson(TankSlotDataJson tankSlotData)
    {
        if (tankSlotData == null) return;
        
        string jsonFolder = GetTankSlotJsonFolder();
        
        // Ensure the folder exists
        if (!Directory.Exists(jsonFolder))
        {
            Directory.CreateDirectory(jsonFolder);
            Debug.Log($"[TankSlotDataConverter] Created tank slot JSON folder: {jsonFolder}");
        }
        
        // Use slotIndex to generate consistent filenames
        string fileName = !string.IsNullOrEmpty(tankSlotData.slotName) 
            ? $"{tankSlotData.slotName}.json" 
            : $"TankSlot {tankSlotData.slotIndex}.json";
            
        string filePath = Path.Combine(jsonFolder, fileName);

        // Make a copy and strip fields that are always sourced from ScriptableObjects at
        // assembly time — keeping them in the JSON would just create stale data.
        TankSlotDataJson saveData = JsonUtility.FromJson<TankSlotDataJson>(JsonUtility.ToJson(tankSlotData));
        TankSlotJsonManager.StripSOSourcedFields(saveData);

        string jsonContent = JsonUtility.ToJson(saveData, true);
        
        try
        {
            File.WriteAllText(filePath, jsonContent);
            Debug.Log($"[TankSlotDataConverter] Saved tank slot {tankSlotData.slotIndex} to: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TankSlotDataConverter] Failed to save tank slot JSON: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Loads a specific tank slot configuration by slot index
    /// </summary>
    public static TankSlotDataJson LoadTankSlotFromJson(int slotIndex)
    {
        string jsonFolder = GetTankSlotJsonFolder();
        string filePath = Path.Combine(jsonFolder, $"TankSlot {slotIndex}.json");
        
        if (File.Exists(filePath))
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                return JsonUtility.FromJson<TankSlotDataJson>(jsonContent);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load tank slot {slotIndex} from JSON: {ex.Message}");
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets the GameObject prefab from a GUID (for loading prefabs from JSON data)
    /// </summary>
    public static GameObject GetPrefabFromGuid(string prefabGuid)
    {
        if (string.IsNullOrEmpty(prefabGuid)) return null;
        
#if UNITY_EDITOR
        string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
        if (!string.IsNullOrEmpty(assetPath))
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }
#else
        // In build, we'd need to load prefabs from Resources or Addressables
        // This would require restructuring how prefabs are stored
        Debug.LogWarning("Prefab loading from GUID not implemented for builds yet");
#endif
        return null;
    }
}
