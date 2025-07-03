using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using AiEditor;

/// <summary>
/// Unified tank management system that handles:
/// 1. Tank parameter calculations from component data
/// 2. AI execution for both navigation and turret control
/// 3. Sensor-based decision making and combat systems
/// 4. All movement and combat operations (consolidated from former Master scripts)
/// </summary>
public class TankMan : MonoBehaviour
{
    [Header("Tank Slot Data")]
    [SerializeField] private TankSlotData tankSlotData;
    
    [Header("AI Configuration")]
    [SerializeField] private bool enableNavAI = true;
    [SerializeField] private bool enableTurretAI = true;
    [SerializeField] private float aiUpdateInterval = 0.1f;
    
    [Header("Wander Settings")]
    [SerializeField] private float wanderRange = 100f;
    [SerializeField] private float wanderReachDistance = 3f;
    
    [Header("Tank Components")]
    [SerializeField] private Transform turretTransform;
    [SerializeField] private Transform firePoint;
    [SerializeField] private UnityEngine.AI.NavMeshAgent navAgent;
    
    [Header("Sensor Settings")]
    [SerializeField] private string tankTag = "Tank";
    
    // Team-based detection support
    private TankTeamInfo myTeamInfo;    [Header("Projectile Settings")]
    [SerializeField] private GameObject bulletPrefab; // Universal bullet prefab for all tanks
    [SerializeField] private float bulletSpeed = 50f; // Speed from turret data
    
    [Header("Calculated Stats - Read Only")]
    [SerializeField] private float totalWeight;
    [SerializeField] private int totalHP;
    [SerializeField] private int enginePower;
    [SerializeField] private int damage;
    [SerializeField] private float range;
    [SerializeField] private float shotsPerSec;
    [SerializeField] private string knockback;
    [SerializeField] private float visionCone;
    [SerializeField] private float visionRange;
    [SerializeField] private float currentHealth;
    [SerializeField] private float armor;
    [SerializeField] private TurretType turretType = TurretType.DirectFire;
    
    [Header("Assigned AI Components")]
    [SerializeField] private AiTreeAsset assignedNavAI;
    [SerializeField] private AiTreeAsset assignedTurretAI;
    
    // Public properties for external access
    public float TotalWeight => totalWeight;
    public int TotalHP => totalHP;
    public int EnginePower => enginePower;
    public int Damage => damage;
    public float Range => range;
    public float ShotsPerSec => shotsPerSec;
    public string Knockback => knockback;
    public float VisionCone => visionCone;
    public float VisionRange => visionRange;
    public float CurrentHealth => currentHealth;
    public float Armor => armor;
    public AiTreeAsset AssignedNavAI => assignedNavAI;
    public AiTreeAsset AssignedTurretAI => assignedTurretAI;

    [Header("Terrain Following")]
    private Quaternion desiredRotation = Quaternion.identity;
    private bool hasValidTerrainRotation = false;
    public float MoveSpeed => Mathf.Max(1f, enginePower - (totalWeight * 0.1f));
    public float TurnSpeed => Mathf.Max(30f, 90f - (totalWeight * 0.5f));
    
    // Public properties for AI Master scripts
    public Transform turretPivot => turretTransform;
    
    // AI interface methods expected by NavAIMaster and TurretAIMaster
    public bool HasTarget() => currentTarget != null;
    public Transform GetCurrentTarget() => currentTarget?.transform;
    public bool IsEnemyVisible() => currentTarget != null && detectedEnemies.Contains(currentTarget);
    public bool IsEnemyWithinDistance(float distance) => currentTarget != null && Vector3.Distance(transform.position, currentTarget.transform.position) <= distance;
    public float GetDistanceToTarget() => currentTarget != null ? Vector3.Distance(transform.position, currentTarget.transform.position) : float.MaxValue;
    
    // AI execution state
    private AiExecutableNode currentNavNode;
    private AiExecutableNode currentTurretNode;
    private AiExecutableNode currentActionNode; // Added for parameter access in actions
    private Coroutine navAiCoroutine;
    private Coroutine turretAiCoroutine;
    private Coroutine currentActionCoroutine;
    
    // Sensor data
    private GameObject currentTarget;
    private List<GameObject> detectedEnemies = new List<GameObject>();
    private List<GameObject> detectedAllies = new List<GameObject>();
    private float lastFireTime;
    
    // Wander State Management
    private Vector3 currentWanderTarget;
    private bool isWandering = false;
    private Vector3 wanderOrigin; // Reference point for wander range checking
    private float wanderStartTime; // Track when we started moving to current wander target
    private float wanderTimeout = 5f; // Timeout in seconds before picking new wander point
    
    void Start()
    {
        if (navAgent == null) navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        
        // Initialize team info - this is critical for enemy detection
        EnsureTeamInfoExists();
        
        // Configure NavMeshAgent for manual rotation control
        if (navAgent != null)
        {
            navAgent.updateRotation = false; // We'll handle rotation manually
            navAgent.speed = MoveSpeed > 0 ? MoveSpeed : 5f; // Set movement speed
        }
        
        // Assign the AI components from tankSlotData for display/reference
        assignedNavAI = tankSlotData != null ? tankSlotData.navAI : null;
        assignedTurretAI = tankSlotData != null ? tankSlotData.turretAI : null;        // Initialize wander origin point
        wanderOrigin = transform.position;
        
        CalculateStats();
        currentHealth = totalHP;
        
        // Update NavMeshAgent speed after stats calculation
        if (navAgent != null)
        {
            navAgent.speed = MoveSpeed;
        }
        
        // Start AI after a small delay to ensure NavMeshAgent is ready
        StartCoroutine(DelayedStartAI());
    }
    
    /// <summary>
    /// Ensures this tank has TankTeamInfo component for team-based detection
    /// </summary>
    void EnsureTeamInfoExists()
    {
        myTeamInfo = GetComponent<TankTeamInfo>();
        if (myTeamInfo == null)
        {
            myTeamInfo = gameObject.AddComponent<TankTeamInfo>();
        }
        
        // If we have TankSlotData, use its team assignment
        if (tankSlotData != null)
        {
            myTeamInfo.teamId = tankSlotData.teamId;
            Debug.Log($"[TankMan] *** {gameObject.name} TEAM SETUP *** Team ID: {myTeamInfo.teamId} (from TankSlotData)");
        }
        else
        {
            Debug.LogWarning($"[TankMan] {gameObject.name} has no TankSlotData - team ID not set!");
        }
    }
    
    IEnumerator DelayedStartAI()
    {
        // Wait a frame for all components to initialize
        yield return null;
        
        // Try to place NavMeshAgent on NavMesh if it isn't already
        if (navAgent != null && navAgent.enabled && !navAgent.isOnNavMesh)
        {
            navAgent.Warp(transform.position);
        }
        
        StartAI();
    }
    
    void FixedUpdate()
    {
        // Keep FixedUpdate empty - we'll do terrain alignment in LateUpdate
    }
    
    void LateUpdate()
    {
        // LateUpdate runs after all other updates, so NavMeshAgent won't override our rotation
        AlignToTerrain();
    }
    
    void Update()
    {
        // Force apply stored rotation if we have one
        if (hasValidTerrainRotation)
        {
            transform.rotation = desiredRotation;
        }
    }
    
