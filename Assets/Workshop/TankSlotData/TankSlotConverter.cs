using UnityEngine;

/// <summary>
/// Simple component to trigger tank slot data conversion from ScriptableObjects to JSON
/// Attach this to any GameObject and click the "Convert Tank Slots" button in the inspector
/// </summary>
public class TankSlotConverter : MonoBehaviour
{
    [Header("Tank Slot Data Conversion")]
    [Tooltip("Click this button to convert all TankSlot ScriptableObjects to JSON files")]
    public bool convertTankSlots = false;
    
    void Update()
    {
        // Check if the conversion flag was set in the inspector
        if (convertTankSlots)
        {
            convertTankSlots = false; // Reset the flag
            ConvertAllTankSlots();
        }
    }
    
    /// <summary>
    /// Converts all tank slot ScriptableObjects to JSON format
    /// </summary>
    public void ConvertAllTankSlots()
    {
        var converter = gameObject.GetComponent<TankSlotDataConverter>();
        if (converter == null)
        {
            converter = gameObject.AddComponent<TankSlotDataConverter>();
        }
        
        converter.ConvertAllTankSlotsToJson();
        
        Debug.Log("Tank slot conversion triggered. Check the console for results.");
    }
    
    /// <summary>
    /// Test method to load and display converted JSON data
    /// </summary>
    [ContextMenu("Test Load JSON Tank Slots")]
    public void TestLoadJsonTankSlots()
    {
        var tankSlots = TankSlotDataConverter.LoadAllTankSlotsFromJson();
        
        Debug.Log($"Loaded {tankSlots.Count} tank slots from JSON:");
        
        foreach (var slot in tankSlots)
        {
            Debug.Log($"  {slot.slotName} (Index: {slot.slotIndex}, Active: {slot.isActive}, " +
                     $"Turret: {slot.turretInstanceId}, Nav: {slot.navAIInstanceId})");
        }
    }
}
