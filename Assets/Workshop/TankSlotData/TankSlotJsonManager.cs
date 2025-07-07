using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AiEditor;

/// <summary>
/// Manager class for handling tank slot configurations in JSON format
/// All tank slot data is stored as JSON files in the persistent data path (AppData)
/// </summary>
public class TankSlotJsonManager : MonoBehaviour
{
    public static TankSlotJsonManager Instance { get; private set; }
    
    private List<TankSlotDataJson> tankSlots = new List<TankSlotDataJson>();
    private bool isInitialized = false;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Initialize the manager and load tank slot data from JSON
    /// </summary>
    void Start()
    {
        InitializeTankSlots();
    }
    
    /// <summary>
    /// Load all tank slot configurations from JSON files
    /// </summary>
    public void InitializeTankSlots()
    {
        if (isInitialized) 
        {
            Debug.Log("[TankSlotJsonManager] Already initialized, skipping");
            return;
        }
        
        Debug.Log("[TankSlotJsonManager] Starting initialization...");
        
        tankSlots = LoadAllTankSlotsFromJson();
        
        Debug.Log($"[TankSlotJsonManager] Loaded {tankSlots.Count} tank slots from JSON files");
        
        // If no tank slots were loaded, create default ones (0-9)
        if (tankSlots.Count == 0)
        {
            Debug.Log("[TankSlotJsonManager] No tank slots found, creating default slots 0-9");
            CreateDefaultTankSlots();
        }
        else
        {
            // Ensure we have all 10 slots (0-9) - fill in any missing ones
            EnsureAllSlotsExist();
        }
        
        Debug.Log($"[TankSlotJsonManager] Initialization complete with {tankSlots.Count} tank slots");
        
        // Print summary for debugging
        foreach (var slot in tankSlots)
        {
            Debug.Log($"[TankSlotJsonManager] Slot {slot.slotIndex}: isActive={slot.isActive}, hasEngine={!string.IsNullOrEmpty(slot.engineFrameInstanceId)}, name='{slot.displayName}'");
        }
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Get tank slot data by index (0-9)
    /// </summary>
    public TankSlotDataJson GetTankSlot(int slotIndex)
    {
        if (!isInitialized) InitializeTankSlots();
        
        // Valid slot indices are 0-9
        if (slotIndex < 0 || slotIndex > 9) 
        {
            Debug.LogWarning($"[TankSlotJsonManager] Invalid slot index: {slotIndex}. Valid range is 0-9.");
            return null;
        }
        
        var slot = tankSlots.Find(slot => slot.slotIndex == slotIndex);
        if (slot == null)
        {
            Debug.LogWarning($"[TankSlotJsonManager] Tank slot {slotIndex} not found! Creating default slot.");
            // Create the missing slot if it doesn't exist
            EnsureAllSlotsExist();
            slot = tankSlots.Find(slot => slot.slotIndex == slotIndex);
        }
        
        return slot;
    }
    
    /// <summary>
    /// Update a tank slot configuration and save to JSON
    /// </summary>
    public void UpdateTankSlot(int slotIndex, TankSlotDataJson updatedSlot)
    {
        if (!isInitialized) InitializeTankSlots();
        
        var existingSlot = GetTankSlot(slotIndex);
        if (existingSlot != null)
        {
            // Update the slot in our list
            int index = tankSlots.FindIndex(slot => slot.slotIndex == slotIndex);
            if (index >= 0)
            {
                tankSlots[index] = updatedSlot;
            }
        }
        else
        {
            // Add new slot
            tankSlots.Add(updatedSlot);
        }
        
        // Save to JSON file
        SaveTankSlotToJson(updatedSlot);
        
        Debug.Log($"[TankSlotJsonManager] Updated tank slot {slotIndex}");
    }
    
    /// <summary>
    /// Get all active tank slots
    /// </summary>
    public List<TankSlotDataJson> GetActiveTankSlots()
    {
        if (!isInitialized) InitializeTankSlots();
        
        return tankSlots.FindAll(slot => slot.isActive);
    }
    
    /// <summary>
    /// Get all tank slots for a specific team
    /// </summary>
    public List<TankSlotDataJson> GetTankSlotsByTeam(int teamId)
    {
        if (!isInitialized) InitializeTankSlots();
        
        return tankSlots.FindAll(slot => slot.teamId == teamId && slot.isActive);
    }
    
    /// <summary>
    /// Get all tank slots (both active and inactive)
    /// </summary>
    public List<TankSlotDataJson> GetAllTankSlots()
    {
        if (!isInitialized) InitializeTankSlots();
        
        return tankSlots;
    }

    
    /// <summary>
    /// Assign an AI component to a tank slot
    /// </summary>
    public void AssignAIToTankSlot(int slotIndex, AiTreeAsset aiComponent, bool isTurretAI)
    {
        var tankSlot = GetTankSlot(slotIndex);
        if (tankSlot == null)
        {
            Debug.LogWarning($"[TankSlotJsonManager] Tank slot {slotIndex} not found");
            return;
        }
        
        if (isTurretAI)
        {
            tankSlot.turretAIInstanceId = aiComponent.instanceId;
        }
        else
        {
            tankSlot.navAIInstanceId = aiComponent.instanceId;
        }
        
        // Update and save
        UpdateTankSlot(slotIndex, tankSlot);
        
        Debug.Log($"[TankSlotJsonManager] Assigned {(isTurretAI ? "turret" : "nav")} AI '{aiComponent.title}' to tank slot {slotIndex}");
    }
    
    /// <summary>
    /// Remove AI assignment from a tank slot
    /// </summary>
    public void RemoveAIFromTankSlot(int slotIndex, bool isTurretAI)
    {
        var tankSlot = GetTankSlot(slotIndex);
        if (tankSlot == null) return;
        
        if (isTurretAI)
        {
            tankSlot.turretAIInstanceId = "";
        }
        else
        {
            tankSlot.navAIInstanceId = "";
        }
        
        // Update and save
        UpdateTankSlot(slotIndex, tankSlot);
        
        Debug.Log($"[TankSlotJsonManager] Removed {(isTurretAI ? "turret" : "nav")} AI from tank slot {slotIndex}");
    }
    
    /// <summary>
    /// Activate/deactivate a tank slot
    /// </summary>
    public void SetTankSlotActive(int slotIndex, bool isActive)
    {
        var tankSlot = GetTankSlot(slotIndex);
        if (tankSlot == null) return;
        
        tankSlot.isActive = isActive;
        UpdateTankSlot(slotIndex, tankSlot);
        
        Debug.Log($"[TankSlotJsonManager] Tank slot {slotIndex} {(isActive ? "activated" : "deactivated")}");
    }
    
    /// <summary>
    /// Get a summary of all tank slot configurations
    /// </summary>
    [ContextMenu("Print Tank Slot Summary")]
    public void PrintTankSlotSummary()
    {
        if (!isInitialized) InitializeTankSlots();
        
        Debug.Log($"=== Tank Slot Configuration Summary ===");
        Debug.Log($"Total tank slots: {tankSlots.Count}");
        Debug.Log($"Active tank slots: {GetActiveTankSlots().Count}");
        
        foreach (var slot in tankSlots)
        {
            string aiInfo = "";
            if (!string.IsNullOrEmpty(slot.turretAIInstanceId))
                aiInfo += $"Turret AI: {slot.turretAIInstanceId.Substring(0, 8)}... ";
            if (!string.IsNullOrEmpty(slot.navAIInstanceId))
                aiInfo += $"Nav AI: {slot.navAIInstanceId.Substring(0, 8)}...";
            
            Debug.Log($"  Slot {slot.slotIndex}: {(slot.isActive ? "ACTIVE" : "inactive")} " +
                     $"Team {slot.teamId} {aiInfo}");
        }
    }
    
    /// <summary>
    /// Creates default tank slots (0-9) with basic configuration
    /// </summary>
    private void CreateDefaultTankSlots()
    {
        for (int i = 0; i < 10; i++)
        {
            var defaultSlot = new TankSlotDataJson
            {
                slotIndex = i,
                slotName = $"TankSlot {i}",
                displayName = $"Tank {i}",
                isActive = (i == 0), // Make first tank slot active by default for testing
                teamId = 0,
                isPlayerControlled = true,
                
                // Initialize empty component assignments
                engineFramePrefabGuid = "",
                engineFrameInstanceId = (i == 0) ? "test_engine_frame" : "", // Give first tank a test engine for testing
                armorPrefabGuid = "",
                armorInstanceId = (i == 0) ? "test_armor" : "", // Give first tank test armor for testing
                turretPrefabGuid = "",
                turretInstanceId = (i == 0) ? "test_turret" : "", // Give first tank test turret for testing
                turretAIInstanceId = "",
                navAIInstanceId = "",
                
                // Initialize default colors (white)
                engineFrameColor = new ColorJson(1f, 1f, 1f, 1f),
                armorColor = new ColorJson(1f, 1f, 1f, 1f),
                turretColor = new ColorJson(1f, 1f, 1f, 1f),
                
                // Initialize default stats - give test values to first tank
                totalWeight = (i == 0) ? 100f : 0f,
                engineWeightCapacity = (i == 0) ? 200 : 0,
                enginePower = (i == 0) ? 150 : 0,
                armorHP = (i == 0) ? 100 : 0,
                turretDamage = (i == 0) ? 25 : 0,
                turretRange = (i == 0) ? 50f : 0f,
                turretShotsPerSec = (i == 0) ? 1f : 0f,
                turretKnockback = (i == 0) ? "Medium" : "",
                turretVisionRange = (i == 0) ? 60f : 60f,
                turretVisionCone = (i == 0) ? 45f : 45f
            };
            
            tankSlots.Add(defaultSlot);
            SaveTankSlotToJson(defaultSlot);
        }
        
        Debug.Log("[TankSlotJsonManager] Created 10 default tank slots (0-9) with Tank 0 active for testing");
    }
    
    /// <summary>
    /// Ensures that all tank slots 0-9 exist, creating any missing ones
    /// </summary>
    private void EnsureAllSlotsExist()
    {
        for (int i = 0; i < 10; i++)
        {
            if (!tankSlots.Exists(slot => slot.slotIndex == i))
            {
                Debug.Log($"[TankSlotJsonManager] Missing tank slot {i}, creating default");
                
                var defaultSlot = new TankSlotDataJson
                {
                    slotIndex = i,
                    slotName = $"TankSlot {i}",
                    isActive = false,
                    teamId = 0,
                    
                    // Initialize empty component assignments
                    engineFramePrefabGuid = "",
                    engineFrameInstanceId = "",
                    armorPrefabGuid = "",
                    armorInstanceId = "",
                    turretPrefabGuid = "",
                    turretInstanceId = "",
                    turretAIInstanceId = "",
                    navAIInstanceId = "",
                    
                    // Initialize default colors (white)
                    engineFrameColor = new ColorJson(1f, 1f, 1f, 1f),
                    armorColor = new ColorJson(1f, 1f, 1f, 1f),
                    turretColor = new ColorJson(1f, 1f, 1f, 1f),
                    
                    // Initialize default stats
                    totalWeight = 0f,
                    engineWeightCapacity = 0,
                    enginePower = 0,
                    armorHP = 0,
                    turretDamage = 0,
                    turretRange = 0f,
                    turretShotsPerSec = 0f,
                    turretKnockback = "",
                    turretVisionRange = 60f,
                    turretVisionCone = 45f
                };
                
                tankSlots.Add(defaultSlot);
                SaveTankSlotToJson(defaultSlot);
            }
        }
        
        // Sort the list to ensure proper ordering
        tankSlots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
    }
    
    // ===== JSON FILE OPERATIONS =====
    
    /// <summary>
    /// Gets the folder path where tank slot JSON files are stored (persistent data path)
    /// </summary>
    private static string GetTankSlotJsonFolder()
    {
        return Path.Combine(Application.persistentDataPath, "TankSlotData");
    }
    
    /// <summary>
    /// Loads all tank slot configurations from JSON files in persistent data path
    /// </summary>
    private static List<TankSlotDataJson> LoadAllTankSlotsFromJson()
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
    private static void SaveTankSlotToJson(TankSlotDataJson tankSlotData)
    {
        if (tankSlotData == null) return;
        
        string jsonFolder = GetTankSlotJsonFolder();
        
        // Ensure the folder exists
        if (!Directory.Exists(jsonFolder))
        {
            Directory.CreateDirectory(jsonFolder);
            Debug.Log($"[TankSlotJsonManager] Created tank slot JSON folder: {jsonFolder}");
        }
        
        // Use slotIndex to generate consistent filenames
        string fileName = !string.IsNullOrEmpty(tankSlotData.slotName) 
            ? $"{tankSlotData.slotName}.json" 
            : $"TankSlot {tankSlotData.slotIndex}.json";
            
        string filePath = Path.Combine(jsonFolder, fileName);
        string jsonContent = JsonUtility.ToJson(tankSlotData, true);
        
        try
        {
            File.WriteAllText(filePath, jsonContent);
            Debug.Log($"[TankSlotJsonManager] Saved tank slot {tankSlotData.slotIndex} to: {filePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TankSlotJsonManager] Failed to save tank slot JSON: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Loads a specific tank slot configuration by slot index
    /// </summary>
    private static TankSlotDataJson LoadTankSlotFromJson(int slotIndex)
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
}
