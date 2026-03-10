using UnityEngine;
using TMPro;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Displays stats of the component(s) that the currently open AI file is assigned to.
/// Attach this to a UI panel in the AI Editor canvas that contains a TMP_Text component.
/// </summary>
public class AssignedComponentStats : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The text component to display the stats")]
    public TMP_Text statsText;
    
    private string lastInstanceId = "";
    
    void Start()
    {
        if (statsText == null)
        {
            statsText = GetComponentInChildren<TMP_Text>();
        }
        
        Debug.Log("[AssignedComponentStats] Component initialized");
        
        // Initial update
        UpdateStats();
    }
    
    void Update()
    {
        // Only refresh when the AI file changes (check instance ID, not tree name)
        string currentInstanceId = GetCurrentAIInstanceId();
        if (currentInstanceId != lastInstanceId)
        {
            lastInstanceId = currentInstanceId;
            Debug.Log($"[AssignedComponentStats] AI file changed to instance: {currentInstanceId}");
            UpdateStats();
        }
    }
    
    /// <summary>
    /// Updates the displayed stats based on the currently open AI file
    /// </summary>
    public void UpdateStats()
    {
        if (statsText == null)
        {
            Debug.LogWarning("[AssignedComponentStats] statsText is null!");
            return;
        }
            
        // Get the currently open AI file's instance ID
        string currentInstanceId = GetCurrentAIInstanceId();
        
        Debug.Log($"[AssignedComponentStats] Current AI Instance ID: {currentInstanceId}");
        
        if (string.IsNullOrEmpty(currentInstanceId))
        {
            statsText.text = "No AI file loaded";
            Debug.Log("[AssignedComponentStats] No AI file loaded");
            return;
        }
        
        // Find all tank slots that use this AI
        var assignedComponents = FindAssignedComponents(currentInstanceId);
        
        Debug.Log($"[AssignedComponentStats] Found {assignedComponents.Count} tank slot(s) using this AI");
        
        if (assignedComponents.Count == 0)
        {
            statsText.text = "Not assigned to any tank";
            return;
        }
        
        // Build the stats display string
        string statsDisplay = BuildStatsDisplay(assignedComponents);
        statsText.text = statsDisplay;
        Debug.Log($"[AssignedComponentStats] Stats display updated:\n{statsDisplay}");
    }
    
    /// <summary>
    /// Gets the instance ID of the currently open AI file in the editor.
    /// Reads directly from the loaded JSON file via AiEditorFileUI to avoid
    /// ambiguity when multiple files share the same name.
    /// </summary>
    private string GetCurrentAIInstanceId()
    {
        // Get the AiEditorFileUI to read the currently loaded file path
        var fileUI = FindObjectOfType<AiEditorFileUI>();
        if (fileUI == null)
        {
            Debug.LogWarning("[AssignedComponentStats] AiEditorFileUI not found");
            return null;
        }
        
        string jsonPath = fileUI.CurrentJsonPath;
        if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
        {
            Debug.Log("[AssignedComponentStats] No AI file currently loaded");
            return null;
        }
        
        try
        {
            string jsonContent = File.ReadAllText(jsonPath);
            var aiTree = JsonUtility.FromJson<AiTreeAssetJson>(jsonContent);
            if (aiTree != null && !string.IsNullOrEmpty(aiTree.instanceId))
            {
                Debug.Log($"[AssignedComponentStats] Current AI: {aiTree.TreeName} instanceId: {aiTree.instanceId}");
                return aiTree.instanceId;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AssignedComponentStats] Error reading current file {jsonPath}: {e.Message}");
        }
        
        Debug.LogWarning("[AssignedComponentStats] Could not find AI instance ID");
        return null;
    }
    
    /// <summary>
    /// Finds all tank slots that have this AI assigned and returns their component data
    /// </summary>
    private List<AssignedComponentInfo> FindAssignedComponents(string aiInstanceId)
    {
        var result = new List<AssignedComponentInfo>();
        
        // Load all tank slot JSON files - correct folder name is TankSlotData
        string tanksFolder = Path.Combine(Application.persistentDataPath, "TankSlotData");
        
        Debug.Log($"[AssignedComponentStats] Looking for tank slots in: {tanksFolder}");
        
        if (!Directory.Exists(tanksFolder))
        {
            Debug.LogWarning($"[AssignedComponentStats] TankSlotData folder does not exist: {tanksFolder}");
            return result;
        }
            
        string[] slotFiles = Directory.GetFiles(tanksFolder, "*.json");
        
        Debug.Log($"[AssignedComponentStats] Found {slotFiles.Length} tank slot files");
        
        foreach (string filePath in slotFiles)
        {
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                var slotData = JsonUtility.FromJson<TankSlotDataJson>(jsonContent);
                
                if (slotData == null)
                {
                    Debug.LogWarning($"[AssignedComponentStats] Could not parse slot data from: {filePath}");
                    continue;
                }
                    
                // Check if this slot uses our AI
                bool isTurretAI = slotData.turretAIInstanceId == aiInstanceId;
                bool isNavAI = slotData.navAIInstanceId == aiInstanceId;
                
                Debug.Log($"[AssignedComponentStats] Slot {slotData.slotIndex} ({slotData.displayName}): TurretAI={slotData.turretAIInstanceId}, NavAI={slotData.navAIInstanceId}, Match={isTurretAI || isNavAI}");
                
                if (isTurretAI || isNavAI)
                {
                    var info = new AssignedComponentInfo
                    {
                        tankName = slotData.displayName,
                        slotIndex = slotData.slotIndex,
                        isTurretAI = isTurretAI,
                        isNavAI = isNavAI,
                        slotData = slotData
                    };
                    result.Add(info);
                    
                    Debug.Log($"[AssignedComponentStats] Added tank slot {slotData.slotIndex} to results");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AssignedComponentStats] Error reading slot file {filePath}: {e.Message}");
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Builds a formatted string displaying the component stats
    /// </summary>
    private string BuildStatsDisplay(List<AssignedComponentInfo> assignedComponents)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("<size=120%><u><color=#004FFF><b>Assigned To:</b></color></u></size>");
        sb.AppendLine();
        
        foreach (var info in assignedComponents)
        {
            string tankDisplayName = string.IsNullOrEmpty(info.tankName) ? $"Tank Slot {info.slotIndex}" : info.tankName;
            
            sb.AppendLine($"<color=#004FFF>{tankDisplayName}</color>");
            
            if (info.slotData != null)
            {
                // Display all component stats
                sb.AppendLine($"  <color=#004FFF>Turret Type:</color> <color=#000000>{info.slotData.turretType}</color>");
                sb.AppendLine($"  <color=#004FFF>Damage:</color> <color=#000000>{info.slotData.turretDamage}</color>");
                sb.AppendLine($"  <color=#004FFF>Range:</color> <color=#000000>{info.slotData.turretRange:F1}</color>");
                sb.AppendLine($"  <color=#004FFF>Fire Rate:</color> <color=#000000>{info.slotData.turretShotsPerSec:F2}/sec</color>");
                sb.AppendLine($"  <color=#004FFF>Bullet Speed:</color> <color=#000000>{info.slotData.turretBulletSpeed:F1}</color>");
                sb.AppendLine($"  <color=#004FFF>Vision Range:</color> <color=#000000>{info.slotData.turretVisionRange:F1}</color>");
                sb.AppendLine($"  <color=#004FFF>Vision Cone:</color> <color=#000000>{info.slotData.turretVisionCone:F0}°</color>");
                sb.AppendLine($"  <color=#004FFF>Knockback:</color> <color=#000000>{info.slotData.turretKnockback}</color>");
                sb.AppendLine($"  <color=#004FFF>Engine Power:</color> <color=#000000>{info.slotData.enginePower}</color>");
                sb.AppendLine($"  <color=#004FFF>Turning Power:</color> <color=#000000>{info.slotData.engineTorque:F0} N·m</color>");
                sb.AppendLine($"  <color=#004FFF>Total Weight:</color> <color=#000000>{info.slotData.totalWeight:F0} kg</color>");
                sb.AppendLine($"  <color=#004FFF>Armor HP:</color> <color=#000000>{info.slotData.armorHP}</color>");
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Helper class to store assigned component information
    /// </summary>
    private class AssignedComponentInfo
    {
        public string tankName;
        public int slotIndex;
        public bool isTurretAI;
        public bool isNavAI;
        public TankSlotDataJson slotData;
    }
}
