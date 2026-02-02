using UnityEngine;
using System.IO;

/// <summary>
/// Utility class for calculating AI component weight based on node count.
/// Weight = floor(nodeCount / 10). E.g., 30 nodes = 3 weight.
/// </summary>
public static class AIWeightCalculator
{
    private const int NODES_PER_WEIGHT = 10;
    
    /// <summary>
    /// Calculate weight for an AI component based on its node count.
    /// Every 10 nodes adds 1 weight.
    /// </summary>
    /// <param name="nodeCount">Number of nodes in the AI tree</param>
    /// <returns>Weight value (floor of nodeCount / 10)</returns>
    public static float CalculateWeight(int nodeCount)
    {
        return Mathf.Floor(nodeCount / (float)NODES_PER_WEIGHT);
    }
    
    /// <summary>
    /// Calculate weight for an AI by loading and counting nodes from its instance ID.
    /// Searches both persistent data path (player AI) and Resources/ShopAI (shop AI).
    /// </summary>
    /// <param name="instanceId">The AI's instance ID</param>
    /// <returns>Weight based on node count, or 0 if AI not found</returns>
    public static float CalculateWeightFromInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return 0f;
        
        int nodeCount = GetNodeCountFromInstanceId(instanceId);
        return CalculateWeight(nodeCount);
    }
    
    /// <summary>
    /// Get the node count for an AI by its instance ID.
    /// </summary>
    /// <param name="instanceId">The AI's instance ID</param>
    /// <returns>Number of nodes in the AI tree, or 0 if not found</returns>
    public static int GetNodeCountFromInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return 0;
        
        // Try persistent data path first (player's saved AI)
        string aiTreesFolder = Path.Combine(Application.persistentDataPath, "AiTrees");
        
        if (Directory.Exists(aiTreesFolder))
        {
            string[] jsonFiles = Directory.GetFiles(aiTreesFolder, "*.json", SearchOption.AllDirectories);
            
            foreach (string filePath in jsonFiles)
            {
                try
                {
                    string jsonContent = File.ReadAllText(filePath);
                    AiTreeAssetJson aiData = JsonUtility.FromJson<AiTreeAssetJson>(jsonContent);
                    
                    if (aiData != null && aiData.instanceId == instanceId)
                    {
                        return aiData.nodes != null ? aiData.nodes.Count : 0;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[AIWeightCalculator] Error reading AI file {filePath}: {e.Message}");
                }
            }
        }
        
        // Try Resources/ShopAI (shop AI files)
        TextAsset[] shopAIFiles = Resources.LoadAll<TextAsset>("ShopAI");
        
        foreach (TextAsset jsonFile in shopAIFiles)
        {
            try
            {
                AiTreeAssetJson aiData = JsonUtility.FromJson<AiTreeAssetJson>(jsonFile.text);
                
                if (aiData != null && aiData.instanceId == instanceId)
                {
                    return aiData.nodes != null ? aiData.nodes.Count : 0;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AIWeightCalculator] Error parsing shop AI {jsonFile.name}: {e.Message}");
            }
        }
        
        Debug.LogWarning($"[AIWeightCalculator] AI with instanceId {instanceId} not found");
        return 0;
    }
    
    /// <summary>
    /// Update the AI weights for a tank slot based on its assigned AI components.
    /// Call this when AI components are assigned or when loading a tank slot.
    /// </summary>
    /// <param name="slotData">The tank slot data to update</param>
    public static void UpdateSlotAIWeights(TankSlotDataJson slotData)
    {
        if (slotData == null)
            return;
        
        // Calculate and set turret AI weight
        slotData.turretAIWeight = CalculateWeightFromInstanceId(slotData.turretAIInstanceId);
        
        // Calculate and set nav AI weight
        slotData.navAIWeight = CalculateWeightFromInstanceId(slotData.navAIInstanceId);
        
        Debug.Log($"[AIWeightCalculator] Updated slot {slotData.slotIndex} AI weights: TurretAI={slotData.turretAIWeight}, NavAI={slotData.navAIWeight}");
    }
    
    /// <summary>
    /// Calculate total weight including AI components.
    /// </summary>
    /// <param name="slotData">The tank slot data</param>
    /// <returns>Total weight including armor, turret, engine, and both AI weights</returns>
    public static float CalculateTotalWeight(TankSlotDataJson slotData)
    {
        if (slotData == null)
            return 0f;
        
        return slotData.armorWeight + 
               slotData.turretWeight + 
               slotData.engineWeight + 
               slotData.turretAIWeight + 
               slotData.navAIWeight;
    }
}
