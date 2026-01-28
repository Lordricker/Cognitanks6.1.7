using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AiEditor;

public class ComponentEntryUI : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text costText;
    public TMP_Text assignedToText;    public Button buyButton;
    public Button sellButton;
    public Button equipButton;
    public Button selectButton; // Assign in inspector
    
    [Header("Skin & Decal")]
    public Button skinButton;           // For all physical components in inventory
    public Button decalButton;          // For turrets only in inventory
    public GameObject skinDecalScrollView; // The scroll view GameObject (will be shown/hidden)
    public ScrollRect scrollRect;       // ScrollRect component for the scroll view
    public Transform contentParent;     // Content transform where image buttons are instantiated
    public GameObject imageButtonPrefab; // Simple button prefab with Image/RawImage component
    public TMP_Text emptyFolderText;    // Text shown when no skins/decals found
    
    private bool isPanelOpen = false;
    private Coroutine clickOutsideCoroutine;

    public System.Action<ComponentData> onSelected; // Set by manager

    private ComponentData component;
    private Color? cachedNormalColor = null;    public RainbowColorSlider colorSlider; // Assign in inspector

    public void Setup(ComponentData data, string assignedToTank, bool isShopView,
                      System.Action<ComponentData> onBuy,
                      System.Action<ComponentData> onSell,
                      System.Action<ComponentData> onEquip,
                      System.Action<ComponentData> onSelect = null,
                      System.Action<ComponentData> onColorChanged = null,
                      System.Action<ComponentData, string> onSkinSelected = null,
                      System.Action<ComponentData, string> onDecalSelected = null)
    {
        component = data;
        titleText.text = data.title;
        costText.text = $"Cost: {data.cost}";
        assignedToText.text = assignedToTank;        buyButton.gameObject.SetActive(isShopView);
        sellButton.gameObject.SetActive(!isShopView);
        equipButton.gameObject.SetActive(!isShopView);
        
        // Skin button - visible for physical components (Turret, Armor, EngineFrame) in inventory view
        if (skinButton != null)
        {
            bool showSkin = !isShopView && 
                (data.category == ComponentCategory.Turret || 
                 data.category == ComponentCategory.Armor || 
                 data.category == ComponentCategory.EngineFrame);
            skinButton.gameObject.SetActive(showSkin);
            skinButton.onClick.RemoveAllListeners();
            if (showSkin)
            {
                skinButton.onClick.AddListener(() => OpenSkinPanel(data, onSkinSelected));
            }
        }
        
        // Decal button - visible only for turrets in inventory view
        if (decalButton != null)
        {
            bool showDecal = !isShopView && data.category == ComponentCategory.Turret;
            decalButton.gameObject.SetActive(showDecal);
            decalButton.onClick.RemoveAllListeners();
            if (showDecal)
            {
                decalButton.onClick.AddListener(() => OpenDecalPanel(data, onDecalSelected));
            }
        }

        buyButton.onClick.RemoveAllListeners();
        sellButton.onClick.RemoveAllListeners();
        equipButton.onClick.RemoveAllListeners();        buyButton.onClick.AddListener(() => onBuy?.Invoke(component));
        sellButton.onClick.AddListener(() => onSell?.Invoke(component));
        equipButton.onClick.AddListener(() => onEquip?.Invoke(component));        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            if (onSelect != null)
                selectButton.onClick.AddListener(() => onSelect(data));
        }
        
        onSelected = onSelect;

        var colors = equipButton.colors;
        Color selectedColor = colors.selectedColor;

        // Cache the prefab's normal color the first time
        if (cachedNormalColor == null)
            cachedNormalColor = colors.normalColor;

        // If assigned, use selectedColor as normalColor so it stays selected visually
        // Use JSON data to determine if this component instance is assigned to any tank slot
        bool isAssigned = false;        string assignedTankName = "";
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI != null && !isShopView)
        {
            foreach (var slot in workshopUI.tankSlots)
            {
                var slotData = TankSlotJsonManager.Instance.GetTankSlot(slot.slotIndex);
                if (slotData != null)
                {
                    // Check assignment based on component type
                    ComponentData assigned = null;
                    
                    if (data.category == ComponentCategory.AITree && data is AiTreeAsset aiAsset)
                    {
                        // For AI trees, check the specific branch type
                        assigned = slot.GetAIComponentByBranchType(aiAsset.branchType);
                    }
                    else
                    {
                        // For other components, use the regular category check
                        assigned = slot.GetComponentByCategory(data.category);
                    }
                    
                    if (assigned != null && assigned.instanceId == data.instanceId)
                    {
                        isAssigned = true;
                        assignedTankName = slot.TankName;
                        break;
                    }
                }
            }
        }
        // Always show the tank slot label if assigned, otherwise blank
        if (isAssigned)
        {
            // Find the slot button label for the assigned tank
            assignedToText.text = assignedTankName;
        }
        else
        {
            assignedToText.text = "";
        }
        if (isAssigned)
        {
            colors.normalColor = selectedColor;
            equipButton.colors = colors;
        }
        else
        {
            colors.normalColor = cachedNormalColor.Value;
            equipButton.colors = colors;
        }        equipButton.interactable = true;

        // Initialize color slider and manage visibility based on view
        if (colorSlider != null)
        {
            // Hide color slider and its preview in shop view, only show in inventory view
            colorSlider.gameObject.SetActive(!isShopView);
            if (colorSlider.colorPreview != null)
                colorSlider.colorPreview.gameObject.SetActive(!isShopView);
            
            colorSlider.SetColor(data.customColor);
            colorSlider.onColorChanged = (color) => {
                data.customColor = color;
                onColorChanged?.Invoke(data);
            };
        }
        
        // Load skin/decal from ComponentCustomizationManager and set on component
        if (!isShopView && ComponentCustomizationManager.Instance != null)
        {
            data.skinPath = ComponentCustomizationManager.Instance.GetSkin(data.instanceId) ?? "";
            data.decalPath = ComponentCustomizationManager.Instance.GetDecal(data.instanceId) ?? "";
        }
        // No model preview to update here
    }
    
    /// <summary>
    /// Opens the skin selection panel
    /// </summary>
    private void OpenSkinPanel(ComponentData data, System.Action<ComponentData, string> onSkinSelected)
    {
        if (isPanelOpen)
            CloseSkinDecalPanel();
            
        Debug.Log($"Opening skin panel for {data.title}");
        
        ShowSkinDecalView(false, data, onSkinSelected); // false = skin, not decal
    }
    
    /// <summary>
    /// Opens the decal selection panel
    /// </summary>
    private void OpenDecalPanel(ComponentData data, System.Action<ComponentData, string> onDecalSelected)
    {
        if (isPanelOpen)
            CloseSkinDecalPanel();
            
        Debug.Log($"Opening decal panel for {data.title}");
        
        ShowSkinDecalView(true, data, onDecalSelected); // true = decal, not skin
    }
    
    /// <summary>
    /// Closes the skin/decal panel
    /// </summary>
    private void CloseSkinDecalPanel()
    {
        if (skinDecalScrollView != null)
            skinDecalScrollView.SetActive(false);
            
        // Clear all instantiated buttons
        if (contentParent != null)
        {
            foreach (Transform child in contentParent)
            {
                Destroy(child.gameObject);
            }
        }
        
        // Stop click-outside detection
        if (clickOutsideCoroutine != null)
        {
            StopCoroutine(clickOutsideCoroutine);
            clickOutsideCoroutine = null;
        }
        
        isPanelOpen = false;
    }
    
    /// <summary>
    /// Shows the skin/decal scroll view and populates it with images
    /// </summary>
    private void ShowSkinDecalView(bool isDecal, ComponentData data, System.Action<ComponentData, string> onSelected)
    {
        if (skinDecalScrollView == null || contentParent == null || imageButtonPrefab == null)
        {
            Debug.LogError("ComponentEntryUI: skinDecalScrollView, contentParent, or imageButtonPrefab not assigned!");
            return;
        }
        
        // Clear any existing buttons
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
        
        // Show the scroll view
        skinDecalScrollView.SetActive(true);
        isPanelOpen = true;
        
        // Start click-outside detection immediately (even if no assets)
        if (clickOutsideCoroutine != null)
            StopCoroutine(clickOutsideCoroutine);
        clickOutsideCoroutine = StartCoroutine(WaitForClickOutside());
        
        // Load images from Resources
        string resourcePath = isDecal ? "KritaArt/Decals" : "KritaArt/Skins";
        Debug.Log($"Loading assets from Resources/{resourcePath}");
        
        // Try loading as both Texture2D and Sprite
        Texture2D[] allTextures = Resources.LoadAll<Texture2D>(resourcePath);
        Sprite[] allSprites = Resources.LoadAll<Sprite>(resourcePath);
        
        // For skins, filter out textures that are in subfolders (additional map textures)
        // We only want root-level textures, not the ones in folders named after the base textures
        // Strategy: textures in subfolders will contain keywords like "disp", "rough", "nor" in their names
        System.Collections.Generic.List<Texture2D> textures = new System.Collections.Generic.List<Texture2D>();
        System.Collections.Generic.List<Sprite> sprites = new System.Collections.Generic.List<Sprite>();
        
        if (!isDecal)
        {
            // For skins: filter out additional map textures (those with disp, rough, nor in their names)
            // These are textures stored in subfolders for the additional material maps
            foreach (var tex in allTextures)
            {
                string nameLower = tex.name.ToLower();
                // Skip textures that are additional maps (contain keywords for height, roughness, normal)
                if (nameLower.Contains("disp") || nameLower.Contains("rough") || nameLower.Contains("nor"))
                {
                    Debug.Log($"Skipping additional map texture: {tex.name}");
                    continue;
                }
                textures.Add(tex);
            }
            
            Debug.Log($"Found {allTextures.Length} total textures, filtered to {textures.Count} root-level skins");
        }
        else
        {
            // For decals, include all textures
            textures.AddRange(allTextures);
            sprites.AddRange(allSprites);
        }
        
        Debug.Log($"Found {textures.Count} textures and {sprites.Count} sprites for display");
        
        bool hasAssets = (textures != null && textures.Count > 0) || (sprites != null && sprites.Count > 0);
        
        // Show/hide empty folder message
        if (emptyFolderText != null)
            emptyFolderText.gameObject.SetActive(!hasAssets);
            
        if (!hasAssets)
        {
            Debug.LogWarning($"No assets found in {resourcePath}");
            // Don't return early - let the click-outside coroutine handle closing
            return;
        }
        
        // Create "None" button (blank)
        CreateImageButton(null, null, data, "", onSelected, isDecal);
        
        // Create buttons for textures
        if (textures != null)
        {
            foreach (var tex in textures)
            {
                CreateImageButton(tex, null, data, $"{resourcePath}/{tex.name}", onSelected, isDecal);
            }
        }
        
        // Create buttons for sprites if no textures
        if ((textures == null || textures.Count == 0) && sprites != null)
        {
            foreach (var sprite in sprites)
            {
                CreateImageButton(null, sprite, data, $"{resourcePath}/{sprite.name}", onSelected, isDecal);
            }
        }
    }
    
    /// <summary>
    /// Creates an image button for skin/decal selection
    /// </summary>
    private void CreateImageButton(Texture2D texture, Sprite sprite, ComponentData data, string path, 
                                   System.Action<ComponentData, string> onSelected, bool isDecal)
    {
        GameObject buttonGO = Instantiate(imageButtonPrefab, contentParent);
        
        // Set image
        RawImage rawImg = buttonGO.GetComponent<RawImage>();
        Image img = buttonGO.GetComponent<Image>();
        
        if (texture == null && sprite == null)
        {
            // This is the "None" button - make it blank/light gray
            if (rawImg != null)
            {
                rawImg.texture = null;
                rawImg.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            }
            if (img != null)
            {
                img.sprite = null;
                img.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            }
        }
        else if (texture != null)
        {
            if (rawImg != null)
            {
                rawImg.texture = texture;
                rawImg.color = Color.white;
            }
            else if (img != null)
            {
                // Convert texture to sprite
                Sprite newSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                img.sprite = newSprite;
                img.color = Color.white;
            }
        }
        else if (sprite != null && img != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
        }
        
        // Setup button click
        Button btn = buttonGO.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => {
                Debug.Log($"Selected {(isDecal ? "decal" : "skin")}: {(string.IsNullOrEmpty(path) ? "None" : path)}");
                onSelected?.Invoke(data, path);
                CloseSkinDecalPanel();
            });
        }
    }
    
    /// <summary>
    /// Coroutine that detects clicks outside the scroll view to close it
    /// </summary>
    private System.Collections.IEnumerator WaitForClickOutside()
    {
        yield return null; // Wait one frame
        
        while (isPanelOpen)
        {
            if (UnityEngine.InputSystem.Mouse.current != null && 
                UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                
                // Check if click is outside the scroll view
                RectTransform scrollRect = skinDecalScrollView.GetComponent<RectTransform>();
                if (scrollRect != null)
                {
                    bool isInside = RectTransformUtility.RectangleContainsScreenPoint(scrollRect, mousePos, null);
                    
                    if (!isInside)
                    {
                        Debug.Log("Clicked outside scroll view - closing");
                        CloseSkinDecalPanel();
                        yield break;
                    }
                }
            }
            
            yield return null;
        }
    }
    
    private void OnDisable()
    {
        // Clean up panel when this entry is disabled/destroyed
        CloseSkinDecalPanel();
    }
}