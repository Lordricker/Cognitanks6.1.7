using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Handles multi-select (left-click drag rectangle), group move (drag bounding box),
/// copy/paste (Ctrl+C/V or buttons), and delete-all for the AI node editor.
/// All mouse input is polled in Update() to avoid EventSystem routing issues.
/// </summary>
public class NodeSelectionManager : MonoBehaviour
{
    [Header("References")]
    public RectTransform content; // The Content panel containing all nodes/lines
    public Button copyButton;     // Pre-placed hidden button — shown when selection active
    public Button deleteAllButton; // Pre-placed hidden button — shown when selection active
    public GameObject pasteButtonPrefab; // Prefab instantiated at cursor on right-click (no drag)
    public GameObject ContextMenuUIPrefab; // Same prefab OutputButtonDrag uses
    public Canvas UICanvasObj; // Reference to the canvas

    [Header("Node Prefabs (for paste)")]
    public GameObject MiddleNodePrefab;
    public GameObject EndNodePrefab;
    public GameObject SubAINodePrefab;
    public GameObject UILinePrefab;

    [Header("Selection Rect Style")]
    public Color selectionRectColor = new Color(0.2f, 0.5f, 1f, 0.25f);
    public Color selectionBorderColor = Color.white;
    public Color boundingBoxColor = new Color(0.2f, 0.5f, 1f, 0.15f);
    public Color boundingBoxBorderColor = new Color(0.4f, 0.7f, 1f, 0.6f);

    // Selection state
    private HashSet<NodeDraggable> selectedNodes = new HashSet<NodeDraggable>();
    private List<UILineConnector> selectedLines = new List<UILineConnector>();

    // Drag-to-select state
    private Vector2 dragStartLocal;  // In content-local space
    private bool isDragSelecting = false;
    private const float dragBuffer = 10f; // pixels in screen space before selection starts
    private GameObject selectionRectGO;
    private RectTransform selectionRectRT;
    private Image selectionRectImage;

    // Bounding box state
    private GameObject boundingBoxGO;
    private RectTransform boundingBoxRT;
    private bool isDraggingBox = false;
    private Vector2 boxDragLastLocal;
    private float gridSize = 32f;

    // Clipboard
    private List<ClipboardNode> clipboard = new List<ClipboardNode>();
    private List<ClipboardConnection> clipboardConnections = new List<ClipboardConnection>();

    // Paste button instance
    private GameObject pasteButtonInstance;

    // Singleton-like access for other scripts to call ClearSelection
    private static NodeSelectionManager _instance;
    public static NodeSelectionManager Instance => _instance;

    void Awake()
    {
        _instance = this;

        if (copyButton != null)
        {
            copyButton.gameObject.SetActive(false);
            copyButton.onClick.AddListener(CopySelection);
        }
        if (deleteAllButton != null)
        {
            deleteAllButton.gameObject.SetActive(false);
            deleteAllButton.onClick.AddListener(DeleteSelection);
        }
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ─────────────────────────── UPDATE (all input polling) ───────────────────────────

    // Track left-click for selection rect
    private bool leftMouseDown = false;
    private Vector2 leftDownScreenPos;

    // Track right-click for paste button
    private bool rightClickDown = false;
    private Vector2 rightClickStartScreen;

    // Input System references
    private Mouse mouse;
    private Keyboard keyboard;

    void Update()
    {
        if (mouse == null) mouse = Mouse.current;
        if (keyboard == null) keyboard = Keyboard.current;
        if (mouse == null) return;

        Vector2 mousePos = mouse.position.ReadValue();

        // ── Keyboard shortcuts ──
        if (keyboard != null)
        {
            bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;

            if (ctrl && keyboard.cKey.wasPressedThisFrame)
            {
                if (selectedNodes.Count > 0)
                    CopySelection();
            }

            if (ctrl && keyboard.vKey.wasPressedThisFrame)
            {
                if (clipboard.Count > 0)
                {
                    Vector2 cursorLocal;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        content, mousePos, null, out cursorLocal);
                    PasteAtPosition(cursorLocal);
                }
            }
        }

        // ── Left mouse button: selection rectangle ──
        if (mouse.leftButton.wasPressedThisFrame)
        {
            // Destroy any open context menu immediately on pointer down
            DestroyContextMenu();

            if (IsPointerOverBackground(mousePos))
            {
                leftMouseDown = true;
                leftDownScreenPos = mousePos;
                isDragSelecting = false;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    content, mousePos, null, out dragStartLocal);

                Debug.Log($"[NodeSelectionManager] LMB down on background at screen {leftDownScreenPos}, local {dragStartLocal}");
            }
        }

