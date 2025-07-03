using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro; // Added TMPro namespace
using UnityEngine.InputSystem;
using System.IO; // For file operations
using AiEditor; // For AiTreeAsset
#if UNITY_EDITOR
using UnityEditor; // For loading assets in editor
#endif

public class ContextMenuUI : MonoBehaviour
{
    [Header("Main Buttons")]
    public Button actionButton;
    public Button conditionButton;
    public Button subAIButton;

    [Header("Panels")]
    public GameObject actionListPanel;
    public GameObject conditionListPanel;
    public GameObject subAIListPanel;

    [Header("Action Sub Buttons")]
    public Button turretButton;
    public Button navButton;

    [Header("Action Sub Panels")]
    public GameObject turretListPanel;
    public GameObject navListPanel;

    [Header("Condition Sub Buttons")]
    public Button conditionTurretButton;
    public Button conditionArmorButton;
    public Button conditionHPButton;
    public Button conditionRangeButton;
    public Button conditionTagButton;
    public Button conditionTargetButton;
    public Button conditionSelfButton;

    [Header("Condition Sub Panels")]
    public GameObject conditionTurretPanel;
    public GameObject conditionArmorPanel;
    public GameObject conditionHPPanel;
    public GameObject conditionRangePanel;
    public GameObject conditionTagPanel;
    public GameObject conditionTargetPanel;
    public GameObject conditionSelfPanel;

    [Header("Node Prefabs")]
    public GameObject EndNodePrefab;
    public GameObject MiddleNodePrefab;
    public GameObject SubAINodePrefab; // Dedicated prefab for SubAI nodes
    public Canvas UICanvasObj; // Reference to the canvas (was GameObject)
    public GameObject UILinePrefab;
    
    [Header("SubAI File Browser")]
    public ScrollRect subAIScrollView; // ScrollView for SubAI files
    public Transform subAIContent; // Content transform inside the ScrollView
    public GameObject fileButtonPrefab; // Prefab for file buttons

    [HideInInspector]
    public Vector2 outputButtonPos; // Set by OutputButtonDrag when spawning ContextMenuUI

    private OutputButtonDrag outputButtonDragRef; // To get the original output button

    // Add this field to track which branch is being built
    public enum BranchType { None, Turret, Nav }
    public BranchType currentBranch = BranchType.None;    // Call this from OutputButtonDrag after instantiating ContextMenuUI
    public void SetOutputButtonInfo(Vector2 pos, OutputButtonDrag dragRef, BranchType branch = BranchType.None)
    {
        outputButtonPos = pos;
        outputButtonDragRef = dragRef;
        currentBranch = branch;
        Debug.Log($"ContextMenuUI: Received branch type {branch}");
    }

    void Start()
    {
        // Ensure all panels are hidden at start
        actionListPanel.SetActive(false);
        conditionListPanel.SetActive(false);
        subAIListPanel.SetActive(false);
        turretListPanel.SetActive(false);
        navListPanel.SetActive(false);
        conditionTurretPanel.SetActive(false);
        conditionArmorPanel.SetActive(false);
        conditionHPPanel.SetActive(false);
        conditionRangePanel.SetActive(false);
        conditionTagPanel.SetActive(false);
        conditionTargetPanel.SetActive(false);
        conditionSelfPanel.SetActive(false);

        // Add listeners
        actionButton.onClick.AddListener(OnActionClicked);
        conditionButton.onClick.AddListener(OnConditionClicked);
        subAIButton.onClick.AddListener(OnSubAIClicked);
        turretButton.onClick.AddListener(OnTurretClicked);
        navButton.onClick.AddListener(OnNavClicked);
        conditionTurretButton.onClick.AddListener(OnConditionTurretClicked);
        conditionArmorButton.onClick.AddListener(OnConditionArmorClicked);
        conditionHPButton.onClick.AddListener(OnConditionHPClicked);
        conditionRangeButton.onClick.AddListener(OnConditionRangeClicked);
        conditionTagButton.onClick.AddListener(OnConditionTagClicked);
        conditionTargetButton.onClick.AddListener(OnConditionTargetClicked);
        conditionSelfButton.onClick.AddListener(OnConditionSelfClicked);        // --- Fix: Hide both the button and its label/text for the unused branch ---
        Debug.Log($"ContextMenuUI Start: currentBranch = {currentBranch}");
        if (currentBranch == BranchType.Turret)
        {
            if (navButton != null) {
                navButton.interactable = false;
                var colors = navButton.colors;
                colors.normalColor = colors.disabledColor;
                navButton.colors = colors;
                Debug.Log("ContextMenuUI: Disabled nav button for Turret branch");
            }
        }
        else if (currentBranch == BranchType.Nav)
        {
            if (turretButton != null) {
                turretButton.interactable = false;
                var colors = turretButton.colors;
                colors.normalColor = colors.disabledColor;
                turretButton.colors = colors;
                Debug.Log("ContextMenuUI: Disabled turret button for Nav branch");
            }
        }

        // Reset all main button states to Normal at start
        ResetButtonColors(actionButton);
        ResetButtonColors(conditionButton);
        ResetButtonColors(subAIButton);
    }

