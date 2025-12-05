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
public class TankMan : MonoBehaviour
{
    [Header("Tank Slot Data")]
    [SerializeField] private TankSlotDataJson tankSlotData;
    
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
    
    [Header("Sensor Settings")]
    [SerializeField] private string tankTag = "Tank";

    [Header("Engine Stats")]
    [Tooltip("Use these inspector values instead of loading from TankSlotData (for testing only)")]
    [SerializeField] private bool overrideWithInspectorValues = false;
    [SerializeField] private float enginePower = 15000f;         // Forward force (N)
    [SerializeField] private float turningPower = 20000f;        // Turning torque (N·m)
    [SerializeField] private float topSpeed = 15f;               // Max speed (m/s)
    [SerializeField] private float maxTurnRate = 120f;           // Max turn rate (deg/s)
    [SerializeField] private float turnRampUpTime = 1.0f;        // Turn ramp-up time (seconds)
    [SerializeField] private float turnStartPowerPercent = 0.5f; // Starting turn power (0-1)
    [SerializeField] private float dragCoefficient = 0.5f;       // Rolling resistance
    [SerializeField] private float angularDragCoefficient = 2.0f; // Turn resistance

    private Rigidbody rb;
    private float currentMoveInput = 0f;
    private float currentTurnInput = 0f;
    private bool isGrounded = false;
    
    // Turning ramp-up state
    private float currentTurningPower = 0f;
    private float turnInputStartTime = 0f;
    private float previousTurnInput = 0f;
    
    // Team-based detection support
    private TankTeamInfo myTeamInfo;
    
    [Header("Projectile Settings")]
    [SerializeField] private GameObject bulletPrefab; // Universal bullet prefab for all tanks
    private float bulletSpeed = 50f; // Speed from turret data (loaded from TankSlotData)
    
    [Header("Tank Stats - Read Only")]
    [SerializeField] private float totalWeight;
    [SerializeField] private int totalHP;
    [SerializeField] private float currentHealth;
    [SerializeField] private float armor;
    [SerializeField] private int damage;
    [SerializeField] private float range;
    [SerializeField] private float shotsPerSec;
    [SerializeField] private string knockback;
    [SerializeField] private float visionCone;
    [SerializeField] private float visionRange;
    [SerializeField] private TurretType turretType = TurretType.DirectFire;
    
    [Header("Assigned AI Components")]
    [SerializeField] private string assignedNavAIInstanceId;
    [SerializeField] private string assignedTurretAIInstanceId;
    [SerializeField] private string assignedNavAITitle; // For display purposes
    [SerializeField] private string assignedTurretAITitle; // For display purposes
    
    // Runtime AI data loaded from JSON
    private AiTreeAsset runtimeNavAI;
    private AiTreeAsset runtimeTurretAI;
    
    // Public properties for external access
    public float TotalWeight => totalWeight;
    public int TotalHP => totalHP;
    public int Damage => damage;
    public float Range => range;
    public float ShotsPerSec => shotsPerSec;
    public string Knockback => knockback;
    public float VisionCone => visionCone;
    public float VisionRange => visionRange;
    public float CurrentHealth => currentHealth;
    public float Armor => armor;
    public AiTreeAsset AssignedNavAI => runtimeNavAI;
    public AiTreeAsset AssignedTurretAI => runtimeTurretAI;


    // Physics-based movement properties (for AI reference)
    public float MoveSpeed => topSpeed; // Max speed the tank can reach
    public float TurnSpeed => maxTurnRate; // Max turn rate in deg/s
    
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
    private float wanderTimeout = 10f; // Timeout in seconds before picking new wander point
    
