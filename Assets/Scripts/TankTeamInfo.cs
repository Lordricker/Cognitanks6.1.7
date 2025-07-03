using UnityEngine;

/// <summary>
/// Simple team identification for tanks
/// </summary>
public class TankTeamInfo : MonoBehaviour 
{
    [Header("Team Assignment")]
    public int teamId = 0;  // 0, 1, 2, or 3
    
    /// <summary>
    /// Check if another tank is an enemy
    /// </summary>
    public bool IsEnemy(TankTeamInfo other) 
    {
        return other != null && other.teamId != this.teamId;
    }
    
    /// <summary>
    /// Check if another tank is an ally (but not yourself)
    /// </summary>
    public bool IsAlly(TankTeamInfo other) 
    {
        return other != null && 
               other.teamId == this.teamId && 
               other != this; // not yourself
    }
    
    /// <summary>
    /// Check if another tank is on the same team (including yourself)
    /// </summary>
    public bool IsSameTeam(TankTeamInfo other)
    {
        return other != null && other.teamId == this.teamId;
    }
    
    /// <summary>
    /// Get team color for UI/visual purposes
    /// </summary>
    public Color GetTeamColor()
    {
        Color[] teamColors = { Color.blue, Color.red, Color.green, Color.yellow };
        return teamId >= 0 && teamId < teamColors.Length ? teamColors[teamId] : Color.white;
    }
    
    /// <summary>
    /// Get team name for UI purposes
    /// </summary>
    public string GetTeamName()
    {
        string[] teamNames = { "Blue Team", "Red Team", "Green Team", "Yellow Team" };
        return teamId >= 0 && teamId < teamNames.Length ? teamNames[teamId] : "Unknown Team";
    }
    
    /// <summary>
    /// Check if this tank can target another tank (i.e., is an enemy)
    /// </summary>
    public bool CanTarget(TankTeamInfo other)
    {
        return IsEnemy(other);
    }
    
    /// <summary>
    /// Check if this tank should support another tank (i.e., is an ally)
    /// </summary>
    public bool ShouldSupport(TankTeamInfo other)
    {
        return IsAlly(other);
    }
}