    // Utility to reset a button's visual state to Normal
    private void ResetButtonColors(Button btn)
    {
        // Do not set btn.image.color; let Unity handle button visuals
    }

    void HideAllConditionPanels()
    {
        conditionTurretPanel.SetActive(false);
        conditionArmorPanel.SetActive(false);
        conditionHPPanel.SetActive(false);
        conditionRangePanel.SetActive(false);
        conditionTagPanel.SetActive(false);
        conditionTargetPanel.SetActive(false);
        conditionSelfPanel.SetActive(false);
    }

    void OnActionClicked()
    {
        actionListPanel.SetActive(true);
        conditionListPanel.SetActive(false);
        subAIListPanel.SetActive(false);
        // Visually set Action as selected, others as normal
        SetButtonSelected(actionButton);
        SetButtonNormal(conditionButton);
        SetButtonNormal(subAIButton);
    }

    void OnConditionClicked()
    {
        actionListPanel.SetActive(false);
        conditionListPanel.SetActive(true);
        subAIListPanel.SetActive(false);
        SetButtonSelected(conditionButton);
        SetButtonNormal(actionButton);
        SetButtonNormal(subAIButton);
    }

    void OnSubAIClicked()
    {
        actionListPanel.SetActive(false);
        conditionListPanel.SetActive(false);
        subAIListPanel.SetActive(true);
        SetButtonSelected(subAIButton);
        SetButtonNormal(actionButton);
        SetButtonNormal(conditionButton);
        
        // Populate the SubAI file browser
        PopulateSubAIFiles();
    }

    // Utility to set a button's color to its selected color
    private void SetButtonSelected(Button btn)
    {
        // Do not set btn.image.color; let Unity handle button visuals
    }
    // Utility to set a button's color to its normal color
    private void SetButtonNormal(Button btn)
    {
        // Do not set btn.image.color; let Unity handle button visuals
    }

    void OnTurretClicked()
    {
        turretListPanel.SetActive(true);
        navListPanel.SetActive(false);
    }

    void OnNavClicked()
    {
        turretListPanel.SetActive(false);
        navListPanel.SetActive(true);
    }

    void OnConditionTurretClicked()
    {
        HideAllConditionPanels();
        conditionTurretPanel.SetActive(true);
    }
    void OnConditionArmorClicked()
    {
        HideAllConditionPanels();
        conditionArmorPanel.SetActive(true);
    }
    void OnConditionHPClicked()
    {
        HideAllConditionPanels();
        conditionHPPanel.SetActive(true);
    }
    void OnConditionRangeClicked()
    {
        HideAllConditionPanels();
        conditionRangePanel.SetActive(true);
    }
    void OnConditionTagClicked()
    {
        HideAllConditionPanels();
        conditionTagPanel.SetActive(true);
    }
    void OnConditionTargetClicked()
    {
        HideAllConditionPanels();
        conditionTargetPanel.SetActive(true);
    }
    
