using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MultiplayerData;

/// <summary>
/// Static data holder for multiplayer match setup.
/// Stores match data before the arena scene loads.
/// ArenaManager reads from this when GameMode is Multiplayer.
/// </summary>
public static class MultiplayerMatchRunner
{
    /// <summary>Whether a multiplayer match is pending (set before scene load, cleared after spawn)</summary>
    public static bool IsMultiplayerMatch { get; set; } = false;
    
    /// <summary>The posted match being challenged</summary>
    public static MatchEntry PosterMatch { get; set; }
    
    /// <summary>Challenger's tank configs (built from their active tank slots)</summary>
    public static List<TankConfigReference> ChallengerTankConfigs { get; set; }
    
    /// <summary>Challenger's Discord user ID</summary>
    public static string ChallengerDiscordId { get; set; }
    
    /// <summary>Challenger's Discord username</summary>
    public static string ChallengerUsername { get; set; }
    
    /// <summary>Challenger's team name</summary>
    public static string ChallengerTeamName { get; set; }
    
    /// <summary>Challenger's ELO</summary>
    public static int ChallengerElo { get; set; }
    
    /// <summary>Random seed for deterministic replay (same seed = same wander paths)</summary>
    public static int MatchSeed { get; set; }
    
    // Track temp AI files so we can clean them up after spawning
    private static List<string> tempAIFiles = new List<string>();
    
    /// <summary>
    /// Convert a TankConfigReference (from Firebase) back to a TankSlotDataJson for spawning.
    /// Uses the server-stored stats rather than local component data.
    /// Writes AI scripts to temp files so TankMan.LoadAIFromInstanceId can find them.
    /// </summary>
    public static TankSlotDataJson ConfigToSlotData(TankConfigReference config, int teamId, int spawnIndex)
    {
        Debug.Log($"[MultiplayerMatchRunner] ConfigToSlotData: tank={config.displayName}, team={teamId}, spawn={spawnIndex}, turretAI_length={config.turretAIScript?.Length ?? 0}, navAI_length={config.navAIScript?.Length ?? 0}");
        
        // Write AI scripts to temp files so the existing LoadAIFromInstanceId can discover them
        string turretAIId = WriteTempAIFile(config.turretAIScript, $"mp_turret_{teamId}_{spawnIndex}");
        string navAIId = WriteTempAIFile(config.navAIScript, $"mp_nav_{teamId}_{spawnIndex}");
        
        Debug.Log($"[MultiplayerMatchRunner] ConfigToSlotData: extracted turretAIId={turretAIId}, navAIId={navAIId}");
        
        var slot = new TankSlotDataJson
        {
            isActive = true,
            teamId = teamId,
            isPlayerControlled = true,
            displayName = config.displayName,
            slotIndex = spawnIndex,
            
            // Component IDs (for model loading)
            engineFrameInstanceId = config.engineFrameId,
            armorInstanceId = config.armorId,
            turretInstanceId = config.turretId,
            
            // AI references - use either the extracted instanceId from the temp file, or the fallback
            turretAIInstanceId = turretAIId,
            navAIInstanceId = navAIId,
            turretAIWeight = config.turretAIWeight,
            navAIWeight = config.navAIWeight,
            
            // Engine stats (from server)
            engineForce = config.engineForce,
            engineTopSpeed = config.engineTopSpeed,
            engineTorque = config.engineTorque,
            engineMaxTurnRate = config.engineMaxTurnRate,
            engineTurnRampTime = config.engineTurnRampTime,
            engineTurnStartPercent = config.engineTurnStartPercent,
            engineFrameHP = config.engineFrameHP,
            engineWeight = config.engineWeight,
            
            // Armor stats (from server)
            armorHP = config.armorHP,
            armorWeight = config.armorWeight,
            
            // Turret stats (from server)
            turretDamage = config.turretDamage,
            turretRange = config.turretRange,
            turretShotsPerSec = config.turretShotsPerSec,
            turretBulletSpeed = config.turretBulletSpeed,
            turretVisionRange = config.turretVisionRange,
            turretVisionCone = config.turretVisionCone,
            turretWeight = config.turretWeight,
            
            // Spawn point
            spawnPointName = $"SpawnPoint{spawnIndex}"
        };
        
        return slot;
    }
    