    #region Tank Parameters System
      /// <summary>
    /// Calculates all tank stats from component data stored in TankSlotData
    /// Call this when tank components change
    /// </summary>
    public void CalculateStats()
    {
        if (tankSlotData == null)
        {
            return;
        }
        
        // Get total weight from TankSlotData (it calculates this)
        totalWeight = tankSlotData.totalWeight;
        
        // Get armor stats from TankSlotData stat fields
        totalHP = 100; // Base HP
        if (tankSlotData.armorHP > 0)
        {
            totalHP += tankSlotData.armorHP;
            armor = tankSlotData.armorHP * 0.25f; // Convert HP to armor value
        }
        else
        {
            armor = 0f;
        }
        
        Debug.Log($"[TankMan] *** {gameObject.name} HP CALCULATION *** Base: 100, ArmorHP: {tankSlotData.armorHP}, Total: {totalHP}, Armor: {armor}");
        
        // Get engine stats from TankSlotData stat fields
        enginePower = tankSlotData.enginePower > 0 ? tankSlotData.enginePower : 1; // Base engine power
        
        // Get turret stats from TankSlotData stat fields
        turretType = tankSlotData.turretType;
        damage = tankSlotData.turretDamage;
        range = tankSlotData.turretRange;
        shotsPerSec = tankSlotData.turretShotsPerSec;
        bulletSpeed = tankSlotData.turretBulletSpeed;
        knockback = tankSlotData.turretKnockback;
        visionCone = tankSlotData.turretVisionCone;
        visionRange = tankSlotData.turretVisionRange;
        
        // Update NavMeshAgent speed if available
        if (navAgent != null)
        {
            navAgent.speed = MoveSpeed;
        }
    }    /// <summary>
    /// Set the tank slot data reference (called by TankAssembly)
    /// </summary>
    public void SetTankSlotData(TankSlotData slotData)
    {
        tankSlotData = slotData;
        assignedNavAI = tankSlotData != null ? tankSlotData.navAI : null;
        assignedTurretAI = tankSlotData != null ? tankSlotData.turretAI : null;
        CalculateStats();
        
        // Ensure team info is properly set
        EnsureTeamInfoExists();
        
        // Update NavMeshAgent configuration after stats calculation
        if (navAgent != null)
        {
            navAgent.speed = MoveSpeed;
            navAgent.updateRotation = false; // Ensure manual rotation control
        }
    }

    /// <summary>
    /// Get the tank slot data reference (used by SimpleTeamManager for team assignment)
    /// </summary>
    public TankSlotData GetTankSlotData()
    {
        return tankSlotData;
    }    /// <summary>
    /// Set the turret and fire point transforms (called by TankAssembly)
    /// </summary>
    public void SetTurretComponents(Transform turret, Transform firePointTransform)
    {
        turretTransform = turret;
        firePoint = firePointTransform;
    }
    
    /// <summary>
    /// Set the bullet prefab reference (called by TankAssembly)
    /// </summary>
    public void SetBulletPrefab(GameObject prefab)
    {
        bulletPrefab = prefab;
    }

    #endregion
    
    #region AI System
      public void StartAI()
    {
        StopAI();
        
        if (enableNavAI && tankSlotData?.navAI != null)
        {
            navAiCoroutine = StartCoroutine(ExecuteNavAI());
        }
        
        if (enableTurretAI && tankSlotData?.turretAI != null)
        {
            turretAiCoroutine = StartCoroutine(ExecuteTurretAI());
        }
    }
    
    public void StopAI()
    {
        if (navAiCoroutine != null)
        {
            StopCoroutine(navAiCoroutine);
            navAiCoroutine = null;
        }
        
        if (turretAiCoroutine != null)
        {
            StopCoroutine(turretAiCoroutine);
            turretAiCoroutine = null;
        }
        
        if (currentActionCoroutine != null)
        {
            StopCoroutine(currentActionCoroutine);
            currentActionCoroutine = null;
        }
        
        // Reset wander state
        isWandering = false;
    }
      /// <summary>
    /// Main navigation AI execution loop
    /// </summary>
    IEnumerator ExecuteNavAI()
    {
        var navAiTree = tankSlotData.navAI;
        if (string.IsNullOrEmpty(navAiTree.startNodeId))
        {
            yield break;
        }

        // Handle StartNavButton case - find nodes connected from StartNavButton
        currentNavNode = GetFirstNodeFromStart(navAiTree);
        
        while (currentNavNode != null)
        {
            yield return new WaitForSeconds(aiUpdateInterval);
            
            // Update sensor data
            UpdateSensorData();
            
            // Execute current node and get next node
            currentNavNode = ExecuteNode(currentNavNode, navAiTree);
        }
    }
      /// <summary>
    /// Main turret AI execution loop  
    /// </summary>
    IEnumerator ExecuteTurretAI()
    {
        var turretAiTree = tankSlotData.turretAI;
        if (string.IsNullOrEmpty(turretAiTree.startNodeId))
        {
            yield break;
        }

        // Handle StartNavButton case - find nodes connected from StartNavButton (same as NavAI)
        currentTurretNode = GetFirstNodeFromStart(turretAiTree);
        
        while (currentTurretNode != null)
        {
            yield return new WaitForSeconds(aiUpdateInterval);
            
            // Update sensor data
            UpdateSensorData();
            
            // Execute current node and get next node
            currentTurretNode = ExecuteNode(currentTurretNode, turretAiTree);
        }
    }
    
    /// <summary>
    /// Executes a single AI node and returns the next node to execute
    /// Implements the top-down, backtrack-on-false, Y-position priority pattern
    /// </summary>
    AiExecutableNode ExecuteNode(AiExecutableNode node, AiTreeAsset tree)
    {
        if (node == null) return null;
        switch (node.nodeType)
        {
            case AiNodeType.Condition:
                bool conditionResult = ExecuteCondition(node);
                return GetNextNodeFromCondition(node, tree, conditionResult);
                
            case AiNodeType.Action:
                ExecuteAction(node);
                return GetNextNodeFromAction(node, tree);
                
            case AiNodeType.SubAI:
                ExecuteSubAI(node, tree);
                return GetNextNodeFromAction(node, tree);
                
            default:
                // Move to first connected node
                if (node.connectedNodeIds.Count > 0)
                {
                    return tree.executableNodes.Find(n => n.nodeId == node.connectedNodeIds[0]);
                }
                return null;
        }
    }    /// <summary>
    /// Gets the next node after a condition based on the result and Y-position priority
    /// </summary>
    AiExecutableNode GetNextNodeFromCondition(AiExecutableNode conditionNode, AiTreeAsset tree, bool conditionResult)
    {
        if (conditionNode.connectedNodeIds.Count == 0)
            return null;
        
        // Sort connected nodes by Y position (highest first)
        var sortedConnections = conditionNode.connectedNodeIds
            .Select(nodeId => tree.executableNodes.Find(n => n.nodeId == nodeId))
            .Where(n => n != null)
            .OrderByDescending(n => n.position.y)
            .ToList();
        
        if (conditionResult)
        {
            // Special handling for turret AI: Try Fire first, fallback to CenterTarget
            if (conditionNode.methodName == "IfEnemy" && sortedConnections.Count >= 2)
            {
                var fireNode = sortedConnections.FirstOrDefault(n => n.methodName == "Fire");
                var centerNode = sortedConnections.FirstOrDefault(n => n.methodName == "CenterTarget");
                
                if (fireNode != null && centerNode != null)
                {
                    // Check if we can fire (turret aimed within 2 degrees)
                    if (CanFire())
                    {
                        Debug.Log($"[TankMan] IfEnemy true - executing Fire (turret aimed)");
                        return fireNode;
                    }
                    else
                    {
                        Debug.Log($"[TankMan] IfEnemy true - executing CenterTarget (turret not aimed)");
                        return centerNode;
                    }
                }
            }
            
            // Default behavior: follow to first connected node (highest Y-position)
            var nextNode = sortedConnections.FirstOrDefault();
            return nextNode;
        }
        else
        {
            // Condition failed - check if this node is connected directly from StartNavButton or StartTurretButton
            bool isTopLevelNode = tree.connections.Any(c => (c.fromNodeId == "StartNavButton" || c.fromNodeId == "StartTurretButton") && c.toNodeId == conditionNode.nodeId);
            if (isTopLevelNode)
            {
                return GetNextAlternativeFromStart(conditionNode, tree);
            }
            
            // Not a top-level node - find the parent node and try its next branch
            AiExecutableNode parentNode = FindParentNode(conditionNode, tree);
            if (parentNode != null && parentNode != conditionNode)
            {
                return GetNextAlternativeFromParent(parentNode, conditionNode, tree);
            }
            
            // No alternatives found - restart from beginning
            return GetFirstNodeFromStart(tree);
        }
    }
    