    void OnConditionSelfClicked()
    {
        HideAllConditionPanels();
        conditionSelfPanel.SetActive(true);
    }

    /// <summary>
    /// Populates the SubAI file browser with files from the appropriate folder based on current branch
    /// </summary>
    void PopulateSubAIFiles()
    {
        if (subAIContent == null || fileButtonPrefab == null)
        {
            Debug.LogWarning("[ContextMenuUI] SubAI content or file button prefab not assigned!");
            return;
        }
        
        // Clear existing file buttons
        foreach (Transform child in subAIContent)
        {
            Destroy(child.gameObject);
        }
        
        // Determine folder based on current branch
        string folderName = "";
        if (currentBranch == BranchType.Turret)
        {
            folderName = "TurretFiles";
        }
        else if (currentBranch == BranchType.Nav)
        {
            folderName = "NavFiles";
        }
        else
        {
            Debug.LogWarning("[ContextMenuUI] Unknown branch type for SubAI files");
            return;
        }
        
        string folderPath = Path.Combine(Application.dataPath, "AiEditor/AISaveFiles", folderName);
        
        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[ContextMenuUI] SubAI folder not found: {folderPath}");
            return;
        }
        
        // Get all .asset files in the folder
        string[] files = Directory.GetFiles(folderPath, "*.asset");
        
        // Get the current file name to exclude it from the list
        string currentFileName = GetCurrentFileName();
        
