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
            var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
            if (tankSlotJsonManager != null)
            {
                foreach (var slot in workshopUI.tankSlots)
                {
                    var save = new TankLoadoutSave();
                    save.tankName = slot.TankName;
                    var slotData = tankSlotJsonManager.GetTankSlot(slot.slotIndex);
                    if (slotData != null)
                    {
                        save.isActive = slotData.isActive; // Save activation state
                        save.engineFrameInstanceId = slotData.engineFrameInstanceId;
                        save.armorInstanceId = slotData.armorInstanceId;
                        save.turretInstanceId = slotData.turretInstanceId;
                        save.turretAIInstanceId = slotData.turretAIInstanceId;
                        save.navAIInstanceId = slotData.navAIInstanceId;
                        
                        Debug.Log($"[PlayerDataManager] Saving slot {slot.TankName}: TurretAI={save.turretAIInstanceId}, NavAI={save.navAIInstanceId}");
                    }
                    playerData.tankLoadouts.Add(save);
                }
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
        Debug.Log("[PlayerDataManager] Erasing all player data...");
        
        // Delete player data save file
        if (File.Exists(saveFilePath))
            File.Delete(saveFilePath);
        playerData = new PlayerData();
        
        // Clear all tank slots and reset active states
        ClearAllTankSlots();
        
        // Clear runtime inventory
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI != null)
        {
            workshopUI.playerInventory.Clear();
            workshopUI.PopulateComponentList();
            
            // Reset all tank slot UI buttons to inactive except first one
            for (int i = 0; i < workshopUI.tankSlots.Count; i++)
            {
                workshopUI.tankSlots[i].SetActive(i == 0); // Only first tank active
                workshopUI.tankSlots[i].SetSelected(false); // Deselect all
                workshopUI.tankSlots[i].UpdateAssignedComponentsFromSlotData(); // Clear UI display
            }
        }
        
        Debug.Log("[PlayerDataManager] Player data erased successfully.");
    }
    
    /// <summary>
    /// Clears all tank slot data and resets them to default state
    /// Only the first tank slot (TankSlot 0) will be active after clearing
    /// </summary>
    public void ClearAllTankSlots()
    {
        Debug.Log("[PlayerDataManager] Clearing all tank slots...");
        
        // Get TankSlotJsonManager instance
        var tankSlotManager = TankSlotJsonManager.Instance;
        if (tankSlotManager == null)
        {
            Debug.LogWarning("[PlayerDataManager] TankSlotJsonManager not found - cannot clear tank slots");
            return;
        }
        
        // Clear all 10 tank slots (0-9)
        for (int i = 0; i < 10; i++)
        {
            var slotData = tankSlotManager.GetTankSlot(i);
            if (slotData != null)
            {
                // Clear all component assignments
                slotData.engineFramePrefabGuid = "";
                slotData.armorPrefabGuid = "";
                slotData.turretPrefabGuid = "";
                slotData.engineFrameInstanceId = "";
                slotData.armorInstanceId = "";
                slotData.turretInstanceId = "";
                slotData.turretAIInstanceId = "";
                slotData.navAIInstanceId = "";
                
                // Clear all component stats
                slotData.totalWeight = 0f;
                slotData.engineWeightCapacity = 0;
                slotData.enginePower = 0;
                slotData.engineFrameHP = 0;
                slotData.engineForce = 0f;
                slotData.engineTopSpeed = 0f;
                slotData.engineTorque = 0f;
                slotData.engineMaxTurnRate = 0f;
                slotData.engineTurnRampTime = 1.0f;
                slotData.engineTurnStartPercent = 0.5f;
                slotData.chassisWeight = 0f;
                slotData.armorWeight = 0f;
                slotData.turretWeight = 0f;
                slotData.engineWeight = 0f;
                slotData.dragCoefficient = 0.5f;
                slotData.angularDragCoefficient = 2.0f;
                slotData.armorHP = 0;
                slotData.turretDamage = 0;
                slotData.turretRange = 0f;
                slotData.turretShotsPerSec = 0f;
                slotData.turretKnockback = "";
                slotData.turretVisionRange = 60f;
                slotData.turretVisionCone = 45f;
                
                // Reset colors to white
                slotData.engineFrameColor = new ColorJson(1f, 1f, 1f, 1f);
                slotData.armorColor = new ColorJson(1f, 1f, 1f, 1f);
                slotData.turretColor = new ColorJson(1f, 1f, 1f, 1f);
                
                // Set activation state - only first tank active
                slotData.isActive = (i == 0);
                
                // Save the cleared slot
                tankSlotManager.UpdateTankSlot(i, slotData);
                
                Debug.Log($"[PlayerDataManager] Cleared tank slot {i} (isActive={slotData.isActive})");
            }
        }
        
        Debug.Log("[PlayerDataManager] All tank slots cleared. Only TankSlot 0 is active.");
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
    /// Now loads from persistent data path (AppData) instead of Unity project folders
    /// </summary>
    private AiEditor.AiTreeAsset LoadAITreeAssetFromDisk(string instanceId, AiEditor.AiBranchType branchType)
    {
        // Load from persistent data path (AppData/AiTrees)
        string aiTreesPath = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees");
        
        if (System.IO.Directory.Exists(aiTreesPath))
        {
            string[] jsonFiles = System.IO.Directory.GetFiles(aiTreesPath, "*.json", System.IO.SearchOption.AllDirectories);
            
            foreach (string filePath in jsonFiles)
            {
                try
                {
                    string jsonContent = System.IO.File.ReadAllText(filePath);
                    
                    // Create a new AiTreeAsset instance and populate it from JSON
                    var aiTreeAsset = ScriptableObject.CreateInstance<AiEditor.AiTreeAsset>();
                    JsonUtility.FromJsonOverwrite(jsonContent, aiTreeAsset);
                    
                    if (aiTreeAsset != null && aiTreeAsset.instanceId == instanceId && aiTreeAsset.branchType == branchType)
                    {
                        return aiTreeAsset;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PlayerDataManager] Could not load AI file {filePath}: {ex.Message}");
                }
            }
            
            // Fallback: try to find by filename if exact instanceId match fails
            foreach (string filePath in jsonFiles)
            {
                try
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                    
                    if (fileName.Contains(instanceId))
                    {
                        string jsonContent = System.IO.File.ReadAllText(filePath);
                        
                        // Create a new AiTreeAsset instance and populate it from JSON
                        var aiTreeAsset = ScriptableObject.CreateInstance<AiEditor.AiTreeAsset>();
                        JsonUtility.FromJsonOverwrite(jsonContent, aiTreeAsset);
                        
                        if (aiTreeAsset != null && aiTreeAsset.branchType == branchType)
                        {
                            Debug.LogWarning($"[PlayerDataManager] Found AI asset by filename matching: {aiTreeAsset.title} (expected instanceId: {instanceId}, filename: {fileName})");
                            // Update the asset's instanceId to match what we're looking for
                            aiTreeAsset.instanceId = instanceId;
                            return aiTreeAsset;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[PlayerDataManager] Could not process AI file {filePath} during fallback: {ex.Message}");
                }
            }
        }
        
        Debug.LogWarning($"[PlayerDataManager] Could not load AI Tree asset with instanceId: {instanceId} and branchType: {branchType} from persistent data path");
        return null;
    }
}
