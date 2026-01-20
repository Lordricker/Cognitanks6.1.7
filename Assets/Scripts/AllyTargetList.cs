using System.Collections.Generic;
using UnityEngine;
using System.Text;

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
    
    // Team Tag System: teamId -> (targetInstanceId -> tagValue)
    // Stores tags assigned by team members to enemy tanks
    private Dictionary<int, Dictionary<int, int>> teamTagLists = new Dictionary<int, Dictionary<int, int>>();
    
    // Maximum number of teams supported
    private const int MAX_TEAMS = 4;
    
    // Target entries automatically expire after this time (in seconds)
    private const float TARGET_EXPIRATION_TIME = 0.5f;
    
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
            teamTagLists[i] = new Dictionary<int, int>();
        }
    }
    
    // Debug timer for logging team tags
    private float debugTimer = 0f;
    private const float DEBUG_INTERVAL = 2f; // Log every 2 seconds
    
    void Update()
    {
        // Clean up expired target entries
        CleanupExpiredTargets();
        
        // Debug logging of team tag lists
        debugTimer += Time.deltaTime;
        if (debugTimer >= DEBUG_INTERVAL)
        {
            debugTimer = 0f;
            LogTeamTagLists();
        }
    }
    
    /// <summary>
    /// Removes target entries that are older than TARGET_EXPIRATION_TIME
    /// </summary>
    private void CleanupExpiredTargets()
    {
        float currentTime = Time.time;
        
        foreach (var teamList in teamTargetLists.Values)
        {
            teamList.RemoveAll(entry => currentTime - entry.lastUpdateTime > TARGET_EXPIRATION_TIME);
        }
    }
    
    /// <summary>
    /// Debug method to log current team tag lists
    /// </summary>
    private void LogTeamTagLists()
    {
        StringBuilder debugOutput = new StringBuilder();
        debugOutput.AppendLine("=== TEAM TAG LISTS ===");
        
        bool hasAnyTags = false;
        foreach (var teamEntry in teamTagLists)
        {
            int teamId = teamEntry.Key;
            var tagDictionary = teamEntry.Value;
            
            if (tagDictionary.Count > 0)
            {
                hasAnyTags = true;
                debugOutput.AppendLine($"Team {teamId}:");
                
                foreach (var tagEntry in tagDictionary)
                {
                    int targetInstanceId = tagEntry.Key;
                    int tagValue = tagEntry.Value;
                    
                    // Try to get the target name from the target list
                    string targetName = $"InstanceID_{targetInstanceId}";
                    foreach (var targetList in teamTargetLists.Values)
                    {
                        var targetEntry = targetList.Find(t => t.targetGameObject != null && t.targetGameObject.GetInstanceID() == targetInstanceId);
                        if (targetEntry != null)
                        {
                            targetName = targetEntry.targetTankName;
                            break;
                        }
                    }
                    
                    debugOutput.AppendLine($"  {targetName} → Tag {tagValue}");
                }
            }
        }
        
        if (!hasAnyTags)
        {
            debugOutput.AppendLine("No team tags assigned.");
        }
        
        Debug.Log(debugOutput.ToString());
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
            // Skip expired entries (older than TARGET_EXPIRATION_TIME)
            if (Time.time - entry.lastUpdateTime > TARGET_EXPIRATION_TIME) continue;
            
            // Skip null/destroyed targets
            if (entry.targetGameObject == null) continue;
            
            // Skip if the reporter (tank providing vision) is dead or null
            if (entry.reporterGameObject == null) continue;
            TankMan reporterTankMan = entry.reporterGameObject.GetComponent<TankMan>();
            if (reporterTankMan == null || reporterTankMan.CurrentHealth <= 0) continue;
            
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
    
    #region Team Tag System
    
    /// <summary>
    /// Sets or updates a team tag for a target.
    /// Tags are shared across all tanks on the same team.
    /// </summary>
    /// <param name="teamId">The team setting the tag</param>
    /// <param name="targetGameObject">The target to tag</param>
    /// <param name="tagValue">The tag value to assign</param>
    public void SetTeamTag(int teamId, GameObject targetGameObject, int tagValue)
    {
        if (targetGameObject == null) return;
        
        // Ensure team tag list exists
        if (!teamTagLists.ContainsKey(teamId))
        {
            teamTagLists[teamId] = new Dictionary<int, int>();
        }
        
        // Use instance ID as the unique identifier for the target
        int targetId = targetGameObject.GetInstanceID();
        
        // Update or add the tag
        if (teamTagLists[teamId].ContainsKey(targetId))
        {
            teamTagLists[teamId][targetId] = tagValue;
        }
        else
        {
            teamTagLists[teamId].Add(targetId, tagValue);
        }
    }
    
    /// <summary>
    /// Gets the team tag for a target.
    /// </summary>
    /// <param name="teamId">The team requesting the tag</param>
    /// <param name="targetGameObject">The target to get the tag for</param>
    /// <returns>The tag value, or -1 if no tag exists</returns>
    public int GetTeamTag(int teamId, GameObject targetGameObject)
    {
        if (targetGameObject == null) return -1;
        
        if (!teamTagLists.ContainsKey(teamId)) return -1;
        
        int targetId = targetGameObject.GetInstanceID();
        
        if (teamTagLists[teamId].ContainsKey(targetId))
        {
            return teamTagLists[teamId][targetId];
        }
        
        return -1; // No tag exists
    }
    
    /// <summary>
    /// Checks if a team has a tag for a target.
    /// </summary>
    /// <param name="teamId">The team to check</param>
    /// <param name="targetGameObject">The target to check</param>
    /// <returns>True if a tag exists</returns>
    public bool HasTeamTag(int teamId, GameObject targetGameObject)
    {
        if (targetGameObject == null) return false;
        
        if (!teamTagLists.ContainsKey(teamId)) return false;
        
        int targetId = targetGameObject.GetInstanceID();
        return teamTagLists[teamId].ContainsKey(targetId);
    }
    
    /// <summary>
    /// Removes a team tag for a target.
    /// </summary>
    /// <param name="teamId">The team to remove the tag from</param>
    /// <param name="targetGameObject">The target to remove the tag for</param>
    public void RemoveTeamTag(int teamId, GameObject targetGameObject)
    {
        if (targetGameObject == null) return;
        
        if (!teamTagLists.ContainsKey(teamId)) return;
        
        int targetId = targetGameObject.GetInstanceID();
        teamTagLists[teamId].Remove(targetId);
    }
    
    /// <summary>
    /// Clears all team tags for a specific team.
    /// </summary>
    /// <param name="teamId">The team to clear tags for</param>
    public void ClearTeamTags(int teamId)
    {
        if (teamTagLists.ContainsKey(teamId))
        {
            teamTagLists[teamId].Clear();
        }
    }
    
    /// <summary>
    /// Clears all team tags for all teams.
    /// </summary>
    public void ClearAllTeamTags()
    {
        foreach (var list in teamTagLists.Values)
        {
            list.Clear();
        }
    }
    
    #endregion
}
