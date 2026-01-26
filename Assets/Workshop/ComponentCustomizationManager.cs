using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Stores customization data (skin, decal, color) for each component instance.
/// Data is keyed by instanceId so it follows the component wherever it's used.
/// </summary>
[Serializable]
public class ComponentCustomizationEntry
{
    public string instanceId;
    public string skinPath = "";     // Path to skin texture in Resources (e.g., "KritaArt/Skins/MetalRust")
    public string decalPath = "";    // Path to decal texture in Resources (e.g., "KritaArt/Decals/Skull") - turrets only
    public float colorR = 1f;
    public float colorG = 1f;
    public float colorB = 1f;
    public float colorA = 1f;
    
    public Color GetColor()
    {
        return new Color(colorR, colorG, colorB, colorA);
    }
    
    public void SetColor(Color color)
    {
        colorR = color.r;
        colorG = color.g;
        colorB = color.b;
        colorA = color.a;
    }
}

[Serializable]
public class ComponentCustomizationData
{
    public List<ComponentCustomizationEntry> entries = new List<ComponentCustomizationEntry>();
}

/// <summary>
/// Manages component customization data (skin, decal, color) per instanceId.
/// This allows customization to follow the component wherever it's used.
/// </summary>
public class ComponentCustomizationManager : MonoBehaviour
{
    public static ComponentCustomizationManager Instance { get; private set; }
    
    private ComponentCustomizationData data = new ComponentCustomizationData();
    private string saveFilePath;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Path.Combine(Application.persistentDataPath, "component_customization.json");
            LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Gets customization entry for a component instanceId, creating one if it doesn't exist
    /// </summary>
    public ComponentCustomizationEntry GetOrCreateEntry(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return null;
            
        var entry = data.entries.Find(e => e.instanceId == instanceId);
        if (entry == null)
        {
            entry = new ComponentCustomizationEntry { instanceId = instanceId };
            data.entries.Add(entry);
        }
        return entry;
    }
    
    /// <summary>
    /// Gets customization entry for a component instanceId, returns null if not found
    /// </summary>
    public ComponentCustomizationEntry GetEntry(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return null;
        return data.entries.Find(e => e.instanceId == instanceId);
    }
    
    /// <summary>
    /// Sets the skin path for a component
    /// </summary>
    public void SetSkin(string instanceId, string skinPath)
    {
        var entry = GetOrCreateEntry(instanceId);
        if (entry != null)
        {
            entry.skinPath = skinPath ?? "";
            SaveData();
            Debug.Log($"Set skin for {instanceId}: {skinPath}");
        }
    }
    
    /// <summary>
    /// Sets the decal path for a component (typically turrets only)
    /// </summary>
    public void SetDecal(string instanceId, string decalPath)
    {
        var entry = GetOrCreateEntry(instanceId);
        if (entry != null)
        {
            entry.decalPath = decalPath ?? "";
            SaveData();
            Debug.Log($"Set decal for {instanceId}: {decalPath}");
        }
    }
    
    /// <summary>
    /// Sets the custom color for a component
    /// </summary>
    public void SetColor(string instanceId, Color color)
    {
        var entry = GetOrCreateEntry(instanceId);
        if (entry != null)
        {
            entry.SetColor(color);
            SaveData();
        }
    }
    
    /// <summary>
    /// Gets the skin path for a component
    /// </summary>
    public string GetSkin(string instanceId)
    {
        var entry = GetEntry(instanceId);
        return entry?.skinPath ?? "";
    }
    
    /// <summary>
    /// Gets the decal path for a component
    /// </summary>
    public string GetDecal(string instanceId)
    {
        var entry = GetEntry(instanceId);
        return entry?.decalPath ?? "";
    }
    
    /// <summary>
    /// Gets the custom color for a component
    /// </summary>
    public Color GetColor(string instanceId)
    {
        var entry = GetEntry(instanceId);
        return entry?.GetColor() ?? Color.white;
    }
    
    /// <summary>
    /// Removes customization entry when a component is sold/deleted
    /// </summary>
    public void RemoveEntry(string instanceId)
    {
        data.entries.RemoveAll(e => e.instanceId == instanceId);
        SaveData();
    }
    
    private void SaveData()
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(saveFilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save component customization data: {e.Message}");
        }
    }
    
    private void LoadData()
    {
        try
        {
            if (File.Exists(saveFilePath))
            {
                string json = File.ReadAllText(saveFilePath);
                data = JsonUtility.FromJson<ComponentCustomizationData>(json);
                Debug.Log($"Loaded {data.entries.Count} component customization entries");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load component customization data: {e.Message}");
            data = new ComponentCustomizationData();
        }
    }
}
