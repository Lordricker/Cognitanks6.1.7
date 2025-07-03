using UnityEngine;

/// <summary>
/// JSON-serializable version of EngineFrameData
/// Contains all the same data as the ScriptableObject version but can be saved/loaded at runtime
/// </summary>
[System.Serializable]
public class EngineFrameDataJson : ComponentDataJson
{
    [Header("Engine Stats")]
    public int weightCapacity;
    public int enginePower;
    
    /// <summary>
    /// Constructor with default values
    /// </summary>
    public EngineFrameDataJson()
    {
        category = ComponentCategoryJson.EngineFrame;
        cost = 75;
        weight = 20;
        weightCapacity = 100;
        enginePower = 50;
    }
    
    /// <summary>
    /// Convert from existing EngineFrameData ScriptableObject
    /// </summary>
    public static EngineFrameDataJson FromScriptableObject(EngineFrameData original)
    {
        EngineFrameDataJson json = new EngineFrameDataJson();
        
        // Copy base properties
        json.title = original.title;
        json.description = original.description;
        json.cost = original.cost;
        json.weight = original.weight;
        json.modelPrefabPath = GetPrefabResourcePath(original.modelPrefab);
        json.category = (ComponentCategoryJson)original.category;
        json.instanceId = original.instanceId;
        json.customColor = original.customColor;
        
        // Copy engine-specific properties
        json.weightCapacity = original.weightCapacity;
        json.enginePower = original.enginePower;
        
        return json;
    }
    
    /// <summary>
    /// Load engine frame data from JSON file
    /// </summary>
    public static EngineFrameDataJson LoadFromJson(string fileName)
    {
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "Components", fileName + ".json");
        
        if (!System.IO.File.Exists(filePath))
        {
            Debug.LogError($"Engine frame component file not found: {filePath}");
            return null;
        }
        
        try
        {
            string json = System.IO.File.ReadAllText(filePath);
            EngineFrameDataJson data = JsonUtility.FromJson<EngineFrameDataJson>(json);
            Debug.Log($"Loaded engine frame component from: {filePath}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load engine frame component: {e.Message}");
            return null;
        }
    }
}