    /// <summary>
    /// Find the parent node that connects to the given node
    /// </summary>
    AiExecutableNode FindParentNode(AiExecutableNode childNode, AiTreeAsset tree)
    {
        foreach (var node in tree.executableNodes)
        {
            if (node.connectedNodeIds.Contains(childNode.nodeId))
            {
                return node;
            }
        }
        return null;
    }
      /// <summary>
    /// Get the next alternative branch from a parent node
    /// </summary>
    AiExecutableNode GetNextAlternativeFromParent(AiExecutableNode parentNode, AiExecutableNode failedChild, AiTreeAsset tree)
    {
        // Sort parent's connections by Y position (highest first)
        var sortedConnections = parentNode.connectedNodeIds
            .Select(nodeId => tree.executableNodes.Find(n => n.nodeId == nodeId))
            .Where(n => n != null)
            .OrderByDescending(n => n.position.y)
            .ToList();

        // Find the failed child and try the next one
        int failedIndex = sortedConnections.FindIndex(n => n.nodeId == failedChild.nodeId);
        if (failedIndex >= 0 && failedIndex + 1 < sortedConnections.Count)
        {
            var nextNode = sortedConnections[failedIndex + 1];            // ...existing code...
            return nextNode;
        }
        
        // No more alternatives from this parent, continue backtracking
        AiExecutableNode grandParent = FindParentNode(parentNode, tree);
        if (grandParent != null && grandParent != parentNode)
        {
            return GetNextAlternativeFromParent(grandParent, parentNode, tree);
        }
        
        return null;
    }
      /// <summary>
    /// Gets the next node after an action
    /// </summary>
    AiExecutableNode GetNextNodeFromAction(AiExecutableNode actionNode, AiTreeAsset tree)
    {
        if (actionNode.connectedNodeIds.Count > 0)
        {
            string nextNodeId = actionNode.connectedNodeIds[0];
            return tree.executableNodes.Find(n => n.nodeId == nextNodeId);
        }
        
        // No connections - restart from beginning
        return GetFirstNodeFromStart(tree);
    }    /// <summary>
    /// Updates sensor data for decision making
    /// </summary>
    void UpdateSensorData()
    {
        detectedEnemies.Clear();
        detectedAllies.Clear();
        currentTarget = null;
        
        // Detect enemies and allies in range
        Collider[] detected = Physics.OverlapSphere(transform.position, visionRange);
        
        foreach (var collider in detected)
        {
            // Skip self detection
            if (collider.gameObject == gameObject) 
            {
                continue;
            }
              // Only detect objects that have TankTeamInfo (tanks)
            TankTeamInfo otherTeamInfo = collider.GetComponent<TankTeamInfo>();
            
            // If no TankTeamInfo on the collider, check the parent (tank parts like engine, turret, armor)
            if (otherTeamInfo == null)
            {
                otherTeamInfo = collider.GetComponentInParent<TankTeamInfo>();
            }
            
            if (otherTeamInfo == null)
            {
                continue; // Skip objects without team info (not tanks)
            }
            
            // Ensure we have our own team info
            if (myTeamInfo == null)
            {
                continue;
            }
            
            // Use team-based detection
            bool isEnemy = myTeamInfo.IsEnemy(otherTeamInfo);
            bool isAlly = myTeamInfo.IsAlly(otherTeamInfo);
            
            // Check if object is within vision cone (use turret direction if available)
            Vector3 visionPosition = turretTransform != null ? turretTransform.position : transform.position;
            Vector3 visionForward = turretTransform != null ? turretTransform.forward : transform.forward;
            
            Vector3 directionToTarget = (collider.transform.position - visionPosition).normalized;
            float angleToTarget = Vector3.Angle(visionForward, directionToTarget);
            bool inVisionCone = angleToTarget <= visionCone * 0.5f; // visionCone is full angle, so half for each side
            
            // Add to appropriate lists based on team and vision
            if (isEnemy && inVisionCone)
            {
                // Check if enemy tank is alive before adding to detected enemies
                TankMan enemyTankMan = collider.GetComponent<TankMan>();
                if (enemyTankMan == null)
                {
                    enemyTankMan = collider.GetComponentInParent<TankMan>();
                }
                
                if (enemyTankMan != null && enemyTankMan.CurrentHealth > 0)
                {
                    detectedEnemies.Add(collider.gameObject);
                }
                else if (enemyTankMan != null)
                {
                    // Debug log for dead tank detection (optional)
                    Debug.Log($"[TankMan] Ignoring dead enemy tank {collider.gameObject.name} (HP: {enemyTankMan.CurrentHealth})");
                }
            }
            else if (isAlly && inVisionCone)
            {
                detectedAllies.Add(collider.gameObject);
            }
        }
        
        // Set current target to closest enemy
        if (detectedEnemies.Count > 0)
        {
            currentTarget = detectedEnemies
                .OrderBy(e => Vector3.Distance(transform.position, e.transform.position))
                .FirstOrDefault();
        }
        else
        {
            currentTarget = null;
        }
    }
    
    #endregion
    
    #region Condition Execution
    
    /// <summary>
    /// Executes condition nodes and returns true/false result
    /// </summary>
    bool ExecuteCondition(AiExecutableNode conditionNode)
    {
        bool result = false;
        
        switch (conditionNode.methodName)
        {
            case "IfSelf":
                result = currentTarget == gameObject;
                break;            case "IfEnemy":
                bool hasTarget = currentTarget != null;
                bool targetIsEnemy = hasTarget && detectedEnemies.Contains(currentTarget);
                result = hasTarget && targetIsEnemy;
                
                // Check if target is alive (ignore dead tanks)
                if (hasTarget)
                {
                    TankMan targetTankMan = currentTarget.GetComponent<TankMan>();
                    if (targetTankMan != null && targetTankMan.CurrentHealth <= 0)
                    {
                        result = false; // Don't target dead tanks
                        Debug.Log($"[TankMan] IfEnemy: Ignoring dead tank {currentTarget.name} (HP: {targetTankMan.CurrentHealth})");
                    }
                }
                
                TankTeamInfo targetTeamInfo = null;
                if (hasTarget && result) // Only check team if target is alive
                {
                    targetTeamInfo = currentTarget.GetComponent<TankTeamInfo>();
                    if (targetTeamInfo != null)
                    {
                        result = result && myTeamInfo.IsEnemy(targetTeamInfo);
                    }
                }
                break;
                
            case "IfAlly":
                result = currentTarget != null && detectedAllies.Contains(currentTarget);
                break;
                
            case "IfAny":
                result = currentTarget != null;
                break;
                
            case "IfRifle":
                result = currentTarget != null && 
                       Vector3.Distance(transform.position, currentTarget.transform.position) <= range;
                break;
                
            case "IfHP":
                // Check if current health meets the condition (e.g., "If HP > 50%" -> numericValue = 50)
                float healthPercent = (currentHealth / totalHP) * 100f;
                if (conditionNode.originalLabel.Contains(">"))
                    result = healthPercent > conditionNode.numericValue;
                else if (conditionNode.originalLabel.Contains("<"))
                    result = healthPercent < conditionNode.numericValue;
                else
                    result = healthPercent >= conditionNode.numericValue;
                break;
                
            case "IfArmor":
                // Check armor condition
                if (conditionNode.originalLabel.Contains(">"))
                    result = armor > conditionNode.numericValue;
                else if (conditionNode.originalLabel.Contains("<"))
                    result = armor < conditionNode.numericValue;
                else
                    result = armor >= conditionNode.numericValue;
                break;
                
            case "IfRange":
                // Check if target is within specified range
                if (currentTarget == null) 
                {
                    result = false;
                }
                else
                {
                    float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
                    if (conditionNode.originalLabel.Contains(">"))
                        result = distance > conditionNode.numericValue;
                    else if (conditionNode.originalLabel.Contains("<"))
                        result = distance < conditionNode.numericValue;
                    else
                        result = distance <= conditionNode.numericValue;
                }
                break;
                    
            case "IfTag":
                result = currentTarget != null && currentTarget.CompareTag(tankTag);
                break;
                
            default:
                result = false;
                break;
        }
        
        return result;
    }
    
    #endregion
    
    #region Action Execution
    
