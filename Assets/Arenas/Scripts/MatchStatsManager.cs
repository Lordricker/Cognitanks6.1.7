using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages match statistics tracking and display on the StatsPanel.
/// Tracks per-tank stats like damage dealt, damage taken, and enemy sighted time.
/// </summary>
public class MatchStatsManager : MonoBehaviour
{
    // Cached references to stat text fields (10 tank slots)
    private TextMeshProUGUI[] turretTypeSlots = new TextMeshProUGUI[10];
    private TextMeshProUGUI[] damageDealtSlots = new TextMeshProUGUI[10];
    private TextMeshProUGUI[] damageTakenSlots = new TextMeshProUGUI[10];
    private TextMeshProUGUI[] enemySightedTimeSlots = new TextMeshProUGUI[10];
    
    // Stats tracking per tank (keyed by TankMan instance ID)
    private Dictionary<int, TankMatchStats> tankStats = new Dictionary<int, TankMatchStats>();
    
    // Map tank instance IDs to slot indices for display
    private Dictionary<int, int> tankToSlotIndex = new Dictionary<int, int>();
    
    private bool isInitialized = false;
    
    /// <summary>
    /// Stats tracked for each tank during the match
    /// </summary>
    public class TankMatchStats
    {
        public string turretType = "None";
        public float damageDealt = 0f;
        public float damageTaken = 0f;
        public float enemySightedTime = 0f;
        
        // For continuous sighting tracking
        public bool isCurrentlySightingEnemy = false;
        public float lastSightingStartTime = 0f;
    }
    
    void Awake()
    {
        // Find and cache all the stat text fields
        InitializeStatSlots();
    }
    
    /// <summary>
    /// Finds all the text fields in the UI hierarchy using the path:
    /// CanvasUI - MatchFinishedPanel - StatsPanel - Tankslotstatpanel - tankslot (0-9)
    /// </summary>
    void InitializeStatSlots()
    {
        // Find CanvasUI
        GameObject canvasUI = GameObject.Find("CanvasUI");
        if (canvasUI == null)
        {
            Debug.LogWarning("[MatchStatsManager] CanvasUI not found. Will retry when displaying stats.");
            return;
        }
        
        // Find MatchFinishedPanel
        Transform matchFinishedPanel = canvasUI.transform.Find("MatchFinishedPanel");
        if (matchFinishedPanel == null)
        {
            Debug.LogWarning("[MatchStatsManager] MatchFinishedPanel not found under CanvasUI. Will retry when displaying stats.");
            return;
        }
        
        // Find StatsPanel
        Transform statsPanel = matchFinishedPanel.Find("StatsPanel");
        if (statsPanel == null)
        {
            Debug.LogWarning("[MatchStatsManager] StatsPanel not found under MatchFinishedPanel. Will retry when displaying stats.");
            return;
        }
        
        // Find Tankslotstatpanel
        Transform tankSlotStatPanel = statsPanel.Find("Tankslotstatpanel");
        if (tankSlotStatPanel == null)
        {
            Debug.LogWarning("[MatchStatsManager] Tankslotstatpanel not found under StatsPanel. Will retry when displaying stats.");
            return;
        }
        
        // Find each tankslot (0-9) and its children
        for (int i = 0; i < 10; i++)
        {
            // First slot is "Tankslot", subsequent are "Tankslot (1)", "Tankslot (2)", etc.
            string slotName = i == 0 ? "Tankslot" : $"Tankslot ({i})";
            Transform tankSlot = tankSlotStatPanel.Find(slotName);
            
            if (tankSlot == null)
            {
                Debug.LogWarning($"[MatchStatsManager] {slotName} not found under Tankslotstatpanel");
                
                // Debug: List all children of Tankslotstatpanel
                Debug.Log($"[MatchStatsManager] Children of Tankslotstatpanel:");
                foreach (Transform child in tankSlotStatPanel)
                {
                    Debug.Log($"  - {child.name}");
                }
                
                continue;
            }
            
            // Find the stat text fields within each tankslot
            // First child is base name, subsequent are "(1)", "(2)", etc.
            string turretTypeName = i == 0 ? "TurretTypeSlot" : $"TurretTypeSlot ({i})";
            string damageDealtName = i == 0 ? "DamageDealtSlot" : $"DamageDealtSlot ({i})";
            string damageTakenName = i == 0 ? "DamageTakenSlot" : $"DamageTakenSlot ({i})";
            string enemySightedTimeName = i == 0 ? "EnemySightedTimeSlot" : $"EnemySightedTimeSlot ({i})";
            
            Transform turretTypeTransform = tankSlot.Find(turretTypeName);
            Transform damageDealtTransform = tankSlot.Find(damageDealtName);
            Transform damageTakenTransform = tankSlot.Find(damageTakenName);
            Transform enemySightedTimeTransform = tankSlot.Find(enemySightedTimeName);
            
            if (turretTypeTransform != null)
            {
                turretTypeSlots[i] = turretTypeTransform.GetComponent<TextMeshProUGUI>();
                if (turretTypeSlots[i] == null)
                    Debug.LogWarning($"[MatchStatsManager] {turretTypeName} found but no TextMeshProUGUI component!");
            }
            else
                Debug.LogWarning($"[MatchStatsManager] {turretTypeName} not found under {slotName}");
                
            if (damageDealtTransform != null)
            {
                damageDealtSlots[i] = damageDealtTransform.GetComponent<TextMeshProUGUI>();
                if (damageDealtSlots[i] == null)
                    Debug.LogWarning($"[MatchStatsManager] {damageDealtName} found but no TextMeshProUGUI component!");
            }
            else
                Debug.LogWarning($"[MatchStatsManager] {damageDealtName} not found under {slotName}");
                
            if (damageTakenTransform != null)
            {
                damageTakenSlots[i] = damageTakenTransform.GetComponent<TextMeshProUGUI>();
                if (damageTakenSlots[i] == null)
                    Debug.LogWarning($"[MatchStatsManager] {damageTakenName} found but no TextMeshProUGUI component!");
            }
            else
                Debug.LogWarning($"[MatchStatsManager] {damageTakenName} not found under {slotName}");
                
            if (enemySightedTimeTransform != null)
            {
                enemySightedTimeSlots[i] = enemySightedTimeTransform.GetComponent<TextMeshProUGUI>();
                if (enemySightedTimeSlots[i] == null)
                    Debug.LogWarning($"[MatchStatsManager] {enemySightedTimeName} found but no TextMeshProUGUI component!");
            }
            else
                Debug.LogWarning($"[MatchStatsManager] {enemySightedTimeName} not found under {slotName}");
            
            // Log what we found for debugging
            Debug.Log($"[MatchStatsManager] Slot {i}: TurretType={turretTypeSlots[i] != null}, DamageDealt={damageDealtSlots[i] != null}, DamageTaken={damageTakenSlots[i] != null}, EnemySightedTime={enemySightedTimeSlots[i] != null}");
        }
        
        isInitialized = true;
        Debug.Log("[MatchStatsManager] Stats panel initialized successfully");
    }
    
