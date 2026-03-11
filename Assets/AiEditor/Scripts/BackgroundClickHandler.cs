using UnityEngine;
using UnityEngine.EventSystems;

public class BackgroundClickHandler : MonoBehaviour, IPointerDownHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // Only respond to left-click — right-click is handled by NodeSelectionManager
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // Hide any active node UI when clicking the background
        NodeDeleteUI.HideAllActiveUI();

        // Clear multi-select when clicking the background
        NodeSelectionManager.StaticClearSelection();
    }
}