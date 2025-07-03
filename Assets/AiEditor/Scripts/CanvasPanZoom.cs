using UnityEngine;
using UnityEngine.EventSystems;

public class CanvasPanZoom : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
{
    public RectTransform content; // The root RectTransform containing all nodes/lines
    public float panSpeed = 1.0f;
    public float zoomSpeed = 0.1f;
    public float minZoom = 0.5f;
    public float maxZoom = 2.5f;

    private Vector2 lastPointerPos;
    private bool isPanning = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        // Only pan if not clicking a node or button
        if (eventData.pointerEnter == gameObject)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content.parent as RectTransform, eventData.position, eventData.pressEventCamera, out lastPointerPos);
            isPanning = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isPanning) return;
        Vector2 pointerPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content.parent as RectTransform, eventData.position, eventData.pressEventCamera, out pointerPos);
        Vector2 delta = pointerPos - lastPointerPos;
        content.anchoredPosition += delta * panSpeed;
        lastPointerPos = pointerPos;
        // Clamp content position so it can't be dragged outside the parent
        ClampContentPosition();
    }

    public void OnScroll(PointerEventData eventData)
    {
        // Mouse wheel zoom centered on mouse
        Vector2 mousePosBefore, mousePosAfter;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, eventData.position, eventData.pressEventCamera, out mousePosBefore);
        float prevScale = content.localScale.x;
        float scroll = eventData.scrollDelta.y;
        float scale = prevScale + scroll * zoomSpeed;
        scale = Mathf.Clamp(scale, minZoom, maxZoom);
        if (Mathf.Approximately(scale, prevScale)) return;
        content.localScale = new Vector3(scale, scale, 1);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, eventData.position, eventData.pressEventCamera, out mousePosAfter);
        Vector2 delta = mousePosAfter - mousePosBefore;
        content.anchoredPosition += (Vector2)content.transform.TransformVector(delta);
    }

    void Update()
    {
        // Mobile pinch zoom centered between fingers
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);
            Vector2 prevTouch0 = touch0.position - touch0.deltaPosition;
            Vector2 prevTouch1 = touch1.position - touch1.deltaPosition;
            float prevDist = (prevTouch0 - prevTouch1).magnitude;
            float currDist = (touch0.position - touch1.position).magnitude;
            float deltaMag = currDist - prevDist;
            float prevScale = content.localScale.x;
            float scale = prevScale + (deltaMag * zoomSpeed * 0.01f);
            scale = Mathf.Clamp(scale, minZoom, maxZoom);
            if (Mathf.Approximately(scale, prevScale)) return;
            Vector2 screenMid = (touch0.position + touch1.position) * 0.5f;
            Vector2 localMidBefore, localMidAfter;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenMid, null, out localMidBefore);
            content.localScale = new Vector3(scale, scale, 1);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenMid, null, out localMidAfter);
            Vector2 delta = localMidAfter - localMidBefore;
            content.anchoredPosition += (Vector2)content.transform.TransformVector(delta);
        }
    }

    private void ClampContentPosition()
    {
        RectTransform parentRect = content.parent as RectTransform;
        if (parentRect == null) return;
        Vector2 parentSize = parentRect.rect.size;
        Vector2 contentSize = content.rect.size * content.localScale;
        // Assume pivot is (0.5, 0.5) for both content and parent
        float clampX = (contentSize.x - parentSize.x) * 0.5f;
        float clampY = (contentSize.y - parentSize.y) * 0.5f;
        // If content is smaller than parent, center it
        float minX = -clampX, maxX = clampX;
        float minY = -clampY, maxY = clampY;
        if (contentSize.x <= parentSize.x) minX = maxX = 0f;
        if (contentSize.y <= parentSize.y) minY = maxY = 0f;
        Vector2 clamped = content.anchoredPosition;
        clamped.x = Mathf.Clamp(clamped.x, minX, maxX);
        clamped.y = Mathf.Clamp(clamped.y, minY, maxY);
        content.anchoredPosition = clamped;
    }

    void OnDisable()
    {
        isPanning = false;
    }
}
