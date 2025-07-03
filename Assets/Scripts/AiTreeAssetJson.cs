using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JSON-serializable version of AiTreeAsset
/// Contains all the same data as the ScriptableObject version but can be saved/loaded at runtime
/// </summary>
[System.Serializable]
public class AiTreeAssetJson
{
    [Header("AI Tree Configuration")]
    public AiBranchTypeJson branchType;
    public List<AiNodeDataJson> nodes = new List<AiNodeDataJson>();
    public List<AiConnectionDataJson> connections = new List<AiConnectionDataJson>();
    
    [Header("Execution Data")]
    public List<AiExecutableNodeJson> executableNodes = new List<AiExecutableNodeJson>();
    public string startNodeId;
    
    // Component metadata (from ComponentData base class)
    public string title;
    public string description;
    public int cost = 100;
    public int weight = 1;
    public ComponentCategoryJson category = ComponentCategoryJson.AITree;
    public string instanceId;
    public Color customColor = Color.white;
    
    // Legacy field for compatibility
    public string treeName;
    
    /// <summary>
    /// Gets or sets the tree name, ensuring synchronization with the title property
    /// </summary>
    public string TreeName
    {
        get { return string.IsNullOrEmpty(title) ? treeName : title; }
        set 
        { 
            title = value;
            treeName = value;
        }
    }
    
    /// <summary>
    /// Constructor with default values
    /// </summary>
    public AiTreeAssetJson()
    {
        category = ComponentCategoryJson.AITree;
        cost = 100;
        weight = 1;
        
        // Generate unique instance ID
        if (string.IsNullOrEmpty(instanceId))
        {
            instanceId = System.Guid.NewGuid().ToString();
        }
    }
    
    /// <summary>
    /// Convert from existing AiTreeAsset ScriptableObject
    /// </summary>
    public static AiTreeAssetJson FromScriptableObject(AiEditor.AiTreeAsset original)
    {
        AiTreeAssetJson json = new AiTreeAssetJson();
        
        // Copy basic properties
        json.branchType = (AiBranchTypeJson)original.branchType;
        json.startNodeId = original.startNodeId;
        json.title = original.title;
        json.description = original.description;
        json.cost = original.cost;
        json.weight = original.weight;
        json.category = (ComponentCategoryJson)original.category;
        json.instanceId = original.instanceId;
        json.customColor = original.customColor;
        json.treeName = original.TreeName;
        
        // Copy nodes
        foreach (var node in original.nodes)
        {
            AiNodeDataJson jsonNode = new AiNodeDataJson();
            jsonNode.nodeId = node.nodeId;
            jsonNode.nodeType = node.nodeType;
            jsonNode.nodeLabel = node.nodeLabel;
            jsonNode.position = node.position;
            
            // Convert Dictionary to List for JSON serialization
            foreach (var kvp in node.properties)
            {
                jsonNode.properties.Add(new NodePropertyJson(kvp.Key, kvp.Value));
            }
            
            json.nodes.Add(jsonNode);
        }
        
        // Copy connections
        foreach (var connection in original.connections)
        {
            AiConnectionDataJson jsonConnection = new AiConnectionDataJson();
            jsonConnection.fromNodeId = connection.fromNodeId;
            jsonConnection.fromPortId = connection.fromPortId;
            jsonConnection.toNodeId = connection.toNodeId;
            jsonConnection.toPortId = connection.toPortId;
            json.connections.Add(jsonConnection);
        }
        
        // Copy executable nodes
        foreach (var execNode in original.executableNodes)
        {
            AiExecutableNodeJson jsonExecNode = new AiExecutableNodeJson();
            jsonExecNode.nodeId = execNode.nodeId;
            jsonExecNode.methodName = execNode.methodName;
            jsonExecNode.originalLabel = execNode.originalLabel;
            jsonExecNode.nodeType = (AiNodeTypeJson)execNode.nodeType;
            jsonExecNode.numericValue = execNode.numericValue;
            jsonExecNode.connectedNodeIds = new List<string>(execNode.connectedNodeIds);
            jsonExecNode.position = execNode.position;
            json.executableNodes.Add(jsonExecNode);
        }
        
        return json;
    }
    
    /// <summary>
    /// Save this AI tree to JSON file
    /// </summary>
    public void SaveToJson(string fileName = null)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = instanceId + ".json";
        }
        
        // Determine folder based on branch type
        string folderName = branchType == AiBranchTypeJson.Turret ? "TurretFiles" : "NavFiles";
        string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folderName);
        
        // Create directory if it doesn't exist
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }
        
        string fullPath = System.IO.Path.Combine(folderPath, fileName);
        string json = JsonUtility.ToJson(this, true);
        System.IO.File.WriteAllText(fullPath, json);
        
        Debug.Log($"Saved AI tree to: {fullPath}");
    }
    
    /// <summary>
    /// Load AI tree from JSON file
    /// </summary>
    public static AiTreeAssetJson LoadFromJson(string fileName, AiBranchTypeJson branchType)
    {
        string folderName = branchType == AiBranchTypeJson.Turret ? "TurretFiles" : "NavFiles";
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folderName, fileName);
        
        if (!System.IO.File.Exists(filePath))
        {
            Debug.LogError($"AI tree file not found: {filePath}");
            return null;
        }
        
        try
        {
            string json = System.IO.File.ReadAllText(filePath);
            AiTreeAssetJson data = JsonUtility.FromJson<AiTreeAssetJson>(json);
            Debug.Log($"Loaded AI tree from: {filePath}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load AI tree: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Get all available AI tree files for a branch type
    /// </summary>
    public static List<string> GetAvailableFiles(AiBranchTypeJson branchType)
    {
        List<string> files = new List<string>();
        string folderName = branchType == AiBranchTypeJson.Turret ? "TurretFiles" : "NavFiles";
        string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees", folderName);
        
        if (System.IO.Directory.Exists(folderPath))
        {
            string[] jsonFiles = System.IO.Directory.GetFiles(folderPath, "*.json");
            foreach (string file in jsonFiles)
            {
                files.Add(System.IO.Path.GetFileName(file));
            }
        }
        
        return files;
    }
}

// JSON-compatible enums and data structures
[System.Serializable]
public enum AiBranchTypeJson { None, Turret, Nav }

[System.Serializable]
public enum AiNodeTypeJson
{
    Start,
    Condition,
    Action,
    SubAI
}

[System.Serializable]
public enum ComponentCategoryJson
{
    Turret,
    Armor,  
    AITree,
    EngineFrame,
    TurretAI,
    NavAI
}

[System.Serializable]
public class AiNodeDataJson
{
    public string nodeId;
    public string nodeType;
    public string nodeLabel;
    public Vector2 position;
    public List<NodePropertyJson> properties = new List<NodePropertyJson>();
}

[System.Serializable]
public class NodePropertyJson
{
    public string key;
    public string value;
    
    public NodePropertyJson() { }
    
    public NodePropertyJson(string key, string value)
    {
        this.key = key;
        this.value = value;
    }
}

[System.Serializable]
public class AiConnectionDataJson
{
    public string fromNodeId;
    public string fromPortId;
    public string toNodeId;
    public string toPortId;
}

[System.Serializable]
public class AiExecutableNodeJson
{
    public string nodeId;
    public string methodName;
    public string originalLabel;
    public AiNodeTypeJson nodeType;
    public float numericValue;
    public List<string> connectedNodeIds = new List<string>();
    public Vector2 position;
}
