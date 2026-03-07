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
    
    [Header("Quit Button")]
    public Button quitButton; // Assign the quit button in inspector
    public Button mainMenuButton; // Slides the main menu panel back down
    
    [Header("Campaign Panel")]
    public Button campaignButton; // Assign the campaign button in inspector
    public GameObject campaignPanel; // Assign the campaign panel in inspector
    [Tooltip("Speed at which the campaign panel slides in/out (pixels per second)")]
    public float campaignPanelSlideSpeed = 2000f; // Adjustable slide speed
    
    private RectTransform campaignPanelRect;
    private Vector2 campaignPanelOnScreenPosition;
    private Vector2 campaignPanelOffScreenPosition;
    private bool isCampaignPanelVisible = false;
    private Coroutine campaignSlideCoroutine;

    [Header("Main Menu Panel")]
    public GameObject mainMenuPanel; // The main menu panel that slides up when Workshop is opened
    [Tooltip("Speed at which the main menu panel slides up/down (pixels per second)")]
    public float mainMenuPanelSlideSpeed = 2000f;
    public Button mainMenuWorkshopButton; // Slides the main menu panel up
    public Button mainMenuSettingsButton; // Opens the settings panel
    public Button mainMenuCreditsButton;  // Opens the credits panel
    public GameObject mainMenuSettingsPanel; // Settings sub-panel
    public GameObject mainMenuCreditsPanel;  // Credits sub-panel

    private RectTransform mainMenuPanelRect;
    private Vector2 mainMenuPanelOnScreenPosition;
    private Vector2 mainMenuPanelOffScreenPosition;
    private Coroutine mainMenuPanelCoroutine;
    private static bool mainMenuDismissed = false; // persists across scene loads

    private CanvasGroup mainMenuSettingsCanvasGroup;
    [Tooltip("Speed at which the settings panel fades in/out (alpha per second)")]
    public float mainMenuSettingsFadeSpeed = 4f;
    private bool isMainMenuSettingsVisible = false;
    private Coroutine mainMenuSettingsCoroutine;

    private CanvasGroup mainMenuCreditsCanvasGroup;
    private bool isMainMenuCreditsVisible = false;
    private Coroutine mainMenuCreditsCoroutine;

    [Header("Erase Data Button")]
    public Button eraseDataButton; // Assign the erase data button in inspector
    
    [Header("Debug UI")]
    public TMP_Text debugText; // Assign in inspector
    
    [Header("Team Weight")]
    public TMP_Text totalActiveTanksWeightText; // Assign in inspector to display total weight of all active tanks

    private Coroutine debugTextCoroutine;
    private ComponentData selectedComponent;

    public TMP_Text itemStatsText;
    public TMP_Text descriptionText;

    private void Awake()
    {
        // Find all transforms in the hierarchy
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        
        foreach (Transform t in allTransforms)
        {
            if (t.gameObject.name.ToLower().Contains("button") && t.GetComponent<ButtonClickSound>() == null)
            {
                t.gameObject.AddComponent<ButtonClickSound>();
            }
        }
    }

    private void Start()
    {
        // Initialize JSON tank slot manager
        tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
        if (tankSlotJsonManager == null)
        {
            GameObject managerGO = new GameObject("TankSlotJsonManager");
            tankSlotJsonManager = managerGO.AddComponent<TankSlotJsonManager>();
        }
        
        // Check if this is the first time the player is launching the game
        if (PlayerDataManager.Instance != null && !PlayerDataManager.Instance.playerData.hasSeenTipsOnFirstLaunch)
        {
            // First time launch - show tips automatically
            tipsVisible = true;
            PlayerDataManager.Instance.playerData.hasSeenTipsOnFirstLaunch = true;
            PlayerDataManager.Instance.SavePlayerData();
            PlayerPrefs.SetInt(TIPS_VISIBLE_KEY, 1);
            PlayerPrefs.Save();
        }
        else
        {
            // Not first time - load tip visibility state from PlayerPrefs
            tipsVisible = PlayerPrefs.GetInt(TIPS_VISIBLE_KEY, 0) == 1;
        }
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

        // Setup quit button listener
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(() => PlayerDataManager.Instance.QuitGame());
        }

        // Setup main menu return button
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(ShowMainMenuPanel);
        }

        // Setup campaign panel slide animation
        if (campaignPanel != null)
        {
            campaignPanelRect = campaignPanel.GetComponent<RectTransform>();
            if (campaignPanelRect != null)
            {
                // Store the on-screen position (current position in inspector)
                campaignPanelOnScreenPosition = campaignPanelRect.anchoredPosition;
                
                // Calculate off-screen position (slide to the right)
                // Move it far enough right that it's completely off screen
                float panelWidth = campaignPanelRect.rect.width;
                campaignPanelOffScreenPosition = campaignPanelOnScreenPosition + new Vector2((panelWidth + 100f) * 1.2f, 0);
                
                // Start with panel off-screen to the right
                campaignPanelRect.anchoredPosition = campaignPanelOffScreenPosition;
                isCampaignPanelVisible = false;
            }
        }
        
        // Setup campaign button to toggle panel
        if (campaignButton != null)
        {
            campaignButton.onClick.AddListener(ToggleCampaignPanel);
        }

        // Setup main menu panel slide animation
        if (mainMenuPanel != null)
        {
            mainMenuPanelRect = mainMenuPanel.GetComponent<RectTransform>();
            if (mainMenuPanelRect != null)
            {
                mainMenuPanelOnScreenPosition = mainMenuPanelRect.anchoredPosition;
                float panelHeight = mainMenuPanelRect.rect.height;
                mainMenuPanelOffScreenPosition = mainMenuPanelOnScreenPosition + new Vector2(0f, (panelHeight + 100f) * 1.2f);
                // Start off-screen if previously dismissed, otherwise show normally
                if (mainMenuDismissed)
                {
                    mainMenuPanelRect.anchoredPosition = mainMenuPanelOffScreenPosition;
                }
                else
                {
                    mainMenuPanelRect.anchoredPosition = mainMenuPanelOnScreenPosition;
                }
            }
        }

        // Setup main menu settings sub-panel (alpha fade)
        if (mainMenuSettingsPanel != null)
        {
            mainMenuSettingsCanvasGroup = mainMenuSettingsPanel.GetComponent<CanvasGroup>();
            if (mainMenuSettingsCanvasGroup == null)
                mainMenuSettingsCanvasGroup = mainMenuSettingsPanel.AddComponent<CanvasGroup>();
            mainMenuSettingsCanvasGroup.alpha = 0f;
            mainMenuSettingsCanvasGroup.interactable = false;
            mainMenuSettingsCanvasGroup.blocksRaycasts = false;
            mainMenuSettingsPanel.SetActive(false);
            isMainMenuSettingsVisible = false;
        }

        // Setup main menu credits sub-panel (alpha fade)
        if (mainMenuCreditsPanel != null)
        {
            mainMenuCreditsCanvasGroup = mainMenuCreditsPanel.GetComponent<CanvasGroup>();
            if (mainMenuCreditsCanvasGroup == null)
                mainMenuCreditsCanvasGroup = mainMenuCreditsPanel.AddComponent<CanvasGroup>();
            mainMenuCreditsCanvasGroup.alpha = 0f;
            mainMenuCreditsCanvasGroup.interactable = false;
            mainMenuCreditsCanvasGroup.blocksRaycasts = false;
            mainMenuCreditsPanel.SetActive(false);
            isMainMenuCreditsVisible = false;
        }

        // Setup main menu button listeners
        if (mainMenuWorkshopButton != null)
            mainMenuWorkshopButton.onClick.AddListener(HideMainMenuPanel);
        if (mainMenuSettingsButton != null)
            mainMenuSettingsButton.onClick.AddListener(ToggleMainMenuSettings);
        if (mainMenuCreditsButton != null)
            mainMenuCreditsButton.onClick.AddListener(ToggleMainMenuCredits);

        // Setup erase data button listener
        if (eraseDataButton != null)
        {
            eraseDataButton.onClick.AddListener(() => PlayerDataManager.Instance.ErasePlayerData());
        }

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
        
        // Load unlocked components from PlayerPrefs
        LoadUnlockedComponents();
        
        LoadPlayerInventoryFromSave();
        
        // Load player cash from PlayerDataManager and update UI
        if (PlayerDataManager.Instance != null)
        {
            playerCash = PlayerDataManager.Instance.GetPlayerCash();
        }
        UpdatePlayerCashUI();
        
        // Refresh shop display to show unlocked components
        PopulateComponentList();
        
        // Clean up any legacy AI files with old instanceId format
        CleanupLegacyAIFiles();
        
        // Tank slots are now managed entirely via JSON - no need for ScriptableObject restoration
        
        // Validate and clear any invalid component references in tank slots
        ValidateAndClearInvalidTankSlotReferences();
        
        // Load tank slots from ScriptableObjects AFTER restoring activation states
        LoadTankSlotsFromScriptableObjects();
        
        // Update total active tanks weight display
        UpdateTotalActiveTanksWeight();
    }
    
    /// <summary>
    /// Validates that all instance IDs in tank slot JSON actually exist in player inventory
    /// Clears any stale references to components that were removed
    /// </summary>
    private void ValidateAndClearInvalidTankSlotReferences()
    {
        if (tankSlotJsonManager == null) return;
        
        // Build a set of all valid instance IDs from player inventory
        var validInstanceIds = new HashSet<string>();
        foreach (var comp in playerInventory)
        {
            if (!string.IsNullOrEmpty(comp.instanceId))
                validInstanceIds.Add(comp.instanceId);
        }
        
        // Check each tank slot and clear invalid references
        var allSlots = tankSlotJsonManager.GetAllTankSlots();
        foreach (var slotData in allSlots)
        {
            bool needsUpdate = false;
            
            // Check turret
            if (!string.IsNullOrEmpty(slotData.turretInstanceId) && !validInstanceIds.Contains(slotData.turretInstanceId))
            {
                Debug.LogWarning($"[WorkshopUIManager] Clearing invalid turret reference in {slotData.slotName}: {slotData.turretInstanceId}");
                slotData.turretInstanceId = "";
                slotData.turretDamage = 0;
                slotData.turretRange = 0;
                slotData.turretShotsPerSec = 0;
                slotData.turretBulletSpeed = 0;
                slotData.turretWeight = 0;
                needsUpdate = true;
            }
            
            // Check armor
            if (!string.IsNullOrEmpty(slotData.armorInstanceId) && !validInstanceIds.Contains(slotData.armorInstanceId))
            {
                Debug.LogWarning($"[WorkshopUIManager] Clearing invalid armor reference in {slotData.slotName}: {slotData.armorInstanceId}");
                slotData.armorInstanceId = "";
                slotData.armorHP = 0;
                slotData.armorWeight = 0;
                needsUpdate = true;
            }
            
            // Check engine frame
            if (!string.IsNullOrEmpty(slotData.engineFrameInstanceId) && !validInstanceIds.Contains(slotData.engineFrameInstanceId))
            {
                Debug.LogWarning($"[WorkshopUIManager] Clearing invalid engine frame reference in {slotData.slotName}: {slotData.engineFrameInstanceId}");
                slotData.engineFrameInstanceId = "";
                slotData.enginePower = 0;
                slotData.engineWeight = 0;
                slotData.engineWeightCapacity = 0;
                needsUpdate = true;
            }
            
            // Check turret AI (validate against disk files)
            if (!string.IsNullOrEmpty(slotData.turretAIInstanceId))
            {
                bool aiExists = validInstanceIds.Contains(slotData.turretAIInstanceId);
                if (!aiExists)
                {
                    // Also check if file exists on disk
                    string aiFolder = Path.Combine(Application.persistentDataPath, "AiTrees", "TurretFiles");
                    string aiFilePath = Path.Combine(aiFolder, $"{slotData.turretAIInstanceId}.json");
                    aiExists = File.Exists(aiFilePath);
                }
                
                if (!aiExists)
                {
                    Debug.LogWarning($"[WorkshopUIManager] Clearing invalid turret AI reference in {slotData.slotName}: {slotData.turretAIInstanceId}");
                    slotData.turretAIInstanceId = "";
                    slotData.turretAIWeight = 0f;
                    needsUpdate = true;
                }
            }
            
            // Check nav AI (validate against disk files)
            if (!string.IsNullOrEmpty(slotData.navAIInstanceId))
            {
                bool aiExists = validInstanceIds.Contains(slotData.navAIInstanceId);
                if (!aiExists)
                {
                    // Also check if file exists on disk
                    string aiFolder = Path.Combine(Application.persistentDataPath, "AiTrees", "NavFiles");
                    string aiFilePath = Path.Combine(aiFolder, $"{slotData.navAIInstanceId}.json");
                    aiExists = File.Exists(aiFilePath);
                }
                
                if (!aiExists)
                {
                    Debug.LogWarning($"[WorkshopUIManager] Clearing invalid nav AI reference in {slotData.slotName}: {slotData.navAIInstanceId}");
                    slotData.navAIInstanceId = "";
                    slotData.navAIWeight = 0f;
                    needsUpdate = true;
                }
            }
            
            // Recalculate total weight if anything was cleared (includes AI weights)
            if (needsUpdate)
            {
                slotData.totalWeight = slotData.engineWeight + slotData.armorWeight + slotData.turretWeight + slotData.turretAIWeight + slotData.navAIWeight;
                tankSlotJsonManager.UpdateTankSlot(slotData.slotIndex, slotData);
            }
        }
    }
    
    private void LoadPlayerInventoryFromSave()
    {
        Debug.Log("[WorkshopUIManager] Loading player inventory from PlayerDataManager...");
        
        playerInventory.Clear();
        
        // Load regular components from PlayerData save file
        int loadedCount = 0;
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
                    loadedCount++;
                }
            }
        }
        
        Debug.Log($"[WorkshopUIManager] Loaded {loadedCount} components from PlayerData");
        
        // Load AI components from JSON files on disk
        LoadAIInventoryFromDisk();
        
        Debug.Log($"[WorkshopUIManager] Total inventory after loading: {playerInventory.Count} components");
        
        // Add default components for new players if inventory is empty
        if (playerInventory.Count == 0)
        {
            Debug.Log("[WorkshopUIManager] Inventory is empty, adding default components...");
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
        // Search shop lists first
        foreach (var c in turretShopComponents) if (c.id == id) return c;
        foreach (var c in armorShopComponents) if (c.id == id) return c;
        foreach (var c in aiTreeShopComponents) if (c.id == id) return c;
        foreach (var c in engineFrameShopComponents) if (c.id == id) return c;
        
        // If not found in shop, search all components from Resources (for owned but not unlocked components)
        var allTurrets = Resources.LoadAll<TurretData>("Workshop/ComponentData/Turrets");
        foreach (var c in allTurrets) if (c.id == id) return c;
        
        var allArmor = Resources.LoadAll<ArmorData>("Workshop/ComponentData/Armors");
        foreach (var c in allArmor) if (c.id == id) return c;
        
        var allEngineFrames = Resources.LoadAll<EngineFrameData>("Workshop/ComponentData/EngineFrames");
        foreach (var c in allEngineFrames) if (c.id == id) return c;
        
        return null;
    }

    public void UpdatePlayerCashUI()
    {
        if (playerCashText != null)
            playerCashText.text = $"${playerCash}";
    }
    
    /// <summary>
    /// Unlocks a component and adds it to the appropriate shop category
    /// Called when player completes arenas
    /// </summary>
    public void UnlockComponent(ComponentData component)
    {
        if (component == null) return;
        
        // Check if already unlocked
        string unlockKey = $"ComponentUnlocked_{component.id}";
        if (PlayerPrefs.GetInt(unlockKey, 0) == 1)
        {
            Debug.Log($"[WorkshopUIManager] Component already unlocked: {component.title}");
            return;
        }
        
        // Determine category and add to appropriate list
        List<ComponentData> targetList = null;
        string categoryName = "";
        
        if (component is TurretData)
        {
            targetList = turretShopComponents;
            categoryName = "Turret";
        }
        else if (component is ArmorData)
        {
            targetList = armorShopComponents;
            categoryName = "Armor";
        }
        else if (component is EngineFrameData)
        {
            targetList = engineFrameShopComponents;
            categoryName = "Engine Frame";
        }
        else if (component is AiTreeAsset)
        {
            // Check if it's turret or nav AI based on category field
            if (component.category == ComponentCategory.TurretAI)
            {
                targetList = turretAIShopComponents;
                categoryName = "Turret AI";
            }
            else if (component.category == ComponentCategory.NavAI)
            {
                targetList = navAIShopComponents;
                categoryName = "Nav AI";
            }
        }
        
        if (targetList != null && !targetList.Contains(component))
        {
            targetList.Add(component);
            PlayerPrefs.SetInt(unlockKey, 1);
            PlayerPrefs.Save();
            Debug.Log($"[WorkshopUIManager] Unlocked {categoryName}: {component.title}");
            
            // Refresh UI if viewing that category
            PopulateComponentList();
        }
    }
    
    /// <summary>
    /// Unlocks multiple components at once (for arena rewards)
    /// </summary>
    public void UnlockComponents(List<ComponentData> components)
    {
        if (components == null || components.Count == 0) return;
        
        foreach (var component in components)
        {
            UnlockComponent(component);
        }
    }
    
    /// <summary>
    /// Loads all unlocked components from PlayerPrefs and adds them to shop lists
    /// Called during Start() to restore progression
    /// </summary>
    private void LoadUnlockedComponents()
    {
        // Load all component ScriptableObjects from Resources
        var allTurrets = Resources.LoadAll<TurretData>("Workshop/ComponentData/Turrets");
        var allArmor = Resources.LoadAll<ArmorData>("Workshop/ComponentData/Armors");
        var allEngineFrames = Resources.LoadAll<EngineFrameData>("Workshop/ComponentData/EngineFrames");
        var allTurretAI = Resources.LoadAll<AiTreeAsset>("ShopAI/TurretAI");
        var allNavAI = Resources.LoadAll<AiTreeAsset>("ShopAI/NavAI");
        
        // Check each component if it's unlocked and add to shop if not already there
        foreach (var turret in allTurrets)
        {
            if (PlayerPrefs.GetInt($"ComponentUnlocked_{turret.id}", 0) == 1 && !turretShopComponents.Contains(turret))
            {
                turretShopComponents.Add(turret);
            }
        }
        
        foreach (var armor in allArmor)
        {
            if (PlayerPrefs.GetInt($"ComponentUnlocked_{armor.id}", 0) == 1 && !armorShopComponents.Contains(armor))
            {
                armorShopComponents.Add(armor);
            }
        }
        
        foreach (var engineFrame in allEngineFrames)
        {
            if (PlayerPrefs.GetInt($"ComponentUnlocked_{engineFrame.id}", 0) == 1 && !engineFrameShopComponents.Contains(engineFrame))
            {
                engineFrameShopComponents.Add(engineFrame);
            }
        }
        
        foreach (var ai in allTurretAI)
        {
            if (PlayerPrefs.GetInt($"ComponentUnlocked_{ai.id}", 0) == 1 && !turretAIShopComponents.Contains(ai))
            {
                turretAIShopComponents.Add(ai);
            }
        }
        
        foreach (var ai in allNavAI)
        {
            if (PlayerPrefs.GetInt($"ComponentUnlocked_{ai.id}", 0) == 1 && !navAIShopComponents.Contains(ai))
            {
                navAIShopComponents.Add(ai);
            }
        }
    }

    public void ClearUnlockedComponentsFromShop()
    {
        // Remove unlocked turrets from shop (keep defaults)
        turretShopComponents.RemoveAll(turret => PlayerPrefs.GetInt($"ComponentUnlocked_{turret.id}", 0) == 1);
        
        // Remove unlocked armor from shop (keep defaults)
        armorShopComponents.RemoveAll(armor => PlayerPrefs.GetInt($"ComponentUnlocked_{armor.id}", 0) == 1);
        
        // Remove unlocked engine frames from shop (keep defaults)
        engineFrameShopComponents.RemoveAll(engineFrame => PlayerPrefs.GetInt($"ComponentUnlocked_{engineFrame.id}", 0) == 1);
        
        // Remove unlocked turret AI from shop (keep defaults)
        turretAIShopComponents.RemoveAll(ai => PlayerPrefs.GetInt($"ComponentUnlocked_{ai.id}", 0) == 1);
        
        // Remove unlocked nav AI from shop (keep defaults)
        navAIShopComponents.RemoveAll(ai => PlayerPrefs.GetInt($"ComponentUnlocked_{ai.id}", 0) == 1);
        
        // For AI trees, we need to check the proxy components
        aiTreeShopComponents.RemoveAll(component => {
            if (component is AiTreeAsset aiTree)
            {
                return PlayerPrefs.GetInt($"ComponentUnlocked_{aiTree.id}", 0) == 1;
            }
            return false;
        });
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
                },
                (comp, skinPath) => {
                    // Handle skin selection
                    OnSkinSelected(comp, skinPath);
                },
                (comp, decalPath) => {
                    // Handle decal selection (turret only)
                    OnDecalSelected(comp, decalPath);
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
            if (statsPanel != null)
                statsPanel.ShowStats(null);
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
        {
            var slotData = TankSlotJsonManager.Instance.GetTankSlot(selectedTankSlot.slotIndex);
            modelPreview.ShowTank(GetEquippedComponentsForSlot(selectedTankSlot), slotData);
        }
        
        // Show full tank stats in stats panel
        float totalWeight = CalculateAndSaveTotalWeight(selectedTankSlot);
        itemStatsText.text = $"Tank Weight: {totalWeight:F1}t";
        descriptionText.text = "";
        if (statsPanel != null)
            statsPanel.ShowTankStats(GetEquippedComponentsForSlot(selectedTankSlot), totalWeight, selectedTankSlot.TankName);
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
        // Use PlayerDataManager for cash transactions
        if (!PlayerDataManager.Instance.SpendPlayerCash(component.cost))
        {
            ShowDebugMessage("Not enough cash!");
            return;
        }
        
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
            PlayerDataManager.Instance.AddPlayerCash(component.cost / 2);
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
                
                PlayerDataManager.Instance.AddPlayerCash(component.cost / 2);
                
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
        {
            var slotData = TankSlotJsonManager.Instance.GetTankSlot(selectedTankSlot.slotIndex);
            modelPreview.ShowTank(GetEquippedComponentsForSlot(selectedTankSlot), slotData);
        }
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
                    slotData.engineWeight = 0;
                    slotData.engineFrameHP = 0;
                    break;
                case ComponentCategory.Armor:
                    slotData.armorInstanceId = "";
                    slotData.armorHP = 0;
                    slotData.armorWeight = 0;
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
                    slotData.turretWeight = 0;
                    break;
                case ComponentCategory.AITree:
                    if (component is AiTreeAsset aiAsset)
                    {
                        if (aiAsset.branchType == AiEditor.AiBranchType.Turret)
                        {
                            slotData.turretAIInstanceId = "";
                            slotData.turretAIWeight = 0f;
                        }
                        else if (aiAsset.branchType == AiEditor.AiBranchType.Nav)
                        {
                            slotData.navAIInstanceId = "";
                            slotData.navAIWeight = 0f;
                        }
                    }
                    break;
            }
            
            // Recalculate total weight after component removal (includes AI weights)
            slotData.totalWeight = slotData.armorWeight + slotData.turretWeight + slotData.engineWeight + slotData.turretAIWeight + slotData.navAIWeight;
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
                        slotData.engineWeight = engineData.weight;
                        slotData.engineFrameHP = 0; // EngineFrameData doesn't have HP property
                    }
                    break;
                case ComponentCategory.Armor:
                    slotData.armorInstanceId = component.instanceId;
                    // Copy armor stats
                    if (component is ArmorData armorData)
                    {
                        slotData.armorHP = armorData.HP;
                        slotData.armorWeight = armorData.weight;
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
                        slotData.turretWeight = turretData.weight;
                    }
                    break;
                case ComponentCategory.AITree:
                    if (component is AiTreeAsset aiAsset)
                    {
                        if (aiAsset.branchType == AiEditor.AiBranchType.Turret)
                        {
                            slotData.turretAIInstanceId = component.instanceId;
                            // Use the weight stored in the component (calculated from node count when saved)
                            slotData.turretAIWeight = component.weight;
                        }
                        else if (aiAsset.branchType == AiEditor.AiBranchType.Nav)
                        {
                            slotData.navAIInstanceId = component.instanceId;
                            // Use the weight stored in the component (calculated from node count when saved)
                            slotData.navAIWeight = component.weight;
                        }
                    }
                    break;
            }
            
            // Recalculate total weight after component assignment (includes AI weights)
            slotData.totalWeight = slotData.armorWeight + slotData.turretWeight + slotData.engineWeight + slotData.turretAIWeight + slotData.navAIWeight;
            
            slotData.displayName = slot.TankName;
        }

        // Save the updated slot data to JSON
        tankSlotJsonManager.UpdateTankSlot(slotIndex, slotData);
        
        // Update total active tanks weight display
        UpdateTotalActiveTanksWeight();
        
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
        
        // Play error sound
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayErrorSound();
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
        Debug.Log("[WorkshopUIManager] Loading tank slots from TankSlotJsonManager...");
        
        foreach (var slot in tankSlots)
        {
            var slotData = TankSlotJsonManager.Instance.GetTankSlot(slot.slotIndex);
            if (slotData != null)
            {
                Debug.Log($"[WorkshopUIManager] Slot {slot.slotIndex} data: engine='{slotData.engineFrameInstanceId}', armor='{slotData.armorInstanceId}', turret='{slotData.turretInstanceId}', turretAI='{slotData.turretAIInstanceId}', navAI='{slotData.navAIInstanceId}'");
                
                slot.SetSelected(false); // Deselect by default
                slot.SetActive(slotData.isActive);
                slot.UpdateAssignedComponentsFromSlotData();
            }
        }
        
        Debug.Log("[WorkshopUIManager] Finished loading tank slots");
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
            {
                var slotData = TankSlotJsonManager.Instance.GetTankSlot(selectedTankSlot.slotIndex);
                modelPreview.ShowTank(GetEquippedComponentsForSlot(selectedTankSlot), slotData);
            }

            // Show full tank stats in stats panel
            float totalWeight = CalculateAndSaveTotalWeight(selectedTankSlot);
            itemStatsText.text = $"Tank Weight: {totalWeight:F1}t";
            descriptionText.text = "";
            if (statsPanel != null)
                statsPanel.ShowTankStats(GetEquippedComponentsForSlot(selectedTankSlot), totalWeight, selectedTankSlot.TankName);
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
            {
                // Skip AI components - their weight is tracked separately in turretAIWeight/navAIWeight
                if (comp.category != ComponentCategory.AITree)
                {
                    totalWeight += comp.weight;
                }
            }
        }
        
        // Add AI weights from slot data (stored separately to avoid double counting)
        var slotData = TankSlotJsonManager.Instance.GetTankSlot(slot.slotIndex);
        if (slotData != null)
        {
            totalWeight += slotData.turretAIWeight + slotData.navAIWeight;
        }
        
        // Save to JSON data
        slotData = TankSlotJsonManager.Instance.GetTankSlot(slot.slotIndex);
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
            var slotData = TankSlotJsonManager.Instance.GetTankSlot(selectedTankSlot.slotIndex);
            modelPreview.ShowTank(GetEquippedComponentsForSlot(selectedTankSlot), slotData);
        }
        else if (selectedComponent == changedComponent && modelPreview != null)
        {
            // If this component is currently selected for individual preview, refresh it
            modelPreview.ShowModel(changedComponent);
        }
    }
    
    /// <summary>
    /// Handle skin selection for a component
    /// </summary>
    private void OnSkinSelected(ComponentData component, string skinPath)
    {
        if (component == null) return;
        
        Debug.Log($"Skin selected for {component.title} (instanceId: {component.instanceId}): {(string.IsNullOrEmpty(skinPath) ? "None" : skinPath)}");
        
        // Update the component's runtime field
        component.skinPath = skinPath ?? "";
        
        // Save skin to ComponentCustomizationManager (per-instanceId)
        if (ComponentCustomizationManager.Instance != null)
        {
            ComponentCustomizationManager.Instance.SetSkin(component.instanceId, skinPath);
        }
        
        // Also save skin path to tank slot JSON for any tanks using this component
        if (TankSlotJsonManager.Instance != null)
        {
            for (int i = 0; i < tankSlots.Count; i++)
            {
                var slotData = TankSlotJsonManager.Instance.GetTankSlot(i);
                bool updated = false;
                
                if (slotData.engineFrameInstanceId == component.instanceId)
                {
                    slotData.engineFrameSkinPath = skinPath ?? "";
                    updated = true;
                }
                else if (slotData.armorInstanceId == component.instanceId)
                {
                    slotData.armorSkinPath = skinPath ?? "";
                    updated = true;
                }
                else if (slotData.turretInstanceId == component.instanceId)
                {
                    slotData.turretSkinPath = skinPath ?? "";
                    updated = true;
                }
                
                if (updated)
                {
                    TankSlotJsonManager.Instance.UpdateTankSlot(i, slotData);
                    Debug.Log($"[WorkshopUIManager] Updated skin path for tank slot {i}");
                }
            }
        }
        
        // Refresh preview
        if (modelPreview != null)
        {
            // If this component is in the selected tank slot, refresh tank view
            if (selectedTankSlot != null && selectedTankSlot.HasComponent(component))
            {
                var slotData = TankSlotJsonManager.Instance.GetTankSlot(selectedTankSlot.slotIndex);
                modelPreview.ShowTank(GetEquippedComponentsForSlot(selectedTankSlot), slotData);
            }
            else
            {
                // Always refresh single component preview when skin is selected
                // (since user is interacting with this component's skin button)
                modelPreview.ShowModel(component);
            }
        }
    }
    
    /// <summary>
    /// Handle decal selection for a turret component
    /// </summary>
    private void OnDecalSelected(ComponentData component, string decalPath)
    {
        if (component == null || component.category != ComponentCategory.Turret) return;
        
        Debug.Log($"Decal selected for {component.title} (instanceId: {component.instanceId}): {(string.IsNullOrEmpty(decalPath) ? "None" : decalPath)}");
        
        // Update the component's runtime field
        component.decalPath = decalPath ?? "";
        
        // Save decal to ComponentCustomizationManager (per-instanceId)
        if (ComponentCustomizationManager.Instance != null)
        {
            ComponentCustomizationManager.Instance.SetDecal(component.instanceId, decalPath);
        }
        
        // Also save decal path to tank slot JSON for any tanks using this turret
        if (TankSlotJsonManager.Instance != null)
        {
            for (int i = 0; i < tankSlots.Count; i++)
            {
                var slotData = TankSlotJsonManager.Instance.GetTankSlot(i);
                
                if (slotData.turretInstanceId == component.instanceId)
                {
                    slotData.turretDecalPath = decalPath ?? "";
                    TankSlotJsonManager.Instance.UpdateTankSlot(i, slotData);
                    Debug.Log($"[WorkshopUIManager] Updated decal path for tank slot {i}");
                }
            }
        }
        
        // Refresh preview
        if (modelPreview != null)
        {
            // If this component is in the selected tank slot, refresh tank view
            if (selectedTankSlot != null && selectedTankSlot.HasComponent(component))
            {
                var slotData = TankSlotJsonManager.Instance.GetTankSlot(selectedTankSlot.slotIndex);
                modelPreview.ShowTank(GetEquippedComponentsForSlot(selectedTankSlot), slotData);
            }
            else
            {
                // Always refresh single component preview when decal is selected
                // (since user is interacting with this component's decal button)
                modelPreview.ShowModel(component);
            }
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
    
    /// <summary>
    /// Toggles the campaign panel (slides in or out)
    /// </summary>
    public void ToggleCampaignPanel()
    {
        if (isCampaignPanelVisible)
        {
            HideCampaignPanel();
        }
        else
        {
            ShowCampaignPanel();
        }
    }
    
    /// <summary>
    /// Shows the campaign panel by sliding it in from the right
    /// </summary>
    public void ShowCampaignPanel()
    {
        if (campaignPanelRect == null) return;
        
        if (campaignSlideCoroutine != null)
        {
            StopCoroutine(campaignSlideCoroutine);
        }
        
        isCampaignPanelVisible = true;
        campaignSlideCoroutine = StartCoroutine(SlideCampaignPanel(campaignPanelOnScreenPosition));
    }
    
    /// <summary>
    /// Hides the campaign panel by sliding it out to the right
    /// </summary>
    public void HideCampaignPanel()
    {
        if (campaignPanelRect == null) return;
        
        if (campaignSlideCoroutine != null)
        {
            StopCoroutine(campaignSlideCoroutine);
        }
        
        isCampaignPanelVisible = false;
        campaignSlideCoroutine = StartCoroutine(SlideCampaignPanel(campaignPanelOffScreenPosition));
    }
    
    /// <summary>
    /// Coroutine to smoothly slide the campaign panel to a target position
    /// </summary>
    private IEnumerator SlideCampaignPanel(Vector2 targetPosition)
    {
        if (campaignPanelRect == null) yield break;
        
        while (Vector2.Distance(campaignPanelRect.anchoredPosition, targetPosition) > 1f)
        {
            campaignPanelRect.anchoredPosition = Vector2.MoveTowards(
                campaignPanelRect.anchoredPosition,
                targetPosition,
                campaignPanelSlideSpeed * Time.unscaledDeltaTime
            );
            yield return null;
        }
        
        // Snap to final position
        campaignPanelRect.anchoredPosition = targetPosition;
        campaignSlideCoroutine = null;
    }

    // ── Main Menu Panel ────────────────────────────────────────────────────

    /// <summary>
    /// Slides the main menu panel up off-screen (called by the Workshop button).
    /// </summary>
    public void HideMainMenuPanel()
    {
        if (mainMenuPanelRect == null) return;
        if (mainMenuPanelCoroutine != null) StopCoroutine(mainMenuPanelCoroutine);
        mainMenuDismissed = true;
        mainMenuPanelCoroutine = StartCoroutine(SlideRectTo(mainMenuPanelRect, mainMenuPanelOffScreenPosition, mainMenuPanelSlideSpeed,
            () => mainMenuPanelCoroutine = null));
    }

    /// <summary>
    /// Slides the main menu panel back down to its on-screen position.
    /// </summary>
    public void ShowMainMenuPanel()
    {
        if (mainMenuPanelRect == null) return;
        if (mainMenuPanelCoroutine != null) StopCoroutine(mainMenuPanelCoroutine);
        mainMenuDismissed = false;
        mainMenuPanelCoroutine = StartCoroutine(SlideRectTo(mainMenuPanelRect, mainMenuPanelOnScreenPosition, mainMenuPanelSlideSpeed,
            () => mainMenuPanelCoroutine = null));
    }

    /// <summary>
    /// Resets the main menu dismissed state so it shows again on next scene load.
    /// Call this on game reset.
    /// </summary>
    public static void ResetMainMenuState()
    {
        mainMenuDismissed = false;
    }

    /// <summary>
    /// Toggles the settings sub-panel; closes credits if open.
    /// </summary>
    public void ToggleMainMenuSettings()
    {
        if (isMainMenuCreditsVisible) HideMainMenuCredits();
        if (isMainMenuSettingsVisible) HideMainMenuSettings();
        else ShowMainMenuSettings();
    }

    public void ShowMainMenuSettings()
    {
        if (mainMenuSettingsPanel == null || mainMenuSettingsCanvasGroup == null) return;
        if (mainMenuSettingsCoroutine != null) StopCoroutine(mainMenuSettingsCoroutine);
        isMainMenuSettingsVisible = true;
        mainMenuSettingsCanvasGroup.alpha = 0f; // prevent flash
        mainMenuSettingsPanel.SetActive(true);
        mainMenuSettingsCanvasGroup.interactable = true;
        mainMenuSettingsCanvasGroup.blocksRaycasts = true;
        mainMenuSettingsCoroutine = StartCoroutine(FadePanel(mainMenuSettingsPanel, mainMenuSettingsCanvasGroup, 0f, 1f, 0.2f,
            () => mainMenuSettingsCoroutine = null));
    }

    public void HideMainMenuSettings()
    {
        if (mainMenuSettingsPanel == null || mainMenuSettingsCanvasGroup == null) return;
        if (mainMenuSettingsCoroutine != null) StopCoroutine(mainMenuSettingsCoroutine);
        isMainMenuSettingsVisible = false;
        mainMenuSettingsCanvasGroup.interactable = false;
        mainMenuSettingsCanvasGroup.blocksRaycasts = false;
        mainMenuSettingsCoroutine = StartCoroutine(FadePanel(mainMenuSettingsPanel, mainMenuSettingsCanvasGroup, 1f, 0f, 0.2f,
            () => { mainMenuSettingsPanel.SetActive(false); mainMenuSettingsCoroutine = null; }));
    }

    /// <summary>
    /// Toggles the credits sub-panel; closes settings if open.
    /// </summary>
    public void ToggleMainMenuCredits()
    {
        if (isMainMenuSettingsVisible) HideMainMenuSettings();
        if (isMainMenuCreditsVisible) HideMainMenuCredits();
        else ShowMainMenuCredits();
    }

    public void ShowMainMenuCredits()
    {
        if (mainMenuCreditsPanel == null || mainMenuCreditsCanvasGroup == null) return;
        if (mainMenuCreditsCoroutine != null) StopCoroutine(mainMenuCreditsCoroutine);
        isMainMenuCreditsVisible = true;
        mainMenuCreditsCanvasGroup.alpha = 0f; // prevent flash
        mainMenuCreditsPanel.SetActive(true);
        mainMenuCreditsCanvasGroup.interactable = true;
        mainMenuCreditsCanvasGroup.blocksRaycasts = true;
        mainMenuCreditsCoroutine = StartCoroutine(FadePanel(mainMenuCreditsPanel, mainMenuCreditsCanvasGroup, 0f, 1f, 0.2f,
            () => mainMenuCreditsCoroutine = null));
    }

    public void HideMainMenuCredits()
    {
        if (mainMenuCreditsPanel == null || mainMenuCreditsCanvasGroup == null) return;
        if (mainMenuCreditsCoroutine != null) StopCoroutine(mainMenuCreditsCoroutine);
        isMainMenuCreditsVisible = false;
        mainMenuCreditsCanvasGroup.interactable = false;
        mainMenuCreditsCanvasGroup.blocksRaycasts = false;
        mainMenuCreditsCoroutine = StartCoroutine(FadePanel(mainMenuCreditsPanel, mainMenuCreditsCanvasGroup, 1f, 0f, 0.2f,
            () => { mainMenuCreditsPanel.SetActive(false); mainMenuCreditsCoroutine = null; }));
    }

    /// <summary>
    /// Coroutine to fade a CanvasGroup alpha to a target value.
    /// Uses unscaled time so it works even when Time.timeScale = 0.
    /// </summary>
    private IEnumerator FadePanel(GameObject panel, CanvasGroup cg, float startAlpha, float endAlpha, float duration, System.Action onComplete)
    {
        float time = 0f;
        cg.alpha = startAlpha;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
            yield return null;
        }
        cg.alpha = endAlpha;
        onComplete?.Invoke();
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float speed, System.Action onComplete)
    {
        if (cg == null) yield break;
        while (Mathf.Abs(cg.alpha - targetAlpha) > 0.01f)
        {
            cg.alpha = Mathf.MoveTowards(cg.alpha, targetAlpha, speed * Time.unscaledDeltaTime);
            yield return null;
        }
        cg.alpha = targetAlpha;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Generic coroutine to slide any RectTransform to a target anchoredPosition.
    /// Uses unscaled time so it works even when Time.timeScale = 0.
    /// </summary>
    private IEnumerator SlideRectTo(RectTransform rect, Vector2 targetPos, float speed, System.Action onComplete)
    {
        if (rect == null) yield break;
        while (Vector2.Distance(rect.anchoredPosition, targetPos) > 1f)
        {
            rect.anchoredPosition = Vector2.MoveTowards(
                rect.anchoredPosition, targetPos, speed * Time.unscaledDeltaTime);
            yield return null;
        }
        rect.anchoredPosition = targetPos;
        onComplete?.Invoke();
    }
    
    /// <summary>
    /// Calculates and displays the total weight of all active tank slots
    /// </summary>
    public void UpdateTotalActiveTanksWeight()
    {
        if (tankSlotJsonManager == null || totalActiveTanksWeightText == null)
            return;
        
        var activeTanks = tankSlotJsonManager.GetActiveTankSlots();
        float totalWeight = 0f;
        
        if (activeTanks != null)
        {
            foreach (var tank in activeTanks)
            {
                totalWeight += tank.totalWeight;
            }
        }
        
        totalActiveTanksWeightText.text = $"Team Weight: {totalWeight:F1}t";
    }
}