    /// <summary>
    /// Executes action nodes
    /// </summary>
    void ExecuteAction(AiExecutableNode actionNode)
    {
        // Store current action node for parameter access
        currentActionNode = actionNode;
        
        // Stop any current action
        if (currentActionCoroutine != null)
        {
            StopCoroutine(currentActionCoroutine);
            currentActionCoroutine = null;
        }
        
        switch (actionNode.methodName)
        {
            case "Fire":
                if (CanFire())
                {
                    Fire();
                }
                break;
                
            case "Wander":
                currentActionCoroutine = StartCoroutine(WanderAction());
                break;
                
            case "Move":
                if (currentTarget != null)
                {
                    currentActionCoroutine = StartCoroutine(MoveToTarget());
                }
                else
                {
                    currentActionCoroutine = StartCoroutine(WanderAction());
                }
                break;
                
            case "Stop":
                StopMovement();
                break;
                
            case "Chase":
                if (currentTarget != null)
                {
                    currentActionCoroutine = StartCoroutine(ChaseTarget());
                }
                break;
                
            case "Flee":
                if (currentTarget != null)
                {
                    currentActionCoroutine = StartCoroutine(FleeFromTarget());
                }
                break;
                
            case "Wait":
                currentActionCoroutine = StartCoroutine(WaitAction());
                break;
                
            case "TrackTarget":
            case "CenterTarget": // Alias for TrackTarget
                if (currentTarget != null)
                {
                    currentActionCoroutine = StartCoroutine(TrackTargetAction());
                }
                break;
                
            default:
                break;
        }
    }
    
    /// <summary>
    /// Executes SubAI nodes by loading and running the referenced AI tree
    /// </summary>
    void ExecuteSubAI(AiExecutableNode subAiNode, AiEditor.AiTreeAsset currentTree)
    {
        if (subAiNode == null || string.IsNullOrEmpty(subAiNode.originalLabel))
        {
            Debug.LogWarning("[TankMan] SubAI node has no label to reference AI file");
            return;
        }
        
        // The SubAI node's originalLabel contains the name of the AI file to load
        string referencedAIName = subAiNode.originalLabel.Trim();
        
        // Load the referenced AI tree asset
        AiEditor.AiTreeAsset referencedAI = LoadSubAITree(referencedAIName, subAiNode, currentTree);
        if (referencedAI == null)
        {
            Debug.LogWarning($"[TankMan] Could not load referenced SubAI: {referencedAIName}");
            return;
        }
        
        // Execute the referenced AI tree based on its branch type
        if (referencedAI.branchType == AiEditor.AiBranchType.Nav)
        {
            // Execute as navigation AI - find the start node and begin execution
            if (!string.IsNullOrEmpty(referencedAI.startNodeId))
            {
                var startNode = referencedAI.executableNodes.Find(n => n.nodeId == referencedAI.startNodeId);
                if (startNode != null)
                {
                    ExecuteSubAIChain(startNode, referencedAI);
                }
            }
        }
        else if (referencedAI.branchType == AiEditor.AiBranchType.Turret)
        {
            // Execute as turret AI - find the start node and begin execution
            if (!string.IsNullOrEmpty(referencedAI.startNodeId))
            {
                var startNode = referencedAI.executableNodes.Find(n => n.nodeId == referencedAI.startNodeId);
                if (startNode != null)
                {
                    ExecuteSubAIChain(startNode, referencedAI);
                }
            }
        }
        
        Debug.Log($"[TankMan] Executed SubAI: {referencedAIName} (branch: {referencedAI.branchType})");
    }
    