    /// <summary>
    /// Public wrapper for writing temp AI files. Called by MultiplayerEntryUI
    /// when joining a match to write both poster and challenger AI to temp folder.
    /// Returns the instanceId from the AI JSON.
    /// </summary>
    public static string WriteTempAIFilePublic(string aiScriptJson, string filePrefix)
    {
        return WriteTempAIFile(aiScriptJson, filePrefix);
    }
    
    /// <summary>
    /// Write an AI script JSON to a temp file in the AiTrees folder.
    /// Returns the instanceId embedded in the JSON so LoadAIFromInstanceId will find it.
    /// </summary>
    private static string WriteTempAIFile(string aiScriptJson, string filePrefix)
    {
        Debug.Log($"[MultiplayerMatchRunner] WriteTempAIFile called: filePrefix={filePrefix}, json_length={aiScriptJson?.Length ?? 0}");
        
        if (string.IsNullOrEmpty(aiScriptJson))
        {
            Debug.LogWarning($"[MultiplayerMatchRunner] WriteTempAIFile: AI script is empty for {filePrefix}");
            return "";
        }
        
        // Extract instanceId from the AI JSON
        // The JSON contains an "instanceId" field that LoadAIFromInstanceId matches against
        string instanceId = ExtractInstanceId(aiScriptJson);
        if (string.IsNullOrEmpty(instanceId))
        {
            Debug.LogWarning($"[MultiplayerMatchRunner] Could not extract instanceId from AI script for {filePrefix}. JSON preview: {aiScriptJson.Substring(0, Mathf.Min(200, aiScriptJson.Length))}");
            return "";
        }
        
        string aiTreesFolder = Path.Combine(Application.persistentDataPath, "AiTrees");
        if (!Directory.Exists(aiTreesFolder))
        {
            Directory.CreateDirectory(aiTreesFolder);
            Debug.Log($"[MultiplayerMatchRunner] Created AiTrees folder: {aiTreesFolder}");
        }
        
        // Write to a temp file with a prefix to distinguish from local AI
        string tempFilePath = Path.Combine(aiTreesFolder, $"{filePrefix}_{instanceId}.json");
        
        try
        {
            File.WriteAllText(tempFilePath, aiScriptJson);
            tempAIFiles.Add(tempFilePath);
            Debug.Log($"[MultiplayerMatchRunner] ✓ Wrote temp AI file: {tempFilePath} (instanceId={instanceId}, size={aiScriptJson.Length} bytes)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MultiplayerMatchRunner] Failed to write temp AI file {tempFilePath}: {e.Message}\n{e.StackTrace}");
            return "";
        }
        
        return instanceId;
    }
    
    /// <summary>
    /// Extract the instanceId field from AI JSON without full deserialization.
    /// Handles any whitespace around the colon (e.g. "instanceId": "value" or "instanceId":"value")
    /// </summary>
    private static string ExtractInstanceId(string json)
    {
        try
        {
            int idx = json.IndexOf("\"instanceId\"");
            if (idx < 0) return "";
            
            // Skip past "instanceId", find colon
            int colonIdx = json.IndexOf(':', idx + 12);
            if (colonIdx < 0) return "";
            
            // Skip whitespace after colon, find opening quote
            int startQuote = json.IndexOf('"', colonIdx + 1);
            if (startQuote < 0) return "";
            
            // Find closing quote
            int endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote < 0) return "";
            
            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
        }
        catch
        {
            return "";
        }
    }
    
    /// <summary>
    /// Clean up temp AI files written for multiplayer matches
    /// </summary>
    public static void CleanupTempAIFiles()
    {
        foreach (string path in tempAIFiles)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log($"[MultiplayerMatchRunner] Cleaned up temp AI: {path}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[MultiplayerMatchRunner] Failed to clean up {path}: {e.Message}");
            }
        }
        tempAIFiles.Clear();
    }
    
    /// <summary>
    /// Clear all match data after the match has been set up
    /// </summary>
    public static void Clear()
    {
        IsMultiplayerMatch = false;
        PosterMatch = null;
        ChallengerTankConfigs = null;
        ChallengerDiscordId = null;
        ChallengerUsername = null;
        ChallengerTeamName = null;
        ChallengerElo = 0;
        MatchSeed = 0;
        
        // Clean up temp AI files
        CleanupTempAIFiles();
    }
}
