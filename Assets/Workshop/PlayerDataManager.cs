using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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
    public int playerCash = 8000; // Player's current cash amount
    public int playerElo = 1000; // Player's ELO rating (synced from Firebase)
    public string lastKnownDiscordId = ""; // Cache Discord ID for offline access
    public bool hasSeenTipsOnFirstLaunch = false; // Track if player has seen tips on first launch
    public int tutorialStep = 0; // 0=new player, 1=after first fight, 2=after second fight, 3=tutorial complete
    public bool tutorialButtonPressed = false; // True once the player has clicked the Tutorial button
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
        
        // Sync player cash FROM playerData TO WorkshopUIManager (not the other way around)
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI != null)
        {
            workshopUI.playerCash = playerData.playerCash;
            workshopUI.UpdatePlayerCashUI();
            Debug.Log($"[PlayerDataManager] Synced player cash to WorkshopUIManager: ${playerData.playerCash}");
        }
        
        // Save all unique instanceIds for each owned component (excluding AI components which are stored on disk)
        // ONLY update ownedComponents if WorkshopUIManager exists (i.e., we're in Workshop scene)
        if (workshopUI != null)
        {
            playerData.ownedComponents.Clear();
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
        else
        {
            Debug.Log("[PlayerDataManager] WorkshopUIManager not found - skipping inventory update (preserving existing data)");
        }

        // Save tank slot assignments by instanceId
        // ONLY update tankLoadouts if WorkshopUIManager exists (i.e., we're in Workshop scene)
        if (workshopUI != null)
        {
            playerData.tankLoadouts.Clear();
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
        else
        {
            Debug.Log("[PlayerDataManager] WorkshopUIManager not found - skipping tank loadouts update (preserving existing data)");
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
            
            // Load player cash into WorkshopUIManager
            var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
            if (workshopUI != null)
            {
                workshopUI.playerCash = playerData.playerCash;
                workshopUI.UpdatePlayerCashUI();
                Debug.Log($"[PlayerDataManager] Loaded player cash: ${playerData.playerCash}");
            }
        }
        else
        {
            playerData = new PlayerData();
            Debug.Log("No player data found. Created new data.");
        }
    }

    public void ErasePlayerData()
    {
        Debug.Log("[PlayerDataManager] Starting ErasePlayerData...");
        
        // Delete playerdata.json file
        if (File.Exists(saveFilePath))
        {
            File.Delete(saveFilePath);
            Debug.Log($"[PlayerDataManager] Deleted playerdata.json");
        }
        
        // Create fresh empty playerData
        playerData = new PlayerData();
        playerData.playerCash = 3000; // Reset to default
        
        // Clear all progression-related PlayerPrefs
        ClearProgressionData();
        
        // Delete all tank slot JSON files from persistent data
        string tankSlotFolder = Path.Combine(Application.persistentDataPath, "TankSlotData");
        if (Directory.Exists(tankSlotFolder))
        {
            try
            {
                Directory.Delete(tankSlotFolder, true); // Delete folder and all contents
                Debug.Log($"[PlayerDataManager] Deleted tank slot data folder: {tankSlotFolder}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerDataManager] Failed to delete tank slot data folder: {ex.Message}");
            }
        }
        
        // Reset ShopAI folders to only contain default files
        ResetShopAIFoldersToDefaults();
        
        // Force TankSlotJsonManager to reinitialize with fresh default slots
        // This must happen BEFORE scene reload so the new scene has clean data
        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager != null)
        {
            Debug.Log("[PlayerDataManager] Reinitializing TankSlotJsonManager with empty slots...");
            tankSlotJsonManager.ReinitializeTankSlots();
            
            // Verify the slots are actually cleared
            var slots = tankSlotJsonManager.GetAllTankSlots();
            foreach (var slot in slots)
            {
                Debug.Log($"[PlayerDataManager] Slot {slot.slotIndex} after reinit: engine={slot.engineFrameInstanceId}, armor={slot.armorInstanceId}, turret={slot.turretInstanceId}");
            }
        }
        
        Debug.Log("[PlayerDataManager] Player data erased successfully. Reloading scene...");
        
        // Reload the current scene to ensure everything is properly reset
        // All UI elements will be recreated fresh, and they'll load from the
        // cleared PlayerData and reinitialized TankSlotJsonManager
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene);
    }
    
    /// <summary>
    /// Resets the ShopAI/NavAI and ShopAI/TurretAI folders to only contain default files
    /// Removes all non-default AI files that were unlocked through arena rewards
    /// </summary>
    private void ResetShopAIFoldersToDefaults()
    {
        // Default files to keep after erase
        string defaultNavAIFileName = "L1R1RifleN.json";
        string defaultTurretAIFileName = "L1R1RifleT.json";

        // Clean the persistentDataPath ShopAI folders (reward files written here in builds)
        CleanPersistentShopAIFolder("NavAI", defaultNavAIFileName);
        CleanPersistentShopAIFolder("TurretAI", defaultTurretAIFileName);

        // Also clean the dataPath Resources folders for editor/dev convenience
        // Reset NavAI folder
        string navAIFolder = Path.Combine(Application.dataPath, "Resources", "ShopAI", "NavAI");
        if (Directory.Exists(navAIFolder))
        {
            try
            {
                string[] navFiles = Directory.GetFiles(navAIFolder, "*.json");
                foreach (string file in navFiles)
                {
                    string fileName = Path.GetFileName(file);
                    // Delete all files except the default
                    if (fileName != defaultNavAIFileName)
                    {
                        File.Delete(file);
                        Debug.Log($"[PlayerDataManager] Deleted non-default Nav AI file: {fileName}");
                        
                        // Also delete .meta files if they exist
                        string metaFile = file + ".meta";
                        if (File.Exists(metaFile))
                        {
                            File.Delete(metaFile);
                        }
                    }
                }
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerDataManager] Failed to reset NavAI folder: {ex.Message}");
            }
        }
        
        // Reset TurretAI folder
        string turretAIFolder = Path.Combine(Application.dataPath, "Resources", "ShopAI", "TurretAI");
        if (Directory.Exists(turretAIFolder))
        {
            try
            {
                string[] turretFiles = Directory.GetFiles(turretAIFolder, "*.json");
                foreach (string file in turretFiles)
                {
                    string fileName = Path.GetFileName(file);
                    // Delete all files except the default
                    if (fileName != defaultTurretAIFileName)
                    {
                        File.Delete(file);
                        Debug.Log($"[PlayerDataManager] Deleted non-default Turret AI file: {fileName}");
                        
                        // Also delete .meta files if they exist
                        string metaFile = file + ".meta";
                        if (File.Exists(metaFile))
                        {
                            File.Delete(metaFile);
                        }
                    }
                }
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlayerDataManager] Failed to reset TurretAI folder: {ex.Message}");
            }
        }
        
        Debug.Log("[PlayerDataManager] Reset shop AI folders to defaults");
    }

    /// <summary>
    /// Deletes non-default reward AI files from persistentDataPath/ShopAI/{subFolder}.
    /// The default file (e.g. L1R1RifleN.json) is preserved.
    /// </summary>
    private void CleanPersistentShopAIFolder(string subFolder, string defaultFileName)
    {
        string dir = Path.Combine(Application.persistentDataPath, "ShopAI", subFolder);
        if (!Directory.Exists(dir)) return;

        try
        {
            foreach (string file in Directory.GetFiles(dir, "*.json"))
            {
                if (Path.GetFileName(file) != defaultFileName)
                {
                    File.Delete(file);
                    Debug.Log($"[PlayerDataManager] Deleted persistent reward AI file: {file}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerDataManager] Failed to clean persistent ShopAI/{subFolder}: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all progression-related PlayerPrefs (unlocked components and arena progress)
    /// </summary>
    private void ClearProgressionData()
    {
        // Get all PlayerPrefs keys (Unity doesn't provide a direct way, so we need to be creative)
        // We'll clear known progression keys by deleting them individually
        
        // Clear component unlock flags
        // Note: We can't enumerate all PlayerPrefs, so we'll clear common ones and let the system recreate them
        string[] componentUnlockKeys = {
            "ComponentUnlocked_Hammer",
            "ComponentUnlocked_Rifle", 
            "ComponentUnlocked_Shotgun",
            "ComponentUnlocked_Sniper",
            "ComponentUnlocked_Artillery",
            "ComponentUnlocked_Caduceus",
            "ComponentUnlocked_Carbon Weave",          // actual title in ScriptableObject
            "ComponentUnlocked_Ceramic Plating",       // actual title in ScriptableObject
            "ComponentUnlocked_MK-VI Alloy Shell",
            "ComponentUnlocked_Vortex Engine",
            "ComponentUnlocked_Accelerator Frame",
            "ComponentUnlocked_Titan Core",
            "ComponentUnlocked_Velocity Chassis"
        };
        
        foreach (string key in componentUnlockKeys)
        {
            PlayerPrefs.DeleteKey(key);
        }
        
        // Clear arena completion flags (these follow pattern "ArenaCompleted_{arenaKey}")
        // We'll clear some common ones, but the system will handle missing ones gracefully
        string[] arenaKeys = {
            "League1_Round1_Arena1",
            "League1_Round2_Arena1", 
            "League1_Round3_Arena1",
            "League2_Round1_Arena2",
            "League2_Round2_Arena2",
            "League2_Round3_Arena2"
        };
        
        foreach (string arenaKey in arenaKeys)
        {
            PlayerPrefs.DeleteKey($"ArenaCompleted_{arenaKey}");
            PlayerPrefs.DeleteKey($"ArenaRewards_{arenaKey}");
        }
        
        // Clear arena selection data
        PlayerPrefs.DeleteKey("SelectedLeague");
        PlayerPrefs.DeleteKey("SelectedRound");
        PlayerPrefs.DeleteKey("SelectedArenaKey");
        PlayerPrefs.DeleteKey("ArenaEntryFee");

        // Clear new-item flag badges
        PlayerPrefs.DeleteKey("NewItemFlag_Turret");
        PlayerPrefs.DeleteKey("NewItemFlag_Armor");
        PlayerPrefs.DeleteKey("NewItemFlag_EngineFrame");
        PlayerPrefs.DeleteKey("NewItemFlag_TurretAI");
        PlayerPrefs.DeleteKey("NewItemFlag_NavAI");

        PlayerPrefs.Save();
        Debug.Log("[PlayerDataManager] Cleared all progression data from PlayerPrefs");
        
        // Reset tutorial step and tutorial tracking
        playerData.tutorialStep = 0;
        playerData.tutorialButtonPressed = false;
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

    /// <summary>
    /// Get the current player cash amount
    /// </summary>
    public int GetPlayerCash()
    {
        return playerData.playerCash;
    }

    /// <summary>
    /// Set the player cash amount and save immediately
    /// </summary>
    public void SetPlayerCash(int amount)
    {
        playerData.playerCash = Mathf.Max(0, amount); // Prevent negative cash
        SavePlayerData();
        Debug.Log($"[PlayerDataManager] Player cash set to ${playerData.playerCash}");
    }

    /// <summary>
    /// Add cash to the player's balance and save immediately
    /// </summary>
    public void AddPlayerCash(int amount)
    {
        playerData.playerCash = Mathf.Max(0, playerData.playerCash + amount);
        
        // Sync to WorkshopUIManager and update UI
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI != null)
        {
            workshopUI.playerCash = playerData.playerCash;
            workshopUI.UpdatePlayerCashUI();
        }
        
        SavePlayerData();
        Debug.Log($"[PlayerDataManager] Added ${amount} to player cash. New balance: ${playerData.playerCash}");
    }

    /// <summary>
    /// Subtract cash from the player's balance (if sufficient funds) and save immediately
    /// Returns true if transaction succeeded, false if insufficient funds
    /// </summary>
    public bool SpendPlayerCash(int amount)
    {
        if (playerData.playerCash >= amount)
        {
            playerData.playerCash -= amount;
            
            // Sync to WorkshopUIManager and update UI
            var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
            if (workshopUI != null)
            {
                workshopUI.playerCash = playerData.playerCash;
                workshopUI.UpdatePlayerCashUI();
            }
            
            SavePlayerData();
            Debug.Log($"[PlayerDataManager] Spent ${amount}. Remaining balance: ${playerData.playerCash}");
            return true;
        }
        else
        {
            Debug.LogWarning($"[PlayerDataManager] Insufficient funds! Need ${amount}, have ${playerData.playerCash}");
            return false;
        }
    }

    #region ELO Management

    /// <summary>
    /// Get the locally cached player ELO
    /// </summary>
    public int GetPlayerElo()
    {
        return playerData.playerElo;
    }

    /// <summary>
    /// Update the locally cached player ELO (called after Firebase sync)
    /// </summary>
    public void SetPlayerElo(int newElo)
    {
        playerData.playerElo = newElo;
        SavePlayerData();
        Debug.Log($"[PlayerDataManager] Player ELO updated to: {newElo}");
    }

    /// <summary>
    /// Cache the Discord ID for reference
    /// </summary>
    public void SetDiscordId(string discordId)
    {
        if (playerData.lastKnownDiscordId != discordId)
        {
            playerData.lastKnownDiscordId = discordId;
            SavePlayerData();
            Debug.Log($"[PlayerDataManager] Discord ID cached: {discordId}");
        }
    }

    /// <summary>
    /// Get the cached Discord ID
    /// </summary>
    public string GetCachedDiscordId()
    {
        return playerData.lastKnownDiscordId;
    }

    #endregion
}