        if (leftMouseDown && mouse.leftButton.isPressed)
        {
            Vector2 currentScreenPos = mousePos;
            float screenDist = Vector2.Distance(leftDownScreenPos, currentScreenPos);

            if (!isDragSelecting && screenDist >= dragBuffer)
            {
                isDragSelecting = true;
                ClearSelection();
                CreateSelectionRect();
                Debug.Log("[NodeSelectionManager] Started drawing selection rect");
            }

            if (isDragSelecting)
            {
                UpdateSelectionRect(currentScreenPos);
            }
        }

        if (leftMouseDown && mouse.leftButton.wasReleasedThisFrame)
        {
            leftMouseDown = false;

            if (isDragSelecting)
            {
                isDragSelecting = false;
                FinalizeSelection();
                Debug.Log("[NodeSelectionManager] Finalized selection");
            }
            else
            {
                // LMB click without drag on background → spawn context menu
                Debug.Log("[NodeSelectionManager] LMB released without drag — spawning context menu");
                SpawnContextMenu(mousePos);
            }
        }

        // ── Right-click (no drag) → paste button ──
        if (mouse.rightButton.wasPressedThisFrame)
        {
            rightClickDown = true;
            rightClickStartScreen = mousePos;
        }

        if (rightClickDown && mouse.rightButton.wasReleasedThisFrame)
        {
            rightClickDown = false;
            float dist = Vector2.Distance(rightClickStartScreen, mousePos);
            if (dist < dragBuffer && clipboard.Count > 0)
            {
                ShowPasteButton(mousePos);
            }
        }
    }

    /// <summary>
    /// Checks if the mouse is currently over the background (empty canvas area),
    /// not over a node, button, or other interactive UI element.
    /// </summary>
    private bool IsPointerOverBackground(Vector2 screenPos)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            Debug.LogWarning("[NodeSelectionManager] No EventSystem found!");
            return false;
        }

        var pointerData = new PointerEventData(eventSystem)
        {
            position = screenPos
        };

        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);

        if (results.Count == 0)
        {
            Debug.Log("[NodeSelectionManager] Raycast hit NOTHING — is there a GraphicRaycaster on the Canvas?");
            return false;
        }

        // Log all hits so we can see what's happening
        string hitNames = string.Join(", ", results.ConvertAll(r => r.gameObject.name));
        Debug.Log($"[NodeSelectionManager] Raycast hits: [{hitNames}]");

        // Check if any hit is a node, button, or the bounding box — if so, we're NOT on the background
        foreach (var result in results)
        {
            var go = result.gameObject;
            if (go.GetComponent<NodeDraggable>() != null) return false;
            if (go.GetComponentInParent<NodeDraggable>() != null) return false;
            if (go.GetComponent<Button>() != null) return false;
            if (go.GetComponent<BoundingBoxDragHandler>() != null) return false;
        }

        // If we got hits but none were nodes/buttons, we're on the background
        return true;
    }

    // ─────────────────────── SELECTION RECT (drawn during drag) ──────────────────────

    private void CreateSelectionRect()
    {
        selectionRectGO = new GameObject("SelectionRect", typeof(RectTransform), typeof(Image));
        selectionRectGO.transform.SetParent(content, false);
        selectionRectRT = selectionRectGO.GetComponent<RectTransform>();
        selectionRectRT.pivot = new Vector2(0, 1); // top-left pivot
        selectionRectImage = selectionRectGO.GetComponent<Image>();
        selectionRectImage.color = selectionRectColor;
        selectionRectImage.raycastTarget = false;

        // Add a border using Outline component
        var outline = selectionRectGO.AddComponent<Outline>();
        outline.effectColor = selectionBorderColor;
        outline.effectDistance = new Vector2(1, -1);

        // Bring to front
        selectionRectRT.SetAsLastSibling();
    }

    private void UpdateSelectionRect(Vector2 currentScreenPos)
    {
        if (selectionRectRT == null) return;

        Vector2 currentLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            content, currentScreenPos, null, out currentLocal);

        // Calculate rect corners (handle any drag direction)
        float minX = Mathf.Min(dragStartLocal.x, currentLocal.x);
        float maxX = Mathf.Max(dragStartLocal.x, currentLocal.x);
        float minY = Mathf.Min(dragStartLocal.y, currentLocal.y);
        float maxY = Mathf.Max(dragStartLocal.y, currentLocal.y);

        selectionRectRT.anchoredPosition = new Vector2(minX, maxY); // top-left with pivot (0,1)
        selectionRectRT.sizeDelta = new Vector2(maxX - minX, maxY - minY);
    }

    // ─────────────────────── FINALIZE SELECTION ───────────────────────

    private void FinalizeSelection()
    {
        if (selectionRectRT == null) return;

        // Get the selection rect bounds in content-local space
        Vector2 pos = selectionRectRT.anchoredPosition;
        Vector2 size = selectionRectRT.sizeDelta;
        // pos is top-left (pivot 0,1), so bounds are:
        float left = pos.x;
        float right = pos.x + size.x;
        float top = pos.y;
        float bottom = pos.y - size.y;
        Rect selectionBounds = Rect.MinMaxRect(left, bottom, right, top);

        // Find all nodes inside the rect
        selectedNodes.Clear();
        selectedLines.Clear();

        var allNodes = content.GetComponentsInChildren<NodeDraggable>();
        foreach (var node in allNodes)
        {
            Vector2 nodePos = node.GetComponent<RectTransform>().anchoredPosition;
            if (selectionBounds.Contains(nodePos))
            {
                selectedNodes.Add(node);
            }
        }

        if (selectedNodes.Count == 0)
        {
            // Nothing selected — destroy the rect
            Destroy(selectionRectGO);
            selectionRectGO = null;
            selectionRectRT = null;
            return;
        }

        // Find fully-internal lines (both ends belong to selected nodes)
        var allLines = content.GetComponentsInChildren<UILineConnector>();
        foreach (var line in allLines)
        {
            if (line.outputRect == null || line.inputRect == null) continue;

            var fromNode = line.outputRect.GetComponentInParent<NodeDraggable>();
            var toNode = line.inputRect.GetComponentInParent<NodeDraggable>();

            if (fromNode != null && toNode != null &&
                selectedNodes.Contains(fromNode) && selectedNodes.Contains(toNode))
            {
                selectedLines.Add(line);
            }
        }

        // ── Repurpose the selection rect as the bounding box ──
        // Enable raycast so it can receive drag events
        selectionRectImage.raycastTarget = true;

        // Move behind all nodes/lines (first child = renders behind everything else)
        selectionRectRT.SetAsFirstSibling();

        // Store as bounding box so existing drag infrastructure works
        boundingBoxGO = selectionRectGO;
        boundingBoxRT = selectionRectRT;

        // Clear selection rect references (now owned by bounding box vars)
        selectionRectGO = null;
        selectionRectRT = null;
        selectionRectImage = null;

        // Add drag handler for group move
        var dragHandler = boundingBoxGO.AddComponent<BoundingBoxDragHandler>();
        dragHandler.Init(this);

        // Show buttons
        if (copyButton != null) copyButton.gameObject.SetActive(true);
        if (deleteAllButton != null) deleteAllButton.gameObject.SetActive(true);
    }

    // ─────────────────────── BOUNDING BOX ───────────────────────

    private void CreateBoundingBox()
    {
        if (selectedNodes.Count == 0) return;

        // Calculate bounds of selected nodes
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var node in selectedNodes)
        {
            var rt = node.GetComponent<RectTransform>();
            Vector2 pos = rt.anchoredPosition;
            Vector2 halfSize = rt.sizeDelta * 0.5f;

            if (pos.x - halfSize.x < minX) minX = pos.x - halfSize.x;
            if (pos.x + halfSize.x > maxX) maxX = pos.x + halfSize.x;
            if (pos.y - halfSize.y < minY) minY = pos.y - halfSize.y;
            if (pos.y + halfSize.y > maxY) maxY = pos.y + halfSize.y;
        }

        float padding = 16f;
        minX -= padding;
        minY -= padding;
        maxX += padding;
        maxY += padding;

        // Create the bounding box GameObject
        boundingBoxGO = new GameObject("BoundingBox", typeof(RectTransform), typeof(Image));
        boundingBoxGO.transform.SetParent(content, false);
        boundingBoxRT = boundingBoxGO.GetComponent<RectTransform>();
        boundingBoxRT.pivot = new Vector2(0, 1); // top-left
        boundingBoxRT.anchoredPosition = new Vector2(minX, maxY);
        boundingBoxRT.sizeDelta = new Vector2(maxX - minX, maxY - minY);

        var image = boundingBoxGO.GetComponent<Image>();
        image.color = boundingBoxColor;
        image.raycastTarget = true; // Must be true so we can drag it

        var outline = boundingBoxGO.AddComponent<Outline>();
        outline.effectColor = boundingBoxBorderColor;
        outline.effectDistance = new Vector2(2, -2);

        // Add drag handler for group move
        var dragHandler = boundingBoxGO.AddComponent<BoundingBoxDragHandler>();
        dragHandler.Init(this);

        // Send to back so nodes render on top
        boundingBoxRT.SetAsFirstSibling();
    }

    /// <summary>
    /// Called by BoundingBoxDragHandler when drag starts on the bounding box.
    /// </summary>
    public void OnBoxDragBegin(PointerEventData eventData)
    {
        isDraggingBox = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            content, eventData.position, null, out boxDragLastLocal);
    }

    /// <summary>
    /// Called by BoundingBoxDragHandler each frame during drag.
    /// Moves all selected nodes + bounding box by delta.
    /// </summary>
    public void OnBoxDrag(PointerEventData eventData)
    {
        if (!isDraggingBox) return;

        Vector2 currentLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            content, eventData.position, null, out currentLocal);

        Vector2 delta = currentLocal - boxDragLastLocal;

        // Snap delta to grid
        Vector2 snappedDelta = new Vector2(
            Mathf.Round(delta.x / gridSize) * gridSize,
            Mathf.Round(delta.y / gridSize) * gridSize
        );

        if (snappedDelta == Vector2.zero) return;

        // Move all selected nodes
        foreach (var node in selectedNodes)
        {
            if (node == null) continue;
            var rt = node.GetComponent<RectTransform>();
            rt.anchoredPosition += snappedDelta;
        }

        // Move bounding box
        if (boundingBoxRT != null)
        {
            boundingBoxRT.anchoredPosition += snappedDelta;
        }

        // Update last tracked position by the snapped amount
        boxDragLastLocal += snappedDelta;

        // Update ALL lines connected to any selected node (not just internal ones)
        UpdateAllConnectedLines();
    }

    /// <summary>
    /// Called by BoundingBoxDragHandler when drag ends.
    /// </summary>
    public void OnBoxDragEnd(PointerEventData eventData)
    {
        isDraggingBox = false;

        // Auto-save
        var aiEditorFileUI = FindFirstObjectByType<AiEditorFileUI>();
        if (aiEditorFileUI != null)
            aiEditorFileUI.AutoSave();
    }

    private void UpdateAllConnectedLines()
    {
        foreach (var node in selectedNodes)
        {
            if (node == null) continue;
            var lines = node.GetConnectedLines();
            foreach (var line in lines)
            {
                if (line != null) line.UpdateLine();
            }
        }
    }

    // ─────────────────────── COPY ───────────────────────

    public void CopySelection()
    {
        if (selectedNodes.Count == 0) return;

        clipboard.Clear();
        clipboardConnections.Clear();

        // Calculate the top-left of the bounding box for relative positioning
        float minX = float.MaxValue, maxY = float.MinValue;
        foreach (var node in selectedNodes)
        {
            var rt = node.GetComponent<RectTransform>();
            Vector2 pos = rt.anchoredPosition;
            if (pos.x < minX) minX = pos.x;
            if (pos.y > maxY) maxY = pos.y;
        }
        Vector2 topLeft = new Vector2(minX, maxY);

        // Build ordered list so we can reference by index
        var nodeList = selectedNodes.ToList();

        for (int i = 0; i < nodeList.Count; i++)
        {
            var node = nodeList[i];
            var rt = node.GetComponent<RectTransform>();

            // Get label from NodeText child
            string label = node.gameObject.name;
            var textChild = node.transform.Find("NodeText");
            if (textChild != null)
            {
                var tmp = textChild.GetComponent<TMPro.TMP_Text>();
                if (tmp != null) label = tmp.text;
            }

            clipboard.Add(new ClipboardNode
            {
                nodeType = node.gameObject.name, // e.g. "MiddleNode(Clone)"
                label = label,
                relativePosition = rt.anchoredPosition - topLeft,
                branchType = node.branchType
            });
        }

        // Copy fully-internal connections
        foreach (var line in selectedLines)
        {
            if (line == null || line.outputRect == null || line.inputRect == null) continue;

            var fromNode = line.outputRect.GetComponentInParent<NodeDraggable>();
            var toNode = line.inputRect.GetComponentInParent<NodeDraggable>();

            int fromIdx = nodeList.IndexOf(fromNode);
            int toIdx = nodeList.IndexOf(toNode);

            if (fromIdx >= 0 && toIdx >= 0)
            {
                clipboardConnections.Add(new ClipboardConnection
                {
                    fromIndex = fromIdx,
                    toIndex = toIdx,
                    fromPortName = line.outputRect.gameObject.name,
                    toPortName = line.inputRect.gameObject.name
                });
            }
        }

        Debug.Log($"[NodeSelectionManager] Copied {clipboard.Count} nodes, {clipboardConnections.Count} connections");
    }

    // ─────────────────────── PASTE ───────────────────────

    /// <summary>
    /// Paste clipboard nodes at the given content-local position (cursor = top-left of group).
    /// </summary>
    public void PasteAtPosition(Vector2 cursorLocalPos)
    {
        if (clipboard.Count == 0) return;

        ClearSelection();
        DestroyPasteButton();

        var instantiatedNodes = new List<NodeDraggable>();

        // Instantiate each node
        foreach (var clipNode in clipboard)
        {
            GameObject prefab = GetPrefabForType(clipNode.nodeType);
            if (prefab == null)
            {
                Debug.LogWarning($"[NodeSelectionManager] No prefab for type: {clipNode.nodeType}");
                continue;
            }

            GameObject nodeGO = Instantiate(prefab, content);
            var rt = nodeGO.GetComponent<RectTransform>();

            // Position: cursor is top-left, offset to bottom-right
            rt.anchoredPosition = cursorLocalPos + clipNode.relativePosition;

            // Set label
            var textChild = nodeGO.transform.Find("NodeText");
            if (textChild != null)
            {
                var tmp = textChild.GetComponent<TMPro.TMP_Text>();
                if (tmp != null) tmp.text = clipNode.label;
            }

            // Also set via TitleName if present
            var titleName = nodeGO.GetComponentInChildren<TitleName>();
            if (titleName != null) titleName.SetTitle(clipNode.label);

            // Set unique nodeId
            var nd = nodeGO.GetComponent<NodeDraggable>();
            if (nd != null)
            {
                nd.nodeId = System.Guid.NewGuid().ToString();
                nd.SetBranchType(clipNode.branchType);
                instantiatedNodes.Add(nd);
            }
            else
            {
                instantiatedNodes.Add(null);
            }
        }

        // Recreate internal connections
        foreach (var conn in clipboardConnections)
        {
            if (conn.fromIndex < 0 || conn.fromIndex >= instantiatedNodes.Count) continue;
            if (conn.toIndex < 0 || conn.toIndex >= instantiatedNodes.Count) continue;

            var fromNode = instantiatedNodes[conn.fromIndex];
            var toNode = instantiatedNodes[conn.toIndex];
            if (fromNode == null || toNode == null) continue;

            // Find output port on fromNode
            Button outputButton = null;
            foreach (var btn in fromNode.GetComponentsInChildren<Button>())
            {
                if (btn.CompareTag("OutputPort") || btn.gameObject.name == conn.fromPortName)
                {
                    outputButton = btn;
                    break;
                }
            }

            // Find input port on toNode
            Button inputButton = null;
            foreach (var btn in toNode.GetComponentsInChildren<Button>())
            {
                if (btn.CompareTag("InputPort"))
                {
                    inputButton = btn;
                    break;
                }
            }

            if (outputButton == null || inputButton == null) continue;

            // Create the line (same pattern as OutputButtonDrag connection creation)
            var lineGO = Instantiate(UILinePrefab, content);
            var connector = lineGO.GetComponent<UILineConnector>();
            if (connector == null) connector = lineGO.AddComponent<UILineConnector>();
            connector.outputRect = outputButton.GetComponent<RectTransform>();
            connector.inputRect = inputButton.GetComponent<RectTransform>();
            connector.canvas = content.GetComponentInParent<Canvas>();
            connector.UpdateLine();

            if (lineGO.GetComponent<UILineClickDeleter>() == null)
                lineGO.AddComponent<UILineClickDeleter>();

            fromNode.RegisterConnectedLine(connector);
            toNode.RegisterConnectedLine(connector);
        }

        // Auto-save
        var aiEditorFileUI = FindFirstObjectByType<AiEditorFileUI>();
        if (aiEditorFileUI != null)
            aiEditorFileUI.AutoSave();

        Debug.Log($"[NodeSelectionManager] Pasted {instantiatedNodes.Count} nodes");
    }

    private GameObject GetPrefabForType(string nodeType)
    {
        if (nodeType.StartsWith("MiddleNode")) return MiddleNodePrefab;
        if (nodeType.StartsWith("EndNode")) return EndNodePrefab;
        if (nodeType.StartsWith("SubAINode")) return SubAINodePrefab;
        // Fallback
        return EndNodePrefab;
    }

    // ─────────────────────── DELETE ALL ───────────────────────

    public void DeleteSelection()
    {
        foreach (var node in selectedNodes)
        {
            if (node == null) continue;
            node.DeleteAllConnectedLines();
            Destroy(node.gameObject);
        }

        selectedNodes.Clear();
        selectedLines.Clear();
        DestroyBoundingBox();
        HideButtons();

        // Auto-save
        var aiEditorFileUI = FindFirstObjectByType<AiEditorFileUI>();
        if (aiEditorFileUI != null)
            aiEditorFileUI.AutoSave();
    }

    // ─────────────────────── PASTE BUTTON ───────────────────────

    private void ShowPasteButton(Vector2 screenPos)
    {
        DestroyPasteButton();

        if (pasteButtonPrefab == null || clipboard.Count == 0) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            content, screenPos, null, out localPos);

        pasteButtonInstance = Instantiate(pasteButtonPrefab, content);
        var rt = pasteButtonInstance.GetComponent<RectTransform>();
        // Reset anchors/pivot so anchoredPosition maps directly to content-local coords
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localPos;

        var btn = pasteButtonInstance.GetComponent<Button>();
        if (btn != null)
        {
            Vector2 pastePos = localPos; // capture for closure
            btn.onClick.AddListener(() =>
            {
                PasteAtPosition(pastePos);
                DestroyPasteButton();
            });
        }
    }

    private void DestroyPasteButton()
    {
        if (pasteButtonInstance != null)
        {
            Destroy(pasteButtonInstance);
            pasteButtonInstance = null;
        }
    }

    // ─────────────────────── CONTEXT MENU (background click) ───────────────────────

    private GameObject currentContextMenu;

    private void SpawnContextMenu(Vector2 screenPos)
    {
        DestroyContextMenu();

        if (ContextMenuUIPrefab == null)
        {
            Debug.LogWarning("[NodeSelectionManager] ContextMenuUIPrefab is not assigned!");
            return;
        }
        if (UICanvasObj == null)
        {
            Debug.LogWarning("[NodeSelectionManager] UICanvasObj is not assigned!");
            return;
        }

        Vector2 spawnPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            content, screenPos,
            UICanvasObj.renderMode == RenderMode.ScreenSpaceOverlay ? null : UICanvasObj.worldCamera,
            out spawnPos);

        currentContextMenu = Instantiate(ContextMenuUIPrefab, content);
        var menuRect = currentContextMenu.GetComponent<RectTransform>();
        menuRect.anchoredPosition = spawnPos;

        var menuScript = currentContextMenu.GetComponent<ContextMenuUI>();
        if (menuScript != null)
        {
            menuScript.UICanvasObj = UICanvasObj;
            // No output button — node will be created standalone (no connection line)
            menuScript.SetOutputButtonInfo(spawnPos, null, ContextMenuUI.BranchType.None);
        }

        Debug.Log($"[NodeSelectionManager] Spawned context menu at {spawnPos}");
    }

    private void DestroyContextMenu()
    {
        if (currentContextMenu != null)
        {
            // Disable immediately so it won't block raycasts this frame
            // (Destroy doesn't remove until end of frame)
            currentContextMenu.SetActive(false);
            Destroy(currentContextMenu);
            currentContextMenu = null;
        }
    }

    // ─────────────────────── CLEAR / DESELECT ───────────────────────

    /// <summary>
    /// Call from anywhere to dismiss the current selection.
    /// Safe to call even when nothing is selected.
    /// </summary>
    public static void StaticClearSelection()
    {
        if (_instance != null)
            _instance.ClearSelection();
    }

    public void ClearSelection()
    {
        selectedNodes.Clear();
        selectedLines.Clear();
        DestroyBoundingBox();
        DestroyPasteButton();
        HideButtons();

        // Clean up drag rect if it exists
        if (selectionRectGO != null)
        {
            Destroy(selectionRectGO);
            selectionRectGO = null;
            selectionRectRT = null;
        }
    }

    private void DestroyBoundingBox()
    {
        if (boundingBoxGO != null)
        {
            Destroy(boundingBoxGO);
            boundingBoxGO = null;
            boundingBoxRT = null;
        }
    }

    private void HideButtons()
    {
        if (copyButton != null) copyButton.gameObject.SetActive(false);
        if (deleteAllButton != null) deleteAllButton.gameObject.SetActive(false);
    }

    // ─────────────────────── DATA CLASSES ───────────────────────

    private class ClipboardNode
    {
        public string nodeType;
        public string label;
        public Vector2 relativePosition;
        public OutputButtonDrag.BranchType branchType;
    }

    private class ClipboardConnection
    {
        public int fromIndex;
        public int toIndex;
        public string fromPortName;
        public string toPortName;
    }
}

// ─────────────────────────────────────────────────────────────────────
// Small helper MonoBehaviour placed on the bounding box at runtime
// to forward drag events back to NodeSelectionManager.
// ─────────────────────────────────────────────────────────────────────
public class BoundingBoxDragHandler : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    private NodeSelectionManager manager;

    public void Init(NodeSelectionManager mgr) { manager = mgr; }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Consume pointer-down so it doesn't fall through to BackgroundClickHandler
        // which would destroy the selection box.
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        manager.OnBoxDragBegin(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        manager.OnBoxDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        manager.OnBoxDragEnd(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Clicking the bounding box itself does nothing special — drag is the interaction.
        // This prevents clicks from falling through to background.
    }
}
