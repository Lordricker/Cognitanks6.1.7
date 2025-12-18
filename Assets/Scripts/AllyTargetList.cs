using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages shared target information across allied tanks.
/// Each team has its own list of spotted enemies.
/// Multiple tanks can report the same enemy (creating multiple entries).
/// </summary>
public class AllyTargetList : MonoBehaviour
{
    // Singleton instance
    private static AllyTargetList _instance;
    public static AllyTargetList Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find existing instance
                _instance = FindFirstObjectByType<AllyTargetList>();
                
                // Create if not found
                if (_instance == null)
                {
                    GameObject go = new GameObject("AllyTargetList");
                    _instance = go.AddComponent<AllyTargetList>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
    /// <summary>
    /// Data structure for a spotted enemy target
    /// </summary>
    [System.Serializable]
    public class TargetEntry
    {
        public string targetTankName;           // Name of the enemy tank
        public GameObject targetGameObject;     // Reference to the enemy GameObject
        public Vector3 worldPosition;           // Last known world position
        public TurretType turretType;           // Enemy turret type
        public string armorType;                // Enemy armor type description
        public int maxHP;                       // Enemy max HP
        public float currentHP;                 // Enemy current HP
        public string reporterTankName;         // Name of the tank that spotted this enemy
        public GameObject reporterGameObject;   // Reference to the reporting tank
        public float lastUpdateTime;            // Time when this entry was last updated
        
        public TargetEntry(GameObject target, TankMan targetTankMan, GameObject reporter, TankMan reporterTankMan)
        {
            targetGameObject = target;
            targetTankName = target.name;
            worldPosition = target.transform.position;
            turretType = targetTankMan.TurretType;
            armorType = GetArmorTypeDescription(targetTankMan);
            maxHP = targetTankMan.TotalHP;
            currentHP = targetTankMan.CurrentHealth;
            reporterGameObject = reporter;
            reporterTankName = reporter.name;
            lastUpdateTime = Time.time;
        }
        
        /// <summary>
        /// Updates the target entry with current data
        /// </summary>
        public void Update(TankMan targetTankMan)
        {
            if (targetGameObject != null)
            {
                worldPosition = targetGameObject.transform.position;
                currentHP = targetTankMan.CurrentHealth;
                lastUpdateTime = Time.time;
            }
        }
        
        private static string GetArmorTypeDescription(TankMan tankMan)
        {
            // Get armor description based on armor value
            float armor = tankMan.Armor;
            if (armor <= 0) return "None";
            if (armor < 25) return "Light";
            if (armor < 50) return "Medium";
            if (armor < 75) return "Heavy";
            return "Ultra-Heavy";
        }
    }
    
    // Dictionary: teamId -> list of target entries for that team
    private Dictionary<int, List<TargetEntry>> teamTargetLists = new Dictionary<int, List<TargetEntry>>();
    
    // Maximum number of teams supported
    private const int MAX_TEAMS = 4;
    
    void Awake()
    {
        // Singleton setup
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialize lists for all teams
        for (int i = 0; i < MAX_TEAMS; i++)
        {
            teamTargetLists[i] = new List<TargetEntry>();
        }
    }
    
    /// <summary>
    /// Reports a spotted enemy to the ally target list.
    /// Called by tanks when they detect an enemy in their vision cone.
    /// </summary>
    /// <param name="reporterTank">The tank reporting the sighting</param>
    /// <param name="enemyTarget">The enemy tank that was spotted</param>
    public void ReportTarget(TankMan reporterTank, GameObject enemyTarget)
    {
        if (reporterTank == null || enemyTarget == null) return;
        
        TankTeamInfo reporterTeamInfo = reporterTank.GetComponent<TankTeamInfo>();
        if (reporterTeamInfo == null) return;
        
        TankMan enemyTankMan = enemyTarget.GetComponent<TankMan>();
        if (enemyTankMan == null)
        {
            enemyTankMan = enemyTarget.GetComponentInParent<TankMan>();
        }
        if (enemyTankMan == null) return;
        
        int teamId = reporterTeamInfo.teamId;
        
        // Ensure team list exists
        if (!teamTargetLists.ContainsKey(teamId))
        {
            teamTargetLists[teamId] = new List<TargetEntry>();
        }
        
        // Check if this reporter already has an entry for this target
        TargetEntry existingEntry = teamTargetLists[teamId].Find(entry => 
            entry.targetGameObject == enemyTarget && 
            entry.reporterGameObject == reporterTank.gameObject);
        
        if (existingEntry != null)
        {
            // Update existing entry
            existingEntry.Update(enemyTankMan);
        }
        else
        {
            // Add new entry
            TargetEntry newEntry = new TargetEntry(
                enemyTarget, 
                enemyTankMan, 
                reporterTank.gameObject, 
                reporterTank
            );
            teamTargetLists[teamId].Add(newEntry);
        }
    }
    
    /// <summary>
    /// Removes a target entry when a tank can no longer see an enemy.
    /// Only removes the entry for the specific reporter tank.
    /// </summary>
    /// <param name="reporterTank">The tank that no longer sees the enemy</param>
    /// <param name="enemyTarget">The enemy that is no longer visible</param>
    public void RemoveTarget(TankMan reporterTank, GameObject enemyTarget)
    {
        if (reporterTank == null || enemyTarget == null) return;
        
        TankTeamInfo reporterTeamInfo = reporterTank.GetComponent<TankTeamInfo>();
        if (reporterTeamInfo == null) return;
        
        int teamId = reporterTeamInfo.teamId;
        
        if (!teamTargetLists.ContainsKey(teamId)) return;
        
        // Remove only the entry from this specific reporter
        teamTargetLists[teamId].RemoveAll(entry => 
            entry.targetGameObject == enemyTarget && 
            entry.reporterGameObject == reporterTank.gameObject);
    }
    
    /// <summary>
    /// Gets all known enemy targets for a specific team.
    /// Returns unique targets (deduplicates entries from multiple reporters).
    /// </summary>
    /// <param name="teamId">The team requesting the target list</param>
    /// <returns>List of unique enemy GameObjects</returns>
    public List<GameObject> GetAllTargetsForTeam(int teamId)
    {
        List<GameObject> uniqueTargets = new List<GameObject>();
        
        if (!teamTargetLists.ContainsKey(teamId)) return uniqueTargets;
        
        foreach (var entry in teamTargetLists[teamId])
        {
            // Skip null/destroyed targets
            if (entry.targetGameObject == null) continue;
            
            // Skip dead targets
            TankMan targetTankMan = entry.targetGameObject.GetComponent<TankMan>();
            if (targetTankMan == null)
            {
                targetTankMan = entry.targetGameObject.GetComponentInParent<TankMan>();
            }
            if (targetTankMan != null && targetTankMan.CurrentHealth <= 0) continue;
            
            // Add if not already in list
            if (!uniqueTargets.Contains(entry.targetGameObject))
            {
                uniqueTargets.Add(entry.targetGameObject);
            }
        }
        
        return uniqueTargets;
    }
    
    /// <summary>
    /// Gets the closest known enemy target for a specific team and tank position.
    /// Uses data from ally target list (Coms).
    /// </summary>
    /// <param name="teamId">The team requesting the target</param>
    /// <param name="requestingPosition">Position of the requesting tank</param>
    /// <returns>Closest enemy GameObject or null if none found</returns>
    public GameObject GetClosestTargetForTeam(int teamId, Vector3 requestingPosition)
    {
        List<GameObject> allTargets = GetAllTargetsForTeam(teamId);
        
        if (allTargets.Count == 0) return null;
        
        GameObject closest = null;
        float closestDistance = float.MaxValue;
        
        foreach (var target in allTargets)
        {
            if (target == null) continue;
            
            float distance = Vector3.Distance(requestingPosition, target.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = target;
            }
        }
        
        return closest;
    }
    
    /// <summary>
    /// Gets detailed target entry information for a specific target from a team's perspective.
    /// Returns the most recent entry if multiple reporters have seen the target.
    /// </summary>
    /// <param name="teamId">The team requesting the information</param>
    /// <param name="targetGameObject">The target to get info about</param>
    /// <returns>TargetEntry with detailed information, or null if not found</returns>
    public TargetEntry GetTargetInfo(int teamId, GameObject targetGameObject)
    {
        if (!teamTargetLists.ContainsKey(teamId)) return null;
        
        TargetEntry mostRecent = null;
        float mostRecentTime = 0f;
        
        foreach (var entry in teamTargetLists[teamId])
        {
            if (entry.targetGameObject == targetGameObject && entry.lastUpdateTime > mostRecentTime)
            {
                mostRecent = entry;
                mostRecentTime = entry.lastUpdateTime;
            }
        }
        
        return mostRecent;
    }
    
    /// <summary>
    /// Clears all target entries for a specific team.
    /// Useful when resetting a match or when a team is eliminated.
    /// </summary>
    /// <param name="teamId">The team to clear</param>
    public void ClearTeamTargets(int teamId)
    {
        if (teamTargetLists.ContainsKey(teamId))
        {
            teamTargetLists[teamId].Clear();
        }
    }
    
    /// <summary>
    /// Clears all target entries for all teams.
    /// Useful when resetting a match.
    /// </summary>
    public void ClearAllTargets()
    {
        foreach (var list in teamTargetLists.Values)
        {
            list.Clear();
        }
    }
    
    /// <summary>
    /// Removes all entries with null/destroyed GameObjects.
    /// Called periodically to clean up stale entries.
    /// </summary>
    public void CleanupNullEntries()
    {
        foreach (var list in teamTargetLists.Values)
        {
            list.RemoveAll(entry => 
                entry.targetGameObject == null || 
                entry.reporterGameObject == null);
        }
    }
    
    /// <summary>
    /// Gets the raw target list for a team (includes duplicates from multiple reporters).
    /// Useful for debugging or advanced targeting logic.
    /// </summary>
    /// <param name="teamId">The team to get entries for</param>
    /// <returns>List of all TargetEntry objects for the team</returns>
    public List<TargetEntry> GetRawTargetList(int teamId)
    {
        if (!teamTargetLists.ContainsKey(teamId))
        {
            return new List<TargetEntry>();
        }
        
        return new List<TargetEntry>(teamTargetLists[teamId]);
    }
    
    /// <summary>
    /// Checks if a team has any known targets.
    /// </summary>
    /// <param name="teamId">The team to check</param>
    /// <returns>True if the team has at least one known target</returns>
    public bool HasTargets(int teamId)
    {
        if (!teamTargetLists.ContainsKey(teamId)) return false;
        
        // Check for any valid (non-null, alive) targets
        foreach (var entry in teamTargetLists[teamId])
        {
            if (entry.targetGameObject != null)
            {
                TankMan targetTankMan = entry.targetGameObject.GetComponent<TankMan>();
                if (targetTankMan == null)
                {
                    targetTankMan = entry.targetGameObject.GetComponentInParent<TankMan>();
                }
                
                if (targetTankMan != null && targetTankMan.CurrentHealth > 0)
                {
                    return true;
                }
            }
        }
        
        return false;
    }
}
