using UnityEngine;
using UnityEngine.UI;

public class UILineConnector : MonoBehaviour
{
    public RectTransform outputRect; // The output button/node
    public RectTransform inputRect;  // The input button/node
    public Canvas canvas;

    private RectTransform contentRect; // The Content panel RectTransform

    void Awake()
    {
        // Find the Content RectTransform in parent hierarchy
        Transform t = transform;
        while (t != null && t.name != "Content") t = t.parent;
        if (t != null && t.GetComponent<RectTransform>() != null)
            contentRect = t.GetComponent<RectTransform>();
        else
            Debug.LogError("UILineConnector: Could not find Content RectTransform in parent hierarchy.");
    }

    // Call this to update the line's position and rotation
    public void UpdateLine()
    {
        if (outputRect == null || inputRect == null || contentRect == null) return;
        Vector2 start, end;
        // Always use Content panel as reference
        Camera cam = null; // Content is in ScreenSpaceOverlay
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contentRect,
            RectTransformUtility.WorldToScreenPoint(cam, outputRect.position),
            cam,
            out start);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contentRect,
            RectTransformUtility.WorldToScreenPoint(cam, inputRect.position),
            cam,
            out end);
        RectTransform lineRect = GetComponent<RectTransform>();
        lineRect.anchoredPosition = start;
        Vector2 diff = end - start;
        lineRect.sizeDelta = new Vector2(diff.magnitude, lineRect.sizeDelta.y);
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
    }
}
