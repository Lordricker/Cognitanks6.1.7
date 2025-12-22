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
    
    // JSON-based tank slot management
    private TankSlotJsonManager tankSlotJsonManager;
    
    public WorkshopModelPreview modelPreview;
    public WorkshopStatsPanel statsPanel;
    
    [Header("Tip Bubbles")]
    public List<GameObject> tipBubbles = new List<GameObject>(); // Assign all tip bubble GameObjects in inspector
    private bool tipsVisible = false;
    private const string TIPS_VISIBLE_KEY = "WorkshopTipsVisible";
    
    [Header("Debug UI")]
    public TMP_Text debugText; // Assign in inspector

    private Coroutine debugTextCoroutine;
    private ComponentData selectedComponent;

    public TMP_Text itemStatsText;
    public TMP_Text descriptionText;

    private void Start()
    {
        // Initialize JSON tank slot manager
        tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager == null)
        {
            GameObject managerGO = new GameObject("TankSlotJsonManager");
            tankSlotJsonManager = managerGO.AddComponent<TankSlotJsonManager>();
        }
        
        // Load tip visibility state from PlayerPrefs
        tipsVisible = PlayerPrefs.GetInt(TIPS_VISIBLE_KEY, 0) == 1;
        UpdateTipBubbleVisibility();
        
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
        
        // Load AI components from AI Editor folders FIRST (before loading player inventory)
        LoadAIComponentsFromFolders();
        
        LoadPlayerInventoryFromSave();
        
        // Clean up any legacy AI files with old instanceId format
        CleanupLegacyAIFiles();
        
        // Tank slots are now managed entirely via JSON - no need for ScriptableObject restoration
        
        // Load tank slots from ScriptableObjects AFTER restoring activation states
        LoadTankSlotsFromScriptableObjects();
    }
    
    private void LoadPlayerInventoryFromSave()
    {
        playerInventory.Clear();
        
        // Load regular components from PlayerData save file
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
        
        // Load AI components from JSON files on disk
        LoadAIInventoryFromDisk();
        
        // Add default components for new players if inventory is empty
        if (playerInventory.Count == 0)
        {
            AddDefaultComponentsToInventory();
        }
    }
    
    private void AddDefaultComponentsToInventory()
    {
        // Add default AITree components that tank slots expect - create JSON files on disk
        var defaultTurretAI = aiTreeShopComponents.Find(c => c.id == "Aggressive Hunter" && 
            c is AiTreeAsset tree && tree.branchType == AiEditor.AiBranchType.Turret);
        if (defaultTurretAI != null)
        {
            // Add TurretAI for Tank Slot 0
            ComponentData turretAI1 = Instantiate(defaultTurretAI);
            turretAI1.instanceId = "Aggressive Hunter_ce9255c7-8383-4919-a8ba-d1686373d471";
            
            // Add TurretAI for Tank Slot 9
            ComponentData turretAI2 = Instantiate(defaultTurretAI);
            turretAI2.instanceId = "Aggressive Hunter_c082be34-e8c2-4937-8aaf-e9f11fced160";
            
            // Create JSON files on disk for these default AI components
            if (defaultTurretAI.name.EndsWith("_JSON"))
            {
                CreateJsonAiFileFromPurchase(turretAI1 as AiTreeAsset);
                CreateJsonAiFileFromPurchase(turretAI2 as AiTreeAsset);
            }
            
            // Add to playerInventory for immediate UI display
            playerInventory.Add(turretAI1);
            playerInventory.Add(turretAI2);
            
            // Note: No need to add to PlayerData save since AI components are stored as JSON files
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
                    break;
                case ComponentCategory.NavAI:
                    // Filter AITree components for Nav branch type  
                    source = aiTreeShopComponents.FindAll(c => 
                        c is AiTreeAsset tree && tree.branchType == AiEditor.AiBranchType.Nav);
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
            }
            else if (currentCategory == ComponentCategory.NavAI)
            {
                // Show Nav AI trees from playerInventory
                source = playerInventory.FindAll(c => c is AiTreeAsset tree && tree.branchType == AiEditor.AiBranchType.Nav);
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
                // Restore color from JSON data to component for visual consistency
                var slotData = TankSlotJsonManager.Instance.GetTankSlot(slot.slotIndex);
                if (slotData != null)
                {
                    switch (comp.category)
                    {
                        case ComponentCategory.EngineFrame:
                            comp.customColor = slotData.engineFrameColor.ToUnityColor();
                            break;
                        case ComponentCategory.Armor:
                            comp.customColor = slotData.armorColor.ToUnityColor();
                            break;
                        case ComponentCategory.Turret:
                            comp.customColor = slotData.turretColor.ToUnityColor();
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
            var slotData = TankSlotJsonManager.Instance.GetTankSlot(slot.slotIndex);
            if (slotData != null)
            {
                // Check regular components by prefab and instanceId
                if ((component.category == ComponentCategory.EngineFrame && 
                     slotData.engineFrameInstanceId == component.instanceId) ||
                    (component.category == ComponentCategory.Armor && 
                     slotData.armorInstanceId == component.instanceId) ||
                    (component.category == ComponentCategory.Turret && 
                     slotData.turretInstanceId == component.instanceId))
                {
                    return slot.TankName;
                }
                
                // Check AI components by instanceId only (since they're stored on disk)
                if (component.category == ComponentCategory.AITree && component is AiTreeAsset aiAsset)
                {
                    if ((aiAsset.branchType == AiEditor.AiBranchType.Turret && 
                         slotData.turretAIInstanceId == component.instanceId) ||
                        (aiAsset.branchType == AiEditor.AiBranchType.Nav && 
                         slotData.navAIInstanceId == component.instanceId))
                    {
                        return slot.TankName;
                    }
                }
                
                // Legacy AI category support
                if ((component.category == ComponentCategory.TurretAI && 
                     slotData.turretAIInstanceId == component.instanceId) ||
                    (component.category == ComponentCategory.NavAI && 
                     slotData.navAIInstanceId == component.instanceId))
                {
                    return slot.TankName;
                }
            }
        }
        return "";
    }

    private void OnBuyComponent(ComponentData component)
    {
        if (playerCash < component.cost)
        {
            ShowDebugMessage("Not enough cash!");
            return;
        }
        
        playerCash -= component.cost;

        // Always instantiate a new copy for all component types (including AI SOs)
        ComponentData newComp = Instantiate(component);
        
        // Generate instanceId with component name prefix for proper loading
        newComp.instanceId = $"{component.title}_{System.Guid.NewGuid().ToString()}";

        // Handle AI components vs regular components
        if (newComp is AiTreeAsset aiTree)
        {
            // For JSON-based AI components: Create actual JSON file on disk instead of saving to PlayerData
            if (component.name.EndsWith("_JSON"))
            {
                CreateJsonAiFileFromPurchase(aiTree);
            }
            
            // Add to playerInventory for immediate UI display
            playerInventory.Add(newComp);
            
            // Note: JSON AI components are NOT saved to PlayerData - they exist as files on disk
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
        }

        PlayerDataManager.Instance.SavePlayerData();
        UpdatePlayerCashUI();
        PopulateComponentList();
    }

    private void OnSellComponent(ComponentData component)
    {
        // Unassign from any tank slot before selling
        foreach (var slot in tankSlots)
        {
            if (slot.HasComponent(component))
            {
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
            }

            // For JSON-based AI components: Delete the JSON file from disk
            if (component.name.EndsWith("_JSON"))
            {
                DeleteJsonAiFile(aiTree);
            }
            else
            {
#if UNITY_EDITOR
                // For ScriptableObject-based AI: delete from AISaveFiles folder (legacy support)
                string saveFolderPath = aiTree.branchType == AiEditor.AiBranchType.Nav
                    ? "Assets/AiEditor/AISaveFiles/NavFiles/"
                    : "Assets/AiEditor/AISaveFiles/TurretFiles/";
                
                if (saveFolderPath != null)
                {
                    string assetPath = saveFolderPath + component.instanceId + ".asset";
                    
                    var existingAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (existingAsset != null)
                    {
                        bool deleted = UnityEditor.AssetDatabase.DeleteAsset(assetPath);
                        if (deleted)
                        {
                            UnityEditor.AssetDatabase.SaveAssets();
                            UnityEditor.AssetDatabase.Refresh();
                        }
                    }
                }
#endif
            }
            
            // Refund half the cost
            playerCash += component.cost / 2;
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
                assignedSlot.UnassignComponent(component);
                UpdateTankLoadoutSave(assignedSlot, component, remove: true);
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
            selectedTankSlot.UnassignComponent(component);
            UpdateTankLoadoutSave(selectedTankSlot, component, remove: true);
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
            }
        }

        // Assign to selected slot
        selectedTankSlot.AssignComponent(assignComponent);
        UpdateTankLoadoutSave(selectedTankSlot, assignComponent);
        PopulateComponentList();
        RefreshSelectedSlotUI();
    }

    // Helper to update the save data for tank loadouts using JSON system
    private void UpdateTankLoadoutSave(TankSlotButtonUI slot, ComponentData component, bool remove = false)
    {
        int slotIndex = tankSlots.IndexOf(slot);
        if (slotIndex < 0 || tankSlotJsonManager == null)
            return;

        // Get the current tank slot data from JSON
        TankSlotDataJson slotData = tankSlotJsonManager.GetTankSlot(slotIndex);
        if (slotData == null)
        {
            // Create new slot if it doesn't exist
            slotData = new TankSlotDataJson
            {
                slotIndex = slotIndex,
                isActive = false,
                teamId = 0,
                isPlayerControlled = true,
                displayName = slot.TankName
            };
        }

        if (remove)
        {
            // Remove the component from the slot and clear its stats
            switch (component.category)
            {
                case ComponentCategory.EngineFrame:
                    slotData.engineFrameInstanceId = "";
                    slotData.enginePower = 0;
                    slotData.engineWeightCapacity = 0;
                    slotData.engineFrameHP = 0;
                    break;
                case ComponentCategory.Armor:
                    slotData.armorInstanceId = "";
                    slotData.armorHP = 0;
                    break;
                case ComponentCategory.Turret:
                    slotData.turretInstanceId = "";
                    slotData.turretType = TurretTypeJson.DirectFire;
                    slotData.turretDamage = 0;
                    slotData.turretRange = 0;
                    slotData.turretShotsPerSec = 0;
                    slotData.turretBulletSpeed = 0;
                    slotData.turretKnockback = "";
                    slotData.turretVisionRange = 0;
                    slotData.turretVisionCone = 0;
                    break;
                case ComponentCategory.AITree:
                    if (component is AiTreeAsset aiAsset)
                    {
                        if (aiAsset.branchType == AiEditor.AiBranchType.Turret)
                            slotData.turretAIInstanceId = "";
                        else if (aiAsset.branchType == AiEditor.AiBranchType.Nav)
                            slotData.navAIInstanceId = "";
                    }
                    break;
            }
            
            // Recalculate total weight after component removal
            slotData.totalWeight = CalculateTotalWeight(slotData);
        }
        else
        {
            // Assign the component to the slot
            switch (component.category)
            {
                case ComponentCategory.EngineFrame:
                    slotData.engineFrameInstanceId = component.instanceId;
                    // Copy engine stats
                    if (component is EngineFrameData engineData)
                    {
                        slotData.enginePower = engineData.enginePower;
                        slotData.engineTorque = engineData.turningPower;
                        slotData.engineWeightCapacity = engineData.weightCapacity;
                        slotData.engineFrameHP = 0; // EngineFrameData doesn't have HP property
                    }
                    break;
                case ComponentCategory.Armor:
                    slotData.armorInstanceId = component.instanceId;
                    // Copy armor stats
                    if (component is ArmorData armorData)
                    {
                        slotData.armorHP = armorData.HP;
                    }
                    break;
                case ComponentCategory.Turret:
                    slotData.turretInstanceId = component.instanceId;
                    // Copy turret stats
                    if (component is TurretData turretData)
                    {
                        slotData.turretType = (TurretTypeJson)turretData.turretType;
                        slotData.turretDamage = turretData.damage;
                        slotData.turretRange = turretData.range;
                        slotData.turretShotsPerSec = turretData.shotspersec;
                        slotData.turretBulletSpeed = turretData.bulletSpeed;
                        slotData.turretKnockback = turretData.knockback;
                        slotData.turretVisionRange = turretData.visionRange;
                        slotData.turretVisionCone = turretData.visionCone;
                    }
                    break;
                case ComponentCategory.AITree:
                    if (component is AiTreeAsset aiAsset)
                    {
                        if (aiAsset.branchType == AiEditor.AiBranchType.Turret)
                            slotData.turretAIInstanceId = component.instanceId;
                        else if (aiAsset.branchType == AiEditor.AiBranchType.Nav)
                            slotData.navAIInstanceId = component.instanceId;
                    }
                    break;
            }
            
            // Recalculate total weight after component assignment
            slotData.totalWeight = CalculateTotalWeight(slotData);
            
            slotData.displayName = slot.TankName;
        }

        // Save the updated slot data to JSON
        tankSlotJsonManager.UpdateTankSlot(slotIndex, slotData);
        
        Debug.Log($"[WorkshopUIManager] Updated tank slot {slotIndex} JSON with component {component.title}");
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
            var slotData = TankSlotJsonManager.Instance.GetTankSlot(slot.slotIndex);
            if (slotData != null)
            {
                slot.SetSelected(false); // Deselect by default
                slot.SetActive(slotData.isActive);
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
        
        // Save to JSON data
        var slotData = TankSlotJsonManager.Instance.GetTankSlot(slot.slotIndex);
        if (slotData != null)
        {
            slotData.totalWeight = totalWeight;
            TankSlotJsonManager.Instance.UpdateTankSlot(slot.slotIndex, slotData);
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
                // Update the color in JSON data
                var slotData = TankSlotJsonManager.Instance.GetTankSlot(slot.slotIndex);
                if (slotData != null)
                {
                    switch (changedComponent.category)
                    {
                        case ComponentCategory.EngineFrame:
                            slotData.engineFrameColor = new ColorJson(changedComponent.customColor);
                            break;
                        case ComponentCategory.Armor:
                            slotData.armorColor = new ColorJson(changedComponent.customColor);
                            break;
                        case ComponentCategory.Turret:
                            slotData.turretColor = new ColorJson(changedComponent.customColor);
                            break;
                    }
                    
                    // Save the updated JSON data
                    TankSlotJsonManager.Instance.UpdateTankSlot(slot.slotIndex, slotData);
                    
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
        
        // Only load JSON-based AI components for shop (no ScriptableObjects)
        
        // Load Nav AI JSON files only
        TextAsset[] navJsonFiles = Resources.LoadAll<TextAsset>("ShopAI/NavAI");
        
        // Process all TextAsset files for Nav AI (attempt to parse as JSON)
        foreach (var jsonFile in navJsonFiles)
        {
            if (jsonFile != null)
            {
                try
                {
                    AiTreeAssetJson jsonAi = JsonUtility.FromJson<AiTreeAssetJson>(jsonFile.text);
                    if (jsonAi != null && !string.IsNullOrEmpty(jsonAi.title))
                    {
                        jsonAi.branchType = AiBranchTypeJson.Nav;
                        jsonAi.category = ComponentCategoryJson.AITree;
                        
                        // Create a proxy ComponentData for compatibility with existing UI
                        ComponentData proxyComponent = CreateAiTreeProxyFromJson(jsonAi);
                        
                        if (!aiTreeShopComponents.Exists(c => c.title == jsonAi.title))
                        {
                            aiTreeShopComponents.Add(proxyComponent);
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Skip invalid JSON files silently
                }
            }
        }
        
        // Load Turret AI JSON files only
        TextAsset[] turretJsonFiles = Resources.LoadAll<TextAsset>("ShopAI/TurretAI");
        
        // Process all TextAsset files for Turret AI (attempt to parse as JSON)
        foreach (var jsonFile in turretJsonFiles)
        {
            if (jsonFile != null)
            {
                try
                {
                    AiTreeAssetJson jsonAi = JsonUtility.FromJson<AiTreeAssetJson>(jsonFile.text);
                    if (jsonAi != null && !string.IsNullOrEmpty(jsonAi.title))
                    {
                        jsonAi.branchType = AiBranchTypeJson.Turret;
                        jsonAi.category = ComponentCategoryJson.AITree;
                        
                        // Create a proxy ComponentData for compatibility with existing UI
                        ComponentData proxyComponent = CreateAiTreeProxyFromJson(jsonAi);
                        
                        if (!aiTreeShopComponents.Exists(c => c.title == jsonAi.title))
                        {
                            aiTreeShopComponents.Add(proxyComponent);
                        }
                    }
                }
                catch (System.Exception)
                {
                    // Skip invalid JSON files silently
                }
            }
        }
    }
    


    private List<ComponentData> LoadAITreeInventoryFromFolders()
    {
        var inventoryItems = new List<ComponentData>();
        
        // Load AI inventory from persistent data path (AppData/AiTrees)
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
                    var aiTreeAsset = ScriptableObject.CreateInstance<AiTreeAsset>();
                    JsonUtility.FromJsonOverwrite(jsonContent, aiTreeAsset);
                    
                    if (aiTreeAsset != null)
                    {
                        // Ensure the instanceId is set (use filename if missing)
                        if (string.IsNullOrEmpty(aiTreeAsset.instanceId))
                        {
                            aiTreeAsset.instanceId = System.IO.Path.GetFileNameWithoutExtension(filePath);
                        }
                        
                        inventoryItems.Add(aiTreeAsset);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[LoadAITreeInventoryFromFolders] Could not load AI file {filePath}: {ex.Message}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[LoadAITreeInventoryFromFolders] AI trees folder does not exist: {aiTreesPath}");
        }
        
        return inventoryItems;
    }

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
                    Debug.LogWarning($"[LoadAITreeAssetFromDisk] Could not load AI file {filePath}: {ex.Message}");
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
                            // Update the asset's instanceId to match what we're looking for
                            aiTreeAsset.instanceId = instanceId;
                            return aiTreeAsset;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[LoadAITreeAssetFromDisk] Could not process AI file {filePath} during fallback: {ex.Message}");
                }
            }
        }
        
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
                        
                        // Update the asset's instanceId
                        asset.instanceId = newInstanceId;
                        
                        // Move the asset to the new filename
                        string moveResult = UnityEditor.AssetDatabase.MoveAsset(filePath, newFilePath);
                        if (string.IsNullOrEmpty(moveResult))
                        {
                            // Update any tank slot data that references the old instanceId
                            UpdateTankSlotReferences(fileName, newInstanceId, asset.branchType);
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
            var slotData = TankSlotJsonManager.Instance.GetTankSlot(slot.slotIndex);
            if (slotData != null)
            {
                if (branchType == AiEditor.AiBranchType.Turret && slotData.turretAIInstanceId == oldInstanceId)
                {
                    slotData.turretAIInstanceId = newInstanceId;
                    TankSlotJsonManager.Instance.UpdateTankSlot(slot.slotIndex, slotData);
                    dataChanged = true;
                }
                else if (branchType == AiEditor.AiBranchType.Nav && slotData.navAIInstanceId == oldInstanceId)
                {
                    slotData.navAIInstanceId = newInstanceId;
                    TankSlotJsonManager.Instance.UpdateTankSlot(slot.slotIndex, slotData);
                    dataChanged = true;
                }
            }
        }
        
        if (dataChanged)
        {
            PlayerDataManager.Instance.SavePlayerData();
        }
    }

    /// <summary>
    /// Creates a proxy ComponentData from AiTreeAssetJson for compatibility with existing UI
    /// </summary>
    private ComponentData CreateAiTreeProxyFromJson(AiTreeAssetJson jsonAi)
    {
        // Create a temporary ScriptableObject instance for UI compatibility
        var proxy = ScriptableObject.CreateInstance<AiTreeAsset>();
        
        // Copy properties from JSON to proxy
        proxy.title = jsonAi.title;
        proxy.description = jsonAi.description;
        proxy.cost = jsonAi.cost;
        proxy.weight = jsonAi.weight;
        proxy.category = (ComponentCategory)jsonAi.category;
        proxy.instanceId = jsonAi.instanceId;
        proxy.customColor = jsonAi.customColor;
        proxy.branchType = (AiEditor.AiBranchType)jsonAi.branchType;
        
        // Note: The id property automatically returns the title, so no need to set it explicitly
        
        // Copy nodes and connections
        proxy.nodes = new List<AiEditor.AiNodeData>();
        foreach (var jsonNode in jsonAi.nodes)
        {
            var node = new AiEditor.AiNodeData();
            node.nodeId = jsonNode.nodeId;
            node.nodeType = jsonNode.nodeType;
            node.nodeLabel = jsonNode.nodeLabel;
            node.position = jsonNode.position;
            
            // Convert properties list to dictionary
            node.properties = new Dictionary<string, string>();
            foreach (var prop in jsonNode.properties)
            {
                node.properties[prop.key] = prop.value;
            }
            
            proxy.nodes.Add(node);
        }
        
        proxy.connections = new List<AiEditor.AiConnectionData>();
        foreach (var jsonConnection in jsonAi.connections)
        {
            var connection = new AiEditor.AiConnectionData();
            connection.fromNodeId = jsonConnection.fromNodeId;
            connection.fromPortId = jsonConnection.fromPortId;
            connection.toNodeId = jsonConnection.toNodeId;
            connection.toPortId = jsonConnection.toPortId;
            proxy.connections.Add(connection);
        }
        
        proxy.executableNodes = new List<AiEditor.AiExecutableNode>();
        foreach (var jsonExecNode in jsonAi.executableNodes)
        {
            var execNode = new AiEditor.AiExecutableNode();
            execNode.nodeId = jsonExecNode.nodeId;
            execNode.methodName = jsonExecNode.methodName;
            execNode.originalLabel = jsonExecNode.originalLabel;
            execNode.nodeType = (AiEditor.AiNodeType)jsonExecNode.nodeType;
            execNode.numericValue = jsonExecNode.numericValue;
            execNode.connectedNodeIds = new List<string>(jsonExecNode.connectedNodeIds);
            execNode.position = jsonExecNode.position;
            proxy.executableNodes.Add(execNode);
        }
        
        proxy.startNodeId = jsonAi.startNodeId;
        
        // Set title and TreeName property instead of the private treeName field
        proxy.title = jsonAi.title;
        proxy.TreeName = jsonAi.TreeName;
        
        // Mark it as a JSON-sourced component for identification
        proxy.name = $"{jsonAi.title}_JSON";
        
        return proxy;
    }
    
    /// <summary>
    /// Creates a JSON file on disk when purchasing a JSON-based AI component
    /// </summary>
    private void CreateJsonAiFileFromPurchase(AiTreeAsset aiTree)
    {
        // Convert the purchased AI back to JSON format
        var jsonAi = ConvertAiTreeToJson(aiTree);
        string jsonContent = JsonUtility.ToJson(jsonAi, true);
        
        // Determine the target folder in persistent data path based on branch type
        string folderName = aiTree.branchType == AiEditor.AiBranchType.Nav ? "NavFiles" : "TurretFiles";
        string targetFolder = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folderName);
            
        // Ensure the folder exists
        if (!System.IO.Directory.Exists(targetFolder))
        {
            System.IO.Directory.CreateDirectory(targetFolder);
        }
        
        // Create the JSON file with the component's instanceId as filename
        string filePath = System.IO.Path.Combine(targetFolder, $"{aiTree.instanceId}.json");
        
        try
        {
            System.IO.File.WriteAllText(filePath, jsonContent);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to create JSON AI file: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Converts an AiTreeAsset back to AiTreeAssetJson format
    /// </summary>
    private AiTreeAssetJson ConvertAiTreeToJson(AiTreeAsset aiTree)
    {
        var jsonAi = new AiTreeAssetJson();
        
        // Copy basic properties
        jsonAi.title = aiTree.title;
        jsonAi.description = aiTree.description;
        jsonAi.cost = aiTree.cost;
        jsonAi.weight = aiTree.weight;
        jsonAi.category = (ComponentCategoryJson)aiTree.category;
        jsonAi.instanceId = aiTree.instanceId;
        jsonAi.customColor = aiTree.customColor;
        jsonAi.branchType = (AiBranchTypeJson)aiTree.branchType;
        jsonAi.startNodeId = aiTree.startNodeId;
        jsonAi.TreeName = aiTree.TreeName;
        
        // Convert nodes
        jsonAi.nodes = new List<AiNodeDataJson>();
        foreach (var node in aiTree.nodes)
        {
            var jsonNode = new AiNodeDataJson();
            jsonNode.nodeId = node.nodeId;
            jsonNode.nodeType = node.nodeType;
            jsonNode.nodeLabel = node.nodeLabel;
            jsonNode.position = node.position;
            
            // Convert properties dictionary to list
            jsonNode.properties = new List<NodePropertyJson>();
            foreach (var prop in node.properties)
            {
                jsonNode.properties.Add(new NodePropertyJson(prop.Key, prop.Value));
            }
            
            jsonAi.nodes.Add(jsonNode);
        }
        
        // Convert connections
        jsonAi.connections = new List<AiConnectionDataJson>();
        foreach (var connection in aiTree.connections)
        {
            var jsonConnection = new AiConnectionDataJson();
            jsonConnection.fromNodeId = connection.fromNodeId;
            jsonConnection.fromPortId = connection.fromPortId;
            jsonConnection.toNodeId = connection.toNodeId;
            jsonConnection.toPortId = connection.toPortId;
            jsonAi.connections.Add(jsonConnection);
        }
        
        // Convert executable nodes
        jsonAi.executableNodes = new List<AiExecutableNodeJson>();
        foreach (var execNode in aiTree.executableNodes)
        {
            var jsonExecNode = new AiExecutableNodeJson();
            jsonExecNode.nodeId = execNode.nodeId;
            jsonExecNode.methodName = execNode.methodName;
            jsonExecNode.originalLabel = execNode.originalLabel;
            jsonExecNode.nodeType = (AiNodeTypeJson)execNode.nodeType;
            jsonExecNode.numericValue = execNode.numericValue;
            jsonExecNode.connectedNodeIds = new List<string>(execNode.connectedNodeIds);
            jsonExecNode.position = execNode.position;
            jsonAi.executableNodes.Add(jsonExecNode);
        }
        
        return jsonAi;
    }

    /// <summary>
    /// Loads purchased AI components from JSON files on disk into player inventory
    /// </summary>
    private void LoadAIInventoryFromDisk()
    {
        string[] folderNames = { "NavFiles", "TurretFiles" };
        
        foreach (string folderName in folderNames)
        {
            string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folderName);
            if (!System.IO.Directory.Exists(folderPath)) continue;
            
            // Look for JSON files (purchased AI components)
            string[] jsonFiles = System.IO.Directory.GetFiles(folderPath, "*.json");
            foreach (string filePath in jsonFiles)
            {
                try
                {
                    string jsonContent = System.IO.File.ReadAllText(filePath);
                    AiTreeAssetJson jsonAi = JsonUtility.FromJson<AiTreeAssetJson>(jsonContent);
                    
                    if (jsonAi != null && !string.IsNullOrEmpty(jsonAi.title))
                    {
                        // Create a proxy ComponentData for the inventory
                        ComponentData proxyComponent = CreateAiTreeProxyFromJson(jsonAi);
                        playerInventory.Add(proxyComponent);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Failed to load AI inventory from {filePath}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Deletes a JSON AI file from disk when selling
    /// </summary>
    private void DeleteJsonAiFile(AiTreeAsset aiTree)
    {
        // Determine the target folder in persistent data path based on branch type
        string folderName = aiTree.branchType == AiEditor.AiBranchType.Nav ? "NavFiles" : "TurretFiles";
        string targetFolder = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folderName);
            
        // Create the JSON file path with the component's instanceId as filename
        string filePath = System.IO.Path.Combine(targetFolder, $"{aiTree.instanceId}.json");
        
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            else
            {
                // Try to find the file by name instead of instanceId
                if (System.IO.Directory.Exists(targetFolder))
                {
                    var files = System.IO.Directory.GetFiles(targetFolder, "*.json");
                    foreach (var file in files)
                    {
                        // Try to match by tree name
                        try
                        {
                            string jsonContent = System.IO.File.ReadAllText(file);
                            var jsonAsset = JsonUtility.FromJson<AiTreeAssetJson>(jsonContent);
                            if (jsonAsset != null && (jsonAsset.title == aiTree.title || jsonAsset.TreeName == aiTree.title))
                            {
                                System.IO.File.Delete(file);
                                return;
                            }
                        }
                        catch (System.Exception parseEx)
                        {
                            Debug.LogWarning($"[DeleteJsonAiFile] Could not parse file {file}: {parseEx.Message}");
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DeleteJsonAiFile] Failed to delete JSON AI file: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Calculate total weight by looking up components and summing their weights
    /// </summary>
    private float CalculateTotalWeight(TankSlotDataJson slotData)
    {
        float totalWeight = 0f;
        
        // Get engine weight
        if (!string.IsNullOrEmpty(slotData.engineFrameInstanceId))
        {
            var engine = playerInventory.Find(c => c.instanceId == slotData.engineFrameInstanceId && c.category == ComponentCategory.EngineFrame);
            if (engine is EngineFrameData engineData)
            {
                totalWeight += engineData.weight;
            }
        }
        
        // Get armor weight
        if (!string.IsNullOrEmpty(slotData.armorInstanceId))
        {
            var armor = playerInventory.Find(c => c.instanceId == slotData.armorInstanceId && c.category == ComponentCategory.Armor);
            if (armor is ArmorData armorData)
            {
                totalWeight += armorData.weight;
            }
        }
        
        // Get turret weight
        if (!string.IsNullOrEmpty(slotData.turretInstanceId))
        {
            var turret = playerInventory.Find(c => c.instanceId == slotData.turretInstanceId && c.category == ComponentCategory.Turret);
            if (turret is TurretData turretData)
            {
                totalWeight += turretData.weight;
            }
        }
        
        return totalWeight;
    }
    
    /// <summary>
    /// Hides a tip bubble permanently (saves state to PlayerPrefs)
    /// Pass the button GameObject as parameter
    /// </summary>
    public void HideTipBubble(GameObject tipBubble)
    {
        if (tipBubble != null)
        {
            tipBubble.SetActive(false);
            // Mark tips as hidden
            tipsVisible = false;
            PlayerPrefs.SetInt(TIPS_VISIBLE_KEY, 0);
            PlayerPrefs.Save();
        }
    }
    
    /// <summary>
    /// Shows all tip bubbles
    /// </summary>
    public void ShowTips()
    {
        tipsVisible = true;
        PlayerPrefs.SetInt(TIPS_VISIBLE_KEY, 1);
        PlayerPrefs.Save();
        UpdateTipBubbleVisibility();
    }
    
    /// <summary>
    /// Toggles tip bubble visibility
    /// </summary>
    public void ToggleTips()
    {
        tipsVisible = !tipsVisible;
        PlayerPrefs.SetInt(TIPS_VISIBLE_KEY, tipsVisible ? 1 : 0);
        PlayerPrefs.Save();
        UpdateTipBubbleVisibility();
    }
    
    /// <summary>
    /// Updates visibility of all tip bubbles based on current state
    /// </summary>
    private void UpdateTipBubbleVisibility()
    {
        foreach (var tipBubble in tipBubbles)
        {
            if (tipBubble != null)
            {
                tipBubble.SetActive(tipsVisible);
            }
        }
    }
}