    /// <summary>
    /// Loads a SubAI tree asset based on the name and context
    /// </summary>
    AiEditor.AiTreeAsset LoadSubAITree(string aiName, AiExecutableNode subAiNode, AiEditor.AiTreeAsset currentTree)
    {
        Debug.Log($"[TankMan] LoadSubAITree: Looking for '{aiName}'");
        
#if UNITY_EDITOR
        // Determine which folder to search based on the current AI tree context
        string folderPath = "";
        
        // Check the current tree's branch type to determine the folder
        if (currentTree.branchType == AiEditor.AiBranchType.Nav)
        {
            folderPath = "Assets/AiEditor/AISaveFiles/NavFiles/";
            Debug.Log($"[TankMan] Current tree is Nav, searching in: {folderPath}");
        }
        else if (currentTree.branchType == AiEditor.AiBranchType.Turret)
        {
            folderPath = "Assets/AiEditor/AISaveFiles/TurretFiles/";
            Debug.Log($"[TankMan] Current tree is Turret, searching in: {folderPath}");
        }
        else
        {
            // Fallback: try both folders
            string[] foldersToTry = {
                "Assets/AiEditor/AISaveFiles/TurretFiles/",
                "Assets/AiEditor/AISaveFiles/NavFiles/",
                "Assets/AiEditor/AISaveFiles/"
            };
            
            Debug.Log($"[TankMan] No specific context, trying multiple folders");
            foreach (string folder in foldersToTry)
            {
                Debug.Log($"[TankMan] Searching in folder: {folder}");
                var result = SearchForAIInFolder(aiName, folder);
                if (result != null) 
                {
                    Debug.Log($"[TankMan] Found '{aiName}' in {folder}");
                    return result;
                }
            }
            Debug.LogWarning($"[TankMan] Could not find '{aiName}' in any folder");
            return null;
        }
        
        var foundAsset = SearchForAIInFolder(aiName, folderPath);
        if (foundAsset != null)
        {
            Debug.Log($"[TankMan] Found '{aiName}' in {folderPath}");
        }
        else
        {
            Debug.LogWarning($"[TankMan] Could not find '{aiName}' in {folderPath}");
        }
        return foundAsset;
#else
        Debug.LogWarning("[TankMan] SubAI loading is only supported in editor mode");
        return null;
#endif
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Searches for an AI asset in a specific folder
    /// </summary>
    AiEditor.AiTreeAsset SearchForAIInFolder(string aiName, string folderPath)
    {
        Debug.Log($"[TankMan] SearchForAIInFolder: Looking for '{aiName}' in '{folderPath}'");
        
        if (!System.IO.Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[TankMan] Directory does not exist: {folderPath}");
            return null;
        }
            
        string[] files = System.IO.Directory.GetFiles(folderPath, "*.asset");
        Debug.Log($"[TankMan] Found {files.Length} .asset files in {folderPath}");
        
        foreach (string filePath in files)
        {
            Debug.Log($"[TankMan] Checking file: {filePath}");
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AiEditor.AiTreeAsset>(filePath);
            if (asset != null)
            {
                Debug.Log($"[TankMan] Loaded asset: TreeName='{asset.TreeName}', title='{asset.title}', filename='{System.IO.Path.GetFileNameWithoutExtension(filePath)}'");
                
                if ((!string.IsNullOrEmpty(asset.TreeName) && asset.TreeName.Equals(aiName, System.StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(asset.title) && asset.title.Equals(aiName, System.StringComparison.OrdinalIgnoreCase)) ||
                    System.IO.Path.GetFileNameWithoutExtension(filePath).Equals(aiName, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[TankMan] Match found! Returning asset: {asset.TreeName ?? asset.title ?? "Unnamed"}");
                    return asset;
                }
            }
            else
            {
                Debug.LogWarning($"[TankMan] Failed to load asset at: {filePath}");
            }
        }
        
        Debug.LogWarning($"[TankMan] No matching asset found for '{aiName}' in '{folderPath}'");
        return null;
    }
#endif
    
    /// <summary>
    /// Executes a chain of SubAI nodes from the referenced AI tree
    /// </summary>
    void ExecuteSubAIChain(AiExecutableNode startNode, AiEditor.AiTreeAsset referencedAI)
    {
        AiExecutableNode currentNode = startNode;
        int maxSteps = 100; // Prevent infinite loops
        int steps = 0;
        
        while (currentNode != null && steps < maxSteps)
        {
            steps++;
            
            // Execute the current node using the same logic as the main AI execution
            switch (currentNode.nodeType)
            {
                case AiEditor.AiNodeType.Condition:
                    bool conditionResult = ExecuteCondition(currentNode);
                    currentNode = GetNextNodeFromCondition(currentNode, referencedAI, conditionResult);
                    break;
                    
                case AiEditor.AiNodeType.Action:
                    ExecuteAction(currentNode);
                    currentNode = GetNextNodeFromAction(currentNode, referencedAI);
                    break;
                    
                case AiEditor.AiNodeType.SubAI:
                    // Recursive SubAI execution (with depth limit)
                    ExecuteSubAI(currentNode, referencedAI);
                    currentNode = GetNextNodeFromAction(currentNode, referencedAI);
                    break;
                    
                default:
                    // Move to next connected node
                    if (currentNode.connectedNodeIds.Count > 0)
                    {
                        currentNode = referencedAI.executableNodes.Find(n => n.nodeId == currentNode.connectedNodeIds[0]);
                    }
                    else
                    {
                        currentNode = null;
                    }
                    break;
            }
        }
        
        if (steps >= maxSteps)
        {
            Debug.LogWarning($"[TankMan] SubAI execution reached maximum steps limit: {referencedAI.TreeName}");
        }
    }
    
    #endregion
    
    #region Combat System
    
    bool CanFire()
    {
        if (currentTarget == null || 
            Time.time - lastFireTime < (1f / shotsPerSec) ||
            Vector3.Distance(transform.position, currentTarget.transform.position) > range)
        {
            return false;
        }
        
        // Check if turret is pointing at target within 2 degrees
        if (turretTransform != null)
        {
            Vector3 turretForward = turretTransform.forward;
            Vector3 directionToTarget = (currentTarget.transform.position - turretTransform.position).normalized;
            float angleToTarget = Vector3.Angle(turretForward, directionToTarget);
            
            // Only fire if turret is pointing within 2 degrees of target
            if (angleToTarget > 2f)
            {
                return false;
            }
        }
        
        return true;
    }    void Fire()
    {
        if (currentTarget == null)
        {
            Debug.LogWarning("[TankMan] Fire() called but no current target!");
            return;
        }
        
        if (firePoint == null)
        {
            Debug.LogWarning($"[TankMan] Fire() called but firePoint is null for {gameObject.name}! Check turret prefab for FirePoint GameObject.");
            return;
        }
        
        lastFireTime = Time.time;
        
        // Simple firing - instantiate bullet if prefab exists
        if (bulletPrefab != null)
        {
            Vector3 direction;
            float launchAngle = 0f;
            
            // Calculate firing direction based on turret type
            if (turretType == TurretType.Artillery)
            {
                // Artillery: Calculate ballistic trajectory
                direction = CalculateArtilleryDirection(out launchAngle);
            }
            else
            {
                // Direct fire: Straight line to target
                direction = (currentTarget.transform.position - firePoint.position).normalized;
            }
            
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(direction));
            
            // Give bullet velocity based on turret's bullet speed
            Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
            if (bulletRb != null)
            {
                // Configure physics based on turret type
                if (turretType == TurretType.Artillery)
                {
                    bulletRb.useGravity = true;
                    // Apply velocity with calculated launch angle
                    Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                    Vector3 launchVelocity = Quaternion.AngleAxis(launchAngle, Vector3.Cross(horizontalDirection, Vector3.up)) * horizontalDirection * bulletSpeed;
                    bulletRb.linearVelocity = launchVelocity;
                }
                else
                {
                    bulletRb.useGravity = false;
                    bulletRb.linearVelocity = direction * bulletSpeed;
                }
            }
            
            // Pass combat stats to bullet
            BulletScript bulletScript = bullet.GetComponent<BulletScript>();
            if (bulletScript != null)
            {
                bulletScript.Initialize(damage, range, myTeamInfo.teamId, turretType == TurretType.Artillery);
                Debug.Log($"[TankMan] *** {gameObject.name} FIRING *** Damage: {damage}, Range: {range}, Team: {myTeamInfo.teamId}, Artillery: {turretType == TurretType.Artillery}");
            }
            else
            {
                Debug.LogWarning("[TankMan] Bullet prefab missing BulletScript component!");
            }
        }
        
        Debug.Log($"[TankMan] Fired {turretType} shot at {currentTarget.name}");
    }
    
    /// <summary>
    /// Calculates artillery firing direction with ballistic trajectory
    /// </summary>
    Vector3 CalculateArtilleryDirection(out float launchAngle)
    {
        Vector3 targetPos = currentTarget.transform.position;
        Vector3 firePos = firePoint.position;
        
        // Calculate horizontal distance and height difference
        Vector3 horizontalDisplacement = Vector3.ProjectOnPlane(targetPos - firePos, Vector3.up);
        float horizontalDistance = horizontalDisplacement.magnitude;
        float heightDifference = targetPos.y - firePos.y;
        
        // Use ballistic formula to calculate optimal launch angle
        // For maximum range with given velocity: angle = 45°
        // For hitting specific target: use ballistic trajectory calculation
        float gravity = Physics.gravity.magnitude;
        float velocitySquared = bulletSpeed * bulletSpeed;
        
        // Calculate launch angle using ballistic formula
        // Using the quadratic formula solution for trajectory
        float discriminant = velocitySquared * velocitySquared - gravity * (gravity * horizontalDistance * horizontalDistance + 2 * heightDifference * velocitySquared);
        
        if (discriminant >= 0)
        {
            // Two possible angles - use the lower one for direct fire
            float angle1 = Mathf.Atan((velocitySquared - Mathf.Sqrt(discriminant)) / (gravity * horizontalDistance));
            float angle2 = Mathf.Atan((velocitySquared + Mathf.Sqrt(discriminant)) / (gravity * horizontalDistance));
            
            // Use lower angle for more direct trajectory, higher angle for artillery arc
            launchAngle = Mathf.Rad2Deg * angle2; // Use high arc for artillery
            launchAngle = Mathf.Clamp(launchAngle, 15f, 75f); // Reasonable artillery angles
        }
        else
        {
            // Target too far - use 45° for maximum distance
            launchAngle = 45f;
        }
        
        // Return horizontal direction (angle will be applied to this)
        return horizontalDisplacement.normalized;
    }
    
    public void TakeDamage(float damageAmount)
    {
        Debug.Log($"[TankMan] {gameObject.name} taking damage: {damageAmount} (current health: {currentHealth})");
        
        // Apply armor reduction
        float finalDamage = Mathf.Max(0, damageAmount - armor);
        currentHealth -= finalDamage;
        
        Debug.Log($"[TankMan] *** {gameObject.name} took {finalDamage} damage (original: {damageAmount}, armor: {armor}). Health: {currentHealth}/{totalHP} ***");
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }    void Die()
    {
        Debug.Log($"[TankMan] {gameObject.name} destroyed!");
        StopAI();
        
        // Disable movement
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.enabled = false;
        }
        
        // Disable the tank (but keep it for visual reference)
        // You could add explosion effects, disable colliders, etc. here
        enabled = false;
        
        // TODO: Add death effects, particle systems, sound, etc.
    }
    
    #endregion
    
    #region Movement Actions
    // All movement actions now properly utilize NavMeshAgent for pathfinding and movement
    // Actions include: Stop, Wander, Move, Chase, Flee, Wait, TrackTarget
    // Each action handles NavMeshAgent state checking and boundary clamping
    
      void StopMovement()
    {
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.ResetPath(); // Stop NavMesh Agent movement
        }
        
        // No need to manually stop rigidbody since we're using NavMeshAgent only
    }
      IEnumerator WanderAction()
    {
        // Wait for NavMeshAgent to be properly initialized and placed on NavMesh
        float waitStartTime = Time.time;
        while (navAgent != null && (!navAgent.enabled || !navAgent.isOnNavMesh))
        {
            // Try to warp agent to current position to place it on NavMesh
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.Warp(transform.position);
            }
            
            // Timeout after 2 seconds of waiting
            if (Time.time - waitStartTime > 2f)
            {
                Debug.LogWarning($"[TankMan] NavMeshAgent failed to initialize after 2 seconds. Enabled: {navAgent?.enabled}, OnNavMesh: {navAgent?.isOnNavMesh}");
                yield break;
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        // Check if we need to set a new wander target
        if (!isWandering || ShouldPickNewWanderTarget())
        {
            SetNewWanderTarget();
            wanderStartTime = Time.time; // Reset timeout timer when setting new target
        }
        
        // Use NavMesh Agent to move to wander target
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            navAgent.SetDestination(currentWanderTarget);
        }
        else
        {
            Debug.LogWarning($"[TankMan] NavMeshAgent is not ready for SetDestination. Enabled: {navAgent?.enabled}, OnNavMesh: {navAgent?.isOnNavMesh}");
            yield break;
        }
        
        // Wait until we reach the target or timeout
        while (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            // Check if path is still pending
            if (navAgent.pathPending)
            {
                yield return null;
                continue;
            }
            
            // Check if we've reached the destination
            if (!navAgent.pathPending && navAgent.remainingDistance < wanderReachDistance)
            {
                break;
            }
            
            // Check for timeout - if we've been trying to reach this target for too long, pick a new one
            if (Time.time - wanderStartTime > wanderTimeout)
            {
                SetNewWanderTarget();
                wanderStartTime = Time.time;
                
                if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                {
                    navAgent.SetDestination(currentWanderTarget);
                }
                continue;
            }
            
            yield return null;
        }
        
        // Mark as no longer wandering so a new target will be picked next time
        isWandering = false;
        
        // Wait briefly at the destination
        yield return new WaitForSeconds(1f);
    }
    