        foreach (string filePath in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            
            // Skip the currently open file
            if (!string.IsNullOrEmpty(currentFileName) && fileName.Equals(currentFileName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            // Create file button
            GameObject fileButton = Instantiate(fileButtonPrefab, subAIContent);
            
            // Set the button text to display the SO's title instead of filename
            TMPro.TMP_Text buttonText = fileButton.GetComponentInChildren<TMPro.TMP_Text>();
            if (buttonText != null)
            {
                // Convert file path to relative asset path for Unity
                string relativePath = "Assets" + filePath.Substring(Application.dataPath.Length);
                
#if UNITY_EDITOR
                // Load the actual ScriptableObject to get its title
                var aiTreeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiTreeAsset>(relativePath);
                if (aiTreeAsset != null && !string.IsNullOrEmpty(aiTreeAsset.TreeName))
                {
                    buttonText.text = aiTreeAsset.TreeName; // Use the SO's title
                }
                else
                {
                    buttonText.text = fileName; // Fallback to filename if no title
                }
#else
                buttonText.text = fileName; // Runtime fallback
#endif
            }
            
            // Add click listener
            Button btn = fileButton.GetComponent<Button>();
            if (btn != null)
            {
                string capturedFileName = fileName; // Capture for closure
                btn.onClick.AddListener(() => OnSubAIFileSelected(capturedFileName));
            }
        }
        
        Debug.Log($"[ContextMenuUI] Loaded {files.Length} SubAI files from {folderPath}");
    }
    
    /// <summary>
    /// Gets the current file name being edited (to exclude from SubAI list)
    /// </summary>
    string GetCurrentFileName()
    {
        try
        {
            // Find the AiEditorFileUI component to get the current file name
            var aiEditorFileUI = FindFirstObjectByType<AiEditorFileUI>();
            if (aiEditorFileUI != null)
            {
                // Try to get the current tree name from the FileButtonPanel
                var fileButtonPanel = GameObject.Find("FileButtonPanel");
                if (fileButtonPanel != null)
                {
                    var fileNameText = fileButtonPanel.transform.Find("FileNameText");
                    if (fileNameText != null)
                    {
                        var textComponent = fileNameText.GetComponent<TMPro.TMP_Text>();
                        if (textComponent != null && !string.IsNullOrEmpty(textComponent.text))
                        {
                            return textComponent.text;
                        }
                    }
                }
                
                // Alternative: try to get from the FileName field if it exists
                if (aiEditorFileUI.FileName != null && !string.IsNullOrEmpty(aiEditorFileUI.FileName.text))
                {
                    return aiEditorFileUI.FileName.text;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ContextMenuUI] Could not get current file name: {ex.Message}");
        }
        
        return null; // Return null if we can't determine the current file
    }

    /// <summary>
    /// Handles when a SubAI file is selected - creates a SubAI node and destroys the context menu
    /// </summary>
    void OnSubAIFileSelected(string fileName)
    {
        Debug.Log($"[ContextMenuUI] SubAI file selected: {fileName}");
        
        // Create the SubAI node
        CreateSubAINode(fileName);
        
        // Destroy the context menu (same as other node types)
        DestroyContextMenu();
    }
    
    /// <summary>
    /// Creates a SubAI node that references the selected AI file
    /// </summary>
    void CreateSubAINode(string aiFileName)
    {
        if (SubAINodePrefab == null)
        {
            Debug.LogError("[ContextMenuUI] SubAINodePrefab is not assigned!");
            return;
        }
        
        // Use the same positioning logic as other nodes
        Vector3 spawnWorld = transform.position + new Vector3(75, 0, 0); // Offset to the right
        
        // Find the proper parent (same as other nodes)
        Transform nodeParent = UICanvasObj.transform;
        var background = UICanvasObj.transform.Find("Background");
        if (background != null)
        {
            var content = background.Find("Content");
            if (content != null)
                nodeParent = content;
        }
        
        // Instantiate the SubAI node using the dedicated SubAI prefab
        GameObject subAINode = Instantiate(SubAINodePrefab, nodeParent);
        RectTransform nodeRect = subAINode.GetComponent<RectTransform>();
        nodeRect.position = spawnWorld;
        
        // Set up the node's canvas reference
        var nodeScript = subAINode.GetComponent<OutputButtonDrag>();
        if (nodeScript != null)
            nodeScript.UICanvas = UICanvasObj;
        
        // Set the branch type to match the current branch
        var nodeDraggable = subAINode.GetComponent<NodeDraggable>();
        var sourceNodeDraggable = outputButtonDragRef?.GetComponentInParent<NodeDraggable>();
        
        if (nodeDraggable != null && sourceNodeDraggable != null)
        {
            nodeDraggable.SetBranchType(sourceNodeDraggable.branchType);
        }
        else if (nodeDraggable != null && currentBranch != BranchType.None)
        {
            nodeDraggable.SetBranchType((OutputButtonDrag.BranchType)(int)currentBranch);
        }
        
        // Also set branch type on the OutputButtonDrag component if present
        var nodeOutputDrag = subAINode.GetComponent<OutputButtonDrag>();
        if (nodeOutputDrag != null && sourceNodeDraggable != null)
        {
            nodeOutputDrag.branchType = sourceNodeDraggable.branchType;
        }
        else if (nodeOutputDrag != null && currentBranch != BranchType.None)
        {
            nodeOutputDrag.branchType = (OutputButtonDrag.BranchType)(int)currentBranch;
        }
        
        // Get the display text for the node (use ScriptableObject title if available)
        string subAIText = GetDisplayTextForFile(aiFileName);
        
        // Update all text components in the node
        foreach (var text in subAINode.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            text.text = subAIText;
        }
        foreach (var text in subAINode.GetComponentsInChildren<Text>())
        {
            text.text = subAIText;
        }
        
        // Create connection line if there's an output button reference
        if (outputButtonDragRef != null)
        {
            CreateConnectionToNode(subAINode);
        }
        
        Debug.Log($"[ContextMenuUI] Created SubAI node: {subAIText}");
    }
    
    /// <summary>
    /// Creates a connection line from the original output button to the new SubAI node
    /// </summary>
    void CreateConnectionToNode(GameObject targetNode)
    {
        // Find the input button on the target node
        Button inputButton = null;
        foreach (var btn in targetNode.GetComponentsInChildren<Button>())
        {
            if (btn.CompareTag("InputPort"))
            {
                inputButton = btn;
                break;
            }
        }
        
        if (inputButton == null)
        {
            Debug.LogError("[ContextMenuUI] SubAI node has no input port!");
            return;
        }
        
        // Use the same connection logic as other nodes
        Vector3 inputWorld = inputButton.transform.position;
        
        // Find the content panel for line parent
        Transform lineParent = UICanvasObj.transform;
        RectTransform contentRect = null;
        var background = UICanvasObj.transform.Find("Background");
        if (background != null)
        {
            var content = background.Find("Content");
            if (content != null)
            {
                lineParent = content;
                contentRect = content.GetComponent<RectTransform>();
            }
        }
        if (contentRect == null) contentRect = UICanvasObj.transform as RectTransform;
        
        // Convert to canvas coordinates
        Vector2 inputCanvas;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            contentRect,
            RectTransformUtility.WorldToScreenPoint(null, inputWorld),
            null,
            out inputCanvas);
        
        // Create and position the connection line
        GameObject connectionLine = Instantiate(UILinePrefab, lineParent);
        RectTransform lineRect = connectionLine.GetComponent<RectTransform>();
        lineRect.anchoredPosition = outputButtonPos;
        
        Vector2 direction = inputCanvas - outputButtonPos;
        lineRect.sizeDelta = new Vector2(direction.magnitude, lineRect.sizeDelta.y);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
        
        // Add line management components
        var lineDeleter = connectionLine.AddComponent<UILineClickDeleter>();
        var connector = connectionLine.AddComponent<UILineConnector>();
        connector.outputRect = (RectTransform)outputButtonDragRef.transform;
        connector.inputRect = inputButton.GetComponent<RectTransform>();
        connector.canvas = UICanvasObj;
        
        // Register the line with both nodes for drag updates
        NodeDraggable outputDraggable = outputButtonDragRef.GetComponentInParent<NodeDraggable>();
        NodeDraggable inputDraggable = inputButton.GetComponentInParent<NodeDraggable>();
        if (outputDraggable != null) outputDraggable.RegisterConnectedLine(connector);
        if (inputDraggable != null) inputDraggable.RegisterConnectedLine(connector);
    }
    
    /// <summary>
    /// Destroys the context menu and cleans up temp lines
    /// </summary>
    void DestroyContextMenu()
    {
        // Clean up any temporary connection line
        if (OutputButtonDrag.currentTempLine != null)
        {
            Destroy(OutputButtonDrag.currentTempLine);
            OutputButtonDrag.currentTempLine = null;
        }
        
        // Destroy the context menu
        Destroy(gameObject);
    }

    void OnEnable()
    {
        // Register a global click handler to close the context menu if clicking outside
        StartCoroutine(WaitForClickOutside());
    }

    System.Collections.IEnumerator WaitForClickOutside()
    {
        // Wait for one frame so the current click doesn't immediately close the menu
        yield return null;
        bool closed = false;
        GraphicRaycaster raycaster = UICanvasObj.GetComponent<GraphicRaycaster>();
        EventSystem eventSystem = EventSystem.current;
        while (!closed)
        {
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                // Raycast to find all UI elements under the pointer
                PointerEventData pointerData = new PointerEventData(eventSystem);
                pointerData.position = Pointer.current.position.ReadValue();
                var results = new System.Collections.Generic.List<RaycastResult>();
                raycaster.Raycast(pointerData, results);
                bool clickedInside = false;
                foreach (var result in results)
                {
                    if (result.gameObject == null) continue;
                    if (result.gameObject.transform.IsChildOf(transform) || result.gameObject == gameObject)
                    {
                        clickedInside = true;
                        break;
                    }
                }
                if (!clickedInside)
                {
                    // Destroy context menu
                    Destroy(gameObject);
                    // Destroy temp line if present
                    if (OutputButtonDrag.currentTempLine != null)
                    {
                        Destroy(OutputButtonDrag.currentTempLine);
                        OutputButtonDrag.currentTempLine = null;
                    }
                    closed = true;
                }
            }            yield return null;        }
    }

