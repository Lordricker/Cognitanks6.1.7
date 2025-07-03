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

    // Track the currently loaded asset path for update-only saves
    private string currentAssetPath = null;

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
        saveButton.onClick.AddListener(OnSaveClicked);
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
               // Also check for patterns that already have numbers (not just #)
               nodeLabel.StartsWith("If Self HP>") ||
               nodeLabel.StartsWith("If Self HP<") ||
               nodeLabel.StartsWith("If HP < ") ||
               nodeLabel.StartsWith("If HP > ") ||
               nodeLabel.StartsWith("If Tag = ") ||
               nodeLabel.StartsWith("If Tag < ") ||
               nodeLabel.StartsWith("If Tag > ") ||
               nodeLabel.StartsWith("If Range<") ||
               nodeLabel.StartsWith("If Range>");
    }

    void ToggleLoadPanel()
    {
        loadPanel.SetActive(!loadPanel.activeSelf);
        if (!loadPanel.activeSelf)
        {
            navFileScrollView.SetActive(false);
            turretFileScrollView.SetActive(false);
        }
    }
    
    void OnSaveClicked()
    {
        // Determine branch by which start button is active
        string folder = "";
        if (startTurretButton.gameObject.activeSelf)
            folder = turretFolder;
        else if (startNavButton.gameObject.activeSelf)
            folder = navFolder;
        else
            return; // No branch selected
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        // Get the filename from the starting node's label (replace spaces with _)
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
        string assetName = treeName.Replace(' ', '_');
        // Only allow updating an existing file
        if (!string.IsNullOrEmpty(currentAssetPath) && File.Exists(currentAssetPath))
        {
            #if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiTreeAsset>(currentAssetPath);
            if (asset != null)
            {
                asset.TreeName = treeName;
                asset.branchType = (folder == navFolder) ? AiEditor.AiBranchType.Nav : AiEditor.AiBranchType.Turret;
                
                // Ensure instanceId is set for existing asset (preserve existing or generate new if missing)
                if (string.IsNullOrEmpty(asset.instanceId))
                {
                    asset.instanceId = asset.title + "_" + System.Guid.NewGuid().ToString();
                }
                // --- Serialize nodes and connections ---
                var content = GameObject.Find("Content");
                var nodeDraggables = content.GetComponentsInChildren<NodeDraggable>();
                var nodeList = new List<AiEditor.AiNodeData>();
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
                    var nodeData = new AiEditor.AiNodeData
                    {
                        nodeId = node.nodeId,
                        nodeType = nodeType, // e.g., EndNode(Clone)
                        nodeLabel = label,   // NodeText
                        position = node.GetComponent<RectTransform>().anchoredPosition,
                        properties = new Dictionary<string, string>()
                    };
                    nodeList.Add(nodeData);
                    nodeIdToDraggable[node.nodeId] = node;
                }
                // Save all output connections for each node
                var lineConnectors = content.GetComponentsInChildren<UILineConnector>();
                var connectionList = new List<AiEditor.AiConnectionData>();
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
                        connectionList.Add(new AiEditor.AiConnectionData
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
                        connectionList.Add(new AiEditor.AiConnectionData
                        {
                            fromNodeId = fromNode.nodeId,
                            fromPortId = portId,
                            toNodeId = toNode.nodeId,
                            toPortId = toPortId
                        });
                    }                }
                asset.nodes = nodeList;
                asset.connections = connectionList;
                
                // Generate execution data
                GenerateExecutionData(asset, nodeList, connectionList);
                
                UnityEditor.EditorUtility.SetDirty(asset);
                // If the name has changed, rename the asset file
                string newFileName = assetName + ".asset";
                string newPath = Path.Combine("Assets", "AiEditor", "AISaveFiles", folder, newFileName);
                if (!currentAssetPath.EndsWith(newFileName))
                {
                    UnityEditor.AssetDatabase.RenameAsset(currentAssetPath, assetName);
                    currentAssetPath = newPath;
                }
                UnityEditor.AssetDatabase.SaveAssets();
            }
            #endif
        }
        else
        {
            // TEMP: Allow new file creation if no file is loaded
            #if UNITY_EDITOR
            var asset = ScriptableObject.CreateInstance<AiTreeAsset>();
            asset.name = assetName;
            asset.TreeName = treeName;
            asset.branchType = (folder == navFolder) ? AiEditor.AiBranchType.Nav : AiEditor.AiBranchType.Turret;
            
            // Generate instanceId for new asset
            asset.instanceId = asset.title + "_" + System.Guid.NewGuid().ToString();
            // --- Serialize nodes and connections ---
            var content = GameObject.Find("Content");
            var nodeDraggables = content.GetComponentsInChildren<NodeDraggable>();
            var nodeList = new List<AiEditor.AiNodeData>();
            var nodeIdToDraggable = new Dictionary<string, NodeDraggable>();
            foreach (var node in nodeDraggables)
            {
                if (string.IsNullOrEmpty(node.nodeId))
                    node.nodeId = System.Guid.NewGuid().ToString();
                var title = node.GetComponentInChildren<TitleName>();
                string label = title != null ? title.titleText.text : node.name;
                string type = label;
                var nodeData = new AiEditor.AiNodeData
                {
                    nodeId = node.nodeId,
                    nodeType = type,
                    nodeLabel = label,
                    position = node.GetComponent<RectTransform>().anchoredPosition,
                    properties = new Dictionary<string, string>()
                };
                nodeList.Add(nodeData);
                nodeIdToDraggable[node.nodeId] = node;
            }
            var lineConnectors = content.GetComponentsInChildren<UILineConnector>();
            var connectionList = new List<AiEditor.AiConnectionData>();
            foreach (var line in lineConnectors)
            {
                var fromNode = line.outputRect != null ? line.outputRect.GetComponentInParent<NodeDraggable>() : null;
                var toNode = line.inputRect != null ? line.inputRect.GetComponentInParent<NodeDraggable>() : null;
                if (fromNode != null && toNode != null)
                {
                    connectionList.Add(new AiEditor.AiConnectionData
                    {
                        fromNodeId = fromNode.nodeId,
                        fromPortId = "OutputPort",
                        toNodeId = toNode.nodeId,
                        toPortId = "InputPort"
                    });
                }
            }            asset.nodes = nodeList;
            asset.connections = connectionList;
            
            // Generate execution data
            GenerateExecutionData(asset, nodeList, connectionList);
            
            string path = Path.Combine("Assets", "AiEditor", "AISaveFiles", folder, assetName + ".asset");
            UnityEditor.AssetDatabase.CreateAsset(asset, path);
            UnityEditor.AssetDatabase.SaveAssets();
            currentAssetPath = path;
            #endif
        }
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
        
        if (!Directory.Exists(Path.Combine("Assets", "AiEditor", "AISaveFiles", folder))) return;
        
        var files = Directory.GetFiles(Path.Combine("Assets", "AiEditor", "AISaveFiles", folder), "*.asset").OrderBy(f => f).ToArray();
        
        foreach (var file in files)
        {
            var btnObj = Instantiate(fileButtonPrefab, contentPanel);
            var btn = btnObj.GetComponent<Button>();
            var txt = btnObj.GetComponentInChildren<TMPro.TMP_Text>();
            
            if (txt != null) 
            {
                // Load the asset to get the proper title instead of using filename
                string assetPath = file.Replace("\\", "/");
#if UNITY_EDITOR
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiTreeAsset>(assetPath);
                if (asset != null && !string.IsNullOrEmpty(asset.TreeName))
                {
                    txt.text = asset.TreeName; // Use TreeName property which handles title/treeName fallback
                }
                else
                {
                    txt.text = Path.GetFileNameWithoutExtension(file); // Fallback to filename
                }
#else
                txt.text = Path.GetFileNameWithoutExtension(file); // Fallback for runtime
#endif
            }
            
            btn.onClick.AddListener(() => OnFileSelected(file));
        }
    }

    void OnFileSelected(string filePath)
    {
        // Load AiTreeAsset and reconstruct node graph
        currentAssetPath = filePath.Replace("\\", "/"); // Track the loaded file for update-only saves
#if UNITY_EDITOR
    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiTreeAsset>(currentAssetPath);
    if (asset != null)
    {
        // Sync all tree name fields when loading
        SyncAllTreeNameFields(asset.TreeName);
        
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
        if (asset.branchType == AiEditor.AiBranchType.Nav)
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
        else if (asset.branchType == AiEditor.AiBranchType.Turret)
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

        // Load nodes and connections
        LoadNodesFromAsset(asset, content, startPanel);
        LoadConnectionsFromAsset(asset, content, startPanel);
        
        loadPanel.SetActive(false);
    }
    
    void LoadNodesFromAsset(AiTreeAsset asset, GameObject content, GameObject startPanel)
    {
        // --- NodeId-based mapping ---
        var nodeIdToGameObject = new Dictionary<string, GameObject>();
        // --- Handle StartNodePanel and all nodes ---
        foreach (var nodeData in asset.nodes)
        {
            GameObject nodeGO = null;            if ((nodeData.nodeType == "Start" || nodeData.nodeLabel == asset.TreeName) && startPanel != null)
            {
                // Update StartNodePanel label and position
                // NEW: Set filename in FileButtonPanel using TitleName component
                SetCurrentTreeName(asset.TreeName);
                
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
    
    void LoadConnectionsFromAsset(AiTreeAsset asset, GameObject content, GameObject startPanel)
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
        foreach (var conn in asset.connections)
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
#endif
        loadPanel.SetActive(false);
    }

    /// <summary>
    /// Generates execution data from the visual node graph for AI runtime execution
    /// </summary>
    private void GenerateExecutionData(AiEditor.AiTreeAsset asset, List<AiEditor.AiNodeData> nodeList, List<AiEditor.AiConnectionData> connectionList)
    {
        asset.executableNodes.Clear();
        
        // Find the start node ID (either StartNavButton or StartTurretButton connections)
        asset.startNodeId = null;
        foreach (var conn in connectionList)
        {
            if (conn.fromNodeId == "StartNavButton" || conn.fromNodeId == "StartTurretButton")
            {
                asset.startNodeId = conn.toNodeId;
                break;
            }
        }
          // Convert each node to executable format
        foreach (var nodeData in nodeList)
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
            
            var executableNode = new AiEditor.AiExecutableNode
            {
                nodeId = nodeData.nodeId,
                methodName = methodName,
                originalLabel = nodeData.nodeLabel,
                nodeType = nodeType,
                numericValue = numericValue, // This comes from ConvertToMethodName parsing the label
                position = nodeData.position,
                connectedNodeIds = new List<string>()
            };
            
            // Find all nodes this one connects to
            foreach (var conn in connectionList)
            {
                if (conn.fromNodeId == nodeData.nodeId)
                {
                    executableNode.connectedNodeIds.Add(conn.toNodeId);
                }
            }
            
            // Sort connected nodes by Y-position for priority handling
            executableNode.connectedNodeIds.Sort((id1, id2) => {
                var node1 = nodeList.Find(n => n.nodeId == id1);
                var node2 = nodeList.Find(n => n.nodeId == id2);
                if (node1 == null || node2 == null) return 0;
                return node1.position.y.CompareTo(node2.position.y); // Higher Y = higher priority
            });
            
            asset.executableNodes.Add(executableNode);
        }
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
}
