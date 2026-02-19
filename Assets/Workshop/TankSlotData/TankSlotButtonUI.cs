using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using AiEditor;

public class TankSlotButtonUI : MonoBehaviour
{
    public Button button;
    public TMP_Text label;
    public Image highlightImage;

    public int slotIndex;
    public string TankName => label != null ? label.text : $"Tank {slotIndex + 1}";

    // Assigned components by category
    private Dictionary<ComponentCategory, object> assignedComponents = new Dictionary<ComponentCategory, object>();

    public bool IsSelected { get; private set; }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        // No color logic here; handled by WorkshopUIManager.UpdateSelectableColor
    }

    public void AssignComponent(ComponentData data)
    {
        // Get the tank slot JSON data
        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager == null)
        {
            Debug.LogError("[TankSlotButtonUI] TankSlotJsonManager not found!");
            return;
        }
        
        var slotData = tankSlotJsonManager.GetTankSlot(slotIndex);
        if (slotData == null)
        {
            Debug.LogError($"[TankSlotButtonUI] Tank slot {slotIndex} not found in JSON manager!");
            return;
        }
        
        if (data.category == ComponentCategory.EngineFrame) {
            slotData.engineFramePrefabGuid = ""; // TODO: Convert prefab to GUID if needed
            slotData.engineFrameInstanceId = data.instanceId;
            // Save the component's custom color
            slotData.engineFrameColor = new ColorJson(data.customColor);
            // Copy stats from ComponentData
            if (data is EngineFrameData engineData)
            {
                slotData.engineWeightCapacity = engineData.weightCapacity;
                slotData.enginePower = engineData.enginePower;
                slotData.engineTorque = engineData.turningPower;
                slotData.engineWeight = data.weight;
                Debug.Log($"[TankSlotButtonUI] Copied engine stats: WeightCapacity={engineData.weightCapacity}, Power={engineData.enginePower}, TurningPower={engineData.turningPower}");
            }
        } else if (data.category == ComponentCategory.Armor) {
            slotData.armorPrefabGuid = ""; // TODO: Convert prefab to GUID if needed
            slotData.armorInstanceId = data.instanceId;
            // Save the component's custom color
            slotData.armorColor = new ColorJson(data.customColor);
            // Copy stats from ComponentData
            if (data is ArmorData armorData)
            {
                slotData.armorHP = armorData.HP;
                slotData.armorWeight = data.weight;
                Debug.Log($"[TankSlotButtonUI] Copied armor stats: HP={armorData.HP}");
            }
        } else if (data.category == ComponentCategory.Turret) {
            slotData.turretPrefabGuid = ""; // TODO: Convert prefab to GUID if needed
            slotData.turretInstanceId = data.instanceId;
            // Save the component's custom color
            slotData.turretColor = new ColorJson(data.customColor);
            // Copy stats from ComponentData
            if (data is TurretData turretData)
            {
                slotData.turretType = (TurretTypeJson)turretData.turretType;
                slotData.turretDamage = turretData.damage;
                slotData.turretRange = turretData.range;
                slotData.turretShotsPerSec = turretData.shotspersec;
                slotData.turretBulletSpeed = turretData.bulletSpeed;
                slotData.turretKnockback = turretData.knockback;
                slotData.turretVisionRange = turretData.visionRange;
                slotData.turretVisionCone = turretData.visionCone;
                slotData.turretWeight = data.weight;
                
                // Copy animation prefab path if present
                if (turretData.animationPrefab != null)
                {
                    slotData.turretAnimationPrefabPath = ComponentDataJson.GetPrefabResourcePath(turretData.animationPrefab);
                }
                else
                {
                    slotData.turretAnimationPrefabPath = "";
                }
                
                // Copy death model prefab path if present
                if (turretData.deathModelPrefab != null)
                {
                    slotData.turretDeathModelPrefabPath = ComponentDataJson.GetPrefabResourcePath(turretData.deathModelPrefab);
                }
                else
                {
                    slotData.turretDeathModelPrefabPath = "";
                }
                
                Debug.Log($"[TankSlotButtonUI] Copied turret stats: Damage={turretData.damage}, Range={turretData.range}, ShotsPerSec={turretData.shotspersec}, AnimPath={slotData.turretAnimationPrefabPath}, DeathModelPath={slotData.turretDeathModelPrefabPath}");
            }
        } else if (data.category == ComponentCategory.AITree) {
            Debug.Log($"AssignComponent: category=AITree, data type={data.GetType().FullName}, instanceId={data.instanceId}");
            if (data is AiTreeAsset aiTreeAsset) {
                if (aiTreeAsset.branchType == AiBranchType.Turret) {
                    slotData.turretAIInstanceId = data.instanceId;
                    slotData.turretAIWeight = data.weight;
                } else if (aiTreeAsset.branchType == AiBranchType.Nav) {
                    slotData.navAIInstanceId = data.instanceId;
                    slotData.navAIWeight = data.weight;
                } else {
                    Debug.LogError($"Tried to assign AiTreeAsset with unsupported branch type: {aiTreeAsset.branchType}");
                }
            } else {
                Debug.LogError($"Tried to assign a non-AiTreeAsset to AITree slot! Actual type: {data.GetType().FullName}, instanceId: {data.instanceId}");
            }
        } else if (data.category == ComponentCategory.TurretAI) {
            // Legacy compatibility: treat as AITree with Turret branch
            Debug.Log($"AssignComponent: legacy category=TurretAI, data type={data.GetType().FullName}, instanceId={data.instanceId}");
            if (data is AiTreeAsset turretAI) {
                slotData.turretAIInstanceId = data.instanceId;
                slotData.turretAIWeight = data.weight;
            } else {
                Debug.LogError($"Tried to assign a non-AiTreeAsset to turretAI slot! Actual type: {data.GetType().FullName}, instanceId: {data.instanceId}");
            }
        } else if (data.category == ComponentCategory.NavAI) {
            // Legacy compatibility: treat as AITree with Nav branch
            Debug.Log($"AssignComponent: legacy category=NavAI, data type={data.GetType().FullName}, instanceId={data.instanceId}");
            if (data is AiTreeAsset navAI) {
                slotData.navAIInstanceId = data.instanceId;
                slotData.navAIWeight = data.weight;
            } else {
                Debug.LogError($"Tried to assign a non-AiTreeAsset to navAI slot! Actual type: {data.GetType().FullName}, instanceId: {data.instanceId}");
            }
        }
        // Add more categories as needed
        
        // Recalculate total weight (include AI weights to match UpdateTankLoadoutSave calculation)
        slotData.totalWeight = slotData.armorWeight + slotData.turretWeight + slotData.engineWeight + slotData.turretAIWeight + slotData.navAIWeight;
        
        // Save the updated slot data to JSON
        tankSlotJsonManager.UpdateTankSlot(slotIndex, slotData);
        
        UpdateAssignedComponentsFromSlotData();
        // Refresh preview and stats if this slot is selected
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (IsSelected && workshopUI != null)
            workshopUI.RefreshSelectedSlotUI();
    }

    public void SetActive(bool active)
    {
        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager != null)
        {
            tankSlotJsonManager.SetTankSlotActive(slotIndex, active);
        }
        
        // Update the total active tanks weight display in WorkshopUIManager
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI != null)
        {
            workshopUI.UpdateTotalActiveTanksWeight();
        }
    }

    public void UnassignComponent(ComponentData data)
    {
        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager == null)
        {
            Debug.LogError("[TankSlotButtonUI] TankSlotJsonManager not found!");
            return;
        }
        
        var slotData = tankSlotJsonManager.GetTankSlot(slotIndex);
        if (slotData == null)
        {
            Debug.LogError($"[TankSlotButtonUI] Tank slot {slotIndex} not found in JSON manager!");
            return;
        }
        
        if (data.category == ComponentCategory.EngineFrame && slotData.engineFrameInstanceId == data.instanceId)
        {
            slotData.engineFramePrefabGuid = "";
            slotData.engineFrameInstanceId = "";
            // Clear engine stats
            slotData.engineWeightCapacity = 0;
            slotData.enginePower = 0;
            slotData.engineTorque = 0;
            slotData.engineWeight = 0;
            slotData.engineFrameHP = 0;
        }
        else if (data.category == ComponentCategory.Armor && slotData.armorInstanceId == data.instanceId)
        {
            slotData.armorPrefabGuid = "";
            slotData.armorInstanceId = "";
            // Clear armor stats
            slotData.armorHP = 0;
            slotData.armorWeight = 0;
        }
        else if (data.category == ComponentCategory.Turret && slotData.turretInstanceId == data.instanceId)
        {
            slotData.turretPrefabGuid = "";
            slotData.turretInstanceId = "";
            // Clear turret stats
            slotData.turretDamage = 0;
            slotData.turretRange = 0f;
            slotData.turretShotsPerSec = 0f;
            slotData.turretKnockback = "";
            slotData.turretVisionRange = 60f; // Reset to default
            slotData.turretVisionCone = 45f; // Reset to default
            slotData.turretWeight = 0;
        }
        else if (data.category == ComponentCategory.AITree) {
            // Handle AITree unassignment based on branch type
            if (data is AiTreeAsset aiTreeAsset) {
                if (aiTreeAsset.branchType == AiBranchType.Turret && slotData.turretAIInstanceId == data.instanceId) {
                    slotData.turretAIInstanceId = "";
                    slotData.turretAIWeight = 0f;
                } else if (aiTreeAsset.branchType == AiBranchType.Nav && slotData.navAIInstanceId == data.instanceId) {
                    slotData.navAIInstanceId = "";
                    slotData.navAIWeight = 0f;
                }
            }
        }
        else if (data.category == ComponentCategory.TurretAI && slotData.turretAIInstanceId == data.instanceId)
        {
            slotData.turretAIInstanceId = "";
            slotData.turretAIWeight = 0f;
        }
        else if (data.category == ComponentCategory.NavAI && slotData.navAIInstanceId == data.instanceId)
        {
            slotData.navAIInstanceId = "";
            slotData.navAIWeight = 0f;
        }
        // Add more categories as needed
        
        // Recalculate total weight after unassignment (include AI weights to match AssignComponent calculation)
        slotData.totalWeight = slotData.armorWeight + slotData.turretWeight + slotData.engineWeight + slotData.turretAIWeight + slotData.navAIWeight;
        
        // Save the updated slot data to JSON
        tankSlotJsonManager.UpdateTankSlot(slotIndex, slotData);
        
        if (assignedComponents.ContainsKey(data.category)) {
            var comp = assignedComponents[data.category] as ComponentData;
            if (comp != null && comp.instanceId == data.instanceId)
                assignedComponents.Remove(data.category);
        }
        UpdateAssignedComponentsFromSlotData();
        // Refresh preview and stats if this slot is selected
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (IsSelected && workshopUI != null)
            workshopUI.RefreshSelectedSlotUI();
    }    public void ClearAllAssignedComponents()
    {
        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager == null)
        {
            Debug.LogError("[TankSlotButtonUI] TankSlotJsonManager not found!");
            return;
        }
        
        var slotData = tankSlotJsonManager.GetTankSlot(slotIndex);
        if (slotData == null)
        {
            Debug.LogError($"[TankSlotButtonUI] Tank slot {slotIndex} not found in JSON manager!");
            return;
        }
        
        // Clear all prefab GUIDs and instance IDs
        slotData.engineFramePrefabGuid = "";
        slotData.armorPrefabGuid = "";
        slotData.turretPrefabGuid = "";
        slotData.engineFrameInstanceId = "";
        slotData.armorInstanceId = "";
        slotData.turretInstanceId = "";
        slotData.turretAIInstanceId = "";
        slotData.navAIInstanceId = "";
        
        // Clear all component stats
        slotData.turretDamage = 0;
        slotData.turretRange = 0f;
        slotData.turretShotsPerSec = 0f;
        slotData.turretKnockback = "";
        slotData.turretVisionRange = 60f; // Reset to default
        slotData.turretVisionCone = 45f; // Reset to default
        slotData.armorHP = 0;
        slotData.engineWeightCapacity = 0;
        slotData.enginePower = 0;
        
        // Save the updated slot data to JSON
        tankSlotJsonManager.UpdateTankSlot(slotIndex, slotData);
        
        UpdateAssignedComponentsFromSlotData();
        // Refresh preview and stats if this slot is selected
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (IsSelected && workshopUI != null)
            workshopUI.RefreshSelectedSlotUI();
    }

    public void UpdateAssignedComponentsFromSlotData()
    {
        assignedComponents.Clear();
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        
        if (tankSlotJsonManager == null || workshopUI == null)
        {
            return;
        }
        
        var slotData = tankSlotJsonManager.GetTankSlot(slotIndex);
        if (slotData == null)
        {
            return;
        }
        
        // EngineFrame
        if (!string.IsNullOrEmpty(slotData.engineFrameInstanceId))
        {
            var comp = workshopUI.playerInventory.Find(c => c.instanceId == slotData.engineFrameInstanceId);
            if (comp != null) 
            {
                // Restore color from TankSlotData
                comp.customColor = slotData.engineFrameColor.ToUnityColor();
                assignedComponents[ComponentCategory.EngineFrame] = comp;
            }
        }
        // Armor
        if (!string.IsNullOrEmpty(slotData.armorInstanceId))
        {
            var comp = workshopUI.playerInventory.Find(c => c.instanceId == slotData.armorInstanceId);
            if (comp != null) 
            {
                // Restore color from TankSlotData
                comp.customColor = slotData.armorColor.ToUnityColor();
                assignedComponents[ComponentCategory.Armor] = comp;
            }
        }
        // Turret
        if (!string.IsNullOrEmpty(slotData.turretInstanceId))
        {
            var comp = workshopUI.playerInventory.Find(c => c.instanceId == slotData.turretInstanceId);
            if (comp != null) 
            {
                // Restore color from TankSlotData
                comp.customColor = slotData.turretColor.ToUnityColor();
                assignedComponents[ComponentCategory.Turret] = comp;
            }
        }
        // AITree components (both legacy TurretAI/NavAI and new AITree category)
        if (!string.IsNullOrEmpty(slotData.turretAIInstanceId)) {
            // Try to find in playerInventory first
            var comp = workshopUI.playerInventory.Find(c => c.instanceId == slotData.turretAIInstanceId);
            if (comp == null) {
                // If not found in inventory, try to load from disk (for AiTreeAssets)
                comp = LoadAITreeAssetFromDisk(slotData.turretAIInstanceId, AiBranchType.Turret);
            }
            if (comp != null) {
                assignedComponents[ComponentCategory.TurretAI] = comp;
                // Also add to AITree category for unified handling
                if (comp is AiTreeAsset) assignedComponents[ComponentCategory.AITree] = comp;
            }
        }
        // NavAI
        if (!string.IsNullOrEmpty(slotData.navAIInstanceId)) {
            // Try to find in playerInventory first
            var comp = workshopUI.playerInventory.Find(c => c.instanceId == slotData.navAIInstanceId);
            if (comp == null) {
                // If not found in inventory, try to load from disk (for AiTreeAssets)
                comp = LoadAITreeAssetFromDisk(slotData.navAIInstanceId, AiBranchType.Nav);
            }
            if (comp != null) {
                assignedComponents[ComponentCategory.NavAI] = comp;
                // Also add to AITree category for unified handling
                if (comp is AiTreeAsset) assignedComponents[ComponentCategory.AITree] = comp;
            }
        }
        // Add more categories as needed
    }

    public bool HasComponent(ComponentData data)
    {
        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager == null) return false;
        
        var slotData = tankSlotJsonManager.GetTankSlot(slotIndex);
        if (slotData == null) return false;
        
        // For AI components, check instanceId directly from TankSlotData
        if (data.category == ComponentCategory.AITree || 
            data.category == ComponentCategory.TurretAI || 
            data.category == ComponentCategory.NavAI)
        {
            if (data is AiTreeAsset aiTree)
            {
                if (aiTree.branchType == AiBranchType.Turret)
                    return slotData.turretAIInstanceId == data.instanceId;
                else if (aiTree.branchType == AiBranchType.Nav)
                    return slotData.navAIInstanceId == data.instanceId;
            }
            // Legacy support for TurretAI/NavAI categories
            if (data.category == ComponentCategory.TurretAI)
                return slotData.turretAIInstanceId == data.instanceId;
            if (data.category == ComponentCategory.NavAI)
                return slotData.navAIInstanceId == data.instanceId;
        }
        
        // For regular components, use the assignedComponents dictionary
        if (assignedComponents.ContainsKey(data.category)) {
            var comp = assignedComponents[data.category] as ComponentData;
            return comp != null && comp.instanceId == data.instanceId;
        }
        return false;
    }    public bool HasCategory(ComponentCategory category)
    {
        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager == null) return false;
        
        var slotData = tankSlotJsonManager.GetTankSlot(slotIndex);
        if (slotData == null) return false;
        
        if (category == ComponentCategory.EngineFrame)
            return !string.IsNullOrEmpty(slotData.engineFrameInstanceId);
        if (category == ComponentCategory.Armor)
            return !string.IsNullOrEmpty(slotData.armorInstanceId);
        if (category == ComponentCategory.Turret)
            return !string.IsNullOrEmpty(slotData.turretInstanceId);
        if (category == ComponentCategory.TurretAI)
            return !string.IsNullOrEmpty(slotData.turretAIInstanceId);
        if (category == ComponentCategory.NavAI)
            return !string.IsNullOrEmpty(slotData.navAIInstanceId);
        if (category == ComponentCategory.AITree)
            return !string.IsNullOrEmpty(slotData.turretAIInstanceId) || !string.IsNullOrEmpty(slotData.navAIInstanceId);
        return false;
    }

    /// <summary>
    /// Gets an AI component by its branch type (used for AITree category handling)
    /// </summary>
    public ComponentData GetAIComponentByBranchType(AiBranchType branchType)
    {
        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager == null) return null;
        
        var slotData = tankSlotJsonManager.GetTankSlot(slotIndex);
        if (slotData == null) return null;
        
        Debug.Log($"[TankSlotButtonUI] GetAIComponentByBranchType called for slot {slotIndex}, branchType={branchType}");
        Debug.Log($"[TankSlotButtonUI] Slot {slotIndex} turretAIInstanceId='{slotData.turretAIInstanceId}', navAIInstanceId='{slotData.navAIInstanceId}'");
        
        if (branchType == AiBranchType.Turret)
        {
            if (!string.IsNullOrEmpty(slotData.turretAIInstanceId))
            {
                Debug.Log($"[TankSlotButtonUI] Loading turret AI for slot {slotIndex}: {slotData.turretAIInstanceId}");
                return LoadAITreeAssetFromDisk(slotData.turretAIInstanceId, AiBranchType.Turret);
            }
            else
            {
                Debug.Log($"[TankSlotButtonUI] Slot {slotIndex} has no turret AI assigned");
            }
        }
        else if (branchType == AiBranchType.Nav)
        {
            if (!string.IsNullOrEmpty(slotData.navAIInstanceId))
            {
                Debug.Log($"[TankSlotButtonUI] Loading nav AI for slot {slotIndex}: {slotData.navAIInstanceId}");
                return LoadAITreeAssetFromDisk(slotData.navAIInstanceId, AiBranchType.Nav);
            }
            else
            {
                Debug.Log($"[TankSlotButtonUI] Slot {slotIndex} has no nav AI assigned");
            }
        }
            
        return null;
    }

    /// <summary>
    /// Checks if assigning this component would conflict with existing assignments
    /// </summary>
    public bool HasConflictingComponent(ComponentData component)
    {
        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager == null) return false;
        
        var slotData = tankSlotJsonManager.GetTankSlot(slotIndex);
        if (slotData == null) return false;
        
        // For AITree components, check branch type conflicts
        if (component.category == ComponentCategory.AITree && component is AiTreeAsset aiAsset)
        {
            if (aiAsset.branchType == AiBranchType.Turret)
            {
                // Only a conflict if there's a different turret AI assigned
                return !string.IsNullOrEmpty(slotData.turretAIInstanceId) && slotData.turretAIInstanceId != component.instanceId;
            }
            else if (aiAsset.branchType == AiBranchType.Nav)
            {
                // Only a conflict if there's a different nav AI assigned
                return !string.IsNullOrEmpty(slotData.navAIInstanceId) && slotData.navAIInstanceId != component.instanceId;
            }
        }
        
        // For other categories, check if there's a different component of the same category
        if (component.category == ComponentCategory.EngineFrame)
            return !string.IsNullOrEmpty(slotData.engineFrameInstanceId) && slotData.engineFrameInstanceId != component.instanceId;
        else if (component.category == ComponentCategory.Armor)
            return !string.IsNullOrEmpty(slotData.armorInstanceId) && slotData.armorInstanceId != component.instanceId;
        else if (component.category == ComponentCategory.Turret)
            return !string.IsNullOrEmpty(slotData.turretInstanceId) && slotData.turretInstanceId != component.instanceId;
        else if (component.category == ComponentCategory.TurretAI)
            return !string.IsNullOrEmpty(slotData.turretAIInstanceId) && slotData.turretAIInstanceId != component.instanceId;
        else if (component.category == ComponentCategory.NavAI)
            return !string.IsNullOrEmpty(slotData.navAIInstanceId) && slotData.navAIInstanceId != component.instanceId;
        
        return false;
    }

    public ComponentData GetComponentByCategory(ComponentCategory category)
    {
        if (assignedComponents.ContainsKey(category))
            return assignedComponents[category] as ComponentData;
        return null;
    }

    public string GetAssignedTankName(ComponentData data)
    {
        return HasComponent(data) ? TankName : "";
    }

    /// <summary>
    /// Loads an AI Tree Asset from disk based on instanceId and branch type
    /// </summary>
    private AiTreeAsset LoadAITreeAssetFromDisk(string instanceId, AiBranchType branchType)
    {
        Debug.Log($"[TankSlotButtonUI] Looking for AI asset: instanceId={instanceId}, branchType={branchType}");
        
        // AI files are stored in persistent data path (AppData), not in Assets folder
        string aiTreesFolder = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees");
        Debug.Log($"[TankSlotButtonUI] Searching in AI trees folder: {aiTreesFolder}");
        
        if (!System.IO.Directory.Exists(aiTreesFolder))
        {
            Debug.LogWarning($"[TankSlotButtonUI] AI trees folder does not exist: {aiTreesFolder}");
            
            // Check if there are any AI files in the workshop inventory instead
            var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
            if (workshopUI != null)
            {
                var aiComponent = workshopUI.playerInventory.Find(c => c.instanceId == instanceId);
                if (aiComponent != null && aiComponent is AiTreeAsset aiFromInventory)
                {
                    Debug.Log($"[TankSlotButtonUI] Found AI in workshop inventory: {aiFromInventory.title}");
                    return aiFromInventory;
                }
            }
            
            return null;
        }
        
        // Search through all JSON files in the AiTrees folder
        string[] jsonFiles = System.IO.Directory.GetFiles(aiTreesFolder, "*.json", System.IO.SearchOption.AllDirectories);
        Debug.Log($"[TankSlotButtonUI] Found {jsonFiles.Length} JSON files in {aiTreesFolder}");
        
        // List all files for debugging
        foreach (string file in jsonFiles)
        {
            string fileName = System.IO.Path.GetFileName(file);
            Debug.Log($"[TankSlotButtonUI] Available AI file: {fileName}");
        }
        
        foreach (string filePath in jsonFiles)
        {
            try
            {
                string jsonContent = System.IO.File.ReadAllText(filePath);
                
                // Create a new AiTreeAsset instance and populate it from JSON
                var aiTreeData = ScriptableObject.CreateInstance<AiTreeAsset>();
                JsonUtility.FromJsonOverwrite(jsonContent, aiTreeData);
                
                if (aiTreeData != null)
                {
                    Debug.Log($"[TankSlotButtonUI] Checking AI file: {aiTreeData.title}, instanceId={aiTreeData.instanceId}, branchType={aiTreeData.branchType} ({(int)aiTreeData.branchType}), file={System.IO.Path.GetFileName(filePath)}");
                    Debug.Log($"[TankSlotButtonUI] Looking for: instanceId={instanceId}, branchType={branchType} ({(int)branchType})");
                    
                    bool instanceIdMatch = aiTreeData.instanceId == instanceId;
                    bool branchTypeMatch = aiTreeData.branchType == branchType;
                    
                    Debug.Log($"[TankSlotButtonUI] InstanceId match: {instanceIdMatch}, BranchType match: {branchTypeMatch}");
                    
                    if (instanceIdMatch && branchTypeMatch)
                    {
                        Debug.Log($"[TankSlotButtonUI] Found matching AI asset: {aiTreeData.title}");
                        return aiTreeData;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TankSlotButtonUI] Failed to load AI file {filePath}: {ex.Message}");
            }
        }
        
        // Fallback: try to find by filename if exact instanceId match fails
        Debug.Log($"[TankSlotButtonUI] Exact match not found, trying filename fallback...");
        
        foreach (string filePath in jsonFiles)
        {
            try
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                
                // Check if the filename contains the instanceId or matches expected patterns
                if (fileName.Contains(instanceId) || fileName.Equals(instanceId))
                {
                    string jsonContent = System.IO.File.ReadAllText(filePath);
                    
                    // Create a new AiTreeAsset instance and populate it from JSON
                    var aiTreeData = ScriptableObject.CreateInstance<AiTreeAsset>();
                    JsonUtility.FromJsonOverwrite(jsonContent, aiTreeData);
                    
                    if (aiTreeData != null && aiTreeData.branchType == branchType)
                    {
                        Debug.LogWarning($"[TankSlotButtonUI] Found AI asset by filename matching: {aiTreeData.title} (expected instanceId: {instanceId}, filename: {fileName})");
                        
                        // Update the asset's instanceId to match what we're looking for
                        aiTreeData.instanceId = instanceId;
                        
                        // Update the saved instanceId in JSON data
                        var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
                        if (tankSlotJsonManager != null)
                        {
                            var slotData = tankSlotJsonManager.GetTankSlot(slotIndex);
                            if (slotData != null)
                            {
                                if (branchType == AiBranchType.Turret)
                                {
                                    slotData.turretAIInstanceId = instanceId;
                                }
                                else if (branchType == AiBranchType.Nav)
                                {
                                    slotData.navAIInstanceId = instanceId;
                                }
                                
                                // Save the corrected data
                                tankSlotJsonManager.UpdateTankSlot(slotIndex, slotData);
                            }
                        }
                        
                        return aiTreeData;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TankSlotButtonUI] Failed to process AI file {filePath} during fallback: {ex.Message}");
            }
        }
        
        // Final fallback: check workshop inventory
        Debug.Log($"[TankSlotButtonUI] File-based search failed, checking workshop inventory...");
        var workshopUIManager = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUIManager != null)
        {
            var aiComponent = workshopUIManager.playerInventory.Find(c => c.instanceId == instanceId);
            if (aiComponent != null && aiComponent is AiTreeAsset aiFromInventory && aiFromInventory.branchType == branchType)
            {
                Debug.Log($"[TankSlotButtonUI] Found AI in workshop inventory as fallback: {aiFromInventory.title}");
                return aiFromInventory;
            }
        }
        
        Debug.LogWarning($"[TankSlotButtonUI] Could not load AI Tree asset with instanceId: {instanceId} and branchType: {branchType} from persistent data path or inventory");
        return null;
    }
}