    /// <summary>
    /// Gets the display text for a SubAI file (uses ScriptableObject title if available, otherwise filename)
    /// </summary>
    string GetDisplayTextForFile(string fileName)
    {
        // Determine folder based on current branch
        string folderName = "";
        if (currentBranch == BranchType.Turret)
        {
            folderName = "TurretFiles";
        }
        else if (currentBranch == BranchType.Nav)
        {
            folderName = "NavFiles";
        }
        else
        {
            return fileName; // Fallback to filename
        }
        
        string folderPath = Path.Combine(Application.dataPath, "AiEditor/AISaveFiles", folderName);
        string filePath = Path.Combine(folderPath, fileName + ".asset");
        
        // Convert file path to relative asset path for Unity
        string relativePath = "Assets" + filePath.Substring(Application.dataPath.Length);
        
#if UNITY_EDITOR
        // Load the actual ScriptableObject to get its title
        var aiTreeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiTreeAsset>(relativePath);
        if (aiTreeAsset != null && !string.IsNullOrEmpty(aiTreeAsset.TreeName))
        {
            return aiTreeAsset.TreeName; // Use the SO's title
        }
#endif
        
        return fileName; // Fallback to filename
    }
    
    /// <summary>
    /// Called when an action button is clicked - creates an action node
    /// </summary>
    public void OnActionFinalButtonClicked()
    {
        Debug.Log("[ContextMenuUI] Action button clicked");
        
        // Get the button text to determine the action type
        string actionText = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject?.GetComponentInChildren<TMPro.TMP_Text>()?.text ?? "Action";
        
        CreateActionNode(actionText);
        DestroyContextMenu();
    }
    