    void Start()
    {
        // Initialize main rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // Configure rigidbody for tank physics
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        // Allow X and Z rotation so tank follows terrain, only control Y (turning) with forces
        rb.constraints = RigidbodyConstraints.None;
        rb.centerOfMass = new Vector3(0, -0.5f, 0); // Lower center for stability
        
        Debug.Log($"{gameObject.name} Rigidbody initialized: mass={rb.mass}, drag={rb.linearDamping}, angularDrag={rb.angularDamping}");

        // Initialize team info - this is critical for enemy detection
        EnsureTeamInfoExists();
        
       
        
        // Load AI from tankSlotData for display/reference
        if (tankSlotData != null)
        {
            runtimeNavAI = LoadAIFromInstanceId(tankSlotData.navAIInstanceId);
            runtimeTurretAI = LoadAIFromInstanceId(tankSlotData.turretAIInstanceId);
            
            // Update display fields
            assignedNavAIInstanceId = tankSlotData.navAIInstanceId;
            assignedTurretAIInstanceId = tankSlotData.turretAIInstanceId;
            assignedNavAITitle = runtimeNavAI != null ? runtimeNavAI.title : "None";
            assignedTurretAITitle = runtimeTurretAI != null ? runtimeTurretAI.title : "None";
        }
        else
        {
            runtimeNavAI = null;
            runtimeTurretAI = null;
            assignedNavAIInstanceId = "";
            assignedTurretAIInstanceId = "";
            assignedNavAITitle = "None";
            assignedTurretAITitle = "None";
        }        // Initialize wander origin point
        wanderOrigin = transform.position;
        
        CalculateStats();
        currentHealth = totalHP;

        // Start AI automatically when the tank spawns
        StartAI();
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
        }
        else
        {
        }
    }
    
   
    
    void FixedUpdate()
    {
        if (rb == null)
            return;

        // Debug: Check if forces are being blocked
        if (!isGrounded)
        {
            // Still allow forces even if not grounded (for testing)
            Debug.LogWarning($"{gameObject.name}: Not grounded - but applying forces anyway for testing");
        }

        // Apply movement forces based on input
        ApplyMovement();
    }
    
    /// <summary>
    /// Applies physics-based movement using engine power and torque
    /// </summary>
    private void ApplyMovement()
    {
        // Debug: Log inputs and forces every 60 frames
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"{gameObject.name} Movement - Input: move={currentMoveInput:F2}, turn={currentTurnInput:F2} | EnginePower={enginePower}N, TurningPower={turningPower}N·m | Mass={rb.mass}kg");
        }
        
        // Forward/backward movement using engine power
        if (Mathf.Abs(currentMoveInput) > 0.01f)
        {
            float force = enginePower * currentMoveInput;
            rb.AddForce(transform.forward * force);
            
            if (Time.frameCount % 60 == 0)
                Debug.Log($"{gameObject.name} Applying FORWARD force: {force}N");
        }
        
        // Rotation using torque with gradual ramp-up
        if (Mathf.Abs(currentTurnInput) > 0.01f)
        {
            // Track when turn input starts
            if (Mathf.Abs(previousTurnInput) < 0.01f)
            {
                turnInputStartTime = Time.time;
            }
            
            // Gradually ramp up turning power
            float timeSinceTurnStart = Time.time - turnInputStartTime;
            float rampProgress = Mathf.Clamp01(timeSinceTurnStart / turnRampUpTime);
            float powerPercent = Mathf.Lerp(turnStartPowerPercent, 1.0f, rampProgress);
            
            float targetPower = turningPower * powerPercent;
            currentTurningPower = Mathf.Lerp(currentTurningPower, targetPower, Time.fixedDeltaTime * 10f);
            
            float torque = currentTurningPower * currentTurnInput;
            rb.AddTorque(Vector3.up * torque);
            
            if (Time.frameCount % 60 == 0)
                Debug.Log($"{gameObject.name} Applying TORQUE: {torque}N·m (power%={powerPercent*100:F0}%)");
        }
        else
        {
            // Reset when not turning
            currentTurningPower = 0f;
            turnInputStartTime = Time.time;
        }
        
        // Store previous turn input for next frame
        previousTurnInput = currentTurnInput;
        
        // Limit speeds to engine's mechanical limits
        if (rb.linearVelocity.magnitude > topSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * topSpeed;
        }
        
        float maxAngularSpeedRad = maxTurnRate * Mathf.Deg2Rad;
        if (rb.angularVelocity.magnitude > maxAngularSpeedRad)
        {
            rb.angularVelocity = rb.angularVelocity.normalized * maxAngularSpeedRad;
        }
    }
    
    /// <summary>
    /// Sets movement input for the tank (-1 to 1 for both move and turn)
    /// </summary>
    public void SetMovementInput(float moveInput, float turnInput)
    {
        currentMoveInput = Mathf.Clamp(moveInput, -1f, 1f);
        currentTurnInput = Mathf.Clamp(turnInput, -1f, 1f);
    }
    
    /// <summary>
    /// Stops all movement
    /// </summary>
    public void StopMovement()
    {
        currentMoveInput = 0f;
        currentTurnInput = 0f;
        
        // Apply strong damping to stop quickly
        if (rb != null)
        {
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, 0.1f);
            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, 0.1f);
        }
    }

    // deleted custom gravity stuff, we can just use regular gravity for now

    // deleted all the 

    // Clamp the root object's X and Z rotation to ±maxTilt degrees
    void ClampXZRotation(float maxTilt)
    {
        Vector3 eulerAngles = transform.eulerAngles;
        float xAngle = eulerAngles.x > 180 ? eulerAngles.x - 360 : eulerAngles.x;
        float zAngle = eulerAngles.z > 180 ? eulerAngles.z - 360 : eulerAngles.z;
        xAngle = Mathf.Clamp(xAngle, -maxTilt, maxTilt);
        zAngle = Mathf.Clamp(zAngle, -maxTilt, maxTilt);
        transform.eulerAngles = new Vector3(xAngle, eulerAngles.y, zAngle);
    }

    // Ground check using trigger collider
    private void OnTriggerEnter(Collider other)
    {
        if (other != null && other != GetComponent<Collider>())
            isGrounded = true;
    }
    private void OnTriggerExit(Collider other)
    {
        if (other != null && other != GetComponent<Collider>())
            isGrounded = false;
    }
    
    void LateUpdate()
    {
        // LateUpdate runs after all other updates, so NavMeshAgent won't override our rotation
        
    }
    
    void Update()
    {
    
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
            Debug.LogError($"{gameObject.name} CalculateStats: tankSlotData is NULL!");
            return;
        }
        
        Debug.Log($"{gameObject.name} CalculateStats called - reading from TankSlotData");
        
        // Calculate total weight from individual components
        totalWeight = tankSlotData.chassisWeight + tankSlotData.armorWeight + 
                      tankSlotData.turretWeight + tankSlotData.engineWeight;
        
        // Update tank slot data's calculated total (for consistency)
        tankSlotData.totalWeight = totalWeight;
        
        // Update Rigidbody mass and drag to match component weights
        if (rb == null)
            rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = totalWeight;
            rb.linearDamping = tankSlotData.dragCoefficient;
            rb.angularDamping = tankSlotData.angularDragCoefficient;
            
            Debug.Log($"{gameObject.name} Rigidbody updated: mass={rb.mass}kg, linearDrag={rb.linearDamping}, angularDrag={rb.angularDamping}");
        }
        
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
        
        // Get engine parameters from TankSlotData OR use inspector overrides
        if (!overrideWithInspectorValues)
        {
            // Load from JSON with fallback defaults
            enginePower = tankSlotData.engineForce > 0 ? tankSlotData.engineForce : 15000f;
            turningPower = tankSlotData.engineTorque > 0 ? tankSlotData.engineTorque : 20000f;
            topSpeed = tankSlotData.engineTopSpeed > 0 ? tankSlotData.engineTopSpeed : 15f;
            maxTurnRate = tankSlotData.engineMaxTurnRate > 0 ? tankSlotData.engineMaxTurnRate : 120f;
            turnRampUpTime = tankSlotData.engineTurnRampTime > 0 ? tankSlotData.engineTurnRampTime : 1.0f;
            turnStartPowerPercent = tankSlotData.engineTurnStartPercent > 0 ? tankSlotData.engineTurnStartPercent : 0.5f;
            dragCoefficient = tankSlotData.dragCoefficient > 0 ? tankSlotData.dragCoefficient : 0.5f;
            angularDragCoefficient = tankSlotData.angularDragCoefficient > 0 ? tankSlotData.angularDragCoefficient : 2.0f;
            
            Debug.Log($"{gameObject.name} loaded from JSON - EnginePower={enginePower}N, TurningPower={turningPower}N·m");
        }
        else
        {
            // Use inspector values (already set in serialized fields)
            Debug.Log($"{gameObject.name} USING INSPECTOR OVERRIDES - EnginePower={enginePower}N, TurningPower={turningPower}N·m");
        }
        
        // Debug log physics configuration (totalWeight was calculated earlier)
        Debug.Log($"Tank {gameObject.name} physics: EnginePower={enginePower}N, Mass={totalWeight}kg, Accel={enginePower/totalWeight:F1}m/s\u00b2");
        
        
        // Get turret stats from TankSlotData stat fields
        turretType = (TurretType)tankSlotData.turretType; // Cast from TurretTypeJson to TurretType
        damage = tankSlotData.turretDamage;
        range = tankSlotData.turretRange;
        shotsPerSec = tankSlotData.turretShotsPerSec;
        bulletSpeed = tankSlotData.turretBulletSpeed;
        knockback = tankSlotData.turretKnockback;
        visionCone = tankSlotData.turretVisionCone;
        visionRange = tankSlotData.turretVisionRange;
        
        

    }    /// <summary>
    /// Set the tank slot data reference (called by TankAssembly)
    /// </summary>
    public void SetTankSlotData(TankSlotDataJson slotData)
    {
        tankSlotData = slotData;
        
        if (tankSlotData != null)
        {
        }
        
        // Load AI from instance IDs (TankSlotDataJson uses instanceIds instead of direct references)
        if (tankSlotData != null)
        {
            runtimeNavAI = LoadAIFromInstanceId(tankSlotData.navAIInstanceId);
            runtimeTurretAI = LoadAIFromInstanceId(tankSlotData.turretAIInstanceId);
            
            // Update display fields for inspector
            assignedNavAIInstanceId = tankSlotData.navAIInstanceId;
            assignedTurretAIInstanceId = tankSlotData.turretAIInstanceId;
            assignedNavAITitle = runtimeNavAI != null ? runtimeNavAI.title : "None";
            assignedTurretAITitle = runtimeTurretAI != null ? runtimeTurretAI.title : "None";
        }
        else
        {
            runtimeNavAI = null;
            runtimeTurretAI = null;
            assignedNavAIInstanceId = "";
            assignedTurretAIInstanceId = "";
            assignedNavAITitle = "None";
            assignedTurretAITitle = "None";
        }
        
        
        CalculateStats();
        
        // Ensure team info is properly set
        EnsureTeamInfoExists();
                
    }

    /// <summary>
    /// Get the tank slot data reference (used by SimpleTeamManager for team assignment)
    /// </summary>
    public TankSlotDataJson GetTankSlotData()
    {
        return tankSlotData;
    }

    /// <summary>
    /// Load AI from instance ID (for TankSlotDataJson compatibility)
    /// </summary>
    private AiTreeAsset LoadAIFromInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
        {
            return null;
        }
            
            
        // Try to find AI in persistent data path first
        string aiTreesFolder = System.IO.Path.Combine(Application.persistentDataPath, "AiTrees");
        
        if (System.IO.Directory.Exists(aiTreesFolder))
        {
            string[] jsonFiles = System.IO.Directory.GetFiles(aiTreesFolder, "*.json", System.IO.SearchOption.AllDirectories);
            
            foreach (string filePath in jsonFiles)
            {
                try
                {
                    string jsonContent = System.IO.File.ReadAllText(filePath);
                    
                    // Create a new AiTreeAsset instance and populate it from JSON
                    var aiTreeData = ScriptableObject.CreateInstance<AiTreeAsset>();
                    JsonUtility.FromJsonOverwrite(jsonContent, aiTreeData);
                    
                    
                    if (aiTreeData != null && aiTreeData.instanceId == instanceId)
                    {
                        return aiTreeData;
                    }
                }
                catch (System.Exception)
                {
                }
            }
        }
        else
        {
        }
        
        // If not found in persistent data, try to find in Resources/ShopAI
        AiTreeAsset shopAI = LoadShopAIFromInstanceId(instanceId);
        if (shopAI != null)
        {
            return shopAI;
        }
        
        return null;
    }    /// <summary>
    /// Load AI from Resources/ShopAI folders (for enemy tanks referencing shop AI)
    /// </summary>
    private AiTreeAsset LoadShopAIFromInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
        {
            return null;
        }
        
        // Search in Resources/ShopAI/NavAI and Resources/ShopAI/TurretAI
        string[] shopAIFolders = { "ShopAI/NavAI", "ShopAI/TurretAI" };
        
        foreach (string folder in shopAIFolders)
        {
            // Load all JSON files from the Resources folder
            TextAsset[] jsonFiles = Resources.LoadAll<TextAsset>(folder);
            
            foreach (TextAsset jsonFile in jsonFiles)
            {
                try
                {
                    // Create a new AiTreeAsset instance and populate it from JSON
                    var aiTreeData = ScriptableObject.CreateInstance<AiTreeAsset>();
                    JsonUtility.FromJsonOverwrite(jsonFile.text, aiTreeData);
                    
                    // Check if this AI matches the instanceId we're looking for
                    if (aiTreeData != null && aiTreeData.instanceId == instanceId)
                    {
                        return aiTreeData;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[LoadShopAIFromInstanceId] Failed to parse shop AI file {jsonFile.name}: {ex.Message}");
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
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
        

        Debug.Log($"[TankMan] AI started for {gameObject.name}");
        StopAI();
        
        if (enableNavAI && runtimeNavAI != null)
        {
            navAiCoroutine = StartCoroutine(ExecuteNavAI());
        }
        else
        {
        }
        
        if (enableTurretAI && runtimeTurretAI != null)
        {
            turretAiCoroutine = StartCoroutine(ExecuteTurretAI());
        }
        else
        {
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
        var navAiTree = runtimeNavAI; // Use the loaded AI asset instead of direct property access
        if (navAiTree == null || string.IsNullOrEmpty(navAiTree.startNodeId))
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
        var turretAiTree = runtimeTurretAI; // Use the loaded AI asset instead of direct property access
        if (turretAiTree == null || string.IsNullOrEmpty(turretAiTree.startNodeId))
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
            // SubAI support removed
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
                        return fireNode;
                    }
                    else
                    {
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
        
        // Debug log every 2 seconds to avoid spam
        bool shouldDebug = Time.time % 2.0f < 0.1f;
        
        if (shouldDebug)
        {
        }
        
        // Detect enemies and allies in range
        Collider[] detected = Physics.OverlapSphere(transform.position, visionRange);
        
        if (shouldDebug)
        {
        }
        
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
            
            if (shouldDebug && (isEnemy || isAlly))
            {
            }
            
            // Check if object is within vision cone (use turret direction if available)
            Vector3 visionPosition = turretTransform != null ? turretTransform.position : transform.position;
            Vector3 visionForward = turretTransform != null ? turretTransform.forward : transform.forward;
            
            Vector3 directionToTarget = (collider.transform.position - visionPosition).normalized;
            float angleToTarget = Vector3.Angle(visionForward, directionToTarget);
            bool inVisionCone = angleToTarget <= visionCone * 0.5f; // visionCone is full angle, so half for each side
            
            if (shouldDebug && (isEnemy || isAlly))
            {
            }
            
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
                    if (shouldDebug)
                    {
                    }
                }
                else if (enemyTankMan != null)
                {
                    // Debug log for dead tank detection (optional)
                    if (shouldDebug)
                    {
                    }
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
                
            if (shouldDebug)
            {
            }
        }
        else
        {
            currentTarget = null;
            if (shouldDebug)
            {
            }
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
        // Only log when a new action is selected (not on repeated calls)
        if (currentActionNode == null || currentActionNode.methodName != actionNode.methodName)
        {
            Debug.Log($"[TankMan] New action selected: {actionNode.methodName}");
        }
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
                StopMovement();
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

    
    #endregion
    
    #region Combat System
    
    bool CanFire()
    {
        
        if (currentTarget == null)
        {
            return false;
        }
        float timeSinceLastFire = Time.time - lastFireTime;
        float fireRate = 1f / shotsPerSec;
        if (timeSinceLastFire < fireRate)
        {
            return false;
        }
        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (distanceToTarget > range)
        {
            return false;
        }
        if (turretTransform != null)
        {
            Vector3 turretForward = turretTransform.forward;
            Vector3 directionToTarget = (currentTarget.transform.position - turretTransform.position).normalized;
            float angleToTarget = Vector3.Angle(turretForward, directionToTarget);
            if (angleToTarget > 2f)
            {
                return false;
            }
        }
        else
        {
        }
        return true;
    }    void Fire()
    {
        if (currentTarget == null)
        {
            return;
        }
        
        if (firePoint == null)
        {
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
            }
            else
            {
            }
        }
        
    }
    
    /// <summary>
    /// Debug method to test firing manually (can be called from inspector buttons)
    /// </summary>
    [ContextMenu("Test Fire")]
    public void TestFire()
    {
        
        if (currentTarget == null)
        {
            
            // Find any other tank as target for testing
            TankMan[] allTanks = FindObjectsByType<TankMan>(FindObjectsSortMode.None);
            foreach (var tank in allTanks)
            {
                if (tank != this && tank.CurrentHealth > 0)
                {
                    currentTarget = tank.gameObject;
                    break;
                }
            }
        }
        
        if (currentTarget != null)
        {
            Fire();
        }
        else
        {
        }
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
        // For maximum range with given velocity: angle = 45Â°
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
            // Target too far - use 45Â° for maximum distance
            launchAngle = 45f;
        }
        
        // Return horizontal direction (angle will be applied to this)
        return horizontalDisplacement.normalized;
    }
    
    public void TakeDamage(float damageAmount)
    {
        
        // Apply armor reduction
        float finalDamage = Mathf.Max(0, damageAmount - armor);
        currentHealth -= finalDamage;
        
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }    void Die()
    {
        StopAI();
       
        // Disable the tank (but keep it for visual reference)
        // You could add explosion effects, disable colliders, etc. here
        enabled = false;
        
        // TODO: Add death effects, particle systems, sound, etc.
    }
    
    #endregion
    
    #region Movement Actions

    // --- Tank Navigation State Functions ---
    // These are called by nav actions (wander, chase, flee, move, wait) to control movement based on waypoint/target

    private enum NavStateType { None, Wait, MoveToWaypoint }
    private enum NavMoveSubState { None, Forward, ForwardTurn, Turn, BrakeTurn }
    private NavStateType lastNavState = NavStateType.None;
    private NavMoveSubState lastMoveSubState = NavMoveSubState.None;

    // Helper to log nav state transitions only when changed
    private void LogNavState(NavStateType newState)
    {
        if (lastNavState != newState)
        {
            Debug.Log($"[TankMan] {gameObject.name} NavState: {newState}");
            lastNavState = newState;
        }
    }

    // Helper to log sub-state transitions for MoveToWaypoint only when changed
    private void LogMoveSubState(NavMoveSubState newSubState)
    {
        if (lastMoveSubState != newSubState)
        {
            Debug.Log($"[TankMan] {gameObject.name} MoveToWaypoint: {newSubState}");
            lastMoveSubState = newSubState;
        }
    }

    void NavState_Wait()
    {
        LogNavState(NavStateType.Wait);
        StopMovement();
    }

    // Move toward a waypoint using simplified input system
    void NavState_MoveToWaypoint(Vector3 waypoint)
    {
        LogNavState(NavStateType.MoveToWaypoint);
        Vector3 toWaypoint = waypoint - transform.position;
        toWaypoint.y = 0f;
        float distance = toWaypoint.magnitude;
        
        if (distance < 0.1f) 
        { 
            LogMoveSubState(NavMoveSubState.BrakeTurn); 
            StopMovement();
            return; 
        }

        Vector3 forward = transform.forward;
        float angle = Vector3.SignedAngle(forward, toWaypoint.normalized, Vector3.up);
        float absAngle = Mathf.Abs(angle);

        if (absAngle < 3f) // Within 3 degrees - forward only
        {
            LogMoveSubState(NavMoveSubState.Forward);
            SetMovementInput(1f, 0f);
        }
        else if (absAngle < 30f) // Between 3 and 30 degrees - forward + turn
        {
            LogMoveSubState(NavMoveSubState.ForwardTurn);
            float turnDirection = Mathf.Sign(angle);
            float turnIntensity = Mathf.Clamp01(absAngle / 30f); // Scale turn intensity from 0 to 1 over the range
            SetMovementInput(1f, turnDirection * turnIntensity);
        }
        else // Beyond 30 degrees - turn only
        {
            LogMoveSubState(NavMoveSubState.Turn);
            float turnDirection = Mathf.Sign(angle);
            float turnIntensity = Mathf.Clamp01(absAngle / 90f); // Stronger turn for larger angles
            SetMovementInput(0f, turnDirection * turnIntensity);
        }
    }


    // --- Movement coroutines ---
    IEnumerator WanderAction()
    {
        
        // Wait for rigidbody to be ready
        float waitStartTime = Time.time;
        while (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            yield return new WaitForSeconds(0.1f);
            if (Time.time - waitStartTime > 2f)
                yield break;
        }

        // Pick a new wander target if needed
        if (!isWandering || ShouldPickNewWanderTarget())
        {
            SetNewWanderTarget();
            wanderStartTime = Time.time;
        }

        // Move towards wander target using force-driven system
        while (true)
        {
            if (!isGrounded)
            {
                yield return null;
                continue;
            }

            float distance = (currentWanderTarget - transform.position).magnitude;
            if (distance < wanderReachDistance)
                break;

            // Use new nav state logic for wandering
            NavState_MoveToWaypoint(currentWanderTarget);

            if (Time.time - wanderStartTime > wanderTimeout)
            {
                SetNewWanderTarget();
                wanderStartTime = Time.time;
            }

            yield return new WaitForFixedUpdate();
        }

        // Mark as no longer wandering so a new target will be picked next time
        isWandering = false;

        

        // Wait briefly at the destination
        yield return new WaitForSeconds(1f);
    }
    
    IEnumerator MoveToTarget()
    {
        // Wait for rigidbody to be ready
        float waitStartTime = Time.time;
        while (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            yield return new WaitForSeconds(0.1f);
            if (Time.time - waitStartTime > 2f)
                yield break;
        }

        while (currentTarget != null)
        {
            if (!isGrounded)
            {
                yield return null;
                continue;
            }

            // Clamp target position to map boundaries
            Vector3 targetPosition = currentTarget.transform.position;
            targetPosition.x = Mathf.Clamp(targetPosition.x, 30f, 770f);
            targetPosition.z = Mathf.Clamp(targetPosition.z, 30f, 770f);

            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance <= 5f)
            {
                // Apply brakes to stop at destination
                float stopTime = 0.5f;
                float stopStart = Time.time;
                while (Time.time - stopStart < stopTime)
                {
                    // Stop movement for smooth braking
                    StopMovement();
                    yield return new WaitForFixedUpdate();
                }
                break;
            }

            // Rotate towards target using input system
            float targetY = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float angleDiff = Mathf.DeltaAngle(transform.eulerAngles.y, targetY);

            if (Mathf.Abs(angleDiff) > 5f)
            {
                float turnDir = Mathf.Sign(angleDiff);
                float turnIntensity = Mathf.Clamp01(Mathf.Abs(angleDiff) / 45f);
                // Turn in place
                SetMovementInput(0f, turnDir * turnIntensity);
            }
            else
            {
                // Facing target, move forward
                SetMovementInput(1f, 0f);
            }

            yield return new WaitForFixedUpdate();
        }
    }
    
    IEnumerator ChaseTarget()
    {
        // Wait for rigidbody to be ready
        float waitStartTime = Time.time;
        while (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            yield return new WaitForSeconds(0.1f);
            if (Time.time - waitStartTime > 2f)
                yield break;
        }

        while (currentTarget != null)
        {
            if (!isGrounded)
            {
                yield return null;
                continue;
            }

            // Clamp target position to map boundaries
            Vector3 targetPosition = currentTarget.transform.position;
            targetPosition.x = Mathf.Clamp(targetPosition.x, 30f, 770f);
            targetPosition.z = Mathf.Clamp(targetPosition.z, 30f, 770f);

            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance <= range * 0.8f)
            {
                // Apply brakes to stop at destination
                float stopTime = 0.5f;
                float stopStart = Time.time;
                while (Time.time - stopStart < stopTime)
                {
                    StopMovement();
                    yield return new WaitForFixedUpdate();
                }
                break;
            }

            // Rotate towards target using input system
            float targetY = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float angleDiff = Mathf.DeltaAngle(transform.eulerAngles.y, targetY);

            if (Mathf.Abs(angleDiff) > 5f)
            {
                float turnDir = Mathf.Sign(angleDiff);
                float turnIntensity = Mathf.Clamp01(Mathf.Abs(angleDiff) / 45f);
                // Turn in place
                SetMovementInput(0f, turnDir * turnIntensity);
            }
            else
            {
                // Facing target, move forward
                SetMovementInput(1f, 0f);
            }

            yield return new WaitForFixedUpdate();
        }
    }
    
    IEnumerator FleeFromTarget()
    {
        // Wait for rigidbody to be ready
        float waitStartTime = Time.time;
        while (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            yield return new WaitForSeconds(0.1f);
            if (Time.time - waitStartTime > 2f)
                yield break;
        }

        while (currentTarget != null)
        {
            if (!isGrounded)
            {
                yield return null;
                continue;
            }

            // Calculate flee direction (away from target)
            Vector3 fleeDirection = (transform.position - currentTarget.transform.position).normalized;
            Vector3 fleeTarget = transform.position + fleeDirection * Mathf.Max(range * 1.5f, 50f);

            // Clamp to map boundaries
            fleeTarget.x = Mathf.Clamp(fleeTarget.x, 30f, 770f);
            fleeTarget.z = Mathf.Clamp(fleeTarget.z, 30f, 770f);

            Vector3 toFlee = fleeTarget - transform.position;
            toFlee.y = 0f;
            float distance = Vector3.Distance(transform.position, currentTarget.transform.position);

            if (distance >= range * 2f)
            {
                // Apply brakes to stop at destination
                float stopTime = 0.5f;
                float stopStart = Time.time;
                while (Time.time - stopStart < stopTime)
                {
                    StopMovement();
                    yield return new WaitForFixedUpdate();
                }
                break;
            }

            // Rotate away from target (flee direction) using input system
            float targetY = Mathf.Atan2(toFlee.x, toFlee.z) * Mathf.Rad2Deg;
            float angleDiff = Mathf.DeltaAngle(transform.eulerAngles.y, targetY);

            if (Mathf.Abs(angleDiff) > 5f)
            {
                float turnDir = Mathf.Sign(angleDiff);
                float turnIntensity = Mathf.Clamp01(Mathf.Abs(angleDiff) / 45f);
                // Turn in place
                SetMovementInput(0f, turnDir * turnIntensity);
            }
            else
            {
                // Facing flee direction, move forward
                SetMovementInput(1f, 0f);
            }

            yield return new WaitForFixedUpdate();
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
    // Draws a gizmo in the Scene view to visualize the tank's current waypoint (wander or target)
    void OnDrawGizmosSelected()
    {
        // Draw wander target if wandering
        if (isWandering)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentWanderTarget, 2f);
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawSphere(currentWanderTarget, 0.5f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentWanderTarget);
        }
        // Draw target position if moving to a target
        else if (currentTarget != null)
        {
            Vector3 targetPos = currentTarget.transform.position;
            // Clamp to map boundaries for visualization
            targetPos.x = Mathf.Clamp(targetPos.x, 30f, 770f);
            targetPos.z = Mathf.Clamp(targetPos.z, 30f, 770f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPos, 2f);
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawSphere(targetPos, 0.5f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetPos);
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

        // // // // Get connected nodes and sort by Y position (highest first)
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

        return connectedNodes.FirstOrDefault(); // Restart from first node
    }
    
    IEnumerator WaitAction()
    {
        // Apply both brakes for 0.2 seconds (tank sits still, turret can still track)
        float waitDuration = 0.2f;
        float startTime = Time.time;
        while (Time.time - startTime < waitDuration)
        {
            NavState_Wait();
            yield return new WaitForFixedUpdate();
        }
    }

    IEnumerator TrackTargetAction()
    {
        // Continuously rotate turret to face the current target
        while (currentTarget != null && turretTransform != null)
        {
            Vector3 targetDirection = currentTarget.transform.position - turretTransform.position;
            if (targetDirection.magnitude > 0.1f)
            {
                targetDirection.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                turretTransform.rotation = Quaternion.RotateTowards(
                    turretTransform.rotation,
                    targetRotation,
                    TurnSpeed * 2f * Time.deltaTime // Turret rotates faster than tank body
                );
            }
            yield return null;
        }
    }
}
