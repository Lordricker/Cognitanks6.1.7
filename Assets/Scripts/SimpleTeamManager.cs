using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple team assignment for both singleplayer and multiplayer battles
/// </summary>
public class SimpleTeamManager : MonoBehaviour
{
    [Header("Team Configuration")]
    [SerializeField] private bool debugMode = true;
    
    private void Start()
    {
        // Auto-assign teams after tanks are spawned
        Invoke(nameof(AssignTeamsFromBattleMode), 0.1f);
    }
    
    /// <summary>
    /// Assigns teams based on battle mode (singleplayer vs multiplayer)
    /// </summary>
    public void AssignTeamsFromBattleMode()
    {
        string battleMode = PlayerPrefs.GetString("BattleMode", "singleplayer");
        
        if (battleMode == "singleplayer")
        {
            AssignSingleplayerTeams();
        }
        else if (battleMode == "multiplayer")
        {
            AssignMultiplayerTeams();
        }
        
        if (debugMode)
        {
            LogTeamSummary();
        }
    }
    
    /// <summary>
    /// Singleplayer: Player tanks = Team 0, Enemy tanks = Team 1
    /// </summary>
    private void AssignSingleplayerTeams()
    {
        TankMan[] allTanks = FindObjectsByType<TankMan>(FindObjectsSortMode.None);
        
        foreach (var tank in allTanks)
        {
            var teamInfo = GetOrAddTeamInfo(tank.gameObject);
            var tankSlotData = tank.GetTankSlotData();
            
            if (tankSlotData != null)
            {
                // Use tankSlotData.teamId directly for singleplayer
                // Player tanks should have teamId = 0, enemy tanks teamId = 1
                teamInfo.teamId = tankSlotData.teamId;
                
                if (debugMode)
                {
                    string teamType = tankSlotData.teamId == 0 ? "Player" : "Enemy";
                    Debug.Log($"[SimpleTeamManager] Assigned {tank.gameObject.name} to {teamType} Team ({tankSlotData.teamId})");
                }
            }
        }
    }
    
    /// <summary>
    /// Multiplayer: Assign teams based on matchmaking results
    /// </summary>
    private void AssignMultiplayerTeams()
    {
        // Get team assignments from matchmaking
        string teamAssignments = PlayerPrefs.GetString("TeamAssignments", "");
        int myTeamId = PlayerPrefs.GetInt("MyTeamId", 0);
        
        if (string.IsNullOrEmpty(teamAssignments))
        {
            Debug.LogWarning("[SimpleTeamManager] No team assignments found for multiplayer battle!");
            return;
        }
        
        string[] assignments = teamAssignments.Split(',');
        TankMan[] allTanks = FindObjectsByType<TankMan>(FindObjectsSortMode.None);
        
        for (int i = 0; i < allTanks.Length && i < assignments.Length; i++)
        {
            var tank = allTanks[i];
            var teamInfo = GetOrAddTeamInfo(tank.gameObject);
            
            if (int.TryParse(assignments[i], out int assignedTeam))
            {
                teamInfo.teamId = assignedTeam;
                
                if (debugMode)
                {
                    string teamType = assignedTeam == myTeamId ? "My" : "Enemy";
                    Debug.Log($"[SimpleTeamManager] Assigned {tank.gameObject.name} to {teamType} Team ({assignedTeam})");
                }
            }
        }
    }
    
    /// <summary>
    /// Get or add TankTeamInfo component
    /// </summary>
    private TankTeamInfo GetOrAddTeamInfo(GameObject tank)
    {
        TankTeamInfo teamInfo = tank.GetComponent<TankTeamInfo>();
        if (teamInfo == null)
        {
            teamInfo = tank.AddComponent<TankTeamInfo>();
        }
        return teamInfo;
    }
    
    /// <summary>
    /// Log a summary of all teams
    /// </summary>
    private void LogTeamSummary()
    {
        TankTeamInfo[] allTanks = FindObjectsByType<TankTeamInfo>(FindObjectsSortMode.None);
        
        // Count tanks per team
        Dictionary<int, int> teamCounts = new Dictionary<int, int>();
        
        foreach (TankTeamInfo tank in allTanks)
        {
            if (!teamCounts.ContainsKey(tank.teamId))
                teamCounts[tank.teamId] = 0;
            teamCounts[tank.teamId]++;
        }
        
        Debug.Log($"[SimpleTeamManager] Team Summary:");
        foreach (var kvp in teamCounts)
        {
            Debug.Log($"  Team {kvp.Key}: {kvp.Value} tanks");
        }
    }
    
    /// <summary>
    /// Get all tanks belonging to a specific team
    /// </summary>
    public List<TankTeamInfo> GetTeamTanks(int teamId)
    {
        List<TankTeamInfo> teamTanks = new List<TankTeamInfo>();
        TankTeamInfo[] allTanks = FindObjectsByType<TankTeamInfo>(FindObjectsSortMode.None);
        
        foreach (TankTeamInfo tank in allTanks)
        {
            if (tank.teamId == teamId)
            {
                teamTanks.Add(tank);
            }
        }
        
        return teamTanks;
    }
}
