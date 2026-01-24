using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AiEditor;

public class AiEditorFileUI : MonoBehaviour
{
    public Button saveButton;
    public Button loadButton;
    public GameObject loadPanel;
    public GameObject guidePanel;
    public Button turretBranchButton;
    public Button navBranchButton;
    public GameObject fileButtonPrefab;
    public Button startTurretButton;
    public Button startNavButton;
    public GameObject navFileScrollView; // Assign the ScrollView GameObject for Nav files
    public GameObject turretFileScrollView; // Assign the ScrollView GameObject for Turret files
    public Transform navFileContent; // Assign the Content transform of the Nav ScrollView
    public Transform turretFileContent; // Assign the Content transform of the Turret ScrollView

    private string navFolder = "NavFiles";
    private string turretFolder = "TurretFiles";
    private string currentJsonPath = ""; // Path to currently loaded JSON file

    // Auto-save functionality
    private bool autoSaveEnabled = true;
    private float lastAutoSaveTime = 0f;
    private const float AUTO_SAVE_COOLDOWN = 0.5f; // Minimum time between auto-saves

    /// <summary>
    /// Public method to trigger auto-save (called by other editor components)
    /// </summary>
    public void AutoSave()
    {
        if (!autoSaveEnabled) return;
        
        // Prevent too frequent saves
        if (Time.time - lastAutoSaveTime < AUTO_SAVE_COOLDOWN) return;
        lastAutoSaveTime = Time.time;
        
        // Only auto-save if we have a current file loaded
        if (string.IsNullOrEmpty(currentJsonPath)) return;
        
        PerformSave();
        Debug.Log("[AiEditorFileUI] Auto-saved changes");
    }
    
