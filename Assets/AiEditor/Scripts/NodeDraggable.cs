using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class NodeDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public float gridSize = 32f; // Size of the grid to snap to
    private RectTransform rectTransform;
    private RectTransform contentRectTransform; // Use Content panel for all coordinate math
    private Vector2 offset;
    private List<UILineConnector> connectedLines = new List<UILineConnector>();

    public string nodeId; // Unique identifier for serialization
    public OutputButtonDrag.BranchType branchType = OutputButtonDrag.BranchType.None; // Branch type for the node

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        // Assign a unique nodeId if not already set
        if (string.IsNullOrEmpty(nodeId))
            nodeId = System.Guid.NewGuid().ToString();
        // Find the Content RectTransform (assumes parent named "Content")
        Transform t = transform;
        while (t != null && t.name != "Content") t = t.parent;
        if (t != null && t.GetComponent<RectTransform>() != null)
            contentRectTransform = t.GetComponent<RectTransform>();
        else
            Debug.LogError("NodeDraggable: Could not find Content RectTransform in parent hierarchy.");
        
        // Initialize branch type based on node name or parent context
        InitializeBranchType();
    }
    
    private void InitializeBranchType()
    {
        // If branchType is already set, don't override it
        if (branchType != OutputButtonDrag.BranchType.None)
            return;
            
        // Check if this is a start node
        if (gameObject.name.Contains("StartNav") || CompareTag("NavOrigin"))
        {
            branchType = OutputButtonDrag.BranchType.Nav;
        }
        else if (gameObject.name.Contains("StartTurret") || CompareTag("TurretOrigin"))
        {
            branchType = OutputButtonDrag.BranchType.Turret;
        }
    }
    
    // Method to set branch type and propagate to OutputButtonDrag component
    public void SetBranchType(OutputButtonDrag.BranchType newBranchType)
    {
        branchType = newBranchType;
        
        // Also set it on any OutputButtonDrag component
        var outputButtonDrag = GetComponent<OutputButtonDrag>();
        if (outputButtonDrag != null)
            outputButtonDrag.branchType = newBranchType;
    }

    public void RegisterConnectedLine(UILineConnector line)
    {
        if (!connectedLines.Contains(line))
            connectedLines.Add(line);
    }

    public void UnregisterConnectedLine(UILineConnector line)
    {
        connectedLines.Remove(line);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Calculate offset between pointer and node center, relative to Content panel
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contentRectTransform,
            eventData.position,
            null, // Content is in ScreenSpaceOverlay or worldCamera is not needed
            out localPoint);
        offset = rectTransform.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contentRectTransform,
            eventData.position,
            null,
            out localPoint);
        // Snap to grid
        Vector2 snapped = new Vector2(
            Mathf.Round((localPoint.x + offset.x) / gridSize) * gridSize,
            Mathf.Round((localPoint.y + offset.y) / gridSize) * gridSize
        );
        rectTransform.anchoredPosition = snapped;
        // Update all connected lines
        foreach (var line in connectedLines)
            if (line != null) line.UpdateLine();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Optionally, you could add logic here for after drag ends
    }

    // Deletes all connected lines (call before destroying node GameObject)
    public void DeleteAllConnectedLines()
    {
        // Copy to avoid modification during iteration
        var linesToDelete = new List<UILineConnector>(connectedLines);
        foreach (var line in linesToDelete)
        {
            if (line != null)
            {
                // Destroy the line GameObject
                Destroy(line.gameObject);
            }
        }
        connectedLines.Clear();
    }
}