    IEnumerator MoveToTarget()
    {
        Debug.Log($"[TankMan] Moving to target: {currentTarget?.name}");
        
        while (currentTarget != null)
        {
            // Check if NavMeshAgent is ready
            if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
            {
                Debug.LogWarning($"[TankMain] MoveToTarget: NavMeshAgent not ready. Enabled: {navAgent?.enabled}, OnNavMesh: {navAgent?.isOnNavMesh}");
                yield return new WaitForSeconds(0.1f);
                continue;
            }
            
            // Get target position and clamp to map boundaries
            Vector3 targetPosition = currentTarget.transform.position;
            targetPosition.x = Mathf.Clamp(targetPosition.x, 30f, 770f);
            targetPosition.z = Mathf.Clamp(targetPosition.z, 30f, 770f);
            
            // Set destination to target position
            navAgent.SetDestination(targetPosition);
            
            // Check if we've reached the target (within reasonable distance)
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distanceToTarget <= 5f) // Close enough - stop moving
            {
                Debug.Log($"[TankMan] Reached target ({distanceToTarget:F1}u <= 5u), stopping movement");
                navAgent.ResetPath();
                break;
            }
            
            // Update destination periodically for moving targets
            yield return new WaitForSeconds(0.2f); // Update 5 times per second
        }
        
