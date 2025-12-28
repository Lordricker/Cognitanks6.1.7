using UnityEngine;
using UnityEngine.EventSystems;

public class BackgroundClickHandler : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        // Hide any active node UI when clicking the background
        NodeDeleteUI.HideAllActiveUI();
    }
}