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

    public System.Action<ComponentData> onSelected; // Set by manager

    private ComponentData component;
    private Color? cachedNormalColor = null;    public RainbowColorSlider colorSlider; // Assign in inspector

    public void Setup(ComponentData data, string assignedToTank, bool isShopView,
                      System.Action<ComponentData> onBuy,
                      System.Action<ComponentData> onSell,
                      System.Action<ComponentData> onEquip,
                      System.Action<ComponentData> onSelect = null,
                      System.Action<ComponentData> onColorChanged = null)
    {
        component = data;
        titleText.text = data.title;
        costText.text = $"Cost: {data.cost}";
        assignedToText.text = assignedToTank;        buyButton.gameObject.SetActive(isShopView);
        sellButton.gameObject.SetActive(!isShopView);
        equipButton.gameObject.SetActive(!isShopView);

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
        // Use ScriptableObject data to determine if this component instance is assigned to any tank slot
        bool isAssigned = false;        string assignedTankName = "";
        var workshopUI = FindFirstObjectByType<WorkshopUIManager>();
        if (workshopUI != null && !isShopView)
        {
            foreach (var slot in workshopUI.tankSlots)
            {
                if (slot.slotData != null)
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
        // No model preview to update here
    }
}