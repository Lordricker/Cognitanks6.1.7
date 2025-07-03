using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JSON-serializable version of ComponentData base class
/// Contains all the same data as the ScriptableObject version but can be saved/loaded at runtime
/// </summary>
[System.Serializable]
public abstract class ComponentDataJson
{
    public string title;
    public string description;
    public int cost;
    public int weight;
    public string modelPrefabPath; // Stored as resource path for JSON compatibility
    public ComponentCategoryJson category;
    
    // Unique instance ID for inventory copies
    public string instanceId;
    
    // Custom color for this component (used for visual customization)
    public Color customColor = Color.white;
    
    /// <summary>
    /// Unique ID for this component (must be unique across all components)
    /// </summary>
    public string id => title;
    
    /// <summary>
    /// Constructor with default values
    /// </summary>
    public ComponentDataJson()
    {
        // Generate unique instance ID
        if (string.IsNullOrEmpty(instanceId))
        {
            instanceId = System.Guid.NewGuid().ToString();
        }
    }
    
    /// <summary>
    /// Load a prefab from the stored path
    /// </summary>
    public GameObject LoadModelPrefab()
    {
        if (string.IsNullOrEmpty(modelPrefabPath)) return null;
        
        // Try loading from Resources first
        GameObject prefab = Resources.Load<GameObject>(modelPrefabPath);
        if (prefab != null) return prefab;
        
        #if UNITY_EDITOR
        // In editor, try loading from full asset path
        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(modelPrefabPath);
        #endif
        
        return prefab;
    }
    
    /// <summary>
    /// Convert a prefab to a resource path for JSON storage
    /// </summary>
    protected static string GetPrefabResourcePath(GameObject prefab)
    {
        if (prefab == null) return "";
        
        #if UNITY_EDITOR
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(prefab);
        
        // If it's in Resources folder, return relative path
        if (assetPath.Contains("/Resources/"))
        {
            int resourcesIndex = assetPath.IndexOf("/Resources/") + "/Resources/".Length;
            string resourcePath = assetPath.Substring(resourcesIndex);
            // Remove .prefab extension for Resources.Load
            if (resourcePath.EndsWith(".prefab"))
                resourcePath = resourcePath.Substring(0, resourcePath.Length - 7);
            return resourcePath;
        }
        
        // For non-Resources assets, store full path (editor only)
        return assetPath;
        #else
        // In build, we can only work with Resources
        return prefab.name;
        #endif
    }
    
    /// <summary>
    /// Save this component data to JSON file
    /// </summary>
    public virtual void SaveToJson(string fileName)
    {
        string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "Components");
        
        // Create directory if it doesn't exist
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }
        
        string fullPath = System.IO.Path.Combine(folderPath, fileName + ".json");
        string json = JsonUtility.ToJson(this, true);
        System.IO.File.WriteAllText(fullPath, json);
        
        Debug.Log($"Saved component to: {fullPath}");
    }
    
    /// <summary>
    /// Get all available component files for a category
    /// </summary>
    public static List<string> GetAvailableFiles(ComponentCategoryJson category)
    {
        List<string> files = new List<string>();
        string folderPath = System.IO.Path.Combine(Application.persistentDataPath, "Components");
        
        if (System.IO.Directory.Exists(folderPath))
        {
            string[] jsonFiles = System.IO.Directory.GetFiles(folderPath, "*.json");
            foreach (string file in jsonFiles)
            {
                try
                {
                    string json = System.IO.File.ReadAllText(file);
                    // Quick check to see if this matches our category
                    if (json.Contains($"\"category\":{(int)category}"))
                    {
                        files.Add(System.IO.Path.GetFileNameWithoutExtension(file));
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to check component file {file}: {e.Message}");
                }
            }
        }
        
        return files;
    }
}
