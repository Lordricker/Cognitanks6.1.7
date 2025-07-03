using System.Collections.Generic;
using System.IO;
using UnityEngine;
using AiEditor;

/// <summary>
/// Manager class for handling tank slot configurations in JSON format
/// This replaces ScriptableObject-based tank slot data for builds where SOs cannot be modified
/// </summary>
public class TankSlotJsonManager : MonoBehaviour
{
    [Header("Tank Slot Configuration")]
    public bool useJsonTankSlots = true;
    
    private List<TankSlotDataJson> tankSlots = new List<TankSlotDataJson>();
    private bool isInitialized = false;
    
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
        if (isInitialized) return;
        
        if (useJsonTankSlots)
        {
            tankSlots = TankSlotDataConverter.LoadAllTankSlotsFromJson();
            Debug.Log($"[TankSlotJsonManager] Loaded {tankSlots.Count} tank slots from JSON");
        }
        else
        {
            Debug.Log("[TankSlotJsonManager] Using ScriptableObject tank slots (editor only)");
        }
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Get tank slot data by index (0-9)
    /// </summary>
    public TankSlotDataJson GetTankSlot(int slotIndex)
    {
        if (!isInitialized) InitializeTankSlots();
        
        if (slotIndex < 0 || slotIndex >= tankSlots.Count) return null;
        
        return tankSlots.Find(slot => slot.slotIndex == slotIndex);
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
        TankSlotDataConverter.SaveTankSlotToJson(updatedSlot);
        
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
    /// Convert ScriptableObject TankSlotData to JSON format (for migration)
    /// </summary>
    public TankSlotDataJson ConvertScriptableObjectToJson(TankSlotData soData, int slotIndex)
    {
        return TankSlotDataConverter.ConvertToJson(soData, $"TankSlot {slotIndex}", slotIndex);
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
}
