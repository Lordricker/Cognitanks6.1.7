using UnityEngine;

public enum ComponentCategory
{
    Turret,
    Armor,  
    AITree,
    EngineFrame,
    // Legacy categories for backwards compatibility
    TurretAI,
    NavAI
}

[CreateAssetMenu(fileName = "Base", menuName = "Scriptable Objects/Base")]
public abstract class ComponentData : ScriptableObject
{
    public string title;
    public string description;
    public int cost;
    public int weight;
    public GameObject modelPrefab;
    public ComponentCategory category;
    [Tooltip("Unique ID for this component (must be unique across all components)")]
    public string id { get { return title; } }

    // Unique instance ID for inventory copies
    public string instanceId;
    
    // Custom color for this component (used for visual customization)
    public Color customColor = Color.white;
}

