using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data structures for multiplayer match posting and replay system
/// </summary>
namespace MultiplayerData
{
    public enum MatchType
    {
        FourVsFour = 4,
        TenVsTen = 10
    }

    public enum MatchState
    {
        Posted,      // Waiting for challenger
        Completed,   // Match was played, replay available
        Expired      // Match timed out without challenger
    }

    /// <summary>
    /// Reference to a tank's configuration (component IDs + stats + AI scripts)
    /// Stats are copied from the player's local data but validated/overridden by server values
    /// </summary>
    [Serializable]
    public class TankConfigReference
    {
        public int slotIndex;
        public string displayName;        // Tank display name
        
        // Component IDs (used to look up server-authoritative stats)
        public string engineFrameId;
        public string armorId;
        public string turretId;
        
        // AI Scripts (copied from player)
        public string turretAIScript;     // Full AI tree JSON
        public string navAIScript;        // Full AI tree JSON
        public float turretAIWeight;      // AI complexity weight
        public float navAIWeight;         // AI complexity weight
        
        // Engine stats (server-authoritative)
        public float engineForce;
        public float engineTopSpeed;
        public float engineTorque;
        public float engineMaxTurnRate;
        public float engineTurnRampTime;
        public float engineTurnStartPercent;
        public int engineFrameHP;
        public float engineWeight;
        
        // Armor stats (server-authoritative)
        public int armorHP;
        public float armorWeight;
        
        // Turret stats (server-authoritative)
        public int turretDamage;
        public float turretRange;
        public float turretShotsPerSec;
        public float turretBulletSpeed;
        public float turretVisionRange;
        public float turretVisionCone;
        public float turretWeight;
    }

    /// <summary>
    /// Data sent when posting a new match
    /// </summary>
    [Serializable]
    public class MatchPostData
    {
        public string odiscordUserId;
        public string discordUsername;
        public string teamName;
        public int playerElo;
        public MatchType matchType;
        public long postedTimestamp;
        public List<TankConfigReference> tankConfigs = new List<TankConfigReference>();
    }

    /// <summary>
    /// Full match entry stored in Firebase
    /// </summary>
    [Serializable]
    public class MatchEntry
    {
        public string matchId;            // Firebase key
        public string odiscordUserId;
        public string discordUsername;
        public string teamName;
        public int playerElo;
        public MatchType matchType;
        public MatchState state;
        public long postedTimestamp;
        public List<TankConfigReference> tankConfigs = new List<TankConfigReference>();
    }

    /// <summary>
    /// Data for a completed match replay
    /// </summary>
    [Serializable]
    public class ReplayData
    {
        public string replayId;           // Firebase key
        public string matchId;            // Original match reference
        
        // Original poster info
        public string posterDiscordId;
        public string posterUsername;
        public string posterTeamName;
        public int posterEloBeforeMatch;
        public List<TankConfigReference> posterTankConfigs = new List<TankConfigReference>();
        
        // Challenger info
        public string challengerDiscordId;
        public string challengerUsername;
        public string challengerTeamName;
        public int challengerEloBeforeMatch;
        public List<TankConfigReference> challengerTankConfigs = new List<TankConfigReference>();
        
        // Match result
        public int randomSeed;            // For deterministic replay
        public string winnerId;           // Discord ID of winner
        public int posterEloChange;       // +/- ELO for poster
        public int challengerEloChange;   // +/- ELO for challenger
        public long completedTimestamp;
    }

    /// <summary>
    /// Player profile stored in Firebase
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public string odiscordUserId;
        public string discordUsername;
        public int elo = 1000;
        public int matchesPlayed = 0;
        public int matchesWon = 0;
        public long lastActiveTimestamp;
    }

    /// <summary>
    /// Helper class to convert to/from Firebase JSON
    /// </summary>
    public static class MatchDataHelper
    {
        public static string ToJson<T>(T data)
        {
            return JsonUtility.ToJson(data, true);
        }

        public static T FromJson<T>(string json)
        {
            return JsonUtility.FromJson<T>(json);
        }

        public static long GetCurrentTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Calculate ELO change based on match result
        /// </summary>
        public static (int winnerChange, int loserChange) CalculateEloChange(int winnerElo, int loserElo)
        {
            const int K = 32; // K-factor for ELO calculation
            
            float expectedWinner = 1f / (1f + Mathf.Pow(10f, (loserElo - winnerElo) / 400f));
            float expectedLoser = 1f / (1f + Mathf.Pow(10f, (winnerElo - loserElo) / 400f));
            
            int winnerChange = Mathf.RoundToInt(K * (1f - expectedWinner));
            int loserChange = Mathf.RoundToInt(K * (0f - expectedLoser));
            
            return (winnerChange, loserChange);
        }
    }
}
