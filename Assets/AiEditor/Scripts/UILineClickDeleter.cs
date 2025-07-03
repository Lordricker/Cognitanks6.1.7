using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

// Attach this to any permanent UI line to allow click-to-delete
public class UILineClickDeleter : MonoBehaviour, IPointerClickHandler
{    public void OnPointerClick(PointerEventData eventData)
    {
        Destroy(gameObject);
    }

    void Start()
    {
        // Ensure a GraphicRaycaster is present on the Canvas for UI clicks
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        // Ensure a CanvasGroup and Image for raycast target
        var img = GetComponent<UnityEngine.UI.Image>();
        if (img == null)
        {
            img = gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0,0,0,0); // invisible but raycastable
        }
        img.raycastTarget = true;
    }
}
