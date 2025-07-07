using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ActiveSlotButtonUI : MonoBehaviour
{
    public Button button;
    public bool isActive = false;
    public TankSlotButtonUI slotButtonUI; // Assign in Inspector or via code

    void Start()
    {
        if (button == null) button = GetComponent<Button>();
        
        // Sync isActive with the JSON data's isActive value
        if (slotButtonUI != null)
        {
            var tankSlotJsonManager = FindFirstObjectByType<TankSlotJsonManager>();
            if (tankSlotJsonManager != null)
            {
                var slotData = tankSlotJsonManager.GetTankSlot(slotButtonUI.slotIndex);
                if (slotData != null)
                {
                    isActive = slotData.isActive;
                    UpdateColor();
                }
            }
        }
        
        button.onClick.AddListener(ToggleActive);
        UpdateColor();
    }

    public void ToggleActive()
    {
        isActive = !isActive;
        UpdateColor();
        // Update the ScriptableObject's isActive field
        if (slotButtonUI != null)
            slotButtonUI.SetActive(isActive);
        
        // Save the updated activation state to PlayerData
        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.SavePlayerData();
            Debug.Log($"[ActiveSlotButtonUI] Toggled {slotButtonUI?.TankName} active state to {isActive} and saved to PlayerData");
        }
        
        // Force the button to lose focus so it doesn't show the selected color highlight
        EventSystem.current.SetSelectedGameObject(null);
    }

    public void SetActive(bool value)
    {
        isActive = value;
        UpdateColor();
        // Update the JSON data's isActive field when called externally
        if (slotButtonUI != null)
            slotButtonUI.SetActive(isActive);
    }

    void UpdateColor()
    {
        var colors = button.colors;
        if (isActive) {
            colors.normalColor = colors.highlightedColor;
        } else {
            colors.normalColor = colors.disabledColor;
        }
        button.colors = colors;
        // Force the button to refresh its visual state
        var selectable = button as Selectable;
        if (selectable != null && !button.interactable) {
            selectable.OnDeselect(null);
        }
    }
}
