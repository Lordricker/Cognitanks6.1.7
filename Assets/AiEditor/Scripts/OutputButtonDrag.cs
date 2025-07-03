using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class OutputButtonDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("Prefabs & References")]
    public GameObject UILinePrefab;
    public GameObject TempUILinePrefab;
    public GameObject ContextMenuUIPrefab;
    public Canvas UICanvas; // Assign at runtime

    private GameObject currentLine;
    // Make these static so all OutputButtonDrag instances share them
    private static GameObject currentMenu;
    public static GameObject currentTempLine; // Made public so ContextMenuUI can access it
    private RectTransform lineRect;
    private Vector2 dragStartPos;
    private bool isDragging = false;
    // Store the content panel for coordinate math
    private RectTransform contentRect;

    [Header("Origin Output Buttons")]
    public Button turretOutputButton;
    public Button navOutputButton;
    public GameObject turretLabel; // Assign in Inspector
    public GameObject navLabel;    // Assign in Inspector
    public enum BranchType { None, Turret, Nav }
    public BranchType branchType = BranchType.None;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (TempUILinePrefab == null) return;
        // Assign UICanvas at runtime if not set
        if (UICanvas == null)
            UICanvas = GetComponentInParent<Canvas>();
        if (UICanvas == null) UICanvas = Object.FindFirstObjectByType<Canvas>();
        if (UICanvas == null) return;
        isDragging = true;
        // Destroy previous temp line and context menu if they exist
        if (currentTempLine != null)
        {
            Destroy(currentTempLine);
            currentTempLine = null;
        }
        if (currentMenu != null)
        {
            Destroy(currentMenu);
            currentMenu = null;
        }
        // Get the Content panel for coordinate math
        Transform parentTransform = UICanvas.transform;
        contentRect = UICanvas.transform as RectTransform;
        var background = UICanvas.transform.Find("Background");
        if (background != null)
        {
            var content = background.Find("Content");
            if (content != null)
            {
                parentTransform = content;
                contentRect = content.GetComponent<RectTransform>();
            }
        }
        // Get the button's world position (center)
        RectTransform buttonRect = (RectTransform)transform;
        Vector3[] worldCorners = new Vector3[4];
        buttonRect.GetWorldCorners(worldCorners);
        Vector3 buttonCenterWorld = (worldCorners[0] + worldCorners[2]) * 0.5f;
        Camera cam = UICanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : UICanvas.worldCamera;
        Vector2 buttonCenterCanvas;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contentRect,
            RectTransformUtility.WorldToScreenPoint(cam, buttonCenterWorld),
            cam,
            out buttonCenterCanvas);
        dragStartPos = buttonCenterCanvas;
        currentTempLine = Instantiate(TempUILinePrefab, parentTransform);
        lineRect = currentTempLine.GetComponent<RectTransform>();
        UpdateLine(dragStartPos, ScreenToCanvasLocal(eventData.position));

        // Hide the unused origin output button if this is an origin output
        if (turretOutputButton != null && navOutputButton != null)
        {
            if (gameObject == turretOutputButton.gameObject)
            {
                navOutputButton.gameObject.SetActive(false);
                if (navLabel != null) navLabel.SetActive(false);
                branchType = BranchType.Turret;
            }
            else if (gameObject == navOutputButton.gameObject)
            {
                turretOutputButton.gameObject.SetActive(false);
                if (turretLabel != null) turretLabel.SetActive(false);
                branchType = BranchType.Nav;
            }
        }        // --- NEW: If this is StartNavButton or StartTurretButton, set branchType accordingly ---
        if (gameObject.name == "StartNavButton")
        {
            branchType = BranchType.Nav;
            Debug.Log($"OutputButtonDrag OnPointerDown: Set branchType to Nav for {gameObject.name}");
            // Also set it on the NodeDraggable component if present
            var nodeDraggable = GetComponentInParent<NodeDraggable>();
            if (nodeDraggable != null)
            {
                nodeDraggable.SetBranchType(BranchType.Nav);
                Debug.Log($"OutputButtonDrag OnPointerDown: Set NodeDraggable branchType to Nav for {gameObject.name}");
            }
        }
        else if (gameObject.name == "StartTurretButton")
        {
            branchType = BranchType.Turret;
            Debug.Log($"OutputButtonDrag OnPointerDown: Set branchType to Turret for {gameObject.name}");
            // Also set it on the NodeDraggable component if present
            var nodeDraggable = GetComponentInParent<NodeDraggable>();
            if (nodeDraggable != null)
            {
                nodeDraggable.SetBranchType(BranchType.Turret);
                Debug.Log($"OutputButtonDrag OnPointerDown: Set NodeDraggable branchType to Turret for {gameObject.name}");
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || currentTempLine == null) return;
        UpdateLine(dragStartPos, ScreenToCanvasLocal(eventData.position));
    }    // Helper: Trace back to the origin node to determine branch type
    private BranchType GetBranchTypeFromNode()
    {
        // Try to find a UILineConnector where this node's input is connected from another node
        var nodeDraggable = GetComponentInParent<NodeDraggable>();
        if (nodeDraggable == null) return BranchType.None;
        
        // First check if this node already has a branchType set
        if (nodeDraggable.branchType != BranchType.None)
            return nodeDraggable.branchType;
        
        // Find all lines in the Content panel
        var content = UICanvas.transform.Find("Background/Content");
        if (content == null) content = UICanvas.transform;
        var lines = content.GetComponentsInChildren<UILineConnector>();
        
        // Look for lines that connect TO this node (inputRect points to this node)
        foreach (var line in lines)
        {
            // Check if this node is the input (receiving end) of any line
            var inputButtons = nodeDraggable.GetComponentsInChildren<Button>();
            foreach (var inputBtn in inputButtons)
            {
                if (inputBtn.CompareTag("InputPort") && line.inputRect == inputBtn.GetComponent<RectTransform>())
                {
                    // Found a line connecting TO this node, now check where it comes from
                    if (line.outputRect != null)
                    {
                        var sourceNodeDraggable = line.outputRect.GetComponentInParent<NodeDraggable>();
                        if (sourceNodeDraggable != null && sourceNodeDraggable.branchType != BranchType.None)
                            return sourceNodeDraggable.branchType;
                        
                        // If source node doesn't have branchType, check if it's a start button
                        var sourceButton = line.outputRect.GetComponent<Button>();
                        if (sourceButton != null)
                        {
                            if (sourceButton.gameObject.name == "StartNavButton" || sourceButton.CompareTag("NavOrigin"))
                                return BranchType.Nav;
                            if (sourceButton.gameObject.name == "StartTurretButton" || sourceButton.CompareTag("TurretOrigin"))
                                return BranchType.Turret;
                        }
                    }
                }
            }
        }
        
        // Fallback: check if this node is directly the StartNavButton or StartTurretButton
        if (gameObject.name == "StartNavButton" || gameObject.CompareTag("NavOrigin"))
            return BranchType.Nav;
        if (gameObject.name == "StartTurretButton" || gameObject.CompareTag("TurretOrigin"))
            return BranchType.Turret;
        return BranchType.None;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;
        // Destroy previous context menu if it exists
        if (currentMenu != null)
        {
            Destroy(currentMenu);
            currentMenu = null;
        }
        // Check if pointer is over an input button BEFORE context menu instantiation
        GameObject hoveredObj = eventData.pointerCurrentRaycast.gameObject;
        if (hoveredObj != null && hoveredObj.CompareTag("InputPort"))
        {
            // Draw permanent UILine from origin output to this input button
            Button inputButton = hoveredObj.GetComponent<Button>();
            if (inputButton != null)
            {
                Vector3 inputWorld = inputButton.transform.position;
                Vector2 inputCanvas;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    contentRect,
                    RectTransformUtility.WorldToScreenPoint(UICanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : UICanvas.worldCamera, inputWorld),
                    UICanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : UICanvas.worldCamera,
                    out inputCanvas);
                // Find the Content panel as the parent for all instantiations
                Transform parentTransform = contentRect.transform;
                GameObject permLine = Instantiate(UILinePrefab, parentTransform);
                RectTransform permLineRect = permLine.GetComponent<RectTransform>();
                permLineRect.anchoredPosition = dragStartPos;
                Vector2 diff = inputCanvas - dragStartPos;
                permLineRect.sizeDelta = new Vector2(diff.magnitude, permLineRect.sizeDelta.y);
                float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
                permLineRect.localRotation = Quaternion.Euler(0, 0, angle);
                // Add click-to-delete functionality
                var lineDeleter = permLine.AddComponent<UILineClickDeleter>();
                // Add connector for dynamic updating
                var connector = permLine.AddComponent<UILineConnector>();
                connector.outputRect = ((RectTransform)transform); // Output button
                connector.inputRect = inputButton.GetComponent<RectTransform>();
                connector.canvas = UICanvas;                // Register this line with both nodes for drag updates
                NodeDraggable outputDraggable = GetComponentInParent<NodeDraggable>();
                NodeDraggable inputDraggable = inputButton.GetComponentInParent<NodeDraggable>();
                if (outputDraggable != null) outputDraggable.RegisterConnectedLine(connector);
                if (inputDraggable != null) inputDraggable.RegisterConnectedLine(connector);                // Propagate branch type from output node to input node
                if (outputDraggable != null && inputDraggable != null)
                {
                    if (outputDraggable.branchType != BranchType.None)
                        inputDraggable.SetBranchType(outputDraggable.branchType);
                    else
                    {
                        // If output node doesn't have branch type, try to determine it
                        BranchType determinedType = GetBranchTypeFromNode();
                        if (determinedType != BranchType.None)
                        {
                            outputDraggable.SetBranchType(determinedType);
                            inputDraggable.SetBranchType(determinedType);
                        }
                    }
                }
                
                // Propagate target flags when connection is made
                var outputTargetHandler = outputDraggable?.GetComponent<TargetDebugHandler>();
                var inputTargetHandler = inputDraggable?.GetComponent<TargetDebugHandler>();
                
                if (outputTargetHandler != null && inputTargetHandler != null)
                {
                    outputTargetHandler.OnConnectionChanged();
                    inputTargetHandler.OnConnectionChanged();
                }
            }
            // Destroy temp line
            if (currentTempLine != null)
            {
                Destroy(currentTempLine);
                currentTempLine = null;
            }
            lineRect = null;
            return;
        }
        // Do NOT destroy the temp line here; leave it until next OnPointerDown
        // Instantiate ContextMenuUI at the mouse position
        if (ContextMenuUIPrefab != null && UICanvas != null)
        {
            Vector2 spawnPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                contentRect,
                eventData.position,
                UICanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : UICanvas.worldCamera,
                out spawnPos);
            // Find the Content panel as the parent for all instantiations
            Transform parentTransform = contentRect.transform;
            currentMenu = Instantiate(ContextMenuUIPrefab, parentTransform);
            var menuRect = currentMenu.GetComponent<RectTransform>();
            menuRect.anchoredPosition = spawnPos;            // Assign the canvas reference at runtime
            var menuScript = currentMenu.GetComponent<ContextMenuUI>();
            if (menuScript != null)
            {
                menuScript.UICanvasObj = UICanvas;                // Use branchType from the node you are dragging from, if available
                var nodeDraggable = GetComponentInParent<NodeDraggable>();
                Debug.Log($"OutputButtonDrag OnPointerUp: Current branchType before check: {branchType} for {gameObject.name}");
                if (nodeDraggable != null && nodeDraggable.branchType != BranchType.None)
                {
                    branchType = nodeDraggable.branchType;
                    Debug.Log($"OutputButtonDrag OnPointerUp: Using NodeDraggable branchType: {branchType}");
                }
                else if (branchType == BranchType.None)
                {
                    // Only try to determine it by tracing back if we don't already have it set
                    branchType = GetBranchTypeFromNode();
                    Debug.Log($"OutputButtonDrag OnPointerUp: Traced branchType: {branchType}");
                }
                
                Debug.Log($"OutputButtonDrag: Setting context menu branch type to {branchType} from gameObject {gameObject.name}");
                menuScript.SetOutputButtonInfo(dragStartPos, this, (ContextMenuUI.BranchType)(int)branchType); // Pass branchType so context menu disables correct button
            }
        }
        // If a node was created, keep only the used output button visible
        // If drag was cancelled (no node created), show both again
        if (turretOutputButton != null && navOutputButton != null)
        {
            // If context menu is open, keep only the used button visible
            if (currentMenu != null)
            {
                if (branchType == BranchType.Turret)
                {
                    navOutputButton.gameObject.SetActive(false);
                    if (navLabel != null) navLabel.SetActive(false);
                }
                else if (branchType == BranchType.Nav)
                {
                    turretOutputButton.gameObject.SetActive(false);
                    if (turretLabel != null) turretLabel.SetActive(false);
                }
            }
            else // If no context menu, show both
            {
                turretOutputButton.gameObject.SetActive(true);
                navOutputButton.gameObject.SetActive(true);
                if (turretLabel != null) turretLabel.SetActive(true);
                if (navLabel != null) navLabel.SetActive(true);
                branchType = BranchType.None;
            }
        }
        lineRect = null;
    }

    private Vector2 ScreenToCanvasLocal(Vector2 screenPos)
    {
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contentRect,
            screenPos,
            UICanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : UICanvas.worldCamera,
            out localPos);
        return localPos;
    }

    private void UpdateLine(Vector2 startLocal, Vector2 endLocal)
    {
        if (lineRect == null) return;
        Vector2 diff = endLocal - startLocal;
        float length = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        lineRect.anchoredPosition = startLocal;
        lineRect.sizeDelta = new Vector2(length, lineRect.sizeDelta.y);
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
    }
}

/*
Remove assignment in the inspector for UICanvas if this is a prefab.
Instead, set it at runtime from the script that instantiates OutputButtonDrag.
For example:
outputButtonDragInstance.UICanvas = FindObjectOfType<Canvas>();
*/