    /// <summary>
    /// Called when a condition button is clicked - creates a condition node
    /// </summary>
    public void OnConditionFinalButtonClicked()
    {
        Debug.Log("[ContextMenuUI] Condition button clicked");
        
        // Get the button text to determine the condition type
        string conditionText = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject?.GetComponentInChildren<TMPro.TMP_Text>()?.text ?? "Condition";
        
        CreateConditionNode(conditionText);
        DestroyContextMenu();
    }
    
    /// <summary>
    /// Creates an action node
    /// </summary>
    void CreateActionNode(string actionText)
    {
        if (EndNodePrefab == null)
        {
            Debug.LogError("[ContextMenuUI] EndNodePrefab is not assigned!");
            return;
        }
        
        // Use the same positioning logic as SubAI nodes
        Vector3 spawnWorld = transform.position + new Vector3(75, 0, 0); // Offset to the right
        
        // Find the proper parent
        Transform nodeParent = UICanvasObj.transform;
        var background = UICanvasObj.transform.Find("Background");
        if (background != null)
        {
            var content = background.Find("Content");
            if (content != null)
                nodeParent = content;
        }
        
        // Instantiate the action node using EndNodePrefab
        GameObject actionNode = Instantiate(EndNodePrefab, nodeParent);
        RectTransform nodeRect = actionNode.GetComponent<RectTransform>();
        nodeRect.position = spawnWorld;
        
        // Set up the node's canvas reference
        var nodeScript = actionNode.GetComponent<OutputButtonDrag>();
        if (nodeScript != null)
            nodeScript.UICanvas = UICanvasObj;
        
        // Set the branch type to match the current branch
        var nodeDraggable = actionNode.GetComponent<NodeDraggable>();
        var sourceNodeDraggable = outputButtonDragRef?.GetComponentInParent<NodeDraggable>();
        
        if (nodeDraggable != null && sourceNodeDraggable != null)
        {
            nodeDraggable.SetBranchType(sourceNodeDraggable.branchType);
        }
        else if (nodeDraggable != null && currentBranch != BranchType.None)
        {
            nodeDraggable.SetBranchType((OutputButtonDrag.BranchType)(int)currentBranch);
        }
        
        // Also set branch type on the OutputButtonDrag component if present
        var nodeOutputDrag = actionNode.GetComponent<OutputButtonDrag>();
        if (nodeOutputDrag != null && sourceNodeDraggable != null)
        {
            nodeOutputDrag.branchType = sourceNodeDraggable.branchType;
        }
        else if (nodeOutputDrag != null && currentBranch != BranchType.None)
        {
            nodeOutputDrag.branchType = (OutputButtonDrag.BranchType)(int)currentBranch;
        }
        
        // Set the node text to show the action
        foreach (var text in actionNode.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            text.text = actionText;
        }
        foreach (var text in actionNode.GetComponentsInChildren<Text>())
        {
            text.text = actionText;
        }
        
        // Create connection line if there's an output button reference
        if (outputButtonDragRef != null)
        {
            CreateConnectionToNode(actionNode);
        }
        
        Debug.Log($"[ContextMenuUI] Created action node: {actionText}");
    }
    
