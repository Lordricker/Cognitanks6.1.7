using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class OwnedComponentEntry
{
    public string id;
    public List<string> instanceIds = new List<string>(); // unique instance ids for each owned copy
}

[Serializable]
public class PlayerData
{
    public List<OwnedComponentEntry> ownedComponents = new List<OwnedComponentEntry>(); // IDs and instanceIds of owned components
    public List<TankLoadoutSave> tankLoadouts = new List<TankLoadoutSave>(); // One per tank slot
}

[Serializable]
public class TankLoadoutSave
{
    public string tankName;
    public bool isActive; // Save/load activation state to prevent ScriptableObject sync issues
    public string engineFrameInstanceId;
    public string armorInstanceId;
    public string turretInstanceId;
    public string turretAIInstanceId;
    public string navAIInstanceId;
    
    // Component stats are now stored directly in TankSlotData
    // No need for ScriptableObject asset paths since stats are copied to TankSlotData
}

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance;
    public PlayerData playerData = new PlayerData();
    private string saveFilePath;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Path.Combine(Application.persistentDataPath, "playerdata.json");
            LoadPlayerData();
        }
        else
        {
            Destroy(gameObject);
        }
    }    public void SavePlayerData()
    {
        Debug.Log("[PlayerDataManager] Starting SavePlayerData...");
        
        // Save all unique instanceIds for each owned component (excluding AI components which are stored on disk)
        playerData.ownedComponents.Clear();
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI != null)
        {
            var grouped = new Dictionary<string, OwnedComponentEntry>();
            foreach (var comp in workshopUI.playerInventory)
            {
                // Skip AI components as they are stored as files on disk, not in inventory
                if (comp.category == ComponentCategory.AITree || 
                    comp.category == ComponentCategory.TurretAI || 
                    comp.category == ComponentCategory.NavAI) 
                {
                    Debug.Log($"[PlayerDataManager] Skipping AI component from save data: {comp.title} ({comp.category})");
                    continue;
                }
                
                if (!grouped.ContainsKey(comp.id))
                    grouped[comp.id] = new OwnedComponentEntry { id = comp.id };
                grouped[comp.id].instanceIds.Add(comp.instanceId);
                Debug.Log($"[PlayerDataManager] Added to save data: {comp.title} (instanceId: {comp.instanceId})");
            }
            playerData.ownedComponents.AddRange(grouped.Values);
        }

        // Save tank slot assignments by instanceId
        playerData.tankLoadouts.Clear();
        if (workshopUI != null)
        {
            foreach (var slot in workshopUI.tankSlots)
            {
                var save = new TankLoadoutSave();
                save.tankName = slot.TankName;
                if (slot.slotData != null)
                {
                    save.isActive = slot.slotData.isActive; // Save activation state
                    save.engineFrameInstanceId = slot.slotData.engineFrameInstanceId;
                    save.armorInstanceId = slot.slotData.armorInstanceId;
                    save.turretInstanceId = slot.slotData.turretInstanceId;
                    save.turretAIInstanceId = slot.slotData.turretAIInstanceId;
                    save.navAIInstanceId = slot.slotData.navAIInstanceId;
                    
                    Debug.Log($"[PlayerDataManager] Saving slot {slot.TankName}: TurretAI={save.turretAIInstanceId}, NavAI={save.navAIInstanceId}");
                }
                playerData.tankLoadouts.Add(save);
            }
        }
        
        string json = JsonUtility.ToJson(playerData, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log($"[PlayerDataManager] Player data saved to {saveFilePath}");
    }

    public void LoadPlayerData()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            playerData = JsonUtility.FromJson<PlayerData>(json);
            Debug.Log("Player data loaded.");
        }
        else
        {
            playerData = new PlayerData();
            Debug.Log("No player data found. Created new data.");
        }
    }

    public void ErasePlayerData()
    {
        if (File.Exists(saveFilePath))
            File.Delete(saveFilePath);
        playerData = new PlayerData();
        // Also clear any runtime inventory if needed
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI != null)
        {
            workshopUI.playerInventory.Clear();
            workshopUI.PopulateComponentList();
        }
        Debug.Log("Player data erased.");
    }

    /// <summary>
    /// Quits the game application, saving player data first
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        
        // Save player data before quitting
        SavePlayerData();
        
        #if UNITY_EDITOR
        // In the editor, stop play mode
        UnityEditor.EditorApplication.isPlaying = false;
        #elif UNITY_WEBGL
        // WebGL doesn't support Application.Quit(), so we can't actually quit
        Debug.LogWarning("Cannot quit in WebGL builds. Consider redirecting to a webpage or showing a message.");
        #else
        // In a built application, quit the application
        Debug.Log("Calling Application.Quit()...");
        Application.Quit();
        
        // Force quit if Application.Quit() doesn't work (some platforms)
        #if UNITY_STANDALONE
        System.Diagnostics.Process.GetCurrentProcess().Kill();
        #endif
        #endif
    }

    // Call this after loading player data to restore AI references and activation states
    // Component stats are now stored directly in TankSlotData, so no ScriptableObject restoration needed
    public void RestoreComponentDataReferences(List<TankSlotData> allSlots)
    {
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI == null)
        {
            Debug.LogWarning("[PlayerDataManager] WorkshopUIManager not found - cannot restore AI references");
            return;
        }

        Debug.Log($"[PlayerDataManager] RestoreComponentDataReferences: Restoring AI references and activation states (component stats are stored directly in TankSlotData)");

        bool saveNeeded = false;
        bool assetsModified = false;

        // First, restore activation states from saved PlayerData
        for (int i = 0; i < allSlots.Count && i < playerData.tankLoadouts.Count; i++)
        {
            var slotData = allSlots[i];
            var savedLoadout = playerData.tankLoadouts[i];
            
            if (slotData != null && savedLoadout != null)
            {
                // Restore activation state from saved data
                bool savedIsActive = savedLoadout.isActive;
                if (slotData.isActive != savedIsActive)
                {
                    Debug.Log($"[PlayerDataManager] Restoring activation state for {slotData.name}: {slotData.isActive} -> {savedIsActive}");
                    slotData.isActive = savedIsActive;
                    assetsModified = true;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(slotData);
#endif
                }
            }
        }

        foreach (var slotData in allSlots)
        {
            if (slotData == null) continue;

            // Restore AI references from disk files
            if (!string.IsNullOrEmpty(slotData.turretAIInstanceId))
            {
                var turretAI = LoadAITreeAssetFromDisk(slotData.turretAIInstanceId, AiEditor.AiBranchType.Turret);
                if (turretAI != null)
                {
                    slotData.turretAI = turretAI;
                    Debug.Log($"[PlayerDataManager] Restored TurretAI reference from disk for {slotData.name}: {turretAI.title}");
                }
                else
                {
                    Debug.LogWarning($"[PlayerDataManager] Could not find TurretAI file with instanceId: {slotData.turretAIInstanceId} - clearing reference");
                    slotData.turretAIInstanceId = null;
                    slotData.turretAI = null;
                    saveNeeded = true;
                    assetsModified = true;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(slotData);
#endif
                }
            }

            if (!string.IsNullOrEmpty(slotData.navAIInstanceId))
            {
                var navAI = LoadAITreeAssetFromDisk(slotData.navAIInstanceId, AiEditor.AiBranchType.Nav);
                if (navAI != null)
                {
                    slotData.navAI = navAI;
                    Debug.Log($"[PlayerDataManager] Restored NavAI reference from disk for {slotData.name}: {navAI.title}");
                }
                else
                {
                    Debug.LogWarning($"[PlayerDataManager] Could not find NavAI file with instanceId: {slotData.navAIInstanceId} - clearing reference");
                    slotData.navAIInstanceId = null;
                    slotData.navAI = null;
                    saveNeeded = true;
                    assetsModified = true;
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(slotData);
#endif
                }
            }
        }

        // If we cleared any invalid references, save the updated data
        if (saveNeeded)
        {
            Debug.Log("[PlayerDataManager] Cleaned up invalid AI references - saving updated data");
            SavePlayerData();
        }

        // If we modified any ScriptableObject assets, save them
        if (assetsModified)
        {
            Debug.Log("[PlayerDataManager] Saving modified TankSlotData assets");
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        Debug.Log("[PlayerDataManager] Activation state and AI reference restoration completed - component stats are already stored in TankSlotData");
    }    /// <summary>
    /// Extracts the component name from an instanceId (removes any GUID suffix)
    /// NOTE: This method is kept for backward compatibility but may not be needed with stat-based approach
    /// </summary>
    private string ExtractComponentName(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return instanceId;
        
        // Remove "(Clone)" suffix if present
        if (instanceId.EndsWith("(Clone)"))
        {
            return instanceId.Substring(0, instanceId.Length - 7).Trim();
        }
        
        return instanceId;
    }

    /// <summary>
    /// Finds and loads permanent component data from Assets/Workshop/ComponentData/
    /// NOTE: This method is kept for backward compatibility but may not be needed with stat-based approach
    /// </summary>
