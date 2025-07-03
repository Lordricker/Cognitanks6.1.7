using UnityEngine;

/// <summary>
/// JSON-serializable version of ArmorData
/// Contains all the same data as the ScriptableObject version but can be saved/loaded at runtime
/// </summary>
[System.Serializable]
public class ArmorDataJson : ComponentDataJson
{
    [Header("Armor Stats")]
    public int HP;
    
    /// <summary>
    /// Constructor with default values
    /// </summary>
    public ArmorDataJson()
    {
        category = ComponentCategoryJson.Armor;
        cost = 50;
        weight = 15;
        HP = 100;
    }
    
    /// <summary>
    /// Convert from existing ArmorData ScriptableObject
    /// </summary>
    public static ArmorDataJson FromScriptableObject(ArmorData original)
    {
        ArmorDataJson json = new ArmorDataJson();
        
        // Copy base properties
        json.title = original.title;
        json.description = original.description;
        json.cost = original.cost;
        json.weight = original.weight;
        json.modelPrefabPath = GetPrefabResourcePath(original.modelPrefab);
        json.category = (ComponentCategoryJson)original.category;
        json.instanceId = original.instanceId;
        json.customColor = original.customColor;
        
        // Copy armor-specific properties
        json.HP = original.HP;
        
        return json;
    }
    
    /// <summary>
    /// Load armor data from JSON file
    /// </summary>
    public static ArmorDataJson LoadFromJson(string fileName)
    {
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, "Components", fileName + ".json");
        
        if (!System.IO.File.Exists(filePath))
        {
            Debug.LogError($"Armor component file not found: {filePath}");
            return null;
        }
        
        try
        {
            string json = System.IO.File.ReadAllText(filePath);
            ArmorDataJson data = JsonUtility.FromJson<ArmorDataJson>(json);
            Debug.Log($"Loaded armor component from: {filePath}");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load armor component: {e.Message}");
            return null;
        }
    }
}