    /// <summary>
    /// Creates a condition node
    /// </summary>
    void CreateConditionNode(string conditionText)
    {
        GameObject nodePrefab = null;
        
        // Use MiddleNodePrefab for conditions (they have outputs), EndNodePrefab for actions
        if (MiddleNodePrefab != null)
        {
            nodePrefab = MiddleNodePrefab;
        }
        else if (EndNodePrefab != null)
        {
            nodePrefab = EndNodePrefab;
        }
        else
        {
            Debug.LogError("[ContextMenuUI] No node prefabs are assigned!");
            return;
        }
        
        // Use the same positioning logic as other nodes
        Vector3 spawnWorld = transform.position + new Vector3(75, 0, 0); // Offset to the right
        
        // Find the proper parent
        Transform nodeParent = UICanvasObj.transform;
        var background = UICanvasObj.transform.Find("Background");
        if (background != null)
        {
            var content = background.Find("Content");
            if (content != null)
                nodeParent = content;
        }
        
        // Instantiate the condition node
        GameObject conditionNode = Instantiate(nodePrefab, nodeParent);
        RectTransform nodeRect = conditionNode.GetComponent<RectTransform>();
        nodeRect.position = spawnWorld;
        
        // Set up the node's canvas reference
        var nodeScript = conditionNode.GetComponent<OutputButtonDrag>();
        if (nodeScript != null)
            nodeScript.UICanvas = UICanvasObj;
        
        // Set the branch type to match the current branch
        var nodeDraggable = conditionNode.GetComponent<NodeDraggable>();
        var sourceNodeDraggable = outputButtonDragRef?.GetComponentInParent<NodeDraggable>();
        
        if (nodeDraggable != null && sourceNodeDraggable != null)
        {
            nodeDraggable.SetBranchType(sourceNodeDraggable.branchType);
        }
        else if (nodeDraggable != null && currentBranch != BranchType.None)
        {
            nodeDraggable.SetBranchType((OutputButtonDrag.BranchType)(int)currentBranch);
        }
        
        // Also set branch type on the OutputButtonDrag component if present
        var nodeOutputDrag = conditionNode.GetComponent<OutputButtonDrag>();
        if (nodeOutputDrag != null && sourceNodeDraggable != null)
        {
            nodeOutputDrag.branchType = sourceNodeDraggable.branchType;
        }
        else if (nodeOutputDrag != null && currentBranch != BranchType.None)
        {
            nodeOutputDrag.branchType = (OutputButtonDrag.BranchType)(int)currentBranch;
        }
        
        // Set the node text to show the condition
        foreach (var text in conditionNode.GetComponentsInChildren<TMPro.TMP_Text>())
        {
            text.text = conditionText;
        }
        foreach (var text in conditionNode.GetComponentsInChildren<Text>())
        {
            text.text = conditionText;
        }
        
        // Note: Number input functionality is now handled by node click events in NodeDeleteUI
        
        // Create connection line if there's an output button reference
        if (outputButtonDragRef != null)
        {
            CreateConnectionToNode(conditionNode);
        }
        
        Debug.Log($"[ContextMenuUI] Created condition node: {conditionText}");
    }
}