    /// <summary>
    /// Registers a tank for stat tracking. Called when tanks are spawned.
    /// </summary>
    /// <param name="tank">The TankMan component</param>
    /// <param name="slotIndex">The slot index (0-9) for display purposes</param>
    public void RegisterTank(TankMan tank, int slotIndex)
    {
        if (tank == null)
        {
            Debug.LogWarning("[MatchStatsManager] RegisterTank: tank is null!");
            return;
        }
        
        int instanceId = tank.GetInstanceID();
        string turretTitle = tank.TurretTitle;
        
        Debug.Log($"[MatchStatsManager] RegisterTank: tank={tank.name}, instanceId={instanceId}, slotIndex={slotIndex}, TurretTitle='{turretTitle}'");
        
        if (!tankStats.ContainsKey(instanceId))
        {
            tankStats[instanceId] = new TankMatchStats();
            tankStats[instanceId].turretType = turretTitle;
            Debug.Log($"[MatchStatsManager] Created new TankMatchStats for {tank.name}, stored turretType='{tankStats[instanceId].turretType}'");
        }
        
        tankToSlotIndex[instanceId] = slotIndex;
        
        Debug.Log($"[MatchStatsManager] Registered tank {tank.name} at slot {slotIndex} with turret title '{turretTitle}'");
    }
    
    /// <summary>
    /// Records damage dealt by a tank
    /// </summary>
    public void RecordDamageDealt(TankMan tank, float damage)
    {
        if (tank == null) return;
        
        int instanceId = tank.GetInstanceID();
        if (tankStats.TryGetValue(instanceId, out TankMatchStats stats))
        {
            stats.damageDealt += damage;
        }
    }
    
    /// <summary>
    /// Records damage taken by a tank
    /// </summary>
    public void RecordDamageTaken(TankMan tank, float damage)
    {
        if (tank == null) return;
        
        int instanceId = tank.GetInstanceID();
        if (tankStats.TryGetValue(instanceId, out TankMatchStats stats))
        {
            stats.damageTaken += damage;
        }
    }
    