#if UNITY_EDITOR
    private T FindPermanentComponentData<T>(string componentName) where T : ComponentData
    {
        string searchPath = "Assets/Workshop/ComponentData/";
        
        // Load all assets of the specified type from the ComponentData folder recursively
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { searchPath });
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            T componentData = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            
            if (componentData != null && componentData.title.Contains(componentName))
            {
                return componentData;
            }
        }
        
        return null;
    }
#endif

    /// <summary>
    /// Loads an AI Tree Asset from disk based on instanceId and branch type
    /// </summary>
    private AiEditor.AiTreeAsset LoadAITreeAssetFromDisk(string instanceId, AiEditor.AiBranchType branchType)
    {
#if UNITY_EDITOR
        string folderPath = branchType == AiEditor.AiBranchType.Turret 
            ? "Assets/AiEditor/AISaveFiles/TurretFiles/" 
            : "Assets/AiEditor/AISaveFiles/NavFiles/";
        
        // Search through all files in the folder to find the one with matching instanceId
        if (System.IO.Directory.Exists(folderPath))
        {
            string[] files = System.IO.Directory.GetFiles(folderPath, "*.asset");
            foreach (string filePath in files)
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiEditor.AiTreeAsset>(filePath);
                if (asset != null && asset.instanceId == instanceId && asset.branchType == branchType)
                {
                    return asset;
                }
            }
        }
        
        // If not found in specific folder, also check main AISaveFiles folder
        string mainFolderPath = "Assets/AiEditor/AISaveFiles/";
        if (System.IO.Directory.Exists(mainFolderPath))
        {
            string[] files = System.IO.Directory.GetFiles(mainFolderPath, "*.asset");
            foreach (string filePath in files)
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiEditor.AiTreeAsset>(filePath);
                if (asset != null && asset.instanceId == instanceId && asset.branchType == branchType)
                {
                    return asset;
                }
            }
        }
        
        // If still not found, try to find by title as a fallback (for legacy compatibility)
        string titleToFind = instanceId.Split('_')[0]; // Extract title from instanceId format
        string[] allFolders = { folderPath, mainFolderPath };
        
        foreach (string folder in allFolders)
        {
            if (System.IO.Directory.Exists(folder))
            {
                string[] files = System.IO.Directory.GetFiles(folder, "*.asset");
                foreach (string filePath in files)
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiEditor.AiTreeAsset>(filePath);
                    if (asset != null && asset.branchType == branchType && 
                        !string.IsNullOrEmpty(asset.title) && asset.title.Equals(titleToFind, System.StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning($"[PlayerDataManager] Found AI asset by title fallback: {asset.title} (expected instanceId: {instanceId}, actual: {asset.instanceId})");
                        return asset;
                    }
                }
            }
        }
        
        Debug.LogWarning($"[PlayerDataManager] Could not load AI Tree asset with instanceId: {instanceId} and branchType: {branchType}");
        return null;
#else
        Debug.LogWarning("[PlayerDataManager] AI Tree asset loading from disk is only supported in editor mode");
        return null;
#endif
    }
}