        Debug.Log($"[TankMan] Move to target ended - target lost or reached");
    }
    
    IEnumerator ChaseTarget()
    {
        Debug.Log($"[TankMan] Starting chase of target: {currentTarget?.name}");
        
        while (currentTarget != null)
        {
            // Check if NavMeshAgent is ready
            if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
            {
                Debug.LogWarning($"[TankMain] ChaseTarget: NavMeshAgent not ready. Enabled: {navAgent?.enabled}, OnNavMesh: {navAgent?.isOnNavMesh}");
                yield return new WaitForSeconds(0.1f);
                continue;
            }
            
            // Get target position and clamp to map boundaries
            Vector3 targetPosition = currentTarget.transform.position;
            targetPosition.x = Mathf.Clamp(targetPosition.x, 30f, 770f);
            targetPosition.z = Mathf.Clamp(targetPosition.z, 30f, 770f);
            
            // Set destination directly to target position for more efficient chasing
            navAgent.SetDestination(targetPosition);
            
            // Check if we're close enough to the target (within weapon range)
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distanceToTarget <= range * 0.8f) // Stop chasing when within 80% of weapon range
            {
                Debug.Log($"[TankMan] Close enough to target ({distanceToTarget:F1}u <= {range * 0.8f:F1}u), stopping chase");
                navAgent.ResetPath(); // Stop moving
                break;
            }
            
            // Update destination every few frames to account for moving targets
            yield return new WaitForSeconds(0.2f); // Update 5 times per second
        }
        
        Debug.Log($"[TankMan] Chase ended - target lost or close enough");
    }
    
    IEnumerator FleeFromTarget()
    {
        Debug.Log($"[TankMan] Starting flee from target: {currentTarget?.name}");
        
        while (currentTarget != null)
        {
            // Check if NavMeshAgent is ready
            if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
            {
                Debug.LogWarning($"[TankMain] FleeFromTarget: NavMeshAgent not ready. Enabled: {navAgent?.enabled}, OnNavMesh: {navAgent?.isOnNavMesh}");
                yield return new WaitForSeconds(0.1f);
                continue;
            }
            
            // Calculate flee direction (away from target)
            Vector3 fleeDirection = (transform.position - currentTarget.transform.position).normalized;
            
            // Calculate flee destination - move a good distance away
            float fleeDistance = Mathf.Max(range * 1.5f, 50f); // Flee at least 1.5x weapon range or 50 units
            Vector3 fleePosition = transform.position + fleeDirection * fleeDistance;
            
            // Clamp to map boundaries
            fleePosition.x = Mathf.Clamp(fleePosition.x, 30f, 770f);
            fleePosition.z = Mathf.Clamp(fleePosition.z, 30f, 770f);
            
            // Set flee destination
            navAgent.SetDestination(fleePosition);
            
            // Check if we're far enough from the target
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distanceToTarget >= range * 2f) // Stop fleeing when we're 2x weapon range away
            {
                Debug.Log($"[TankMan] Far enough from target ({distanceToTarget:F1}u >= {range * 2f:F1}u), stopping flee");
                break;
            }
            
            // Update flee destination every few frames
            yield return new WaitForSeconds(0.3f); // Update ~3 times per second
        }
        
        Debug.Log($"[TankMan] Flee ended - target lost or far enough away");
    }
    
    IEnumerator WaitAction()
    {
        Debug.Log($"[TankMan] Waiting in place");
        
        // Stop all movement - ensure NavMeshAgent stops properly
        StopMovement();
        
        // Double-check that movement is actually stopped
        if (navAgent != null && navAgent.enabled && navAgent.hasPath)
        {
            navAgent.ResetPath();
            Debug.Log($"[TankMan] Force-stopped remaining NavMesh path");
        }
        
        // Wait for a specified time (default 2 seconds)
        // TODO: In the future, this could be made configurable from AI node parameters
        float waitTime = 2f;
        
        Debug.Log($"[TankMan] Waiting for {waitTime} seconds...");
        yield return new WaitForSeconds(waitTime);
        
        Debug.Log($"[TankMan] Finished waiting - ready for next action");
    }
    
    IEnumerator TrackTargetAction()
    {
        Debug.Log($"[TankMan] Tracking target with turret");
        
        while (currentTarget != null && turretTransform != null)
        {
            // Calculate direction to target
            Vector3 targetDirection = (currentTarget.transform.position - turretTransform.position);
            
            // Only rotate if there's a meaningful distance to the target
            if (targetDirection.magnitude > 0.1f)
            {
                targetDirection.Normalize();
                
                // Create rotation to look at target
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                
                // Smoothly rotate turret towards target
                turretTransform.rotation = Quaternion.RotateTowards(
                    turretTransform.rotation, 
                    targetRotation, 
                    TurnSpeed * 2f * Time.deltaTime // Turret rotates faster than tank body
                );
            }
            
            yield return null;
        }
        
        Debug.Log($"[TankMan] Lost target or no turret transform");
    }
      void MoveInDirection(Vector3 direction)
    {
        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) 
        {
            Debug.LogWarning($"[TankMan] MoveInDirection called but NavMeshAgent not ready. Enabled: {navAgent?.enabled}, OnNavMesh: {navAgent?.isOnNavMesh}");
            return;
        }
        
        if (direction == Vector3.zero)
        {
            // Stop smoothly
            navAgent.ResetPath();
            return;
        }
        
        // Calculate target position in the given direction
        Vector3 targetPosition = transform.position + direction.normalized * 10f; // Move 10 units in direction
        
        // Clamp target to map boundaries
        targetPosition.x = Mathf.Clamp(targetPosition.x, 30f, 770f);
        targetPosition.z = Mathf.Clamp(targetPosition.z, 30f, 770f);
        
        // Set NavMesh destination - NavMeshAgent will handle both movement and rotation
        navAgent.SetDestination(targetPosition);
    }
    
    /// <summary>
    /// Limits tank rotation to ±30 degrees on X and Z axes for natural terrain following
    /// </summary>
    void LimitTankRotation()
    {
        Vector3 eulerAngles = transform.eulerAngles;
        
        // Convert angles to -180 to 180 range for easier clamping
        float xAngle = eulerAngles.x > 180 ? eulerAngles.x - 360 : eulerAngles.x;
        float zAngle = eulerAngles.z > 180 ? eulerAngles.z - 360 : eulerAngles.z;
        
        // Clamp X and Z rotation to ±30 degrees
        float maxTilt = 30f;
        xAngle = Mathf.Clamp(xAngle, -maxTilt, maxTilt);
        zAngle = Mathf.Clamp(zAngle, -maxTilt, maxTilt);
        
        // Keep Y rotation unchanged (tank can rotate freely horizontally)
        Vector3 clampedRotation = new Vector3(xAngle, eulerAngles.y, zAngle);
        
        // Apply the clamped rotation
        transform.eulerAngles = clampedRotation;
        
        // Note: No need to dampen angular velocity since rigidbody is kinematic
    }
    
    /// <summary>
    /// Align tank to terrain by raycasting to detect ground slope and tilt accordingly
    /// </summary>
    void AlignToTerrain()
    {
        // Perform raycasts from tank to ground to detect terrain slope
        float rayDistance = 30f; // Distance to cast rays
        // Only detect layer 11 (Terrain)
        LayerMask terrainLayer = 1 << 11; // Only terrain layer
        
        // Cast rays from further out to get better slope detection
        Vector3 frontPoint = transform.position + transform.forward * 4f;
        Vector3 backPoint = transform.position - transform.forward * 4f;
        Vector3 leftPoint = transform.position - transform.right * 4f;
        Vector3 rightPoint = transform.position + transform.right * 4f;
        
        // Start rays from well above the tank
        Vector3 rayStart = Vector3.up * 15f;
        
        bool frontHit = Physics.Raycast(frontPoint + rayStart, Vector3.down, out RaycastHit frontHitInfo, rayDistance, terrainLayer);
        bool backHit = Physics.Raycast(backPoint + rayStart, Vector3.down, out RaycastHit backHitInfo, rayDistance, terrainLayer);
        bool leftHit = Physics.Raycast(leftPoint + rayStart, Vector3.down, out RaycastHit leftHitInfo, rayDistance, terrainLayer);
        bool rightHit = Physics.Raycast(rightPoint + rayStart, Vector3.down, out RaycastHit rightHitInfo, rayDistance, terrainLayer);
        
        // Visual debugging - draw the rays in Scene view
        Debug.DrawRay(frontPoint + rayStart, Vector3.down * rayDistance, frontHit ? Color.green : Color.red, 0.1f);
        Debug.DrawRay(backPoint + rayStart, Vector3.down * rayDistance, backHit ? Color.green : Color.red, 0.1f);
        Debug.DrawRay(leftPoint + rayStart, Vector3.down * rayDistance, leftHit ? Color.green : Color.red, 0.1f);
        Debug.DrawRay(rightPoint + rayStart, Vector3.down * rayDistance, rightHit ? Color.green : Color.red, 0.1f);
        
        if (frontHit && backHit && leftHit && rightHit)
        {
            // Calculate pitch (X rotation) from front-back height difference
            float frontHeight = frontHitInfo.point.y;
            float backHeight = backHitInfo.point.y;
            float heightDifference = frontHeight - backHeight;
            float pitchAngle = Mathf.Atan2(heightDifference, 8f) * Mathf.Rad2Deg; // Using 8f for distance between points
            
            // Calculate roll (Z rotation) from left-right height difference
            float leftHeight = leftHitInfo.point.y;
            float rightHeight = rightHitInfo.point.y;
            float sideDifference = rightHeight - leftHeight;
            float rollAngle = Mathf.Atan2(sideDifference, 8f) * Mathf.Rad2Deg;
            
            // Get current Y rotation - since NavAgent updateRotation is false, we need to calculate turning manually
            float yRotation = transform.eulerAngles.y;
            
            // Manual Y-axis rotation towards movement direction if NavMeshAgent is moving
            if (navAgent != null && navAgent.velocity.magnitude > 0.1f)
            {
                Vector3 moveDirection = navAgent.velocity.normalized;
                float targetYRotation = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                yRotation = Mathf.LerpAngle(yRotation, targetYRotation, Time.deltaTime * TurnSpeed * 0.1f);
            }
            
            // Limit pitch and roll to reasonable values for tank movement
            pitchAngle = Mathf.Clamp(pitchAngle, -30f, 30f);
            rollAngle = Mathf.Clamp(rollAngle, -30f, 30f);
            
            // Store the desired rotation for application in Update
            desiredRotation = Quaternion.Euler(pitchAngle, yRotation, rollAngle);
            hasValidTerrainRotation = true;
        }
        else
        {
            // If we can't detect terrain properly, just handle Y rotation for movement
            if (navAgent != null && navAgent.velocity.magnitude > 0.1f)
            {
                Vector3 moveDirection = navAgent.velocity.normalized;
                float targetYRotation = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                float currentYRotation = transform.eulerAngles.y;
                float newYRotation = Mathf.LerpAngle(currentYRotation, targetYRotation, Time.deltaTime * TurnSpeed * 0.1f);
                
                desiredRotation = Quaternion.Euler(transform.eulerAngles.x, newYRotation, transform.eulerAngles.z);
                hasValidTerrainRotation = true;
            }
        }
    }

    /// <summary>
    /// Sets a new wander target within the allowed range
    /// </summary>
    private void SetNewWanderTarget()
    {
        // Update wander origin to current tank position for free roaming
        wanderOrigin = transform.position;
        
        // Map boundaries (matching MoveInDirection clamp values)
        float minBoundary = 30f;
        float maxBoundary = 770f;
        
        Vector3 potentialTarget;
        int maxAttempts = 10; // Prevent infinite loops
        int attempts = 0;
        
        // Keep generating targets until we find one within map boundaries
        do
        {
            // Generate random point within wander range from new origin
            Vector2 randomCircle = Random.insideUnitCircle * wanderRange;
            potentialTarget = wanderOrigin + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            // Clamp target to map boundaries
            potentialTarget.x = Mathf.Clamp(potentialTarget.x, minBoundary, maxBoundary);
            potentialTarget.z = Mathf.Clamp(potentialTarget.z, minBoundary, maxBoundary);
            
            attempts++;
            
            // If we've tried many times and still getting clamped targets, 
            // generate a target closer to center of valid area
            if (attempts > 5)
            {
                // Find center of valid area relative to tank position
                float centerX = Mathf.Clamp(transform.position.x, minBoundary + 50f, maxBoundary - 50f);
                float centerZ = Mathf.Clamp(transform.position.z, minBoundary + 50f, maxBoundary - 50f);
                
                // Generate target in smaller range around the adjusted center
                Vector2 smallerCircle = Random.insideUnitCircle * Mathf.Min(wanderRange * 0.5f, 100f);
                potentialTarget = new Vector3(centerX + smallerCircle.x, wanderOrigin.y, centerZ + smallerCircle.y);
                
                // Final boundary clamp
                potentialTarget.x = Mathf.Clamp(potentialTarget.x, minBoundary, maxBoundary);
                potentialTarget.z = Mathf.Clamp(potentialTarget.z, minBoundary, maxBoundary);
                
                break;
            }
            
        } while ((potentialTarget.x <= minBoundary || potentialTarget.x >= maxBoundary || 
                  potentialTarget.z <= minBoundary || potentialTarget.z >= maxBoundary) && 
                 attempts < maxAttempts);
        
        // Check if we should prefer forward or backward movement based on tank orientation
        Vector3 tankForward = transform.forward; // Tank forward is now +Z direction
        Vector3 directionToTarget = (potentialTarget - transform.position).normalized;
        
        // Calculate dot product to determine if target is more forward or backward
        float forwardAlignment = Vector3.Dot(tankForward, directionToTarget);
        
        // If target is behind us (dot product < 0), consider generating a forward target instead
        if (forwardAlignment < -0.3f) // Allow some tolerance
        {
            // Generate a new target more in the forward direction, but keep it within boundaries
            Vector3 forwardDirection = tankForward + Random.insideUnitCircle.x * 0.5f * Vector3.forward + Random.insideUnitCircle.y * 0.5f * Vector3.back;
            forwardDirection.Normalize();
            Vector3 forwardTarget = transform.position + forwardDirection * Random.Range(wanderRange * 0.3f, wanderRange);
            
            // Clamp forward target to boundaries
            forwardTarget.x = Mathf.Clamp(forwardTarget.x, minBoundary, maxBoundary);
            forwardTarget.z = Mathf.Clamp(forwardTarget.z, minBoundary, maxBoundary);
            
            // Only use forward target if it's significantly different from original
            float distanceImprovement = Vector3.Distance(transform.position, forwardTarget) - Vector3.Distance(transform.position, potentialTarget);
            if (distanceImprovement > 10f) // Only if forward target is meaningfully better
            {
                potentialTarget = forwardTarget;
            }
        }
        
        currentWanderTarget = potentialTarget;
        isWandering = true;
    }
    
    /// <summary>
    /// Checks if we should pick a new wander target
    /// </summary>
    private bool ShouldPickNewWanderTarget()
    {
        // Always pick new target since we update origin each time - no range restrictions
        // This allows free roaming behavior
        return false; // Never force a new target based on range since origin moves with tank
    }
    
    #endregion
    
    #region Debug Visualization
    
    void OnDrawGizmos()
    {
        // Always show vision cone in editor for easier debugging
        if (visionCone > 0 && visionRange > 0)
        {
            // Get the position and rotation for vision cone (use turret if available, otherwise tank)
            Vector3 visionPosition = turretTransform != null ? turretTransform.position : transform.position;
            Vector3 visionForward = turretTransform != null ? turretTransform.forward : transform.forward;
            
            // Draw a simple cone wireframe that's always visible
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f); // Very light green
            
            float halfAngle = visionCone * 0.5f;
            float baseRadius = Mathf.Tan(halfAngle * Mathf.Deg2Rad) * visionRange;
            Vector3 baseCenter = visionPosition + visionForward * visionRange;
            
            // Draw main direction ray
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(visionPosition, visionForward * visionRange);
            
            // Draw cone edges (4 main directions)
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            Vector3 right = turretTransform != null ? turretTransform.right : transform.right;
            Vector3 up = turretTransform != null ? turretTransform.up : transform.up;
            
            // Top, bottom, left, right edges of the cone
            Vector3[] edgeDirections = {
                Quaternion.AngleAxis(halfAngle, right) * visionForward,
                Quaternion.AngleAxis(-halfAngle, right) * visionForward,
                Quaternion.AngleAxis(halfAngle, up) * visionForward,
                Quaternion.AngleAxis(-halfAngle, up) * visionForward
            };
            
            foreach (var direction in edgeDirections)
            {
                Gizmos.DrawRay(visionPosition, direction * visionRange);
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Get the position and rotation for vision cone (use turret if available, otherwise tank)
        Vector3 visionPosition = turretTransform != null ? turretTransform.position : transform.position;
        Vector3 visionForward = turretTransform != null ? turretTransform.forward : transform.forward;
        Vector3 visionRight = turretTransform != null ? turretTransform.right : transform.right;
        
        // Draw sensor range (full sphere) - always from tank center
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);
        
        // Draw vision cone from turret position
        if (visionCone > 0 && visionRange > 0)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Semi-transparent green for better visibility
            
            // Calculate cone parameters
            float halfAngle = visionCone * 0.5f;
            int segments = 20; // More segments for smoother cone
            
            // Draw cone using simpler method
            Vector3 startPos = visionPosition;
            
            // Draw the cone as lines from center to perimeter
            for (int i = 0; i <= segments; i++)
            {
                for (int j = 0; j <= segments; j++)
                {
                    float horizontalAngle = (i / (float)segments) * 360f;
                    float verticalAngle = (j / (float)segments) * halfAngle;
                    
                    // Create direction vector for this point on the cone
                    Vector3 direction = visionForward;
                    direction = Quaternion.AngleAxis(verticalAngle, visionRight) * direction;
                    direction = Quaternion.AngleAxis(horizontalAngle, visionForward) * direction;
                    
                    Vector3 conePoint = startPos + direction * visionRange;
                    
                    // Draw line from center to cone point
                    if (i % 4 == 0 && j % 4 == 0) // Draw fewer lines to avoid clutter
                    {
                        Gizmos.DrawLine(startPos, conePoint);
                    }
                }
            }
            
            // Draw cone base circle
            Gizmos.color = Color.green;
            float baseRadius = Mathf.Tan(halfAngle * Mathf.Deg2Rad) * visionRange;
            Vector3 baseCenter = startPos + visionForward * visionRange;
            
            // Draw circle at the base of the cone
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (i / (float)segments) * 360f * Mathf.Deg2Rad;
                float angle2 = ((i + 1) / (float)segments) * 360f * Mathf.Deg2Rad;
                
                Vector3 point1 = baseCenter + (Mathf.Cos(angle1) * visionRight + Mathf.Sin(angle1) * Vector3.Cross(visionForward, visionRight)) * baseRadius;
                Vector3 point2 = baseCenter + (Mathf.Cos(angle2) * visionRight + Mathf.Sin(angle2) * Vector3.Cross(visionForward, visionRight)) * baseRadius;
                
                Gizmos.DrawLine(point1, point2);
            }
            
            // Draw main direction indicator (turret forward)
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(startPos, visionForward * visionRange);
            
            // Draw vision cone info text position
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(startPos + visionForward * (visionRange * 0.1f), Vector3.one * 0.5f);
        }
        
        // Draw weapon range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
        
        // Draw current target
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
            
            // Draw target info
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(currentTarget.transform.position, Vector3.one * 2f);
        }
        
        // Draw detected enemies
        if (Application.isPlaying && detectedEnemies != null)
        {
            Gizmos.color = Color.orange;
            foreach (var enemy in detectedEnemies)
            {
                if (enemy != null)
                {
                    Gizmos.DrawWireCube(enemy.transform.position, Vector3.one * 1.5f);
                    Gizmos.DrawLine(transform.position, enemy.transform.position);
                }
            }
        }
        
        // Draw wander system visualization
        if (Application.isPlaying)
        {
            // Draw wander origin and range
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(wanderOrigin, wanderRange);
            
            // Draw current wander target if wandering
            if (isWandering)
            {
                Gizmos.color = Color.green;
                
                // Draw tall capsule for better waypoint visibility
                Vector3 bottom = currentWanderTarget - Vector3.up * 2f;
                Vector3 top = currentWanderTarget + Vector3.up * 2f;
                Gizmos.DrawWireCube(currentWanderTarget, new Vector3(wanderReachDistance * 2f, 4f, wanderReachDistance * 2f));
                Gizmos.DrawLine(bottom, top);
                
                Gizmos.DrawLine(transform.position, currentWanderTarget);
            }
        }
    }
    
    #endregion

    /// <summary>
    /// Gets the first node to execute from StartNavButton based on Y-position priority
    /// </summary>
    AiExecutableNode GetFirstNodeFromStart(AiTreeAsset tree)
    {
        // Check for both StartNavButton and StartTurretButton
        string startButtonId = tree.connections.Any(c => c.fromNodeId == "StartNavButton") ? "StartNavButton" : "StartTurretButton";
        
        // Find all connections from start button
        var startConnections = tree.connections
            .Where(c => c.fromNodeId == startButtonId)
            .Select(c => c.toNodeId)
            .ToList();

        if (startConnections.Count == 0)
        {
            // Fallback to old method if no start button connections found
            return tree.executableNodes.Find(n => n.nodeId == tree.startNodeId);
        }

        // Get connected nodes and sort by Y position (highest first)
        var connectedNodes = startConnections
            .Select(nodeId => tree.executableNodes.Find(n => n.nodeId == nodeId))
            .Where(n => n != null)
            .OrderByDescending(n => n.position.y)
            .ToList();
        return connectedNodes.FirstOrDefault();
    }

    /// <summary>
    /// Gets alternative nodes from StartNavButton when backtracking from a failed top-level node
    /// </summary>
    AiExecutableNode GetNextAlternativeFromStart(AiExecutableNode failedNode, AiTreeAsset tree)
    {
        // Check for both StartNavButton and StartTurretButton
        string startButtonId = tree.connections.Any(c => c.fromNodeId == "StartNavButton") ? "StartNavButton" : "StartTurretButton";
        
        // Find all connections from start button
        var startConnections = tree.connections
            .Where(c => c.fromNodeId == startButtonId)
            .Select(c => c.toNodeId)
            .ToList();

        // Get connected nodes and sort by Y position (highest first)
        var connectedNodes = startConnections
            .Select(nodeId => tree.executableNodes.Find(n => n.nodeId == nodeId))
            .Where(n => n != null)
            .OrderByDescending(n => n.position.y)
            .ToList();

        // Find the failed node and try the next one
        int failedIndex = connectedNodes.FindIndex(n => n.nodeId == failedNode.nodeId);
        if (failedIndex >= 0 && failedIndex + 1 < connectedNodes.Count)
        {
            var nextNode = connectedNodes[failedIndex + 1];
            return nextNode;
        }

        Debug.Log($"[TankMan] No more alternatives from {startButtonId}, restarting");
        return connectedNodes.FirstOrDefault(); // Restart from first node
    }
}
