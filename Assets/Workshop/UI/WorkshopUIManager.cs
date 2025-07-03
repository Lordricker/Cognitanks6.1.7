using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using AiEditor;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WorkshopUIManager : MonoBehaviour
{
    public Toggle shopToggle;
    public Toggle inventoryToggle;
    
    public Toggle turretToggle;
    public Toggle armorToggle;
    public Toggle turretAIToggle;
    public Toggle navAIToggle;
    public Toggle engineFrameToggle;

    public Transform scrollContentParent;
    public GameObject componentEntryPrefab;

    private bool isShopView = true;
    private ComponentCategory currentCategory = ComponentCategory.Turret;
    
    [Header("Shop Components By Category")]
    public List<ComponentData> turretShopComponents;
    public List<ComponentData> armorShopComponents;
    public List<ComponentData> turretAIShopComponents;
    public List<ComponentData> navAIShopComponents;
    public List<ComponentData> aiTreeShopComponents;
    public List<ComponentData> engineFrameShopComponents;

    public List<ComponentData> playerInventory;

    [Header("Player Cash")]
    public int playerCash = 1000;
    public TMP_Text playerCashText;

    public List<TankSlotButtonUI> tankSlots;
    private TankSlotButtonUI selectedTankSlot;
    
    public WorkshopModelPreview modelPreview;
    public WorkshopStatsPanel statsPanel;
    
    [Header("Debug UI")]
    public TMP_Text debugText; // Assign in inspector

    private Coroutine debugTextCoroutine;
    private ComponentData selectedComponent;

    public TMP_Text itemStatsText;
    public TMP_Text descriptionText;

    private void Start()
    {
        // Ensure only one of Shop/Inventory is active
        shopToggle.isOn = true;
        inventoryToggle.isOn = false;
        
        // Ensure only one category is active
        turretToggle.isOn = true;
        armorToggle.isOn = false;
        turretAIToggle.isOn = false;
        navAIToggle.isOn = false;
        engineFrameToggle.isOn = false;

        SetViewShop(true);
        SetCategory(ComponentCategory.Turret);

        shopToggle.onValueChanged.AddListener((isOn) => { if (isOn) SetViewShop(true); });
        inventoryToggle.onValueChanged.AddListener((isOn) => { if (isOn) SetViewShop(false); });
        
        turretToggle.onValueChanged.AddListener((isOn) => { if (isOn) SetCategory(ComponentCategory.Turret); });
        armorToggle.onValueChanged.AddListener((isOn) => { if (isOn) SetCategory(ComponentCategory.Armor); });
        turretAIToggle.onValueChanged.AddListener((isOn) => { if (isOn) SetCategory(ComponentCategory.TurretAI); });
        navAIToggle.onValueChanged.AddListener((isOn) => { if (isOn) SetCategory(ComponentCategory.NavAI); });
        engineFrameToggle.onValueChanged.AddListener((isOn) => { if (isOn) SetCategory(ComponentCategory.EngineFrame); });

        UpdatePlayerCashUI();
        UpdateToggleColors();

        // Setup tank slot button listeners
        for (int i = 0; i < tankSlots.Count; i++)
        {
            int idx = i;
            tankSlots[i].button.onClick.AddListener(() => OnTankSlotSelected(idx));
            tankSlots[i].SetSelected(false);
        }
        selectedTankSlot = null;
        
        LoadPlayerInventoryFromSave();
        
        // Load AI components from AI Editor folders
        LoadAIComponentsFromFolders();
        
        // Clean up any legacy AI files with old instanceId format
        CleanupLegacyAIFiles();
        
        // Restore component data references and activation states for all tank slots after loading
        var allSlotData = new List<TankSlotData>();
        foreach (var slot in tankSlots)
        {
            if (slot.slotData != null)
                allSlotData.Add(slot.slotData);
        }
        
        PlayerDataManager.Instance.RestoreComponentDataReferences(allSlotData);
        
        // Load tank slots from ScriptableObjects AFTER restoring activation states
        LoadTankSlotsFromScriptableObjects();
    }
    
    private void LoadPlayerInventoryFromSave()
    {
        playerInventory.Clear();
        foreach (var entry in PlayerDataManager.Instance.playerData.ownedComponents)
        {
            ComponentData prefab = FindComponentPrefabById(entry.id);
            if (prefab != null)
            {
                foreach (var instanceId in entry.instanceIds)
                {
                    ComponentData newComp = Instantiate(prefab);
                    newComp.instanceId = instanceId;
                    playerInventory.Add(newComp);
                }
            }
        }
        
        // Add default components for new players if inventory is empty
        if (playerInventory.Count == 0)
        {
            AddDefaultComponentsToInventory();
        }
    }
    
    private void AddDefaultComponentsToInventory()
    {
        Debug.Log("[WorkshopUIManager] Adding default components for new player");
        
        // Add default AITree components that tank slots expect - now stored on disk
        var defaultTurretAI = aiTreeShopComponents.Find(c => c.id == "Aggressive Hunter" && 
            c is AiTreeAsset tree && tree.branchType == AiEditor.AiBranchType.Turret);
        if (defaultTurretAI != null)
        {
            // Add TurretAI for Tank Slot 0
            ComponentData turretAI1 = Instantiate(defaultTurretAI);
            turretAI1.instanceId = "Aggressive Hunter_ce9255c7-8383-4919-a8ba-d1686373d471";
            playerInventory.Add(turretAI1);
            
            // Add TurretAI for Tank Slot 9
            ComponentData turretAI2 = Instantiate(defaultTurretAI);
            turretAI2.instanceId = "Aggressive Hunter_c082be34-e8c2-4937-8aaf-e9f11fced160";
            playerInventory.Add(turretAI2);
            
            // Also add to save data
            var entry = PlayerDataManager.Instance.playerData.ownedComponents.Find(e => e.id == defaultTurretAI.id);
            if (entry == null)
            {
                entry = new OwnedComponentEntry { id = defaultTurretAI.id, instanceIds = new List<string>() };
                PlayerDataManager.Instance.playerData.ownedComponents.Add(entry);
            }
            entry.instanceIds.Add(turretAI1.instanceId);
            entry.instanceIds.Add(turretAI2.instanceId);

#if UNITY_EDITOR
            // In editor: Also create copies in AISaveFiles folders for AI Editor compatibility
            string assetPath1 = "Assets/AiEditor/AISaveFiles/TurretFiles/" + turretAI1.instanceId + ".asset";
            ComponentData editorCopy1 = Instantiate(turretAI1);
            UnityEditor.AssetDatabase.CreateAsset(editorCopy1, assetPath1);
            
            string assetPath2 = "Assets/AiEditor/AISaveFiles/TurretFiles/" + turretAI2.instanceId + ".asset";
            ComponentData editorCopy2 = Instantiate(turretAI2);
            UnityEditor.AssetDatabase.CreateAsset(editorCopy2, assetPath2);
            
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("[WorkshopUIManager] Created default AI components in playerInventory and AISaveFiles folders");
#else
            Debug.Log("[WorkshopUIManager] Created default AI components in playerInventory");
#endif
        }
        else
        {
            Debug.LogError("Could not find 'Aggressive Hunter' AITree Turret component in shop components!");
        }
    }

    private ComponentData FindComponentPrefabById(string id)
    {
        // Search all shop lists for a matching id
        foreach (var c in turretShopComponents) if (c.id == id) return c;
        foreach (var c in armorShopComponents) if (c.id == id) return c;
        foreach (var c in aiTreeShopComponents) if (c.id == id) return c;
        foreach (var c in engineFrameShopComponents) if (c.id == id) return c;
        return null;
    }

    private void UpdatePlayerCashUI()
    {
        if (playerCashText != null)
            playerCashText.text = $"${playerCash}";
    }

    public static void UpdateSelectableColor(Selectable selectable, bool isSelected)
    {
        var colors = selectable.colors;
        Color prefabSelected = colors.selectedColor;
        Color prefabPressed = colors.pressedColor;
        colors.normalColor = isSelected ? prefabSelected : prefabPressed;
        selectable.colors = colors;
    }

    private void SetViewShop(bool isShop)
    {
        isShopView = isShop;
        UpdateToggleColors();
        PopulateComponentList();
    }

    private void SetCategory(ComponentCategory category)
    {
        currentCategory = category;
        UpdateToggleColors();
        PopulateComponentList();
    }

    private void UpdateToggleColors()
    {
        UpdateSelectableColor(shopToggle, shopToggle.isOn);
        UpdateSelectableColor(inventoryToggle, inventoryToggle.isOn);
        UpdateSelectableColor(turretToggle, turretToggle.isOn);
        UpdateSelectableColor(armorToggle, armorToggle.isOn);
        UpdateSelectableColor(turretAIToggle, turretAIToggle.isOn);
        UpdateSelectableColor(navAIToggle, navAIToggle.isOn);
        UpdateSelectableColor(engineFrameToggle, engineFrameToggle.isOn);

        // Also update tank slot buttons
        foreach (var slot in tankSlots)
            UpdateSelectableColor(slot.button, slot.IsSelected);
    }    public void PopulateComponentList()
    {
        foreach (Transform child in scrollContentParent)
            Destroy(child.gameObject);

        List<ComponentData> source;
        
        if (isShopView)
        {
            // Use the selected category's shop list
            switch (currentCategory)
            {
                case ComponentCategory.Turret:
                    source = turretShopComponents;
                    break;
                case ComponentCategory.Armor:
                    source = armorShopComponents;
                    break;
                case ComponentCategory.TurretAI:
                    // Filter AITree components for Turret branch type
                    source = aiTreeShopComponents.FindAll(c => 
                        c is AiTreeAsset tree && tree.branchType == AiEditor.AiBranchType.Turret);
                    Debug.Log($"[WorkshopUIManager] TurretAI shop category: Found {source.Count} turret AI assets from {aiTreeShopComponents.Count} total AI assets");
                    break;
                case ComponentCategory.NavAI:
                    // Filter AITree components for Nav branch type  
                    source = aiTreeShopComponents.FindAll(c => 
                        c is AiTreeAsset tree && tree.branchType == AiEditor.AiBranchType.Nav);
                    Debug.Log($"[WorkshopUIManager] NavAI shop category: Found {source.Count} nav AI assets from {aiTreeShopComponents.Count} total AI assets");
                    break;
                case ComponentCategory.EngineFrame:
                    source = engineFrameShopComponents;
                    break;
                default:
                    source = new List<ComponentData>();
                    break;
            }
        }
        else
        {
            // Inventory: handle AI categories by branch type from playerInventory
            if (currentCategory == ComponentCategory.TurretAI)
            {
                // Show Turret AI trees from playerInventory
                source = playerInventory.FindAll(c => c is AiTreeAsset tree && tree.branchType == AiEditor.AiBranchType.Turret);
                Debug.Log($"[WorkshopUIManager] TurretAI inventory: Found {source.Count} turret AI assets in playerInventory");
            }
            else if (currentCategory == ComponentCategory.NavAI)
            {
                // Show Nav AI trees from playerInventory
                source = playerInventory.FindAll(c => c is AiTreeAsset tree && tree.branchType == AiEditor.AiBranchType.Nav);
                Debug.Log($"[WorkshopUIManager] NavAI inventory: Found {source.Count} nav AI assets in playerInventory");
            }
            else
            {
                // Filter standard inventory by category
                source = playerInventory.FindAll(c => c.category == currentCategory);
            }
        }

        foreach (var component in source)
        {
            GameObject entryGO = Instantiate(componentEntryPrefab, scrollContentParent);
            ComponentEntryUI entryUI = entryGO.GetComponent<ComponentEntryUI>();
            
            entryUI.Setup(
                component,
                GetAssignedTankName(component),
                isShopView,
                OnBuyComponent,
                OnSellComponent,
                OnEquipComponent,
                OnComponentSelected, // selection callback
                (changedComponent) => {
                    // Handle color changes for components
                    OnComponentColorChanged(changedComponent);
                }
            );
        }
    }

    private void OnTankSlotSelected(int index)
    {
        // If the clicked slot is already selected, unselect all
        if (selectedTankSlot == tankSlots[index])
        {
            selectedTankSlot.SetSelected(false);
            UpdateSelectableColor(selectedTankSlot.button, false);
            selectedTankSlot = null;
            Debug.Log("All tank slots unselected");
            // Unselect in EventSystem so button is not visually selected
            EventSystem.current.SetSelectedGameObject(null);
            UpdateToggleColors();
            PopulateComponentList();
            // Clear tank preview when no slot is selected
            if (modelPreview != null)
                modelPreview.ClearPreview();
            // Clear stats and description when no slot is selected
            itemStatsText.text = "";
            descriptionText.text = "";
            return;
        }

        if (selectedTankSlot != null)
            selectedTankSlot.SetSelected(false);

        selectedTankSlot = tankSlots[index];
        selectedTankSlot.SetSelected(true);
        Debug.Log($"tankslot{index} selected");
        UpdateToggleColors();
        PopulateComponentList();

        // Show tank preview for selected slot
        if (modelPreview != null)
            modelPreview.ShowTank(GetEquippedComponentsForSlot(selectedTankSlot));
        
        // Sum weights of all equipped components and update ItemStats text, clear description
        float totalWeight = CalculateAndSaveTotalWeight(selectedTankSlot);
        itemStatsText.text = $"Total Weight: {totalWeight}";
        descriptionText.text = "";
    }
    
    // Helper to get equipped components for a tank slot
    private Dictionary<ComponentCategory, ComponentData> GetEquippedComponentsForSlot(TankSlotButtonUI slot)
    {
        var equipped = new Dictionary<ComponentCategory, ComponentData>();
        foreach (ComponentCategory cat in System.Enum.GetValues(typeof(ComponentCategory)))
        {
            var comp = slot.GetComponentByCategory(cat);
            if (comp != null)
            {
                // Restore color from TankSlotData to component for visual consistency
                if (slot.slotData != null)
                {
                    switch (comp.category)
                    {
                        case ComponentCategory.EngineFrame:
                            comp.customColor = slot.slotData.engineFrameColor;
                            break;
                        case ComponentCategory.Armor:
                            comp.customColor = slot.slotData.armorColor;
                            break;
                        case ComponentCategory.Turret:
                            comp.customColor = slot.slotData.turretColor;
                            break;
                    }
                }
                equipped[cat] = comp;
            }
        }
        return equipped;
    }
    
    private string GetAssignedTankName(ComponentData component)
    {
        foreach (var slot in tankSlots)
        {
            if (slot.slotData != null)
            {
                // Check regular components by prefab and instanceId
                if ((component.category == ComponentCategory.EngineFrame && 
                     slot.slotData.engineFramePrefab == component.modelPrefab && 
                     slot.slotData.engineFrameInstanceId == component.instanceId) ||
                    (component.category == ComponentCategory.Armor && 
                     slot.slotData.armorPrefab == component.modelPrefab && 
                     slot.slotData.armorInstanceId == component.instanceId) ||
                    (component.category == ComponentCategory.Turret && 
                     slot.slotData.turretPrefab == component.modelPrefab && 
                     slot.slotData.turretInstanceId == component.instanceId))
                {
                    return slot.TankName;
                }
                
                // Check AI components by instanceId only (since they're stored on disk)
                if (component.category == ComponentCategory.AITree && component is AiTreeAsset aiAsset)
                {
                    if ((aiAsset.branchType == AiEditor.AiBranchType.Turret && 
                         slot.slotData.turretAIInstanceId == component.instanceId) ||
                        (aiAsset.branchType == AiEditor.AiBranchType.Nav && 
                         slot.slotData.navAIInstanceId == component.instanceId))
                    {
                        return slot.TankName;
                    }
                }
                
                // Legacy AI category support
                if ((component.category == ComponentCategory.TurretAI && 
                     slot.slotData.turretAIInstanceId == component.instanceId) ||
                    (component.category == ComponentCategory.NavAI && 
                     slot.slotData.navAIInstanceId == component.instanceId))
                {
                    return slot.TankName;
                }
            }
        }
        return "";
    }

    private void OnBuyComponent(ComponentData component)
    {
        Debug.Log($"[WorkshopUIManager] Attempting to buy component: {component.title} (cost: ${component.cost})");
        
        if (playerCash < component.cost)
        {
            ShowDebugMessage("Not enough cash!");
            return;
        }
        
        playerCash -= component.cost;

        // Always instantiate a new copy for all component types (including AI SOs)
        ComponentData newComp = Instantiate(component);
        
        // Generate clean instanceId without title prefix - just a GUID
        newComp.instanceId = System.Guid.NewGuid().ToString();

        // Handle AI components vs regular components
        if (newComp is AiTreeAsset aiTree)
        {
            // AI components ALWAYS go into playerInventory (both editor and build)
            playerInventory.Add(newComp);
            var entry = PlayerDataManager.Instance.playerData.ownedComponents.Find(e => e.id == component.id);
            if (entry == null)
            {
                entry = new OwnedComponentEntry { id = component.id, instanceIds = new List<string>() };
                PlayerDataManager.Instance.playerData.ownedComponents.Add(entry);
            }
            entry.instanceIds.Add(newComp.instanceId);
            Debug.Log($"[WorkshopUIManager] Added AI component to playerInventory: {newComp.title}");

#if UNITY_EDITOR
            // In editor: ALSO save a copy to AISaveFiles folder for AI Editor compatibility
            string saveFolderPath = null;
            if (aiTree.branchType == AiEditor.AiBranchType.Nav)
                saveFolderPath = "Assets/AiEditor/AISaveFiles/NavFiles/";
            else if (aiTree.branchType == AiEditor.AiBranchType.Turret)
                saveFolderPath = "Assets/AiEditor/AISaveFiles/TurretFiles/";
            
            if (saveFolderPath != null)
            {
                string assetPath = saveFolderPath + newComp.instanceId + ".asset";
                // Create a separate copy for AI Editor
                ComponentData editorCopy = Instantiate(newComp);
                UnityEditor.AssetDatabase.CreateAsset(editorCopy, assetPath);
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log($"[WorkshopUIManager] Also created AI Editor copy in AISaveFiles folder: {assetPath}");
            }
#endif
        }
        else
        {
            // Regular components go into player inventory and save data (both editor and build)
            playerInventory.Add(newComp);
            var entry = PlayerDataManager.Instance.playerData.ownedComponents.Find(e => e.id == component.id);
            if (entry == null)
            {
                entry = new OwnedComponentEntry { id = component.id, instanceIds = new List<string>() };
                PlayerDataManager.Instance.playerData.ownedComponents.Add(entry);
            }
            entry.instanceIds.Add(newComp.instanceId);
            Debug.Log($"[WorkshopUIManager] Added regular component to inventory: {newComp.title}");
        }

        PlayerDataManager.Instance.SavePlayerData();
        UpdatePlayerCashUI();
        PopulateComponentList();
        
        Debug.Log($"[WorkshopUIManager] Successfully purchased {newComp.title} for ${component.cost}");
    }

    private void OnSellComponent(ComponentData component)
    {
        Debug.Log($"[WorkshopUIManager] Attempting to sell component: {component.title} (instanceId: {component.instanceId})");
        
        // Unassign from any tank slot before selling
        foreach (var slot in tankSlots)
        {
            if (slot.HasComponent(component))
            {
                Debug.Log($"[WorkshopUIManager] Unassigning {component.title} from {slot.TankName}");
                slot.UnassignComponent(component);
            }
        }

        // Handle different component types
        if (component is AiTreeAsset aiTree)
        {
            // AI components are ALWAYS removed from playerInventory (both editor and build)
            if (playerInventory.Contains(component))
            {
                // Remove only the exact instance (by instanceId)
                var toRemove = playerInventory.Find(c => c.instanceId == component.instanceId);
                if (toRemove != null)
                    playerInventory.Remove(toRemove);
                
                // Remove from player save data (remove instanceId from OwnedComponentEntry)
                var entry = PlayerDataManager.Instance.playerData.ownedComponents.Find(e => e.id == component.id);
                if (entry != null)
                {
                    entry.instanceIds.Remove(component.instanceId);
                    if (entry.instanceIds.Count == 0)
                        PlayerDataManager.Instance.playerData.ownedComponents.Remove(entry);
                }
                
                Debug.Log($"[WorkshopUIManager] Sold AI component from playerInventory: {component.title}");
            }

#if UNITY_EDITOR
            // In editor: ALSO delete the AI Editor copy from AISaveFiles folder
            Debug.Log($"[WorkshopUIManager] Selling AI component: {aiTree.title} ({aiTree.branchType}) with instanceId: {component.instanceId}");
            
            string saveFolderPath = null;
            if (aiTree.branchType == AiEditor.AiBranchType.Nav)
                saveFolderPath = "Assets/AiEditor/AISaveFiles/NavFiles/";
            else if (aiTree.branchType == AiEditor.AiBranchType.Turret)
                saveFolderPath = "Assets/AiEditor/AISaveFiles/TurretFiles/";
            
            if (saveFolderPath != null)
            {
                string assetPath = saveFolderPath + component.instanceId + ".asset";
                Debug.Log($"[WorkshopUIManager] Attempting to delete AI Editor copy at: {assetPath}");
                
                var existingAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (existingAsset != null)
                {
                    bool deleted = UnityEditor.AssetDatabase.DeleteAsset(assetPath);
                    if (deleted)
                    {
                        UnityEditor.AssetDatabase.SaveAssets();
                        UnityEditor.AssetDatabase.Refresh();
                        Debug.Log($"[WorkshopUIManager] Successfully deleted AI Editor copy from AISaveFiles folder: {assetPath}");
                    }
                    else
                    {
                        Debug.LogError($"[WorkshopUIManager] Failed to delete AI Editor copy: {assetPath}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[WorkshopUIManager] AI Editor copy not found for deletion: {assetPath}");
                }
            }
#endif
            
            // Refund half the cost
            playerCash += component.cost / 2;
            Debug.Log($"[WorkshopUIManager] Refunded ${component.cost / 2} for {component.title}");
        }
        else
        {
            // Handle regular components (not AI)
            if (playerInventory.Contains(component))
            {
                // Remove only the exact instance (by instanceId)
                var toRemove = playerInventory.Find(c => c.instanceId == component.instanceId);
                if (toRemove != null)
                    playerInventory.Remove(toRemove);
                
                playerCash += component.cost / 2;
                
                // Remove from player save data (remove instanceId from OwnedComponentEntry)
                var entry = PlayerDataManager.Instance.playerData.ownedComponents.Find(e => e.id == component.id);
                if (entry != null)
                {
                    entry.instanceIds.Remove(component.instanceId);
                    if (entry.instanceIds.Count == 0)
                        PlayerDataManager.Instance.playerData.ownedComponents.Remove(entry);
                }
                
                Debug.Log($"[WorkshopUIManager] Sold regular component: {component.title}");
            }
        }

        PlayerDataManager.Instance.SavePlayerData();
        UpdatePlayerCashUI();
        PopulateComponentList();
        
        if (selectedTankSlot != null && modelPreview != null)
            modelPreview.ShowTank(GetEquippedComponentsForSlot(selectedTankSlot));
    }

    private void OnEquipComponent(ComponentData component)
    {
        Debug.Log($"[WorkshopUIManager] OnEquipComponent called for: {component.title} (instanceId: {component.instanceId})");
        
        // Find which tank slot (if any) this component is currently assigned to
        TankSlotButtonUI assignedSlot = null;
        foreach (var slot in tankSlots)
        {
            if (slot.HasComponent(component))
            {
                assignedSlot = slot;
                break;
            }
        }

        // If no tank slot is selected
        if (selectedTankSlot == null)
        {
            if (assignedSlot != null)
            {
                // Unassign from current slot
                Debug.Log($"[WorkshopUIManager] No slot selected - unassigning {component.title} from {assignedSlot.TankName}");
                assignedSlot.UnassignComponent(component);
                UpdateTankLoadoutSave(assignedSlot, component, remove: true);
                PlayerDataManager.Instance.SavePlayerData();
                PopulateComponentList();
            }
            else
            {
                ShowDebugMessage("No tank slot selected!");
            }
            return;
        }

        // If component is already assigned to the selected slot, unequip it
        if (assignedSlot == selectedTankSlot)
        {
            Debug.Log($"[WorkshopUIManager] Component {component.title} already assigned to {selectedTankSlot.TankName} - unequipping");
            selectedTankSlot.UnassignComponent(component);
            UpdateTankLoadoutSave(selectedTankSlot, component, remove: true);
            PlayerDataManager.Instance.SavePlayerData();
            PopulateComponentList();
            RefreshSelectedSlotUI();
            return;
        }

        // Check if the selected slot already has a conflicting component
        if (selectedTankSlot.HasConflictingComponent(component))
        {
            ShowDebugMessage($"Component type already assigned to {selectedTankSlot.TankName}!");
            return;
        }

        // If assigned to different slot, move to selected slot
        if (assignedSlot != null && assignedSlot != selectedTankSlot)
        {
            Debug.Log($"[WorkshopUIManager] Moving {component.title} from {assignedSlot.TankName} to {selectedTankSlot.TankName}");
            assignedSlot.UnassignComponent(component);
            UpdateTankLoadoutSave(assignedSlot, component, remove: true);
        }

        // Load the actual component for assignment (important for AI components)
        ComponentData assignComponent = component;
        if (component.category == ComponentCategory.AITree)
        {
            // For AI trees, they are now always in playerInventory (no need to load from disk)
            if (component is AiTreeAsset aiAsset)
            {
                // Use the component directly from playerInventory
                assignComponent = component;
                Debug.Log($"[WorkshopUIManager] Using AI component from playerInventory: {assignComponent.title} (instanceId: {assignComponent.instanceId})");
            }
        }

        // Assign to selected slot
        Debug.Log($"[WorkshopUIManager] Assigning {assignComponent.title} to {selectedTankSlot.TankName}");
        selectedTankSlot.AssignComponent(assignComponent);
        UpdateTankLoadoutSave(selectedTankSlot, assignComponent);
        PlayerDataManager.Instance.SavePlayerData();
        PopulateComponentList();
        RefreshSelectedSlotUI();
    }

    // Helper to update the save data for tank loadouts
    private void UpdateTankLoadoutSave(TankSlotButtonUI slot, ComponentData component, bool remove = false)
    {
        int slotIndex = tankSlots.IndexOf(slot);
        if (slotIndex < 0)
            return;
        // Ensure the list is large enough
        while (PlayerDataManager.Instance.playerData.tankLoadouts.Count <= slotIndex)
            PlayerDataManager.Instance.playerData.tankLoadouts.Add(new TankLoadoutSave());
        var loadout = PlayerDataManager.Instance.playerData.tankLoadouts[slotIndex];        if (remove)
        {
            // Remove the component from the loadout
            switch (component.category)
            {
                case ComponentCategory.EngineFrame:
                    loadout.engineFrameInstanceId = null;
                    break;
                case ComponentCategory.Armor:
                    loadout.armorInstanceId = null;
                    break;
                case ComponentCategory.Turret:
                    loadout.turretInstanceId = null;
                    break;
                case ComponentCategory.AITree:
                    if (component is AiTreeAsset aiAsset)
                    {
                        if (aiAsset.branchType == AiEditor.AiBranchType.Turret)
                            loadout.turretAIInstanceId = null;
                        else if (aiAsset.branchType == AiEditor.AiBranchType.Nav)
                            loadout.navAIInstanceId = null;
                    }
                    break;
            }
        }
        else
        {
            // Assign the component to the loadout
            switch (component.category)
            {
                case ComponentCategory.EngineFrame:
                    loadout.engineFrameInstanceId = component.instanceId;
                    break;
                case ComponentCategory.Armor:
                    loadout.armorInstanceId = component.instanceId;
                    break;
                case ComponentCategory.Turret:
                    loadout.turretInstanceId = component.instanceId;
                    break;
                case ComponentCategory.AITree:
                    if (component is AiTreeAsset aiAsset)
                    {
                        if (aiAsset.branchType == AiEditor.AiBranchType.Turret)
                            loadout.turretAIInstanceId = component.instanceId;
                        else if (aiAsset.branchType == AiEditor.AiBranchType.Nav)
                            loadout.navAIInstanceId = component.instanceId;
                    }
                    break;
            }
            
            loadout.tankName = slot.TankName;
        }
    }

    private void OnComponentSelected(ComponentData component)
    {
        selectedComponent = component;
        // Show model
        if (modelPreview != null)
            modelPreview.ShowModel(component);
        // Show stats
        if (statsPanel != null)
            statsPanel.ShowStats(component);
    }

    // Debug message system
    public void ShowDebugMessage(string message, float duration = 1.5f)
    {
        if (debugTextCoroutine != null)
            StopCoroutine(debugTextCoroutine);
        debugTextCoroutine = StartCoroutine(ShowDebugMessageRoutine(message, duration));
    }

    private IEnumerator ShowDebugMessageRoutine(string message, float duration)
    {
        debugText.text = message;
        debugText.color = Color.red;
        debugText.gameObject.SetActive(true);

        // Optional: flash effect
        float elapsed = 0f;
        while (elapsed < duration)
        {
            debugText.alpha = Mathf.PingPong(Time.time * 2f, 1f); // Flash
            elapsed += Time.deltaTime;
            yield return null;
        }
        debugText.gameObject.SetActive(false);
        debugText.text = "";
    }

    private void LoadTankSlotsFromScriptableObjects()
    {
        foreach (var slot in tankSlots)
        {
            if (slot.slotData != null)
            {
                slot.SetSelected(false); // Deselect by default
                slot.SetActive(slot.slotData.isActive);
                slot.UpdateAssignedComponentsFromSlotData();
            }
        }
    }

    // Make this method public so it can be accessed from TankSlotButtonUI
    public ComponentData FindComponentByPrefab(GameObject prefab, List<ComponentData> list)
    {
        foreach (var c in list)
            if (c.modelPrefab == prefab)
                return c;
        return null;
    }

    // Add this method to allow external refresh of the selected slot's preview and stats
    public void RefreshSelectedSlotUI()
    {
        if (selectedTankSlot != null)
        {
            // Show tank preview for selected slot
            if (modelPreview != null)
                modelPreview.ShowTank(GetEquippedComponentsForSlot(selectedTankSlot));

            // Sum weights of all equipped components and update ItemStats text, clear description
            float totalWeight = CalculateAndSaveTotalWeight(selectedTankSlot);
            itemStatsText.text = $"Total Weight: {totalWeight}";
            descriptionText.text = "";
        }
    }

    // Calculate total weight of equipped components and save it to TankSlotData
    public float CalculateAndSaveTotalWeight(TankSlotButtonUI slot)
    {
        float totalWeight = 0f;
        var equipped = GetEquippedComponentsForSlot(slot);
        foreach (var comp in equipped.Values)
        {
            if (comp != null)
                totalWeight += comp.weight;
        }
        
        // Save to TankSlotData
        if (slot.slotData != null)
        {
            slot.slotData.totalWeight = totalWeight;
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(slot.slotData);
            #endif
        }
        
        return totalWeight;
    }
    
    // Handle component color changes
    private void OnComponentColorChanged(ComponentData changedComponent)
    {
        // Update the color in TankSlotData if this component is assigned to a tank slot
        bool componentUpdated = false;
        foreach (var slot in tankSlots)
        {
            if (slot.HasComponent(changedComponent))
            {
                // Update the color in TankSlotData
                if (slot.slotData != null)
                {
                    switch (changedComponent.category)
                    {
                        case ComponentCategory.EngineFrame:
                            slot.slotData.engineFrameColor = changedComponent.customColor;
                            break;
                        case ComponentCategory.Armor:
                            slot.slotData.armorColor = changedComponent.customColor;
                            break;
                        case ComponentCategory.Turret:
                            slot.slotData.turretColor = changedComponent.customColor;
                            break;
                    }
                    
                    // Mark the ScriptableObject as dirty for saving
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(slot.slotData);
                    #endif
                    
                    componentUpdated = true;
                }
                break;
            }
        }
        
        // Update the preview based on current selection state
        if (selectedTankSlot != null && componentUpdated && modelPreview != null)
        {
            // If a tank slot is selected and this component is part of it, refresh the tank preview
            modelPreview.ShowTank(GetEquippedComponentsForSlot(selectedTankSlot));
        }
        else if (selectedComponent == changedComponent && modelPreview != null)
        {
            // If this component is currently selected for individual preview, refresh it
            modelPreview.ShowModel(changedComponent);
        }
    }
    
    private void LoadAIComponentsFromFolders()
    {
        // Clear existing AI shop components to reload fresh
        aiTreeShopComponents.Clear();
        
        // Both editor and build: Load AI assets from Shop Resources folders
        Debug.Log("[WorkshopUIManager] Loading AI assets from Resources/ShopAI folders...");
        
        // Load Nav AI assets for shop
        AiTreeAsset[] navAiAssets = Resources.LoadAll<AiTreeAsset>("ShopAI/NavAI");
        Debug.Log($"[WorkshopUIManager] Found {navAiAssets.Length} NavAI assets in Resources/ShopAI/NavAI");
        
        foreach (var aiTreeAsset in navAiAssets)
        {
            if (aiTreeAsset != null)
            {
                Debug.Log($"[WorkshopUIManager] Processing NavAI asset: {aiTreeAsset.title} (branch: {aiTreeAsset.branchType})");
                
                // Set branch type to Nav if not already set
                if (aiTreeAsset.branchType != AiEditor.AiBranchType.Nav)
                {
                    Debug.LogWarning($"[WorkshopUIManager] NavAI asset {aiTreeAsset.title} has wrong branch type: {aiTreeAsset.branchType}, setting to Nav");
                    aiTreeAsset.branchType = AiEditor.AiBranchType.Nav;
                }
                
                // Ensure the tree asset has proper category set
                if (aiTreeAsset.category != ComponentCategory.AITree)
                {
                    aiTreeAsset.category = ComponentCategory.AITree;
                }
                
                // Add to shop if not already present (check by title and branch type)
                if (!aiTreeShopComponents.Exists(c => c.title == aiTreeAsset.title && 
                    c is AiTreeAsset tree && tree.branchType == aiTreeAsset.branchType))
                {
                    aiTreeShopComponents.Add(aiTreeAsset);
                    Debug.Log($"[WorkshopUIManager] Added NavAI to shop: {aiTreeAsset.title} ({aiTreeAsset.branchType})");
                }
                else
                {
                    Debug.Log($"[WorkshopUIManager] NavAI already exists in shop: {aiTreeAsset.title}");
                }
            }
        }
        
        // Load Turret AI assets for shop
        AiTreeAsset[] turretAiAssets = Resources.LoadAll<AiTreeAsset>("ShopAI/TurretAI");
        Debug.Log($"[WorkshopUIManager] Found {turretAiAssets.Length} TurretAI assets in Resources/ShopAI/TurretAI");
        
        foreach (var aiTreeAsset in turretAiAssets)
        {
            if (aiTreeAsset != null)
            {
                Debug.Log($"[WorkshopUIManager] Processing TurretAI asset: {aiTreeAsset.title} (branch: {aiTreeAsset.branchType})");
                
                // Set branch type to Turret if not already set
                if (aiTreeAsset.branchType != AiEditor.AiBranchType.Turret)
                {
                    Debug.LogWarning($"[WorkshopUIManager] TurretAI asset {aiTreeAsset.title} has wrong branch type: {aiTreeAsset.branchType}, setting to Turret");
                    aiTreeAsset.branchType = AiEditor.AiBranchType.Turret;
                }
                
                // Ensure the tree asset has proper category set
                if (aiTreeAsset.category != ComponentCategory.AITree)
                {
                    aiTreeAsset.category = ComponentCategory.AITree;
                }
                
                // Add to shop if not already present (check by title and branch type)
                if (!aiTreeShopComponents.Exists(c => c.title == aiTreeAsset.title && 
                    c is AiTreeAsset tree && tree.branchType == aiTreeAsset.branchType))
                {
                    aiTreeShopComponents.Add(aiTreeAsset);
                    Debug.Log($"[WorkshopUIManager] Added TurretAI to shop: {aiTreeAsset.title} ({aiTreeAsset.branchType})");
                }
                else
                {
                    Debug.Log($"[WorkshopUIManager] TurretAI already exists in shop: {aiTreeAsset.title}");
                }
            }
        }
        
        Debug.Log($"[WorkshopUIManager] Loaded {aiTreeShopComponents.Count} AI components into shop from Resources/ShopAI folders");
        
        // Debug: Show what we actually loaded
        foreach (var ai in aiTreeShopComponents)
        {
            if (ai is AiTreeAsset tree)
            {
                Debug.Log($"[WorkshopUIManager] Shop AI: '{tree.title}' - Branch: {tree.branchType}, Category: {tree.category}");
            }
        }
    }
    
    private void LoadAiTreeAssetsFromPath(string path, bool addToShop)
    {
#if UNITY_EDITOR
        if (!System.IO.Directory.Exists(path))
        {
            Debug.LogWarning($"[WorkshopUIManager] AI path does not exist: {path}");
            return;
        }
            
        string[] files = System.IO.Directory.GetFiles(path, "*.asset");
        Debug.Log($"[WorkshopUIManager] Found {files.Length} .asset files in {path}");
        
        foreach (string filePath in files)
        {
            var aiTreeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiTreeAsset>(filePath);
            if (aiTreeAsset != null)
            {
                // Ensure the tree asset has proper category set
                if (aiTreeAsset.category != ComponentCategory.AITree)
                {
                    aiTreeAsset.category = ComponentCategory.AITree;
                    UnityEditor.EditorUtility.SetDirty(aiTreeAsset);
                }
                
                if (addToShop)
                {
                    // Add to shop if not already present (check by title and branch type)
                    if (!aiTreeShopComponents.Exists(c => c.title == aiTreeAsset.title && 
                        c is AiTreeAsset tree && tree.branchType == aiTreeAsset.branchType))
                    {
                        aiTreeShopComponents.Add(aiTreeAsset);
                        Debug.Log($"[WorkshopUIManager] Added {aiTreeAsset.title} ({aiTreeAsset.branchType}) to shop");
                    }
                }
            }
        }
#endif
    }

    private List<ComponentData> LoadAITreeInventoryFromFolders()
    {
        var inventoryItems = new List<ComponentData>();
        
#if UNITY_EDITOR
        // In editor: Load AI inventory from AISaveFiles folders
        Debug.Log("[WorkshopUIManager] Editor mode: Loading AI inventory from Assets/AiEditor/AISaveFiles/NavFiles and TurretFiles folders");
        
        string navFilesPath = "Assets/AiEditor/AISaveFiles/NavFiles/";
        string turretFilesPath = "Assets/AiEditor/AISaveFiles/TurretFiles/";
        
        // Load Nav AI from AISaveFiles/NavFiles
        string[] navGuids = AssetDatabase.FindAssets("t:AiTreeAsset", new[] { navFilesPath });
        foreach (string guid in navGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            AiTreeAsset aiTreeAsset = AssetDatabase.LoadAssetAtPath<AiTreeAsset>(assetPath);
            if (aiTreeAsset != null && aiTreeAsset.branchType == AiEditor.AiBranchType.Nav)
            {
                inventoryItems.Add(aiTreeAsset);
                Debug.Log($"[WorkshopUIManager] Added NavAI to inventory: {aiTreeAsset.title} (instanceId: {aiTreeAsset.instanceId})");
            }
        }
        
        // Load Turret AI from AISaveFiles/TurretFiles
        string[] turretGuids = AssetDatabase.FindAssets("t:AiTreeAsset", new[] { turretFilesPath });
        foreach (string guid in turretGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            AiTreeAsset aiTreeAsset = AssetDatabase.LoadAssetAtPath<AiTreeAsset>(assetPath);
            if (aiTreeAsset != null && aiTreeAsset.branchType == AiEditor.AiBranchType.Turret)
            {
                inventoryItems.Add(aiTreeAsset);
                Debug.Log($"[WorkshopUIManager] Added TurretAI to inventory: {aiTreeAsset.title} (instanceId: {aiTreeAsset.instanceId})");
            }
        }
#else
        // In build: AI inventory is managed through playerInventory list (no file system access)
        Debug.Log("[WorkshopUIManager] Build mode: Loading AI inventory from playerInventory list");
        
        foreach (var component in playerInventory)
        {
            if (component is AiTreeAsset aiTreeAsset)
            {
                inventoryItems.Add(aiTreeAsset);
                Debug.Log($"[WorkshopUIManager] Added AI to inventory: {aiTreeAsset.title} (branch: {aiTreeAsset.branchType}, instanceId: {aiTreeAsset.instanceId})");
            }
        }
#endif
        
        Debug.Log($"[WorkshopUIManager] Loaded {inventoryItems.Count} AI components into inventory");
        return inventoryItems;
    }

    /// <summary>
    /// Loads an AI Tree Asset from disk based on instanceId and branch type
    /// </summary>
    private AiEditor.AiTreeAsset LoadAITreeAssetFromDisk(string instanceId, AiEditor.AiBranchType branchType)
    {
#if UNITY_EDITOR
        // In editor: Load from AISaveFiles folders (where purchased/created AI is stored)
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
                    Debug.Log($"[WorkshopUIManager] Found AI asset in AISaveFiles: {asset.title} (instanceId: {instanceId})");
                    return asset;
                }
            }
        }
        
        // If not found in specific folder, also check main AISaveFiles folder for legacy assets
        string mainFolderPath = "Assets/AiEditor/AISaveFiles/";
        if (System.IO.Directory.Exists(mainFolderPath))
        {
            string[] files = System.IO.Directory.GetFiles(mainFolderPath, "*.asset");
            foreach (string filePath in files)
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiEditor.AiTreeAsset>(filePath);
                if (asset != null && asset.instanceId == instanceId && asset.branchType == branchType)
                {
                    Debug.Log($"[WorkshopUIManager] Found AI asset in main AISaveFiles: {asset.title} (instanceId: {instanceId})");
                    return asset;
                }
            }
        }