    /// <summary>
    /// Called when a tank starts sighting an enemy
    /// </summary>
    public void StartEnemySighting(TankMan tank)
    {
        if (tank == null) return;
        
        int instanceId = tank.GetInstanceID();
        if (tankStats.TryGetValue(instanceId, out TankMatchStats stats))
        {
            if (!stats.isCurrentlySightingEnemy)
            {
                stats.isCurrentlySightingEnemy = true;
                stats.lastSightingStartTime = Time.time;
            }
        }
    }
    
    /// <summary>
    /// Called when a tank stops sighting enemies
    /// </summary>
    public void StopEnemySighting(TankMan tank)
    {
        if (tank == null) return;
        
        int instanceId = tank.GetInstanceID();
        if (tankStats.TryGetValue(instanceId, out TankMatchStats stats))
        {
            if (stats.isCurrentlySightingEnemy)
            {
                stats.isCurrentlySightingEnemy = false;
                stats.enemySightedTime += Time.time - stats.lastSightingStartTime;
            }
        }
    }
    
    /// <summary>
    /// Updates enemy sighted time for tanks currently sighting enemies.
    /// Call this at the end of the match to finalize any ongoing sightings.
    /// </summary>
    public void FinalizeAllSightings()
    {
        foreach (var kvp in tankStats)
        {
            TankMatchStats stats = kvp.Value;
            if (stats.isCurrentlySightingEnemy)
            {
                stats.enemySightedTime += Time.time - stats.lastSightingStartTime;
                stats.isCurrentlySightingEnemy = false;
            }
        }
    }
    
    /// <summary>
    /// Displays all tracked stats on the stats panel
    /// </summary>
    public void DisplayStats()
    {
        // Re-initialize if needed (panel might not have been active at Awake)
        if (!isInitialized)
        {
            InitializeStatSlots();
        }
        
        // Finalize any ongoing sightings
        FinalizeAllSightings();
        
        // Clear all slots first
        for (int i = 0; i < 10; i++)
        {
            if (turretTypeSlots[i] != null) turretTypeSlots[i].text = "-";
            if (damageDealtSlots[i] != null) damageDealtSlots[i].text = "0";
            if (damageTakenSlots[i] != null) damageTakenSlots[i].text = "0";
            if (enemySightedTimeSlots[i] != null) enemySightedTimeSlots[i].text = "0.0s";
        }
        
        // Fill in stats for each registered tank
        foreach (var kvp in tankToSlotIndex)
        {
            int instanceId = kvp.Key;
            int slotIndex = kvp.Value;
            
            Debug.Log($"[MatchStatsManager] DisplayStats: Processing instanceId={instanceId}, slotIndex={slotIndex}");
            
            if (slotIndex < 0 || slotIndex >= 10)
            {
                Debug.LogWarning($"[MatchStatsManager] DisplayStats: Invalid slotIndex {slotIndex} for instanceId {instanceId}");
                continue;
            }
            
            if (tankStats.TryGetValue(instanceId, out TankMatchStats stats))
            {
                Debug.Log($"[MatchStatsManager] DisplayStats: Found stats for instanceId {instanceId}: turretType='{stats.turretType}', damageDealt={stats.damageDealt}, damageTaken={stats.damageTaken}, enemySightedTime={stats.enemySightedTime}");
                
                if (turretTypeSlots[slotIndex] != null)
                {
                    turretTypeSlots[slotIndex].text = stats.turretType;
                    Debug.Log($"[MatchStatsManager] DisplayStats: Set slot {slotIndex} turret type to '{stats.turretType}'");
                }
                else
                    Debug.LogWarning($"[MatchStatsManager] DisplayStats: turretTypeSlots[{slotIndex}] is null!");
                
                if (damageDealtSlots[slotIndex] != null)
                    damageDealtSlots[slotIndex].text = Mathf.RoundToInt(stats.damageDealt).ToString();
                
                if (damageTakenSlots[slotIndex] != null)
                    damageTakenSlots[slotIndex].text = Mathf.RoundToInt(stats.damageTaken).ToString();
                
                if (enemySightedTimeSlots[slotIndex] != null)
                    enemySightedTimeSlots[slotIndex].text = $"{stats.enemySightedTime:F1}s";
            }
            else
            {
                Debug.LogWarning($"[MatchStatsManager] DisplayStats: No stats found for instanceId {instanceId}!");
            }
        }
        
        Debug.Log("[MatchStatsManager] Stats displayed on panel");
    }
    
    /// <summary>
    /// Gets the singleton instance of MatchStatsManager
    /// </summary>
    public static MatchStatsManager Instance { get; private set; }
    
    void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    
    void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