    /// <summary>
    /// Core save logic (extracted from OnSaveClicked)
    /// </summary>
    private void PerformSave()
    {
        // Determine branch by which start button is active
        string folder = "";
        if (startTurretButton.gameObject.activeSelf)
            folder = turretFolder;
        else if (startNavButton.gameObject.activeSelf)
            folder = navFolder;
        else
            return; // No branch selected

        // Get the filename from the in-game title field
        // Always get the tree name from FileButtonPanel first, then fallback to FileName field
        string treeName = "NewAI";
        
        // PRIORITY 1: Get from FileButtonPanel (the primary display location)
        string currentTreeName = GetCurrentTreeName();
        if (!string.IsNullOrEmpty(currentTreeName))
        {
            treeName = currentTreeName;
        }
        // PRIORITY 2: Fallback to FileName field if FileButtonPanel is empty
        else if (FileName != null && !string.IsNullOrEmpty(FileName.text))
        {
            treeName = FileName.text;
        }
        string assetName = treeName;
        
        // Check if we're updating an existing file
        if (!string.IsNullOrEmpty(currentJsonPath) && System.IO.File.Exists(currentJsonPath))
        {
            // Load existing JSON file and update it
            try
            {
                string jsonContent = System.IO.File.ReadAllText(currentJsonPath);
                var jsonAsset = JsonUtility.FromJson<AiTreeAssetJson>(jsonContent);
                
                if (jsonAsset != null)
                {
                    // Check if the tree name has changed
                    bool treeNameChanged = jsonAsset.TreeName != treeName;
                    
                    // Update the tree name and title
                    jsonAsset.TreeName = treeName;
                    jsonAsset.title = treeName; // Also update the title
                    jsonAsset.branchType = (folder == navFolder) ? AiBranchTypeJson.Nav : AiBranchTypeJson.Turret;
                    
                    // Ensure instanceId is set for existing asset (preserve existing or generate new if missing)
                    if (string.IsNullOrEmpty(jsonAsset.instanceId))
                    {
                        jsonAsset.instanceId = System.Guid.NewGuid().ToString();
                    }
                    
                    // Update all node data and connections from the current editor state
                    UpdateJsonAssetFromEditor(jsonAsset);
                    
                    string updatedJsonContent = JsonUtility.ToJson(jsonAsset, true);
                    
                    // If the current file is in Assets folder, migrate it to persistent data path
                    if (currentJsonPath.Contains("Assets"))
                    {
                        // Save to persistent data path with the new filename if tree name changed, otherwise use original
                        string persistentFolderPath = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folder);
                        if (!System.IO.Directory.Exists(persistentFolderPath))
                        {
                            System.IO.Directory.CreateDirectory(persistentFolderPath);
                        }
                        
                        string fileName = treeNameChanged ? $"{assetName}.json" : System.IO.Path.GetFileName(currentJsonPath);
                        string newFilePath = System.IO.Path.Combine(persistentFolderPath, fileName);
                        System.IO.File.WriteAllText(newFilePath, updatedJsonContent);
                        
                        // Delete the old file from Assets folder
                        try
                        {
#if UNITY_EDITOR
                            // Normalize path for Unity AssetDatabase (use forward slashes and ensure proper format)
                            string assetPath = currentJsonPath.Replace("\\", "/");
                            if (!assetPath.StartsWith("Assets/"))
                            {
                                // Convert absolute path to relative Assets path if needed
                                int assetsIndex = assetPath.IndexOf("Assets/");
                                if (assetsIndex >= 0)
                                {
                                    assetPath = assetPath.Substring(assetsIndex);
                                }
                            }
                            
                            // Delete the asset file
                            UnityEditor.AssetDatabase.DeleteAsset(assetPath);
                            UnityEditor.AssetDatabase.Refresh();
#endif
                        }
                        catch (System.Exception deleteEx)
                        {
                            Debug.LogWarning($"[AiEditorFileUI] Could not delete old file {currentJsonPath}: {deleteEx.Message}");
                        }
                        
                        // Update currentJsonPath to the new location
                        currentJsonPath = newFilePath;
                    }
                    else
                    {
                        // File is already in persistent data path
                        if (treeNameChanged)
                        {
                            // Tree name changed, create new file with new name
                            string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folder);
                            string newFilePath = System.IO.Path.Combine(folderPath, $"{assetName}.json");
                            System.IO.File.WriteAllText(newFilePath, updatedJsonContent);
                            
                            // Delete the old file
                            try
                            {
                                System.IO.File.Delete(currentJsonPath);
                            }
                            catch (System.Exception deleteEx)
                            {
                                Debug.LogWarning($"[AiEditorFileUI] Could not delete old file {currentJsonPath}: {deleteEx.Message}");
                            }
                            
                            // Update currentJsonPath to the new file
                            currentJsonPath = newFilePath;
                        }
                        else
                        {
                            // Tree name didn't change, just overwrite the existing file
                            System.IO.File.WriteAllText(currentJsonPath, updatedJsonContent);
                        }
                    }
                    
                    Debug.Log($"[AiEditorFileUI] Updated existing file: {currentJsonPath}");
                }
                else
                {
                    Debug.LogError($"[AiEditorFileUI] Failed to parse existing JSON file: {currentJsonPath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AiEditorFileUI] Failed to update existing file {currentJsonPath}: {e.Message}");
            }
        }
        else
        {
            // Create new file
            var jsonAsset = new AiTreeAssetJson();
            jsonAsset.TreeName = treeName;
            jsonAsset.title = treeName;
            jsonAsset.branchType = (folder == navFolder) ? AiBranchTypeJson.Nav : AiBranchTypeJson.Turret;
            jsonAsset.instanceId = System.Guid.NewGuid().ToString();
            
            // Populate the JSON asset with current editor state
            UpdateJsonAssetFromEditor(jsonAsset);
            
            // Create the folder if it doesn't exist
            string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folder);
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }
            
            // Save the JSON file
            string filePath = System.IO.Path.Combine(folderPath, $"{assetName}.json");
            string jsonContent = JsonUtility.ToJson(jsonAsset, true);
            System.IO.File.WriteAllText(filePath, jsonContent);
            
            // Update currentJsonPath for future saves
            currentJsonPath = filePath;
            
            Debug.Log($"[AiEditorFileUI] Created new file: {filePath}");
        }
    }

    // Prefabs for node types (assign in inspector)
    // public GameObject startNodePrefab; // No longer needed, always reuse StartNodePanel
    public GameObject EndNodePrefab;
    public GameObject MiddleNodePrefab;
    public GameObject SubAINodePrefab;
    public GameObject UILinePrefab;

    // New public field for tree name
    public TMPro.TMP_Text FileName;

    void Start()
    {
        saveButton.onClick.AddListener(ToggleGuidePanel);
        loadButton.onClick.AddListener(ToggleLoadPanel);
        
        turretBranchButton.onClick.AddListener(() => ShowFilePanel(turretFileScrollView, turretFileContent, turretFolder));
        navBranchButton.onClick.AddListener(() => ShowFilePanel(navFileScrollView, navFileContent, navFolder));
        loadPanel.SetActive(false);
        navFileScrollView.SetActive(false);
        turretFileScrollView.SetActive(false);
    }

    // Helper function to determine if a node should have number input based on its label
    private bool ShouldHaveNumberInput(string nodeLabel)
    {
        if (string.IsNullOrEmpty(nodeLabel))
            return false;
            
        // Check for all the specific patterns that need number input
        return nodeLabel.Contains("If Self HP>#") || 
               nodeLabel.Contains("If Self HP<#") ||
               nodeLabel.Contains("If HP < #") ||
               nodeLabel.Contains("If HP > #") ||
               nodeLabel.Contains("If Tag = #") ||
               nodeLabel.Contains("If Tag < #") ||
               nodeLabel.Contains("If Tag > #") ||
               nodeLabel.Contains("If Range<#") ||
               nodeLabel.Contains("If Range>#") ||
               nodeLabel.Contains("LeadTarget#") ||
               nodeLabel.Contains("Lead Target #") ||
               nodeLabel.Contains("RotateUp") ||
               nodeLabel.Contains("Rotate Up") ||
               nodeLabel.Contains("RotateDown") ||
               nodeLabel.Contains("Rotate Down") ||
               nodeLabel.Contains("RotateLeft") ||
               nodeLabel.Contains("Rotate Left") ||
               nodeLabel.Contains("RotateRight") ||
               nodeLabel.Contains("Rotate Right") ||
               nodeLabel.Contains("MyTag#") ||
               nodeLabel.Contains("My Tag#") ||
               nodeLabel.Contains("TeamTag#") ||
               nodeLabel.Contains("Team Tag#") ||
               nodeLabel.Contains("IfMyTag") ||
               nodeLabel.Contains("If My Tag") ||
               nodeLabel.Contains("IfTeamTag") ||
               nodeLabel.Contains("If Team Tag") ||
               // Also check for patterns that already have numbers (not just #)
               nodeLabel.StartsWith("If Self HP>") ||
               nodeLabel.StartsWith("If Self HP<") ||
               nodeLabel.StartsWith("If HP < ") ||
               nodeLabel.StartsWith("If HP > ") ||
               nodeLabel.StartsWith("If Tag = ") ||
               nodeLabel.StartsWith("If Tag < ") ||
               nodeLabel.StartsWith("If Tag > ") ||
               nodeLabel.StartsWith("If Range<") ||
               nodeLabel.StartsWith("If Range>") ||
               nodeLabel.StartsWith("LeadTarget") ||
               nodeLabel.StartsWith("Lead Target ") ||
               nodeLabel.StartsWith("MyTag") ||
               nodeLabel.StartsWith("My Tag") ||
               nodeLabel.StartsWith("TeamTag") ||
               nodeLabel.StartsWith("Team Tag") ||
               nodeLabel.StartsWith("IfMyTag") ||
               nodeLabel.StartsWith("If My Tag") ||
               nodeLabel.StartsWith("IfTeamTag") ||
               nodeLabel.StartsWith("If Team Tag") ||
               nodeLabel.Contains("Cycle"); // Added for cycle nodes
    }

    void ToggleLoadPanel()
    {
        loadPanel.SetActive(!loadPanel.activeSelf);
        if (!loadPanel.activeSelf)
        {
            navFileScrollView.SetActive(false);
            turretFileScrollView.SetActive(false);
        }
        else
        {
            // Refresh file lists when opening the load panel
            // This ensures file name changes are reflected
            if (navFileScrollView.activeSelf)
            {
                ShowFilePanel(navFileScrollView, navFileContent, navFolder);
            }
            if (turretFileScrollView.activeSelf)
            {
                ShowFilePanel(turretFileScrollView, turretFileContent, turretFolder);
            }
        }
    }

    void ToggleGuidePanel()
    {
        if (guidePanel.activeSelf)
        {
            // Fade out
            StartCoroutine(FadePanel(guidePanel, 1f, 0f, 0.2f, () => guidePanel.SetActive(false)));
        }
        else
        {
            // Fade in
            guidePanel.SetActive(true);
            StartCoroutine(FadePanel(guidePanel, 0f, 1f, 0.2f, null));
        }
    }

    private System.Collections.IEnumerator FadePanel(GameObject panel, float startAlpha, float endAlpha, float duration, System.Action onComplete)
    {
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = panel.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = startAlpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, time / duration);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        onComplete?.Invoke();
    }
    
    void OnSaveClicked()
    {
        PerformSave();
    }
    
    /// <summary>
    /// Refreshes the currently visible file panel to reflect any file changes
    /// </summary>
    private void RefreshCurrentFilePanel()
    {
        if (navFileScrollView.activeSelf)
        {
            ShowFilePanel(navFileScrollView, navFileContent, navFolder);
        }
        else if (turretFileScrollView.activeSelf)
        {
            ShowFilePanel(turretFileScrollView, turretFileContent, turretFolder);
        }
    }
    
    /// <summary>
    /// Updates the JSON asset with current node positions, connections, and properties from the editor
    /// </summary>
    private void UpdateJsonAssetFromEditor(AiTreeAssetJson jsonAsset)
    {
        // --- Serialize nodes and connections ---
        var content = GameObject.Find("Content");
        var nodeDraggables = content.GetComponentsInChildren<NodeDraggable>();
        var nodeList = new List<AiNodeDataJson>();
        var nodeIdToDraggable = new Dictionary<string, NodeDraggable>();
        
        foreach (var node in nodeDraggables)
        {
            // Ensure nodeId is set
            if (string.IsNullOrEmpty(node.nodeId))
                node.nodeId = System.Guid.NewGuid().ToString();
                
            // Find the child named "NodeText" with TMP_Text
            string label = node.name;
            var textChild = node.transform.Find("NodeText");
            if (textChild != null)
            {
                var tmp = textChild.GetComponent<TMPro.TMP_Text>();
                if (tmp != null)
                    label = tmp.text;
            }
            
            // Save the node type as the GameObject name (e.g., EndNode(Clone), MiddleNode(Clone), SubAINode(Clone))
            string nodeType = node.name;
            
            var nodeData = new AiNodeDataJson
            {
                nodeId = node.nodeId,
                nodeType = nodeType, // e.g., EndNode(Clone)
                nodeLabel = label,   // NodeText
                position = node.GetComponent<RectTransform>().anchoredPosition,
                properties = new List<NodePropertyJson>()
            };
            
            // TODO: Add any additional node properties here if needed
            
            nodeList.Add(nodeData);
            nodeIdToDraggable[node.nodeId] = node;
        }
        
        // Save all output connections for each node
        var lineConnectors = content.GetComponentsInChildren<UILineConnector>();
        var connectionList = new List<AiConnectionDataJson>();
        
        foreach (var line in lineConnectors)
        {
            // Check if outputRect is StartNavButton or StartTurretButton under StartNodePanel
            var outputButton = line.outputRect != null ? line.outputRect.GetComponent<Button>() : null;
            string fromNodeId = null;
            string fromPortId = null;
            
            if (outputButton != null)
            {
                if (outputButton.gameObject.name == "StartNavButton")
                {
                    fromNodeId = "StartNavButton";
                    fromPortId = "NavOrigin";
                }
                else if (outputButton.gameObject.name == "StartTurretButton")
                {
                    fromNodeId = "StartTurretButton";
                    fromPortId = "TurretOrigin";
                }
            }
            
            var fromNode = line.outputRect != null ? line.outputRect.GetComponentInParent<NodeDraggable>() : null;
            var toNode = line.inputRect != null ? line.inputRect.GetComponentInParent<NodeDraggable>() : null;
            
            if (fromNodeId != null && toNode != null)
            {
                string toPortId = line.inputRect != null ? line.inputRect.gameObject.name : "InputPort";
                string toNodeId = toNode.nodeId;
                connectionList.Add(new AiConnectionDataJson
                {
                    fromNodeId = fromNodeId,
                    fromPortId = fromPortId,
                    toNodeId = toNodeId,
                    toPortId = toPortId
                });
            }
            else if (fromNode != null && toNode != null)
            {
                // Store the tag for the output port if it's an origin (NavOrigin, TurretOrigin), otherwise use OutputPort
                string portId = "OutputPort";
                if (line.outputRect != null)
                {
                    var tag = line.outputRect.gameObject.tag;
                    if (tag == "NavOrigin" || tag == "TurretOrigin")
                        portId = tag;
                    else
                        portId = line.outputRect.gameObject.name;
                }
                string toPortId = line.inputRect != null ? line.inputRect.gameObject.name : "InputPort";
                connectionList.Add(new AiConnectionDataJson
                {
                    fromNodeId = fromNode.nodeId,
                    fromPortId = portId,
                    toNodeId = toNode.nodeId,
                    toPortId = toPortId
                });
            }
        }
        
        jsonAsset.nodes = nodeList;
        jsonAsset.connections = connectionList;
        
        // Generate execution data
        GenerateExecutionData(jsonAsset, nodeList, connectionList);
    }

    // Helper to get the current tree name from FileButtonPanel
    string GetCurrentTreeName()
    {
        // Look for UICanvas>FileButtonPanel>FileNameText
        var uiCanvas = GameObject.Find("UICanvas");
        if (uiCanvas != null)
        {
            var fileButtonPanel = uiCanvas.transform.Find("FileButtonPanel");
            if (fileButtonPanel != null)
            {
                // Look for FileNameText specifically (not TitleName component)
                var fileNameText = fileButtonPanel.transform.Find("FileNameText");
                if (fileNameText != null)
                {
                    var tmpText = fileNameText.GetComponent<TMPro.TMP_Text>();
                    if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                    {
                        return tmpText.text;
                    }
                }
            }
        }
        return "";
    }

    // Helper to set the current tree name in FileButtonPanel
    void SetCurrentTreeName(string treeName)
    {
        // Look for UICanvas>FileButtonPanel>FileNameText
        var uiCanvas = GameObject.Find("UICanvas");
        if (uiCanvas != null)
        {
            var fileButtonPanel = uiCanvas.transform.Find("FileButtonPanel");
            if (fileButtonPanel != null)
            {
                // Look for FileNameText specifically
                var fileNameText = fileButtonPanel.transform.Find("FileNameText");
                if (fileNameText != null)
                {
                    var tmpText = fileNameText.GetComponent<TMPro.TMP_Text>();
                    if (tmpText != null)
                    {
                        tmpText.text = treeName;
                        return;
                    }
                }
            }
        }
        
        // Fallback: Try to set in old location for backwards compatibility
        var startNodePanel = GameObject.Find("StartNodePanel");
        if (startNodePanel != null)
        {
            var fileNameTextObj = startNodePanel.transform.Find("FileNameText");
            if (fileNameTextObj != null)
            {
                var tmp = fileNameTextObj.GetComponent<TMPro.TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = treeName;
                }
            }
        }
    }

    // Helper to get the label from the starting node (LEGACY - keeping for backwards compatibility)
    string GetStartNodeLabel()
    {
        // First try the new location
        string treeName = GetCurrentTreeName();
        if (!string.IsNullOrEmpty(treeName))
            return treeName;
            
        // Fallback to old location for backwards compatibility
        var startNodePanel = GameObject.Find("StartNodePanel");
        if (startNodePanel != null)
        {
            var fileNameText = startNodePanel.GetComponentInChildren<TMPro.TMP_Text>();
            if (fileNameText != null)
                return fileNameText.text;
        }
        return "";
    }

    void ShowFilePanel(GameObject scrollView, Transform contentPanel, string folder)
    {
        navFileScrollView.SetActive(false);
        turretFileScrollView.SetActive(false);
        scrollView.SetActive(true);
        
        // Clear previous
        foreach (Transform child in contentPanel) Destroy(child.gameObject);
        
        // Only load player-owned JSON files from persistent data path
        var allFiles = new List<string>();
        
        // Look for JSON files in Application.persistentDataPath (user-created/edited files)
        string jsonFolderPath = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folder);
        if (System.IO.Directory.Exists(jsonFolderPath)) 
        {
            var persistentFiles = System.IO.Directory.GetFiles(jsonFolderPath, "*.json");
            allFiles.AddRange(persistentFiles);
        }
        else
        {
            // Create the directory if it doesn't exist
            System.IO.Directory.CreateDirectory(jsonFolderPath);
        }
        
        // Process all player-owned files
        var sortedFiles = allFiles.OrderBy(f => f).ToArray();
        foreach (var file in sortedFiles)
        {
            CreateFileButton(file, contentPanel, folder);
        }
    }
    
    void CreateFileButton(string file, Transform contentPanel, string folder)
    {
        var btnObj = Instantiate(fileButtonPrefab, contentPanel);
        var btn = btnObj.GetComponent<Button>();
        var txt = btnObj.GetComponentInChildren<TMPro.TMP_Text>();
        
        if (txt != null) 
        {
            // Load the JSON to get the proper title instead of using filename
            string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
            
            // Try to read the file and parse the TreeName
            try
            {
                string jsonContent = System.IO.File.ReadAllText(file);
                AiTreeAssetJson jsonAsset = JsonUtility.FromJson<AiTreeAssetJson>(jsonContent);
                if (jsonAsset != null && !string.IsNullOrEmpty(jsonAsset.TreeName))
                {
                    txt.text = jsonAsset.TreeName; // Use TreeName property
                }
                else if (jsonAsset != null && !string.IsNullOrEmpty(jsonAsset.title))
                {
                    txt.text = jsonAsset.title; // Fallback to title
                }
                else
                {
                    txt.text = fileName; // Fallback to filename
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AiEditorFileUI] Could not parse JSON file {fileName}: {e.Message}");
                txt.text = fileName; // Fallback to filename
            }
        }
        
        btn.onClick.AddListener(() => OnFileSelected(file, folder));
    }
    
    void LoadJsonAssetIntoEditor(AiTreeAssetJson jsonAsset)
    {
        // Sync all tree name fields when loading
        SyncAllTreeNameFields(jsonAsset.TreeName);
        
        // Clear existing nodes and lines from the Content panel, except StartNodePanel
        var content = GameObject.Find("Content");
        var startPanel = GameObject.Find("StartNodePanel");
        // Explicitly destroy all node and line prefabs before loading
        var toDestroy = new List<GameObject>();
        foreach (Transform child in content.transform)
        {
            if (child.gameObject == startPanel) continue;
            string n = child.gameObject.name;
            if (n == "UILine(Clone)" || n == "MiddleNode(Clone)" || n == "SubAINode(Clone)" || n == "EndNode(Clone)")
                toDestroy.Add(child.gameObject);
        }
        foreach (var go in toDestroy)
            DestroyImmediate(go);

        // --- Always run branch label/button hiding logic FIRST ---
        if (jsonAsset.branchType == AiBranchTypeJson.Nav)
        {
            var turretLabel = GameObject.Find("TurretLabel");
            if (turretLabel != null) turretLabel.SetActive(false);
            
            var navLabel = GameObject.Find("NavLabel");
            if (navLabel != null) navLabel.SetActive(true);
            
            // Find buttons under StartNodePanel (consistent with connection code)
            if (startPanel != null)
            {
                var turretBtn = startPanel.transform.Find("StartTurretButton");
                if (turretBtn != null) turretBtn.gameObject.SetActive(false);
                
                var navBtn = startPanel.transform.Find("StartNavButton");
                if (navBtn != null) navBtn.gameObject.SetActive(true);
            }
        }
        else if (jsonAsset.branchType == AiBranchTypeJson.Turret)
        {
            var navLabel = GameObject.Find("NavLabel");
            if (navLabel != null) navLabel.SetActive(false);
            
            var turretLabel = GameObject.Find("TurretLabel");
            if (turretLabel != null) turretLabel.SetActive(true);
            
            // Find buttons under StartNodePanel (consistent with connection code)
            if (startPanel != null)
            {
                var navBtn = startPanel.transform.Find("StartNavButton");
                if (navBtn != null) navBtn.gameObject.SetActive(false);
                
                var turretBtn = startPanel.transform.Find("StartTurretButton");
                if (turretBtn != null) turretBtn.gameObject.SetActive(true);
            }
        }

        // COLLAPSE: Detect SubAI-prefixed nodes and collapse them back into SubAI nodes for the visual editor
        CollapseSubAINodes(jsonAsset);

        // Load nodes and connections
        LoadNodesFromJsonAsset(jsonAsset, content, startPanel);
        LoadConnectionsFromJsonAsset(jsonAsset, content, startPanel);
    }

    void OnFileSelected(string filePath, string folder)
    {
        // Load AiTreeAssetJson and reconstruct node graph
        // All files shown in AI Editor are now player-owned, so we can directly use the file
        currentJsonPath = filePath.Replace("\\", "/");
        
        // Try to load the JSON file directly
        try
        {
            string jsonContent = System.IO.File.ReadAllText(currentJsonPath);
            AiTreeAssetJson jsonAsset = JsonUtility.FromJson<AiTreeAssetJson>(jsonContent);
            
            if (jsonAsset != null)
            {
                // Set branch type based on folder
                jsonAsset.branchType = (folder == navFolder) ? AiBranchTypeJson.Nav : AiBranchTypeJson.Turret;
                
                // Load the node graph using shared logic
                LoadJsonAssetIntoEditor(jsonAsset);
                
                loadPanel.SetActive(false);
            }
            else
            {
                Debug.LogError($"[AiEditorFileUI] Failed to parse JSON from file: {currentJsonPath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AiEditorFileUI] Failed to load file {currentJsonPath}: {e.Message}");
        }
    }
    
    void LoadNodesFromJsonAsset(AiTreeAssetJson jsonAsset, GameObject content, GameObject startPanel)
    {
        // --- NodeId-based mapping ---
        var nodeIdToGameObject = new Dictionary<string, GameObject>();
        // --- Handle StartNodePanel and all nodes ---
        foreach (var nodeData in jsonAsset.nodes)
        {
            GameObject nodeGO = null;            if ((nodeData.nodeType == "Start" || nodeData.nodeLabel == jsonAsset.TreeName) && startPanel != null)
            {
                // Update StartNodePanel label and position
                // NEW: Set filename in FileButtonPanel using TitleName component
                SetCurrentTreeName(jsonAsset.TreeName);
                
                var rect = startPanel.GetComponent<RectTransform>();
                rect.anchoredPosition = nodeData.position;
                var title = startPanel.GetComponentInChildren<TitleName>();
                if (title != null)
                    title.SetTitle(nodeData.nodeLabel);                // Set nodeId if possible
                var nd = startPanel.GetComponent<NodeDraggable>();
                if (nd != null) nd.nodeId = nodeData.nodeId;
                // Set TMP_Text child named "Text" to node label
                var textChild = startPanel.transform.Find("Text");
                if (textChild != null)
                {
                    var tmp = textChild.GetComponent<TMPro.TMP_Text>();
                    if (tmp != null)
                        tmp.text = nodeData.nodeLabel;
                }
                nodeGO = startPanel;
            }
            else
            {
                // Instantiate other nodes based on type
                if (nodeData.nodeType.StartsWith("EndNode"))
                {
                    nodeGO = Instantiate(EndNodePrefab, content.transform);
                }
                else if (nodeData.nodeType.StartsWith("MiddleNode"))
                {
                    nodeGO = Instantiate(MiddleNodePrefab, content.transform);
                }
                else if (nodeData.nodeType.StartsWith("SubAINode"))
                {
                    nodeGO = Instantiate(SubAINodePrefab, content.transform);
                }                
                else
                {
                    // Fallback: use EndNodePrefab
                    nodeGO = Instantiate(EndNodePrefab, content.transform);
                }
                var rect = nodeGO.GetComponent<RectTransform>();
                rect.anchoredPosition = nodeData.position;
                var title = nodeGO.GetComponentInChildren<TitleName>();
                if (title != null)
                    title.SetTitle(nodeData.nodeLabel);
                // Also set TMP_Text directly if present (for action nodes)
                var labelText = nodeGO.GetComponentInChildren<TMPro.TMP_Text>();
                if (labelText != null)
                    labelText.text = nodeData.nodeLabel;
                // Set nodeId
                var nd = nodeGO.GetComponent<NodeDraggable>();
                if (nd != null) nd.nodeId = nodeData.nodeId;
                // Set TMP_Text child named "Text" to node label
                var textChild = nodeGO.transform.Find("Text");
                if (textChild != null)
                {
                    var tmp = textChild.GetComponent<TMPro.TMP_Text>();
                    if (tmp != null)
                        tmp.text = nodeData.nodeLabel;
                }
                
                // Note: Number input functionality is now handled by node click events
                // The number is stored directly in NodeText, no need for NumberInputButton setup
            }
              // All node data including numbers is now stored directly in nodeData.nodeLabel (NodeText)
            
            // Register in map
            if (!string.IsNullOrEmpty(nodeData.nodeId) && nodeGO != null)
                nodeIdToGameObject[nodeData.nodeId] = nodeGO;        }
    }
    
    void LoadConnectionsFromJsonAsset(AiTreeAssetJson jsonAsset, GameObject content, GameObject startPanel)
    {
        // --- Create nodeId mapping for connections ---
        var nodeIdToGameObject = new Dictionary<string, GameObject>();
        
        // Add StartNodePanel to mapping
        if (startPanel != null)
        {
            var startDraggable = startPanel.GetComponent<NodeDraggable>();
            if (startDraggable != null && !string.IsNullOrEmpty(startDraggable.nodeId))
            {
                nodeIdToGameObject[startDraggable.nodeId] = startPanel;
            }
        }
        
        // Add all other nodes to mapping
        var nodeDraggables = content.GetComponentsInChildren<NodeDraggable>();
        foreach (var node in nodeDraggables)
        {
            if (!string.IsNullOrEmpty(node.nodeId) && node.gameObject != startPanel)
            {
                nodeIdToGameObject[node.nodeId] = node.gameObject;
            }
        }
        
        // --- Recreate connections using nodeId mapping ---
        foreach (var conn in jsonAsset.connections)
        {
            // Special handling for StartNavButton/StartTurretButton as origin
            GameObject fromNode = null;
            Button outputButton = null;            
            
            if (conn.fromNodeId == "StartNavButton")
            {
                if (startPanel != null)
                {
                    var navBtn = startPanel.transform.Find("StartNavButton");
                    if (navBtn != null && navBtn.gameObject.activeSelf)
                        outputButton = navBtn.GetComponent<Button>();
                }
                fromNode = startPanel;
            }
            else if (conn.fromNodeId == "StartTurretButton")
            {
                if (startPanel != null)
                {
                    var turretBtn = startPanel.transform.Find("StartTurretButton");
                    if (turretBtn != null && turretBtn.gameObject.activeSelf)
                        outputButton = turretBtn.GetComponent<Button>();
                }
                fromNode = startPanel;
            }
            else if (!string.IsNullOrEmpty(conn.fromNodeId) && nodeIdToGameObject.ContainsKey(conn.fromNodeId))
            {
                fromNode = nodeIdToGameObject[conn.fromNodeId];
            }
            
            if (string.IsNullOrEmpty(conn.toNodeId) || !nodeIdToGameObject.ContainsKey(conn.toNodeId)) continue;
            var toNode = nodeIdToGameObject[conn.toNodeId];
            
            // Find input port/button
            Button inputButton = null;
            foreach (var btn in toNode.GetComponentsInChildren<Button>())
                if (btn.CompareTag("InputPort")) { inputButton = btn; break; }
            
            // For non-origin, find output port/button
            if (outputButton == null && fromNode != null)
            {
                if (conn.fromPortId == "NavOrigin" || conn.fromPortId == "TurretOrigin")
                {
                    foreach (var btn in fromNode.GetComponentsInChildren<Button>())
                        if (btn.CompareTag(conn.fromPortId)) { outputButton = btn; break; }
                }
                else
                {
                    foreach (var btn in fromNode.GetComponentsInChildren<Button>())
                        if (btn.CompareTag("OutputPort")) { outputButton = btn; break; }
                }
            }
            
            if (outputButton == null || inputButton == null) continue;
            
            // Instantiate line using UILinePrefab
            var lineGO = Instantiate(UILinePrefab, content.transform);
            var lineRect = lineGO.GetComponent<RectTransform>();
            
            // Set up UILineConnector
            var connector = lineGO.GetComponent<UILineConnector>();
            if (connector == null) connector = lineGO.AddComponent<UILineConnector>();
            connector.outputRect = outputButton.GetComponent<RectTransform>();
            connector.inputRect = inputButton.GetComponent<RectTransform>();
            connector.canvas = content.GetComponentInParent<Canvas>();
            connector.UpdateLine();
            
            // Add click-to-delete functionality
            if (lineGO.GetComponent<UILineClickDeleter>() == null)
                lineGO.AddComponent<UILineClickDeleter>();
            
            // Register with NodeDraggable for drag updates
            var fromDraggable = fromNode != null ? fromNode.GetComponent<NodeDraggable>() : null;
            var toDraggable = toNode.GetComponent<NodeDraggable>();            
            if (fromDraggable != null) fromDraggable.RegisterConnectedLine(connector);
            if (toDraggable != null) toDraggable.RegisterConnectedLine(connector);        
        }
    }

    /// <summary>
    /// Generates execution data from the visual node graph for AI runtime execution.
    /// IMPORTANT: Works on COPIES of node/connection lists so the visual editor state
    /// (including SubAI nodes) is preserved in the JSON's nodes/connections arrays.
    /// </summary>
    private void GenerateExecutionData(AiTreeAssetJson jsonAsset, List<AiNodeDataJson> nodeList, List<AiConnectionDataJson> connectionList)
    {
        jsonAsset.executableNodes.Clear();
        
        // IMPORTANT: Create deep copies of the lists so we don't modify the saved editor state
        // SubAI nodes should remain in the nodes/connections arrays for the visual editor
        // Only the executableNodes array should be flattened
        var nodeListCopy = new List<AiNodeDataJson>();
        foreach (var node in nodeList)
        {
            nodeListCopy.Add(new AiNodeDataJson
            {
                nodeId = node.nodeId,
                nodeType = node.nodeType,
                nodeLabel = node.nodeLabel,
                position = node.position,
                properties = node.properties != null ? new List<NodePropertyJson>(node.properties) : new List<NodePropertyJson>()
            });
        }
        
        var connectionListCopy = new List<AiConnectionDataJson>();
        foreach (var conn in connectionList)
        {
            connectionListCopy.Add(new AiConnectionDataJson
            {
                fromNodeId = conn.fromNodeId,
                fromPortId = conn.fromPortId,
                toNodeId = conn.toNodeId,
                toPortId = conn.toPortId
            });
        }
        
        // Expand SubAI nodes in the COPIES only (not the originals)
        ExpandSubAINodes(ref nodeListCopy, ref connectionListCopy, jsonAsset.branchType);
        
        // Find the start node ID from the EXPANDED list
        jsonAsset.startNodeId = null;
        foreach (var conn in connectionListCopy)
        {
            if (conn.fromNodeId == "StartNavButton" || conn.fromNodeId == "StartTurretButton")
            {
                jsonAsset.startNodeId = conn.toNodeId;
                break;
            }
        }
        
        // Convert each node from the EXPANDED list to executable format
        foreach (var nodeData in nodeListCopy)
        {
            float numericValue = 0f;
            string methodName = AiEditor.AiMethodConverter.ConvertToMethodName(nodeData.nodeLabel, out numericValue);
            
            // Determine node type - check GameObject name first, then label content
            AiEditor.AiNodeType nodeType;
            if (nodeData.nodeType.Contains("SubAINode"))
            {
                nodeType = AiEditor.AiNodeType.SubAI;
            }
            else
            {
                nodeType = AiEditor.AiMethodConverter.DetermineNodeType(nodeData.nodeLabel);
            }
            
            // The numeric value is now extracted directly from the node label by AiMethodConverter.ConvertToMethodName
            // No need to check NumberInputButton since we store numbers directly in NodeText
            
            var executableNode = new AiExecutableNodeJson
            {
                nodeId = nodeData.nodeId,
                methodName = methodName,
                originalLabel = nodeData.nodeLabel,
                nodeType = (AiNodeTypeJson)nodeType, // Cast AiEditor.AiNodeType to AiNodeTypeJson
                numericValue = numericValue, // This comes from ConvertToMethodName parsing the label
                position = nodeData.position,
                connectedNodeIds = new List<string>()
            };
            
            // Find all nodes this one connects to (from the EXPANDED connection list)
            foreach (var conn in connectionListCopy)
            {
                if (conn.fromNodeId == nodeData.nodeId)
                {
                    executableNode.connectedNodeIds.Add(conn.toNodeId);
                }
            }
            
            // Sort connected nodes by Y-position for priority handling
            executableNode.connectedNodeIds.Sort((id1, id2) => {
                var node1 = nodeListCopy.Find(n => n.nodeId == id1);
                var node2 = nodeListCopy.Find(n => n.nodeId == id2);
                if (node1 == null || node2 == null) return 0;
                return node1.position.y.CompareTo(node2.position.y); // Higher Y = higher priority
            });
            
            jsonAsset.executableNodes.Add(executableNode);
        }
        
        // CRITICAL: Update nodes AND connections arrays with expanded data
        // TankMan queries connections to find nodes connected from StartNavButton
        // The visual editor will collapse SubAI_ prefixed nodes back into SubAI nodes on load
        
        // Update nodes array with expanded nodes
        jsonAsset.nodes.Clear();
        foreach (var node in nodeListCopy)
        {
            jsonAsset.nodes.Add(node);
        }
        
        // Update connections array with expanded connections
        jsonAsset.connections.Clear();
        foreach (var conn in connectionListCopy)
        {
            jsonAsset.connections.Add(conn);
        }
        
        Debug.Log($"[AiEditorFileUI] Generated {jsonAsset.executableNodes.Count} executable nodes");
        Debug.Log($"[AiEditorFileUI] Saved {jsonAsset.nodes.Count} nodes and {jsonAsset.connections.Count} connections (all flattened)");
    }
    
    /// <summary>
    /// Helper method to find a node GameObject by its nodeId
    /// </summary>
    private GameObject FindNodeGameObjectById(string nodeId)
    {
        var content = GameObject.Find("Content");
        if (content == null) return null;
        
        var nodeDraggables = content.GetComponentsInChildren<NodeDraggable>();
        foreach (var node in nodeDraggables)
        {
            if (node.nodeId == nodeId)
                return node.gameObject;
        }
        return null;
    }

    // Helper to keep FileName field and FileButtonPanel in sync
    void SyncAllTreeNameFields(string treeName)
    {
        // Update FileNameText at UICanvas>Background>FileButtonPanel>FileNameText (primary display)
        SetCurrentTreeName(treeName);
        
        // Update FileName field (legacy/backup) - if it exists in inspector
        if (FileName != null)
        {
            FileName.text = treeName;
        }
    }
    
    /// <summary>
    /// Collapses SubAI-prefixed nodes back into SubAI nodes for the visual editor.
    /// This reverses the flattening done during save so the user sees SubAI nodes instead of expanded nodes.
    /// Node ID format: SubAI_{guid}_{SubAIFileName}_{originalNodeId}
    /// </summary>
    private void CollapseSubAINodes(AiTreeAssetJson jsonAsset)
    {
        // Pattern: SubAI_{8-char-guid}_{filename}_
        var subAIPattern = new System.Text.RegularExpressions.Regex(@"^SubAI_([a-f0-9]{8})_(.+?)_(.+)$");
        
        // Group nodes by their SubAI source (guid_filename combo)
        var subAIGroups = new Dictionary<string, List<AiNodeDataJson>>();
        var subAIFileNames = new Dictionary<string, string>(); // group key -> original filename
        var nodesToRemove = new List<AiNodeDataJson>();
        
        foreach (var node in jsonAsset.nodes)
        {
            var match = subAIPattern.Match(node.nodeId);
            if (match.Success)
            {
                string guid = match.Groups[1].Value;
                string fileName = match.Groups[2].Value;
                string groupKey = $"{guid}_{fileName}";
                
                if (!subAIGroups.ContainsKey(groupKey))
                {
                    subAIGroups[groupKey] = new List<AiNodeDataJson>();
                    // Restore the original filename (underscores back to spaces)
                    subAIFileNames[groupKey] = fileName.Replace("_", " ").Trim();
                }
                subAIGroups[groupKey].Add(node);
                nodesToRemove.Add(node);
            }
        }
        
        if (subAIGroups.Count == 0)
        {
            Debug.Log("[AiEditorFileUI] No SubAI-prefixed nodes found, skipping collapse");
            return;
        }
        
        Debug.Log($"[AiEditorFileUI] Found {subAIGroups.Count} SubAI group(s) to collapse");
        
        // Build new connections list
        var newConnections = new List<AiConnectionDataJson>();
        
        // For each SubAI group, create a single SubAI node
        foreach (var kvp in subAIGroups)
        {
            string groupKey = kvp.Key;
            var groupNodes = kvp.Value;
            string subAIFileName = subAIFileNames[groupKey];
            
            // Try to read the original SubAI node position from node properties
            float originalX = 0, originalY = 0;
            bool foundOriginalPos = false;
            
            // Check the first node's properties for stored position
            if (groupNodes.Count > 0 && groupNodes[0].properties != null)
            {
                var xProp = groupNodes[0].properties.Find(p => p.key == "OriginalSubAI_X");
                var yProp = groupNodes[0].properties.Find(p => p.key == "OriginalSubAI_Y");
                
                if (xProp != null && yProp != null)
                {
                    if (float.TryParse(xProp.value, out originalX) && float.TryParse(yProp.value, out originalY))
                    {
                        foundOriginalPos = true;
                    }
                }
            }
            
            // Fallback: calculate from minimum position if original wasn't stored
            if (!foundOriginalPos)
            {
                originalX = float.MaxValue;
                originalY = float.MaxValue;
                foreach (var n in groupNodes)
                {
                    if (n.position.x < originalX) originalX = n.position.x;
                    if (n.position.y < originalY) originalY = n.position.y;
                }
            }
            
            // Create SubAI node with a unique ID based on the group
            string subAINodeId = $"CollapsedSubAI_{groupKey}";
            var subAINode = new AiNodeDataJson
            {
                nodeId = subAINodeId,
                nodeType = "SubAINode(Clone)",
                nodeLabel = subAIFileName,
                position = new Vector2(originalX, originalY),
                properties = new List<NodePropertyJson>()
            };
            
            // Build set of group node IDs for quick lookup
            var groupNodeIds = new HashSet<string>(groupNodes.Select(n => n.nodeId));
            
            // Track which external sources connect to this SubAI (to avoid duplicates)
            var externalSourcesHandled = new HashSet<string>();
            
            // Process all connections involving this group
            foreach (var conn in jsonAsset.connections)
            {
                bool fromInGroup = groupNodeIds.Contains(conn.fromNodeId);
                bool toInGroup = groupNodeIds.Contains(conn.toNodeId);
                
                if (fromInGroup && toInGroup)
                {
                    // Internal connection - skip it (will be removed)
                    continue;
                }
                else if (!fromInGroup && toInGroup)
                {
                    // External -> Group: rewire to SubAI node (dedupe by source)
                    string sourceKey = $"{conn.fromNodeId}_{conn.fromPortId}";
                    if (!externalSourcesHandled.Contains(sourceKey))
                    {
                        newConnections.Add(new AiConnectionDataJson
                        {
                            fromNodeId = conn.fromNodeId,
                            fromPortId = conn.fromPortId,
                            toNodeId = subAINodeId,
                            toPortId = "Input"
                        });
                        externalSourcesHandled.Add(sourceKey);
                    }
                }
                else if (fromInGroup && !toInGroup)
                {
                    // Group -> External: rewire from SubAI node
                    newConnections.Add(new AiConnectionDataJson
                    {
                        fromNodeId = subAINodeId,
                        fromPortId = "Output",
                        toNodeId = conn.toNodeId,
                        toPortId = conn.toPortId
                    });
                }
                // If neither end is in group, it's handled below
            }
            
            // Add the SubAI node to the nodes list
            jsonAsset.nodes.Add(subAINode);
            
            Debug.Log($"[AiEditorFileUI] Collapsed {groupNodes.Count} nodes into SubAI: {subAIFileName} (id: {subAINodeId})");
        }
        
        // Build set of ALL group node IDs
        var allGroupNodeIds = new HashSet<string>();
        foreach (var group in subAIGroups.Values)
        {
            foreach (var node in group)
            {
                allGroupNodeIds.Add(node.nodeId);
            }
        }
        
        // Keep connections that don't involve any group nodes
        foreach (var conn in jsonAsset.connections)
        {
            bool fromInAnyGroup = allGroupNodeIds.Contains(conn.fromNodeId);
            bool toInAnyGroup = allGroupNodeIds.Contains(conn.toNodeId);
            
            if (!fromInAnyGroup && !toInAnyGroup)
            {
                newConnections.Add(conn);
            }
        }
        
        // Remove all the expanded nodes that are now collapsed
        foreach (var node in nodesToRemove)
        {
            jsonAsset.nodes.Remove(node);
        }
        
        // Replace connections with the new list
        jsonAsset.connections.Clear();
        jsonAsset.connections.AddRange(newConnections);
        
        // Clean up any duplicate connections
        var uniqueConnections = jsonAsset.connections
            .GroupBy(c => $"{c.fromNodeId}_{c.fromPortId}_{c.toNodeId}_{c.toPortId}")
            .Select(g => g.First())
            .ToList();
        
        jsonAsset.connections.Clear();
        jsonAsset.connections.AddRange(uniqueConnections);
        
        Debug.Log($"[AiEditorFileUI] After collapse: {jsonAsset.nodes.Count} nodes, {jsonAsset.connections.Count} connections");
    }
    
    /// <summary>
    /// Expands SubAI nodes by inlining the referenced AI tree's nodes and connections.
    /// This "flattens" the tree so TankMan can execute it normally without special SubAI handling.
    /// Handles nested SubAI references by iterating until no SubAI nodes remain.
    /// </summary>
    private void ExpandSubAINodes(ref List<AiNodeDataJson> nodeList, ref List<AiConnectionDataJson> connectionList, AiBranchTypeJson branchType)
    {
        // Track which SubAI files we've already loaded to prevent infinite recursion
        var loadedSubAIs = new HashSet<string>();
        
        // Keep expanding until no more SubAI nodes exist (handles nested SubAI)
        int maxIterations = 50; // Safety limit to prevent infinite loops
        int iteration = 0;
        
        while (iteration < maxIterations)
        {
            iteration++;
            
            // Find all SubAI nodes in current list
            var subAINodes = nodeList.Where(n => n.nodeType.Contains("SubAINode")).ToList();
            
            if (subAINodes.Count == 0)
            {
                Debug.Log($"[AiEditorFileUI] SubAI expansion complete after {iteration} iteration(s)");
                break;
            }
            
            Debug.Log($"[AiEditorFileUI] Iteration {iteration}: Found {subAINodes.Count} SubAI node(s) to expand");
            
            foreach (var subAINode in subAINodes)
            {
                // The node label contains the SubAI file name/title
                string subAIName = subAINode.nodeLabel;
                
                if (string.IsNullOrEmpty(subAIName))
                {
                    Debug.LogWarning($"[AiEditorFileUI] SubAI node has no label, removing");
                    nodeList.Remove(subAINode);
                    connectionList.RemoveAll(c => c.fromNodeId == subAINode.nodeId || c.toNodeId == subAINode.nodeId);
                    continue;
                }
                
                // Prevent infinite recursion if a SubAI references itself or creates a cycle
                string subAIKey = $"{subAIName}_{subAINode.nodeId}";
                if (loadedSubAIs.Contains(subAIName))
                {
                    Debug.LogWarning($"[AiEditorFileUI] Circular SubAI reference detected: {subAIName}, removing node");
                    nodeList.Remove(subAINode);
                    connectionList.RemoveAll(c => c.fromNodeId == subAINode.nodeId || c.toNodeId == subAINode.nodeId);
                    continue;
                }
                loadedSubAIs.Add(subAIName);
                
                // Load the referenced SubAI tree
                AiTreeAssetJson subAITree = LoadSubAITree(subAIName, branchType);
                
                if (subAITree == null || subAITree.nodes == null || subAITree.nodes.Count == 0)
                {
                    Debug.LogWarning($"[AiEditorFileUI] Could not load SubAI tree or tree is empty: {subAIName}");
                    nodeList.Remove(subAINode);
                    connectionList.RemoveAll(c => c.fromNodeId == subAINode.nodeId || c.toNodeId == subAINode.nodeId);
                    continue;
                }
                
                Debug.Log($"[AiEditorFileUI] Expanding SubAI: {subAIName} with {subAITree.nodes.Count} nodes and {subAITree.connections.Count} connections");
                
                // Generate unique prefix for inlined node IDs that includes the SubAI name
                // Format: SubAI_{guid}_{SubAIFileName}_ so we can extract the filename on load
                // Use a safe filename (replace spaces/special chars)
                string safeSubAIName = System.Text.RegularExpressions.Regex.Replace(subAIName, @"[^a-zA-Z0-9]", "_");
                string nodeIdPrefix = $"SubAI_{System.Guid.NewGuid().ToString().Substring(0, 8)}_{safeSubAIName}_";
                
                // Find ALL connections TO the SubAI node (these are the "parent" sources)
                var incomingConnections = connectionList.Where(c => c.toNodeId == subAINode.nodeId).ToList();
                
                // Find connections FROM the SubAI node (if any - these would chain to other nodes)
                var outgoingConnections = connectionList.Where(c => c.fromNodeId == subAINode.nodeId).ToList();
                
                // Find ALL entry points in the SubAI tree (all nodes connected from StartNavButton/StartTurretButton)
                var subAIEntryNodeIds = new List<string>();
                foreach (var conn in subAITree.connections)
                {
                    if (conn.fromNodeId == "StartNavButton" || conn.fromNodeId == "StartTurretButton")
                    {
                        if (!subAIEntryNodeIds.Contains(conn.toNodeId))
                        {
                            subAIEntryNodeIds.Add(conn.toNodeId);
                        }
                    }
                }
                
                if (subAIEntryNodeIds.Count == 0)
                {
                    Debug.LogWarning($"[AiEditorFileUI] SubAI tree {subAIName} has no start node connections, removing");
                    nodeList.Remove(subAINode);
                    connectionList.RemoveAll(c => c.fromNodeId == subAINode.nodeId || c.toNodeId == subAINode.nodeId);
                    continue;
                }
                
                Debug.Log($"[AiEditorFileUI] SubAI {subAIName} has {subAIEntryNodeIds.Count} entry point(s): {string.Join(", ", subAIEntryNodeIds)}");
                
                // Copy all nodes from the SubAI tree with new IDs
                var nodeIdMapping = new Dictionary<string, string>(); // Old ID -> New ID
                foreach (var subNode in subAITree.nodes)
                {
                    string newNodeId = nodeIdPrefix + subNode.nodeId;
                    nodeIdMapping[subNode.nodeId] = newNodeId;
                    
                    // Create a copy of the node with the new ID
                    var newNode = new AiNodeDataJson
                    {
                        nodeId = newNodeId,
                        nodeType = subNode.nodeType,
                        nodeLabel = subNode.nodeLabel,
                        position = new Vector2(
                            subAINode.position.x + subNode.position.x,
                            subAINode.position.y + subNode.position.y
                        ),
                        properties = subNode.properties != null ? new List<NodePropertyJson>(subNode.properties) : new List<NodePropertyJson>()
                    };
                    
                    // Store the ORIGINAL SubAI node position so we can restore it on load
                    newNode.properties.Add(new NodePropertyJson
                    {
                        key = "OriginalSubAI_X",
                        value = subAINode.position.x.ToString()
                    });
                    newNode.properties.Add(new NodePropertyJson
                    {
                        key = "OriginalSubAI_Y",
                        value = subAINode.position.y.ToString()
                    });
                    
                    nodeList.Add(newNode);
                    Debug.Log($"[AiEditorFileUI] Added node: {newNodeId} ({subNode.nodeLabel})");
                }
                
                // Copy all internal connections from the SubAI tree with mapped IDs
                foreach (var subConn in subAITree.connections)
                {
                    // Skip start button connections - we replace them with connections from parent
                    if (subConn.fromNodeId == "StartNavButton" || subConn.fromNodeId == "StartTurretButton")
                        continue;
                    
                    // Map the node IDs
                    string newFromId = nodeIdMapping.ContainsKey(subConn.fromNodeId) ? nodeIdMapping[subConn.fromNodeId] : subConn.fromNodeId;
                    string newToId = nodeIdMapping.ContainsKey(subConn.toNodeId) ? nodeIdMapping[subConn.toNodeId] : subConn.toNodeId;
                    
                    connectionList.Add(new AiConnectionDataJson
                    {
                        fromNodeId = newFromId,
                        fromPortId = subConn.fromPortId,
                        toNodeId = newToId,
                        toPortId = subConn.toPortId
                    });
                }
                
                // KEY FIX: Replace the SubAI's StartNavButton with each incoming connection source
                // For each parent source that connected TO the SubAI node, create connections to ALL entry points
                foreach (var inConn in incomingConnections)
                {
                    string parentSourceId = inConn.fromNodeId;
                    string parentSourcePort = inConn.fromPortId;
                    
                    // Connect the parent source to EACH entry point of the SubAI
                    foreach (var entryNodeId in subAIEntryNodeIds)
                    {
                        string mappedEntryId = nodeIdMapping.ContainsKey(entryNodeId) ? nodeIdMapping[entryNodeId] : entryNodeId;
                        
                        connectionList.Add(new AiConnectionDataJson
                        {
                            fromNodeId = parentSourceId,
                            fromPortId = parentSourcePort,
                            toNodeId = mappedEntryId,
                            toPortId = "Input"
                        });
                        Debug.Log($"[AiEditorFileUI] Rewired: {parentSourceId} -> {mappedEntryId}");
                    }
                    
                    // Remove the original incoming connection (it pointed to the SubAI node)
                    connectionList.Remove(inConn);
                }
                
                // Handle outgoing connections from SubAI node (if any)
                // Find end nodes in the SubAI tree (nodes with no outgoing connections within SubAI)
                if (outgoingConnections.Count > 0)
                {
                    var subAIEndNodeIds = new List<string>();
                    foreach (var subNode in subAITree.nodes)
                    {
                        bool hasOutgoing = subAITree.connections.Any(c => 
                            c.fromNodeId == subNode.nodeId && 
                            c.fromNodeId != "StartNavButton" && 
                            c.fromNodeId != "StartTurretButton");
                        if (!hasOutgoing)
                        {
                            string mappedId = nodeIdMapping[subNode.nodeId];
                            subAIEndNodeIds.Add(mappedId);
                        }
                    }
                    
                    // Rewire outgoing connections from SubAI node to come from the SubAI's end nodes
                    foreach (var outConn in outgoingConnections)
                    {
                        // Add new connections from each end node to the original destination
                        foreach (var endNodeId in subAIEndNodeIds)
                        {
                            connectionList.Add(new AiConnectionDataJson
                            {
                                fromNodeId = endNodeId,
                                fromPortId = outConn.fromPortId,
                                toNodeId = outConn.toNodeId,
                                toPortId = outConn.toPortId
                            });
                        }
                        
                        // Remove the original outgoing connection
                        connectionList.Remove(outConn);
                    }
                }
                
                // Remove the original SubAI node from the node list
                nodeList.Remove(subAINode);
                
                Debug.Log($"[AiEditorFileUI] Finished expanding {subAIName}: now {nodeList.Count} nodes, {connectionList.Count} connections");
            }
        }
        
        if (iteration >= maxIterations)
        {
            Debug.LogError($"[AiEditorFileUI] SubAI expansion hit max iterations ({maxIterations}), possible infinite loop!");
        }
        
        Debug.Log($"[AiEditorFileUI] Final after SubAI expansion: {nodeList.Count} nodes, {connectionList.Count} connections");
    }
    
    /// <summary>
    /// Loads a SubAI tree by name from the appropriate folder
    /// </summary>
    private AiTreeAssetJson LoadSubAITree(string subAIName, AiBranchTypeJson branchType)
    {
        // Determine folder based on branch type
        string folderName = branchType == AiBranchTypeJson.Turret ? "TurretFiles" : "NavFiles";
        
        // Try persistent data path first (user-created AI)
        string persistentFolder = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folderName);
        if (System.IO.Directory.Exists(persistentFolder))
        {
            string[] jsonFiles = System.IO.Directory.GetFiles(persistentFolder, "*.json");
            foreach (string filePath in jsonFiles)
            {
                try
                {
                    string jsonContent = System.IO.File.ReadAllText(filePath);
                    var aiTree = JsonUtility.FromJson<AiTreeAssetJson>(jsonContent);
                    
                    // Match by tree name/title
                    if (aiTree != null && (aiTree.TreeName == subAIName || aiTree.title == subAIName))
                    {
                        Debug.Log($"[AiEditorFileUI] Loaded SubAI from persistent: {filePath}");
                        return aiTree;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AiEditorFileUI] Error reading SubAI file {filePath}: {e.Message}");
                }
            }
        }
        
        // Try Assets folder (editor-time .asset files)
        string assetFolder = System.IO.Path.Combine(Application.dataPath, "AiEditor/AISaveFiles", folderName);
        if (System.IO.Directory.Exists(assetFolder))
        {
            string[] assetFiles = System.IO.Directory.GetFiles(assetFolder, "*.asset");
            foreach (string filePath in assetFiles)
            {
#if UNITY_EDITOR
                string relativePath = "Assets" + filePath.Substring(Application.dataPath.Length);
                var aiTreeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiTreeAsset>(relativePath);
                if (aiTreeAsset != null && (aiTreeAsset.TreeName == subAIName || aiTreeAsset.title == subAIName))
                {
                    // Convert ScriptableObject to JSON format
                    var jsonVersion = AiTreeAssetJson.FromScriptableObject(aiTreeAsset);
                    Debug.Log($"[AiEditorFileUI] Loaded SubAI from assets: {relativePath}");
                    return jsonVersion;
                }
#endif
            }
        }
        
        Debug.LogWarning($"[AiEditorFileUI] SubAI tree not found: {subAIName}");
        return null;
    }
}