#else
        // In build: AI assets should be accessed via playerInventory, not from disk
        Debug.LogWarning($"[WorkshopUIManager] LoadAITreeAssetFromDisk called in build mode for instanceId: {instanceId}");
#endif
        
        Debug.LogWarning($"[WorkshopUIManager] Could not find AI asset with instanceId: {instanceId} and branch type: {branchType}");
        return null;
    }
    
    /// <summary>
    /// Cleans up legacy AI files that have old instanceId format (with title prefix)
    /// and renames them to use clean GUID instanceIds
    /// </summary>
    private void CleanupLegacyAIFiles()
    {
#if UNITY_EDITOR
        string[] folders = { 
            "Assets/AiEditor/AISaveFiles/NavFiles", 
            "Assets/AiEditor/AISaveFiles/TurretFiles" 
        };
        
        foreach (string folder in folders)
        {
            if (!System.IO.Directory.Exists(folder)) continue;
            
            string[] files = System.IO.Directory.GetFiles(folder, "*.asset");
            foreach (string filePath in files)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                
                // Check if this file has the old instanceId format (contains underscore and title prefix)
                if (fileName.Contains("_") && !System.Guid.TryParse(fileName, out _))
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiTreeAsset>(filePath);
                    if (asset != null)
                    {
                        // Generate new clean instanceId
                        string newInstanceId = System.Guid.NewGuid().ToString();
                        string newFilePath = folder + "/" + newInstanceId + ".asset";
                        
                        Debug.Log($"[WorkshopUIManager] Cleaning up legacy AI file: {fileName} -> {newInstanceId}");
                        
                        // Update the asset's instanceId
                        asset.instanceId = newInstanceId;
                        
                        // Move the asset to the new filename
                        string moveResult = UnityEditor.AssetDatabase.MoveAsset(filePath, newFilePath);
                        if (string.IsNullOrEmpty(moveResult))
                        {
                            Debug.Log($"[WorkshopUIManager] Successfully renamed AI file to: {newInstanceId}.asset");
                            
                            // Update any tank slot data that references the old instanceId
                            UpdateTankSlotReferences(fileName, newInstanceId, asset.branchType);
                        }
                        else
                        {
                            Debug.LogError($"[WorkshopUIManager] Failed to rename AI file: {moveResult}");
                        }
                    }
                }
            }
        }
        
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
    
    /// <summary>
    /// Updates tank slot data references from old instanceId to new instanceId
    /// </summary>
    private void UpdateTankSlotReferences(string oldInstanceId, string newInstanceId, AiEditor.AiBranchType branchType)
    {
        bool dataChanged = false;
        
        foreach (var slot in tankSlots)
        {
            if (slot.slotData != null)
            {
                if (branchType == AiEditor.AiBranchType.Turret && slot.slotData.turretAIInstanceId == oldInstanceId)
                {
                    slot.slotData.turretAIInstanceId = newInstanceId;
                    dataChanged = true;
                    Debug.Log($"[WorkshopUIManager] Updated {slot.slotData.name} turretAI reference: {oldInstanceId} -> {newInstanceId}");
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(slot.slotData);
#endif
                }
                else if (branchType == AiEditor.AiBranchType.Nav && slot.slotData.navAIInstanceId == oldInstanceId)
                {
                    slot.slotData.navAIInstanceId = newInstanceId;
                    dataChanged = true;
                    Debug.Log($"[WorkshopUIManager] Updated {slot.slotData.name} navAI reference: {oldInstanceId} -> {newInstanceId}");
#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(slot.slotData);
#endif
                }
            }
        }
        
        if (dataChanged)
        {
            PlayerDataManager.Instance.SavePlayerData();
        }
    }
}