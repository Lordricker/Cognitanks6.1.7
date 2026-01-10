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
    [SerializeField] private Transform firePoint1;
    
    [Header("Hammer Animation")]
    [SerializeField] private GameObject hammerDownPrefab; // Animation prefab assigned by TankAssembly
    private GameObject hammerDownInstance; // Pre-instantiated hammer down model
    private Coroutine hammerSwingCoroutine;
    
    [Header("Sensor Settings")]
    [SerializeField] private string tankTag = "Tank";

    [Header("Engine Stats")]
    [Tooltip("Use these inspector values instead of loading from TankSlotData (for testing only)")]
    [SerializeField] private bool overrideWithInspectorValues = false;
    [SerializeField] private float enginePower = 15000f;         // Forward force (N)
    [SerializeField] private float turningPower = 20000f;        // Turning torque (N·m)
    [SerializeField] private float topSpeed = 15f;               // Max speed (m/s)
    [SerializeField] private float maxTurnRate = 70f;           // Max turn rate (deg/s)
    [SerializeField] private float turnRampUpTime = 1.0f;        // Turn ramp-up time (seconds)
    [SerializeField] private float turnStartPowerPercent = 0.5f; // Starting turn power (0-1)
    [SerializeField] private float dragCoefficient = 0.5f;       // Rolling resistance
    [SerializeField] private float angularDragCoefficient = 2.0f; // Turn resistance
    
    [Header("Airborne Physics")]
    [SerializeField] private float airborneGravityMultiplier = 10f; // Extra downward force when airborne (multiplier of Physics.gravity)

    [Header("Turret Rotation")]
    [SerializeField] private float turretRotationSpeed = 1.5f; // Multiplier for turret rotation speed (relative to tank turn speed)
    [SerializeField] private float turretRampUpTime = 0.3f; // Time to ramp up to full turret rotation speed

    private Rigidbody rb;
    private float currentMoveInput = 0f;
    private float currentTurnInput = 0f;
    private bool isGrounded = false;
    
    // Turning ramp-up state (for tank body rotation)
    private float currentTurningPower = 0f;
    private float turnInputStartTime = 0f;
    private float previousTurnInput = 0f;
    
    // Turret rotation ramp-up state
    private float currentTurretRotationSpeed = 0f;
    private float turretRotationStartTime = 0f;
    private Quaternion previousTurretRotation = Quaternion.identity;
    
    // Team-based detection support
    private TankTeamInfo myTeamInfo;
    
    [Header("Projectile Settings")]
    [SerializeField] private GameObject bulletPrefab; // Universal bullet prefab for all tanks
    private float bulletSpeed = 50f; // Speed from turret data (loaded from TankSlotData)
    
    [Header("Death Effects")]
    [SerializeField] private GameObject deathExplosionPrefab; // Fire explosion effect for tank death
    
    [Header("Tank Driving Sound")]
    private AudioSource tankDrivingAudioSource;
    private bool isTankMoving = false;
    private float drivingSoundFadeDuration = 0.3f;
    
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
    public TurretType TurretType => turretType;
    public AiTreeAsset AssignedNavAI => runtimeNavAI;
    public AiTreeAsset AssignedTurretAI => runtimeTurretAI;
    public Rigidbody Rb => rb;

    public float ParseKnockback()
    {
        switch (knockback)
        {
            case "Low": return 1000f;
            case "Medium": return 4000f;
            case "High": return 12000f;
            default: return 1000f; // Default knockback force
        }
    }


    // Physics-based movement properties (for AI reference)
    // Apply Coms penalty if currently using Coms intel (simulates distracted driving)
    public float MoveSpeed => isCurrentlyUsingComs ? topSpeed * COMS_SPEED_PENALTY : topSpeed;
    public float TurnSpeed => isCurrentlyUsingComs ? maxTurnRate * COMS_SPEED_PENALTY : maxTurnRate;
    
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
    private AiExecutableNode currentNavActionNode; // Current action for Nav AI
    private AiExecutableNode currentTurretActionNode; // Current action for Turret AI
    private Coroutine navAiCoroutine;
    private Coroutine turretAiCoroutine;
    private Coroutine currentNavActionCoroutine; // Coroutine for Nav AI actions
    private Coroutine currentTurretActionCoroutine; // Coroutine for Turret AI actions
    
    // Track last executed chain to prevent duplicate logs
    private string lastLoggedNavChain = "";
    private string lastLoggedTurretChain = "";
    
    // Cycle node memory - tracks which child node each Cycle instance is currently executing
    private Dictionary<string, int> cycleNodeMemory = new Dictionary<string, int>();
    private Dictionary<string, float> cycleNodeTimeSpent = new Dictionary<string, float>();
    private Dictionary<string, float> cycleNodeLastUpdateTime = new Dictionary<string, float>();
    
    // Sensor data
    private GameObject currentTarget; // Target for actions (flee, chase, fire, etc.)
    private GameObject evaluationTarget; // Target for condition evaluation (HP, Armor checks)
    private List<GameObject> detectedEnemies = new List<GameObject>();
    private List<GameObject> previousDetectedEnemies = new List<GameObject>(); // Track previous frame's enemies for AllyTargetList cleanup
    private List<GameObject> detectedAllies = new List<GameObject>();
    private GameObject comsTarget; // Target acquired via Coms (AllyTargetList) when on a Coms branch
    private float lastFireTime;
    private float currentLeadDistance = 0f; // Track the lead distance currently being used by tracking actions
    
    // Coms penalty tracking
    private bool isCurrentlyUsingComs = false; // True when the current AI iteration is using Coms intel
    private const float COMS_SPEED_PENALTY = 0.5f; // 50% speed reduction when using Coms
    private const float COMS_ALLY_DELAY_PER_TANK = 0.1f; // Additional AI delay per ally
    
    // Private Tag System: targetInstanceId -> tagValue
    // Personal tags that only this tank can access
    private Dictionary<int, int> privateTagList = new Dictionary<int, int>();
    
    // Knockback state
    private Collider[] wheelColliders;
    private bool frictionReduced = false;
    private float frictionRestoreTime = 0f;
    private Vector3 lastBulletVelocity = Vector3.zero;
    private float lastBulletKnockback = 0f;
    
    // Rotation action state tracking - track the actual target angle to persist across AI iterations
    private string lastUsedNavNodeId = "";
    private string lastUsedTurretNodeId = "";
    private float storedRotationTarget = 0f;
    
    // Debug tracking
    private Quaternion lastTurretRotation = Quaternion.identity; // For turret rotation speed debug
    private float lastDebugLogTime = 0f; // Track time for debug interval
    
    // Wander State Management
    private Vector3 currentWanderTarget;
    private bool isWandering = false;
    private Vector3 wanderOrigin; // Reference point for wander range checking
    private float wanderStartTime; // Track when we started moving to current wander target
    private float wanderTimeout = 10f; // Timeout in seconds before picking new wander point
    
    // Spawn/Home tracking
    private Vector3 spawnPosition; // Store spawn position for Home action
    
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

        // Initialize team info - this is critical for enemy detection
        EnsureTeamInfoExists();
        
        // Find wheel colliders for knockback friction control
        Transform wheelContainer = transform.Find("WheelColliders");
        if (wheelContainer != null)
        {
            wheelColliders = wheelContainer.GetComponentsInChildren<Collider>();
        }
       
        
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
        
        // Store spawn position for Home action
        spawnPosition = transform.position;
        
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

        // Apply extra gravity when airborne to make tanks fall faster
        if (!isGrounded)
        {
            rb.AddForce(Physics.gravity * (airborneGravityMultiplier - 1f) * rb.mass, ForceMode.Force);
        }

        // Apply movement forces based on input
        ApplyMovement();
    }
    
    /// <summary>
    /// Applies physics-based movement using engine power and torque
    /// Only applies forces when grounded - airborne tanks "ragdoll" under physics
    /// </summary>
    private void ApplyMovement()
    {
        // Only apply movement forces when grounded - otherwise let physics take over (ragdoll effect)
        if (!isGrounded)
        {
            // While airborne, reset turning power and inputs so tank doesn't continue driving on landing
            currentTurningPower = 0f;
            turnInputStartTime = Time.time;
            currentMoveInput = 0f;
            currentTurnInput = 0f;
            return;
        }
        
        // Forward/backward movement using engine power (EnginePower drives the tank forward)
        if (Mathf.Abs(currentMoveInput) > 0.01f)
        {
            float force = enginePower * currentMoveInput;
            rb.AddForce(transform.forward * force);
        }
        
        // Rotation using torque with gradual ramp-up (TurningPower rotates the tank body)
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
        }
        else
        {
            // Reset when not turning
            currentTurningPower = 0f;
            turnInputStartTime = Time.time;
        }
        
        // Store previous turn input for next frame
        previousTurnInput = currentTurnInput;
        
        // Handle tank driving sound based on movement
        bool isCurrentlyMoving = Mathf.Abs(currentMoveInput) > 0.1f || Mathf.Abs(currentTurnInput) > 0.1f;
        if (isCurrentlyMoving && !isTankMoving)
        {
            StartTankDrivingSound();
        }
        else if (!isCurrentlyMoving && isTankMoving)
        {
            StopTankDrivingSound();
        }
        isTankMoving = isCurrentlyMoving;
        
        // Aggressively limit speeds to engine's mechanical limits
        // Note: Speed limits are reduced by 50% when using IfComs (see MoveSpeed/TurnSpeed properties)
        float currentSpeed = rb.linearVelocity.magnitude;
        if (currentSpeed > MoveSpeed)
        {
            // Clamp more aggressively to counteract physics solver overshooting
            rb.linearVelocity = rb.linearVelocity.normalized * Mathf.Min(currentSpeed * 0.95f, MoveSpeed);
        }
        
        float maxAngularSpeedRad = TurnSpeed * Mathf.Deg2Rad;
        float currentAngularSpeed = rb.angularVelocity.magnitude;
        if (currentAngularSpeed > maxAngularSpeedRad)
        {
            // Clamp more aggressively to counteract physics solver overshooting
            rb.angularVelocity = rb.angularVelocity.normalized * Mathf.Min(currentAngularSpeed * 0.95f, maxAngularSpeedRad);
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
    
    /// <summary>
    /// Directly translates the tank upward to unstuck it from friction
    /// Only called from movement actions, so no need to check if trying to move
    /// Just checks if tank is not moving (stuck)
    /// </summary>
    private void UnstuckTank()
    {
        // Don't require isGrounded - tanks can be stuck while technically flagged as airborne
        if (rb != null)
        {
            // Check if tank is stuck (no horizontal movement)
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            if (horizontalVelocity.magnitude < 0.1f)
            {
                // Apply strong upward force to lift tank out of stuck position
                float upwardForce = rb.mass * 20f; // Strong impulse to lift tank
                rb.AddForce(Vector3.up * upwardForce, ForceMode.Impulse);
                Debug.Log($"[{gameObject.name}] UnstuckTank: Applied upward force {upwardForce:F0}N (velocity was {horizontalVelocity.magnitude:F3})");
            }
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
        // Skip bullet triggers - they shouldn't affect ground detection
        if (other.GetComponent<BulletScript>() != null)
        {
            // Handle bullet pre-knockback friction reduction
            if (wheelColliders != null && wheelColliders.Length > 0)
            {
                Rigidbody bulletRb = other.GetComponent<Rigidbody>();
                if (bulletRb != null)
                {
                    lastBulletVelocity = bulletRb.linearVelocity;
                    lastBulletKnockback = other.GetComponent<BulletScript>().GetKnockbackValue();
                }
                
                // Reduce friction immediately before the bullet's main collision hits
                ReduceWheelFriction();
                
                // Set restore time for 0.1 seconds from now
                frictionRestoreTime = Time.time + 0.1f;
            }
            return; // Don't affect ground state
        }
        
        // Check for ground detection (non-bullet objects)
        if (other != null && other != GetComponent<Collider>())
            isGrounded = true;
    }
    private void OnTriggerExit(Collider other)
    {
        // Skip bullet triggers - they shouldn't affect ground detection
        if (other.GetComponent<BulletScript>() != null)
            return;
            
        if (other != null && other != GetComponent<Collider>())
            isGrounded = false;
    }
    
    void LateUpdate()
    {
        // LateUpdate runs after all other updates, so NavMeshAgent won't override our rotation
        
        // Lock turret to tank body tilt - turret is mechanically fixed to the tank chassis
        // Only allow Y-axis (horizontal rotation) and X-axis (pitch) freedom for aiming
        // Z-axis (roll) must match the tank root to tilt with terrain
        if (turretTransform != null)
        {
            // Work with local rotation to preserve AI-controlled Y and X rotations
            Vector3 turretLocalEuler = turretTransform.localEulerAngles;
            // Z should always be 0 in local space (no roll relative to tank body)
            turretTransform.localEulerAngles = new Vector3(turretLocalEuler.x, turretLocalEuler.y, 0f);
        }
    }
    
    void Update()
    {
        // Check if we need to restore wheel friction after knockback
        if (frictionReduced && Time.time >= frictionRestoreTime)
        {
            RestoreWheelFriction();
        }
        
        // Debug logging for velocity and turret rotation (every 1 second)
        if (Time.time - lastDebugLogTime >= 1.0f)
        {
            float elapsedTime = Time.time - lastDebugLogTime;
            
            if (rb != null)
            {
                float currentVelocity = rb.linearVelocity.magnitude;
            }
            
            if (turretTransform != null && lastTurretRotation != Quaternion.identity)
            {
                float angleDiff = Quaternion.Angle(lastTurretRotation, turretTransform.rotation);
                float anglesPerSecond = angleDiff / elapsedTime; // Use actual elapsed time, not frame deltaTime
            }
            
            lastTurretRotation = turretTransform != null ? turretTransform.rotation : Quaternion.identity;
            lastDebugLogTime = Time.time;
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
        
        // Calculate total weight from individual components
        totalWeight = tankSlotData.armorWeight + 
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
            maxTurnRate = tankSlotData.engineMaxTurnRate > 0 ? tankSlotData.engineMaxTurnRate : 70f;
            turnRampUpTime = tankSlotData.engineTurnRampTime > 0 ? tankSlotData.engineTurnRampTime : 1.0f;
            turnStartPowerPercent = tankSlotData.engineTurnStartPercent > 0 ? tankSlotData.engineTurnStartPercent : 0.5f;
            dragCoefficient = tankSlotData.dragCoefficient > 0 ? tankSlotData.dragCoefficient : 0.5f;
            angularDragCoefficient = tankSlotData.angularDragCoefficient > 0 ? tankSlotData.angularDragCoefficient : 2.0f;
        }
        else
        {
            // Use inspector values (already set in serialized fields)
        }
        
        
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
    /// Load AI from Resources/ShopAI folder (for enemy tanks referencing shop AI)
    /// Searches the entire ShopAI folder for all available AI files
    /// </summary>
    private AiTreeAsset LoadShopAIFromInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
        {
            return null;
        }
        
        // Load all JSON files from the Resources/ShopAI folder (includes all AI, even ones player can't buy yet)
        TextAsset[] jsonFiles = Resources.LoadAll<TextAsset>("ShopAI");
        
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
            catch (System.Exception)
            {
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
        
        // Also look for firePoint1 if it exists (for double-barrel shotguns)
        Transform firePoint1Transform = turret.Find("FirePoint1");
        if (firePoint1Transform != null)
        {
            firePoint1 = firePoint1Transform;
        }
    }
    
    /// <summary>
    /// Set the bullet prefab reference (called by TankAssembly)
    /// </summary>
    public void SetBulletPrefab(GameObject prefab)
    {
        bulletPrefab = prefab;
    }
    
    /// <summary>
    /// Set the hammer animation prefab (called by TankAssembly during assembly)
    /// Pre-instantiates the hammer down model for performance
    /// </summary>
    public void SetHammerAnimationPrefab(GameObject prefab, Color turretColor)
    {
        hammerDownPrefab = prefab;
        if (prefab != null && turretTransform != null)
        {
            // Pre-instantiate hammer down model as inactive
            hammerDownInstance = Instantiate(prefab, turretTransform.position, turretTransform.rotation, turretTransform.parent);
            hammerDownInstance.transform.localScale = turretTransform.localScale;
            hammerDownInstance.SetActive(false);
            
            // Apply the same color as the main turret
            ApplyColorToModel(hammerDownInstance, turretColor);
            
            Debug.Log($"[TankMan] Pre-instantiated hammer animation prefab: {prefab.name} with color: {turretColor}");
        }
    }
    
    /// <summary>
    /// Apply color to all renderers in a model (matches TankAssembly implementation)
    /// </summary>
    private void ApplyColorToModel(GameObject model, Color color)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            foreach (var mat in renderer.materials)
            {
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                else if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);
            }
        }
    }
    
    /// <summary>
    /// Set the death explosion prefab reference (called by TankAssembly)
    /// </summary>
    public void SetDeathExplosionPrefab(GameObject prefab)
    {
        deathExplosionPrefab = prefab;
    }

    private void StartTankDrivingSound()
    {
        if (SoundManager.Instance == null || SoundManager.Instance.tankDrivingSound == null) return;
        
        if (tankDrivingAudioSource == null)
        {
            tankDrivingAudioSource = gameObject.AddComponent<AudioSource>();
            tankDrivingAudioSource.clip = SoundManager.Instance.tankDrivingSound;
            tankDrivingAudioSource.loop = true;
            tankDrivingAudioSource.playOnAwake = false;
            tankDrivingAudioSource.volume = 0f;
            tankDrivingAudioSource.spatialBlend = 1f; // 3D audio
        }
        
        tankDrivingAudioSource.Play();
        StartCoroutine(FadeTankDrivingSound(SoundManager.Instance.masterVolume * SoundManager.Instance.sfxVolume * SoundManager.Instance.tankDrivingVolume));
    }

    private void StopTankDrivingSound()
    {
        if (tankDrivingAudioSource != null && tankDrivingAudioSource.isPlaying)
        {
            StartCoroutine(FadeTankDrivingSoundOut());
        }
    }

    private IEnumerator FadeTankDrivingSound(float targetVolume)
    {
        float startVolume = tankDrivingAudioSource.volume;
        float elapsed = 0f;
        while (elapsed < drivingSoundFadeDuration)
        {
            elapsed += Time.deltaTime;
            tankDrivingAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / drivingSoundFadeDuration);
            yield return null;
        }
        tankDrivingAudioSource.volume = targetVolume;
    }

    private IEnumerator FadeTankDrivingSoundOut()
    {
        float startVolume = tankDrivingAudioSource.volume;
        float elapsed = 0f;
        while (elapsed < drivingSoundFadeDuration)
        {
            elapsed += Time.deltaTime;
            tankDrivingAudioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / drivingSoundFadeDuration);
            yield return null;
        }
        tankDrivingAudioSource.volume = 0f;
        tankDrivingAudioSource.Stop();
    }

    #endregion
    
    #region AI System
      public void StartAI()
    {
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
        
        if (currentNavActionCoroutine != null)
        {
            StopCoroutine(currentNavActionCoroutine);
            currentNavActionCoroutine = null;
        }
        
        if (currentTurretActionCoroutine != null)
        {
            StopCoroutine(currentTurretActionCoroutine);
            currentTurretActionCoroutine = null;
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
            // Calculate AI update delay based on Coms usage and ally count
            float currentUpdateInterval = CalculateAIUpdateInterval();
            yield return new WaitForSeconds(currentUpdateInterval);
            
            try
            {
                // Update sensor data
                UpdateSensorData();
                
                // Execute current node and get next node
                currentNavNode = ExecuteNode(currentNavNode, navAiTree);
            }
            catch (System.Exception)
            {
                // Restart from beginning on error
                currentNavNode = GetFirstNodeFromStart(navAiTree);
            }
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
            // Calculate AI update delay based on Coms usage and ally count
            float currentUpdateInterval = CalculateAIUpdateInterval();
            yield return new WaitForSeconds(currentUpdateInterval);
            
            try
            {
                // Update sensor data
                UpdateSensorData();
                
                // Execute current node and get next node
                currentTurretNode = ExecuteNode(currentTurretNode, turretAiTree);
            }
            catch (System.Exception)
            {
                // Restart from beginning on error
                currentTurretNode = GetFirstNodeFromStart(turretAiTree);
            }
        }
    }
    
    /// <summary>
    /// Executes a single AI node and returns the next node to execute
    /// Implements the top-down, backtrack-on-false, Y-position priority pattern
    /// </summary>
    AiExecutableNode ExecuteNode(AiExecutableNode node, AiTreeAsset tree)
    {
        if (node == null) return null;
        
        // Determine if this is Nav or Turret AI
        bool isNavAI = (tree == runtimeNavAI);
        
        // Check if this is a Cycle node (identified by methodName starting with "Cycle")
        if (node.methodName.StartsWith("Cycle"))
        {
            return ExecuteCycleNode(node, tree);
        }
        
        switch (node.nodeType)
        {
            case AiNodeType.Condition:
                bool conditionResult = ExecuteCondition(node, tree);
                return GetNextNodeFromCondition(node, tree, conditionResult);
            case AiNodeType.Action:
                // Build condition chain up to this action
                string actionChain = BuildConditionChain(node, tree);
                ref string lastLoggedChain = ref (isNavAI ? ref lastLoggedNavChain : ref lastLoggedTurretChain);
                
                if (actionChain != lastLoggedChain)
                {
                    string aiTypeStr = isNavAI ? "Nav" : "Turret";
                    Debug.Log($"[{aiTypeStr}] {gameObject.name} → {actionChain}");
                    lastLoggedChain = actionChain;
                }
                
                ExecuteAction(node, tree);
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
    }
    
    /// <summary>
    /// Builds a readable condition chain string by traversing back to the start
    /// </summary>
    string BuildConditionChain(AiExecutableNode currentNode, AiTreeAsset tree)
    {
        var chain = new System.Collections.Generic.List<string>();
        var visitedNodes = new System.Collections.Generic.HashSet<string>();
        
        // Add current node
        chain.Add(currentNode.originalLabel);
        visitedNodes.Add(currentNode.nodeId);
        
        // Walk backwards to find parent conditions and cycle nodes
        AiExecutableNode node = currentNode;
        while (node != null && chain.Count < 10) // Limit chain length to prevent infinite loops
        {
            AiExecutableNode parent = FindParentNode(node, tree);
            if (parent != null && !visitedNodes.Contains(parent.nodeId))
            {
                // Include both Condition nodes, Cycle nodes, and Coms action nodes in the chain
                if (parent.nodeType == AiNodeType.Condition || 
                    parent.methodName.StartsWith("Cycle") || 
                    parent.methodName == "Coms")
                {
                    chain.Insert(0, parent.originalLabel);
                    visitedNodes.Add(parent.nodeId);
                    node = parent;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }
        
        return string.Join(" → ", chain);
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
            // Follow to first connected node (highest Y-position)
            var nextNode = sortedConnections.FirstOrDefault();
            
            // Special handling: If the next node is an action node that is Fire, CenterTarget, or LeadTarget,
            // and we have both Fire and a tracking node available, choose based on whether we can fire
            if (nextNode != null && nextNode.nodeType == AiNodeType.Action)
            {
                if (nextNode.methodName == "Fire" || nextNode.methodName == "CenterTarget" || nextNode.methodName == "LeadTarget" || nextNode.methodName == "TrackTarget")
                {
                    // Check if both Fire and a tracking action (CenterTarget/LeadTarget/TrackTarget) are available as siblings
                    var fireNode = sortedConnections.FirstOrDefault(n => n.methodName == "Fire");
                    var trackingNode = sortedConnections.FirstOrDefault(n => n.methodName == "CenterTarget" || n.methodName == "LeadTarget" || n.methodName == "TrackTarget");
                    
                    if (fireNode != null && trackingNode != null)
                    {
                        // Choose based on whether we can fire
                        if (CanFire())
                        {
                            return fireNode;
                        }
                        else
                        {
                            return trackingNode;
                        }
                    }
                }
            }
            
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
            var nextNode = sortedConnections[failedIndex + 1];
            return nextNode;
        }
        
        // No more alternatives from this parent
        // Check if the parent is a top-level node (connected to Start)
        bool parentIsTopLevel = tree.connections.Any(c => 
            (c.fromNodeId == "StartNavButton" || c.fromNodeId == "StartTurretButton") && 
            c.toNodeId == parentNode.nodeId);
        
        if (parentIsTopLevel)
        {
            // Parent is top-level - get its next sibling from Start
            return GetNextAlternativeFromStart(parentNode, tree);
        }
        
        // Special case: If this parent is IfSelf, skip over it when backtracking
        // (IfSelf is always true, just switches target context, so when all its children fail, skip it)
        if (parentNode.methodName == "IfSelf")
        {
            // Find grandparent and skip past IfSelf
            AiExecutableNode grandParent = FindParentNode(parentNode, tree);
            if (grandParent != null && grandParent != parentNode)
            {
                return GetNextAlternativeFromParent(grandParent, parentNode, tree);
            }
            else
            {
                // IfSelf is top-level, try its next sibling
                return GetNextAlternativeFromStart(parentNode, tree);
            }
        }
        
        // Continue normal backtracking to grandparent
        AiExecutableNode grandParent2 = FindParentNode(parentNode, tree);
        if (grandParent2 != null && grandParent2 != parentNode)
        {
            return GetNextAlternativeFromParent(grandParent2, parentNode, tree);
        }
        
        // No grandparent found - restart from beginning
        return GetFirstNodeFromStart(tree);
    }
      /// <summary>
    /// Gets the next node after an action
    /// Actions always restart from the beginning so the tree is re-evaluated every cycle
    /// This allows higher priority conditions to interrupt cycle nodes
    /// </summary>
    AiExecutableNode GetNextNodeFromAction(AiExecutableNode actionNode, AiTreeAsset tree)
    {
        // Always restart from beginning after executing an action
        // This ensures the AI re-evaluates all conditions every update cycle (0.1s)
        // and can properly backtrack when conditions become false
        // Cycle nodes use memory to resume where they left off when reached again
        return GetFirstNodeFromStart(tree);
    }
    
    #endregion
    
    #region Cycle Node Logic
    
    /// <summary>
    /// Executes a Cycle node, managing memory to cycle through its connected action nodes based on time
    /// Cycles through nodes from top to bottom, tracking time on each node
    /// Resumes from last position when interrupted by higher priority nodes
    /// </summary>
    AiExecutableNode ExecuteCycleNode(AiExecutableNode cycleNode, AiTreeAsset tree)
    {
        if (cycleNode == null || cycleNode.connectedNodeIds.Count == 0)
        {
            return GetFirstNodeFromStart(tree);
        }
        
        // Parse the cycle time from the node label (e.g., "Cycle 5" -> 5 seconds)
        float cycleTime = ParseCycleTime(cycleNode.originalLabel);
        if (cycleTime <= 0)
        {
            cycleTime = 1f; // Default to 1 second if invalid
        }
        
        // Get sorted child nodes by Y position (highest first = top to bottom)
        var sortedChildren = cycleNode.connectedNodeIds
            .Select(nodeId => tree.executableNodes.Find(n => n.nodeId == nodeId))
            .Where(n => n != null)
            .OrderByDescending(n => n.position.y)
            .ToList();
        
        if (sortedChildren.Count == 0)
        {
            return GetFirstNodeFromStart(tree);
        }
        
        // Initialize memory for this cycle node if it doesn't exist
        if (!cycleNodeMemory.ContainsKey(cycleNode.nodeId))
        {
            cycleNodeMemory[cycleNode.nodeId] = 0; // Start at first child (top)
            cycleNodeTimeSpent[cycleNode.nodeId] = 0f;
            cycleNodeLastUpdateTime[cycleNode.nodeId] = Time.time;
        }
        
        // Get current state
        int currentChildIndex = cycleNodeMemory[cycleNode.nodeId];
        float timeSpent = cycleNodeTimeSpent[cycleNode.nodeId];
        float lastUpdateTime = cycleNodeLastUpdateTime[cycleNode.nodeId];
        
        // Calculate time elapsed since last update (handles interruptions)
        float deltaTime = Time.time - lastUpdateTime;
        cycleNodeLastUpdateTime[cycleNode.nodeId] = Time.time;
        
        // Increment time spent on current node
        timeSpent += deltaTime;
        cycleNodeTimeSpent[cycleNode.nodeId] = timeSpent;
        
        // Check if time for current node has expired
        if (timeSpent >= cycleTime)
        {
            // Move to next child (top to bottom)
            currentChildIndex++;
            
            // Wrap around if we've reached the end
            if (currentChildIndex >= sortedChildren.Count)
            {
                currentChildIndex = 0; // Back to top
            }
            
            // Update memory and reset time for new node
            cycleNodeMemory[cycleNode.nodeId] = currentChildIndex;
            cycleNodeTimeSpent[cycleNode.nodeId] = 0f;
            
            // Note: Rotation targets are now tracked per-node, so no need to clear global state
        }
        
        // Return the current child node
        var currentChild = sortedChildren[currentChildIndex];
        return currentChild;
    }
    
    /// <summary>
    /// Parses the cycle time from the cycle node's label (e.g., "Cycle 5" -> 5.0f)
    /// </summary>
    private float ParseCycleTime(string nodeLabel)
    {
        if (string.IsNullOrEmpty(nodeLabel) || !nodeLabel.StartsWith("Cycle"))
            return 0f;
        
        // Extract number after "Cycle "
        string[] parts = nodeLabel.Split(' ');
        if (parts.Length >= 2)
        {
            if (float.TryParse(parts[1], out float time))
            {
                return time;
            }
        }
        return 0f;
    }
    
    /// <summary>
    /// Gets the parent cycle node for a given action node
    /// </summary>
    AiExecutableNode GetParentCycleNode(AiExecutableNode actionNode, AiTreeAsset tree)
    {
        foreach (var node in tree.executableNodes)
        {
            if (node.methodName.StartsWith("Cycle") && node.connectedNodeIds.Contains(actionNode.nodeId))
            {
                return node;
            }
        }
        return null;
    }
    
    #endregion
    
    #region Sensor Updates
    
    /// <summary>
    /// Updates sensor data for decision making
    /// </summary>
    void UpdateSensorData()
    {
        // Store previous detected enemies before clearing (for AllyTargetList cleanup)
        previousDetectedEnemies.Clear();
        previousDetectedEnemies.AddRange(detectedEnemies);
        
        detectedEnemies.Clear();
        detectedAllies.Clear();
        // NOTE: Do NOT clear currentTarget here - it needs to persist between condition evaluations
        // The conditions (IfEnemy, IfAlly, IfAny) will set currentTarget as needed
        
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
            
            // Check if object is within vision cone (use turret direction for vision)
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
                // Only add if alive
                if (enemyTankMan != null && enemyTankMan.CurrentHealth > 0)
                {
                    GameObject tankRoot = enemyTankMan.gameObject;
                    if (!detectedEnemies.Contains(tankRoot))
                    {
                        detectedEnemies.Add(tankRoot);
                        if (shouldDebug)
                        {
                        }
                    }
                }
            }
            else if (isAlly && inVisionCone)
            {
                // Check if ally tank is alive before adding to detected allies
                TankMan allyTankMan = collider.GetComponent<TankMan>();
                if (allyTankMan == null)
                {
                    allyTankMan = collider.GetComponentInParent<TankMan>();
                }
                
                if (allyTankMan != null && allyTankMan.CurrentHealth > 0)
                {
                    // Use the tank's root GameObject to avoid duplicates from tank parts
                    GameObject tankRoot = allyTankMan.gameObject;
                    if (!detectedAllies.Contains(tankRoot))
                    {
                        detectedAllies.Add(tankRoot);
                    }
                }
            }
        }
        
        // Update AllyTargetList with currently detected enemies (in vision cone)
        UpdateAllyTargetList();
        
        // Remove currentTarget if it is dead
        if (currentTarget != null)
        {
            TankMan targetTankMan = currentTarget.GetComponent<TankMan>() ?? currentTarget.GetComponentInParent<TankMan>();
            if (targetTankMan != null && targetTankMan.CurrentHealth <= 0)
            {
                currentTarget = null;
            }
        }
        // NOTE: We no longer set currentTarget here - let the AI conditions (IfEnemy, IfAny, IfAlly) set it
        // This prevents the target from being cleared/overwritten between condition evaluations in the same branch
    }
    
    /// <summary>
    /// Calculates the AI update interval based on Coms usage and number of allies.
    /// Base interval + (0.2s * number of alive allies) when using Coms.
    /// Simulates the processing delay from managing allied intel.
    /// </summary>
    float CalculateAIUpdateInterval()
    {
        if (!isCurrentlyUsingComs)
        {
            // Not using Coms - use base interval
            return aiUpdateInterval;
        }
        
        // Count alive allies on the same team
        int aliveAllyCount = 0;
        if (myTeamInfo != null)
        {
            TankMan[] allTanks = FindObjectsByType<TankMan>(FindObjectsSortMode.None);
            foreach (var tank in allTanks)
            {
                if (tank == this) continue; // Skip self
                
                TankTeamInfo otherTeamInfo = tank.GetComponent<TankTeamInfo>();
                if (otherTeamInfo != null && 
                    otherTeamInfo.teamId == myTeamInfo.teamId && 
                    tank.CurrentHealth > 0)
                {
                    aliveAllyCount++;
                }
            }
        }
        
        // Base interval + 0.2s per ally
        float interval = aiUpdateInterval + (COMS_ALLY_DELAY_PER_TANK * aliveAllyCount);
        return interval;
    }
    
    /// <summary>
    /// Updates the shared AllyTargetList with enemies this tank can see.
    /// Reports new targets and removes targets that are no longer visible.
    /// </summary>
    void UpdateAllyTargetList()
    {
        if (AllyTargetList.Instance == null) return;
        
        // Report all currently detected enemies to AllyTargetList
        foreach (var enemy in detectedEnemies)
        {
            if (enemy != null)
            {
                AllyTargetList.Instance.ReportTarget(this, enemy);
            }
        }
        
        // Remove enemies that were visible last frame but aren't anymore
        foreach (var previousEnemy in previousDetectedEnemies)
        {
            if (previousEnemy != null && !detectedEnemies.Contains(previousEnemy))
            {
                AllyTargetList.Instance.RemoveTarget(this, previousEnemy);
            }
        }
    }
    
    #endregion
    
    #region Coms Branch Detection
    
    /// <summary>
    /// Checks if a given node is on a Coms branch by traversing up the AI tree.
    /// If any parent node is "IfComs" or "Coms", this node is on a Coms branch.
    /// </summary>
    /// <param name="node">The node to check</param>
    /// <param name="tree">The AI tree containing the node</param>
    /// <returns>True if the node is on a Coms branch, false otherwise</returns>
    bool IsOnComsBranch(AiExecutableNode node, AiTreeAsset tree)
    {
        if (node == null || tree == null) return false;
        
        try
        {
            var visitedNodes = new HashSet<string>();
            AiExecutableNode currentNode = node;
            
            // Traverse up the tree to find any Coms parent
            while (currentNode != null)
            {
                // Prevent infinite loops
                if (visitedNodes.Contains(currentNode.nodeId))
                    break;
                visitedNodes.Add(currentNode.nodeId);
                
                // Check if this node is a Coms node
                if (currentNode.methodName == "IfComs" || 
                    currentNode.methodName == "Coms" ||
                    (currentNode.originalLabel != null && 
                     (currentNode.originalLabel.ToLower().Contains("coms") || 
                      currentNode.originalLabel.ToLower().Contains("comms"))))
                {
                    return true;
                }
                
                // Find parent node
                currentNode = FindParentNode(currentNode, tree);
            }
        }
        catch (System.Exception)
        {
        }
        
        return false;
    }
    
    /// <summary>
    /// Gets a target from the AllyTargetList (Coms) for this tank's team.
    /// Returns the closest known enemy from allied intel.
    /// </summary>
    /// <returns>Closest enemy from ally intel, or null if none available</returns>
    GameObject GetComsTarget()
    {
        if (AllyTargetList.Instance == null || myTeamInfo == null) return null;
        
        try
        {
            return AllyTargetList.Instance.GetClosestTargetForTeam(myTeamInfo.teamId, transform.position);
        }
        catch (System.Exception)
        {
            return null;
        }
    }
    
    /// <summary>
    /// Gets all targets from the AllyTargetList (Coms) for this tank's team.
    /// </summary>
    /// <returns>List of all known enemies from allied intel</returns>
    List<GameObject> GetAllComsTargets()
    {
        if (AllyTargetList.Instance == null || myTeamInfo == null) return new List<GameObject>();
        
        return AllyTargetList.Instance.GetAllTargetsForTeam(myTeamInfo.teamId);
    }
    
    #endregion
    
    #region Condition Execution
    
    /// <summary>
    /// Executes condition nodes and returns true/false result
    /// </summary>
    /// <param name="conditionNode">The condition node to evaluate</param>
    /// <param name="tree">The AI tree for Coms branch checking</param>
    bool ExecuteCondition(AiExecutableNode conditionNode, AiTreeAsset tree)
    {
        bool result = false;
        
        // Check if this node is on a Coms branch (can use AllyTargetList)
        bool isOnComs = IsOnComsBranch(conditionNode, tree);
        
        switch (conditionNode.methodName)
        {
            case "IfSelf":
                // Set evaluation target to self (for HP/armor checks) but keep currentTarget for actions
                evaluationTarget = gameObject;
                result = true;
                break;
                
            case "IfComs":
                // Coms condition - check if AllyTargetList has any targets available
                // If no targets available, returns false to allow backtracking to other branches
                comsTarget = GetComsTarget();
                if (comsTarget != null)
                {
                    result = true;
                    // Don't set currentTarget here - let child conditions handle targeting
                }
                else
                {
                    result = false;
                    isCurrentlyUsingComs = false;
                }
                break;
                
            case "IfEnemy":
                // Check personal vision for enemies
                if (detectedEnemies.Count > 0)
                {
                    // Find closest enemy from detected list
                    GameObject closestEnemy = detectedEnemies
                        .Where(e => e != null)
                        .OrderBy(e => Vector3.Distance(transform.position, e.transform.position))
                        .FirstOrDefault();
                    
                    if (closestEnemy != null)
                    {
                        // Validate target is alive
                        TankMan targetTankMan = closestEnemy.GetComponent<TankMan>();
                        if (targetTankMan == null)
                        {
                            targetTankMan = closestEnemy.GetComponentInParent<TankMan>();
                        }
                        
                        if (targetTankMan != null && targetTankMan.CurrentHealth > 0)
                        {
                            result = true;
                            currentTarget = closestEnemy;
                            evaluationTarget = closestEnemy;
                            comsTarget = null;
                            isCurrentlyUsingComs = false;
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    else
                    {
                        result = false;
                    }
                }
                else if (isOnComs)
                {
                    // On a Coms branch - check AllyTargetList for targets
                    comsTarget = GetComsTarget();
                    if (comsTarget != null)
                    {
                        // Found target via Coms - apply Coms penalties
                        result = true;
                        evaluationTarget = comsTarget;
                        currentTarget = comsTarget; // Update currentTarget for actions
                        isCurrentlyUsingComs = true; // Set flag to apply speed/delay penalties
                    }
                    else
                    {
                        result = false;
                        comsTarget = null;
                        isCurrentlyUsingComs = false;
                    }
                }
                else
                {
                    // No enemies in personal vision and not on Coms branch
                    result = false;
                    comsTarget = null;
                    isCurrentlyUsingComs = false;
                }
                break;
                
            case "IfAlly":
                // Check personal vision for allies
                if (detectedAllies.Count > 0)
                {
                    // Find closest ally from detected list
                    GameObject closestAlly = detectedAllies
                        .Where(a => a != null)
                        .OrderBy(a => Vector3.Distance(transform.position, a.transform.position))
                        .FirstOrDefault();
                    
                    if (closestAlly != null)
                    {
                        // Validate ally is alive
                        TankMan allyTankMan = closestAlly.GetComponent<TankMan>();
                        if (allyTankMan == null)
                        {
                            allyTankMan = closestAlly.GetComponentInParent<TankMan>();
                        }
                        
                        if (allyTankMan != null && allyTankMan.CurrentHealth > 0)
                        {
                            result = true;
                            currentTarget = closestAlly;
                            evaluationTarget = closestAlly;
                            isCurrentlyUsingComs = false;
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    else
                    {
                        result = false;
                    }
                }
                else
                {
                    result = false;
                }
                break;
                
            case "IfAny":
                // Check personal vision for ANY tank (enemy OR ally)
                bool hasEnemyInVision = detectedEnemies.Count > 0;
                bool hasAllyInVision = detectedAllies.Count > 0;
                
                string allyNames = detectedAllies.Count > 0 ? string.Join(", ", detectedAllies.Select(a => a?.name ?? "null")) : "none";
                string enemyNames = detectedEnemies.Count > 0 ? string.Join(", ", detectedEnemies.Select(e => e?.name ?? "null")) : "none";
                
                if (hasEnemyInVision || hasAllyInVision)
                {
                    result = true;
                    isCurrentlyUsingComs = false; // Using personal vision
                    
                    // Find the closest tank from BOTH lists combined
                    GameObject selectedTarget = null;
                    float closestDistance = float.MaxValue;
                    
                    // Check all enemies
                    foreach (var enemy in detectedEnemies)
                    {
                        if (enemy != null)
                        {
                            float dist = Vector3.Distance(transform.position, enemy.transform.position);
                            if (dist < closestDistance)
                            {
                                closestDistance = dist;
                                selectedTarget = enemy;
                            }
                        }
                    }
                    
                    // Check all allies
                    foreach (var ally in detectedAllies)
                    {
                        if (ally != null)
                        {
                            float dist = Vector3.Distance(transform.position, ally.transform.position);
                            if (dist < closestDistance)
                            {
                                closestDistance = dist;
                                selectedTarget = ally;
                            }
                        }
                    }
                    
                    // Set BOTH evaluationTarget and currentTarget to the closest tank
                    evaluationTarget = selectedTarget;
                    currentTarget = selectedTarget; // Critical: IfRange and actions need this!
                }
                else if (isOnComs)
                {
                    // On a Coms branch - check AllyTargetList for any targets
                    comsTarget = GetComsTarget();
                    if (comsTarget != null)
                    {
                        result = true;
                        currentTarget = comsTarget; // Update currentTarget for actions
                        evaluationTarget = comsTarget;
                        isCurrentlyUsingComs = true; // Set flag to apply speed/delay penalties
                    }
                    else
                    {
                        result = false;
                        isCurrentlyUsingComs = false;
                    }
                }
                else
                {
                    result = false;
                    isCurrentlyUsingComs = false;
                }
                break;
                
            case "IfRifle":
                // Check if target is detected through vision (uses visionRange and visionCone, not firing range)
                result = currentTarget != null && detectedEnemies.Contains(currentTarget);
                break;
                
            case "IfHP":
                // Check evaluation target's HP (set by IfSelf/IfEnemy/IfAlly)
                if (evaluationTarget == null)
                {
                    result = false;
                }
                else
                {
                    TankMan targetTankMan = evaluationTarget.GetComponent<TankMan>();
                    if (targetTankMan == null)
                    {
                        targetTankMan = evaluationTarget.GetComponentInParent<TankMan>();
                    }
                    
                    if (targetTankMan != null)
                    {
                        float healthPercent = (targetTankMan.CurrentHealth / targetTankMan.TotalHP) * 100f;
                        if (conditionNode.originalLabel.Contains(">"))
                            result = healthPercent > conditionNode.numericValue;
                        else if (conditionNode.originalLabel.Contains("<"))
                            result = healthPercent < conditionNode.numericValue;
                        else
                            result = healthPercent >= conditionNode.numericValue;
                    }
                    else
                    {
                        result = false;
                    }
                }
                break;
                
            case "IfArmor":
                // Check evaluation target's armor (set by IfSelf/IfEnemy/IfAlly)
                if (evaluationTarget == null)
                {
                    result = false;
                }
                else
                {
                    TankMan targetTankMan = evaluationTarget.GetComponent<TankMan>();
                    if (targetTankMan == null)
                    {
                        targetTankMan = evaluationTarget.GetComponentInParent<TankMan>();
                    }
                    
                    if (targetTankMan != null)
                    {
                        if (conditionNode.originalLabel.Contains(">"))
                            result = targetTankMan.Armor > conditionNode.numericValue;
                        else if (conditionNode.originalLabel.Contains("<"))
                            result = targetTankMan.Armor < conditionNode.numericValue;
                        else
                            result = targetTankMan.Armor >= conditionNode.numericValue;
                    }
                    else
                    {
                        result = false;
                    }
                }
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
            
            case "IfMyTag":
                // Check private tag on current target
                if (currentTarget == null)
                {
                    result = false;
                }
                else
                {
                    int privateTag = GetPrivateTag(currentTarget);
                    if (privateTag == -1)
                    {
                        // No tag exists - for != conditions, this should be true (no tag != any value)
                        // For other conditions, this should be false
                        result = conditionNode.originalLabel.Contains("!=");
                    }
                    else
                    {
                        if (conditionNode.originalLabel.Contains(">"))
                            result = privateTag > conditionNode.numericValue;
                        else if (conditionNode.originalLabel.Contains("<"))
                            result = privateTag < conditionNode.numericValue;
                        else if (conditionNode.originalLabel.Contains("!="))
                            result = privateTag != (int)conditionNode.numericValue;
                        else // equals
                            result = privateTag == (int)conditionNode.numericValue;
                    }
                }
                break;
            
            case "IfTeamTag":
                // Check team tag on current target
                if (currentTarget == null || myTeamInfo == null)
                {
                    result = false;
                }
                else
                {
                    int teamTag = AllyTargetList.Instance.GetTeamTag(myTeamInfo.teamId, currentTarget);
                    if (teamTag == -1)
                    {
                        // No tag exists - for != conditions, this should be true (no tag != any value)
                        // For other conditions, this should be false
                        result = conditionNode.originalLabel.Contains("!=");
                    }
                    else
                    {
                        if (conditionNode.originalLabel.Contains(">"))
                            result = teamTag > conditionNode.numericValue;
                        else if (conditionNode.originalLabel.Contains("<"))
                            result = teamTag < conditionNode.numericValue;
                        else if (conditionNode.originalLabel.Contains("!="))
                            result = teamTag != (int)conditionNode.numericValue;
                        else // equals
                            result = teamTag == (int)conditionNode.numericValue;
                    }
                }
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
    void ExecuteAction(AiExecutableNode actionNode, AiTreeAsset tree)
    {
        // Determine if this is Nav or Turret AI
        bool isNavAI = (tree == runtimeNavAI);
        
        // Check if this action is on a Coms branch (can use AllyTargetList targets)
        bool isOnComs = IsOnComsBranch(actionNode, tree);
        
        // Use appropriate variables based on AI type
        ref AiExecutableNode currentActionNode = ref (isNavAI ? ref currentNavActionNode : ref currentTurretActionNode);
        ref Coroutine currentActionCoroutine = ref (isNavAI ? ref currentNavActionCoroutine : ref currentTurretActionCoroutine);
        
        // Store current action node for parameter access
        currentActionNode = actionNode;

        // Stop any current action
        if (currentActionCoroutine != null)
        {
            StopCoroutine(currentActionCoroutine);
            currentActionCoroutine = null;
        }
        
        // For actions that need a target, ensure we have one (either from personal vision or Coms)
        // Note: currentTarget is already set by IfEnemy/IfAny conditions which check Coms if on a Coms branch
        // If we're on a Coms branch and still don't have a target, try to get one now
        if (isOnComs && currentTarget == null)
        {
            GameObject comsTargetNow = GetComsTarget();
            if (comsTargetNow != null)
            {
                currentTarget = comsTargetNow;
            }
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
                currentActionCoroutine = StartCoroutine(WanderAction(actionNode.nodeId, isNavAI));
                break;
            case "Move":
                if (currentTarget != null)
                {
                    currentActionCoroutine = StartCoroutine(MoveToTarget(actionNode.nodeId, isNavAI));
                }
                else
                {
                    currentActionCoroutine = StartCoroutine(WanderAction(actionNode.nodeId, isNavAI));
                }
                break;
            case "Stop":
                StopMovement();
                // Update node ID tracking even for immediate actions
                if (isNavAI)
                    lastUsedNavNodeId = actionNode.nodeId;
                else
                    lastUsedTurretNodeId = actionNode.nodeId;
                break;
            case "Chase":
                // Chase works with both personal vision and Coms targets
                // currentTarget was set by IfEnemy or refreshed above if on Coms branch
                if (currentTarget != null)
                {
                    currentActionCoroutine = StartCoroutine(ChaseTarget(actionNode.nodeId, isNavAI));
                }
                break;
            case "Flee":
                // Flee works with both personal vision and Coms targets
                // currentTarget was set by IfEnemy or refreshed above if on Coms branch
                if (currentTarget != null)
                {
                    currentActionCoroutine = StartCoroutine(FleeFromTarget(actionNode.nodeId, isNavAI));
                }
                break;
            case "Wait":
                StopMovement();
                currentActionCoroutine = StartCoroutine(WaitAction(actionNode.nodeId, isNavAI));
                break;
            case "LeadTarget":
                // LeadTarget works with both personal vision and Coms targets
                // currentTarget was set by IfEnemy or refreshed above if on Coms branch
                if (currentTarget != null)
                {
                    // Get lead distance from node's numeric value (default 0 for center targeting)
                    float leadDistance = actionNode.numericValue;
                    currentLeadDistance = leadDistance; // Store for CanFire to use
                    currentActionCoroutine = StartCoroutine(LeadTargetAction(leadDistance, actionNode.nodeId, isNavAI));
                }
                break;
            case "TrackTarget":
            case "CenterTarget": // Alias for TrackTarget
                if (currentTarget != null)
                {
                    currentLeadDistance = 0f; // Track target center (no lead)
                    currentActionCoroutine = StartCoroutine(TrackTargetAction(actionNode.nodeId, isNavAI));
                }
                break;
            case "AlignFront":
                currentActionCoroutine = StartCoroutine(AlignFrontAction(actionNode.nodeId, isNavAI));
                break;
            case "AlignRight":
                currentActionCoroutine = StartCoroutine(AlignRightAction(actionNode.nodeId, isNavAI));
                break;
            case "AlignLeft":
                currentActionCoroutine = StartCoroutine(AlignLeftAction(actionNode.nodeId, isNavAI));
                break;
            case "AlignBack":
                currentActionCoroutine = StartCoroutine(AlignBackAction(actionNode.nodeId, isNavAI));
                break;
            case "RotateUp":
                // Stop BOTH nav and turret actions to prevent conflicts with other rotation actions
                if (currentNavActionCoroutine != null) { StopCoroutine(currentNavActionCoroutine); currentNavActionCoroutine = null; }
                if (currentTurretActionCoroutine != null) { StopCoroutine(currentTurretActionCoroutine); currentTurretActionCoroutine = null; }
                currentActionCoroutine = StartCoroutine(RotateUpAction(actionNode.numericValue, actionNode.nodeId, isNavAI));
                break;
            case "RotateDown":
                // Stop BOTH nav and turret actions to prevent conflicts with other rotation actions
                if (currentNavActionCoroutine != null) { StopCoroutine(currentNavActionCoroutine); currentNavActionCoroutine = null; }
                if (currentTurretActionCoroutine != null) { StopCoroutine(currentTurretActionCoroutine); currentTurretActionCoroutine = null; }
                currentActionCoroutine = StartCoroutine(RotateDownAction(actionNode.numericValue, actionNode.nodeId, isNavAI));
                break;
            case "MapCenter":
                currentActionCoroutine = StartCoroutine(MapCenterAction(actionNode.nodeId, isNavAI));
                break;
            case "Home":
                currentActionCoroutine = StartCoroutine(HomeAction(actionNode.nodeId, isNavAI));
                break;
            case "Forward":
                currentActionCoroutine = StartCoroutine(ForwardAction(actionNode.nodeId, isNavAI));
                break;
            case "RotateRight":
                // Pass the node to the action so it can track which specific node is being executed
                currentActionCoroutine = StartCoroutine(RotateRightAction(actionNode.numericValue, actionNode.nodeId, isNavAI));
                break;
            case "RotateLeft":
                // Pass the node to the action so it can track which specific node is being executed
                currentActionCoroutine = StartCoroutine(RotateLeftAction(actionNode.numericValue, actionNode.nodeId, isNavAI));
                break;
            case "MyTag":
                // Assign a personal tag to the current target
                if (currentTarget != null)
                {
                    int tagValue = (int)actionNode.numericValue;
                    SetPrivateTag(currentTarget, tagValue);
                }
                // Update node ID tracking for immediate actions
                if (isNavAI)
                    lastUsedNavNodeId = actionNode.nodeId;
                else
                    lastUsedTurretNodeId = actionNode.nodeId;
                break;
            case "TeamTag":
                // Assign a team tag to the current target (shared with allies)
                if (currentTarget != null && myTeamInfo != null)
                {
                    int tagValue = (int)actionNode.numericValue;
                    AllyTargetList.Instance.SetTeamTag(myTeamInfo.teamId, currentTarget, tagValue);
                }
                // Update node ID tracking for immediate actions
                if (isNavAI)
                    lastUsedNavNodeId = actionNode.nodeId;
                else
                    lastUsedTurretNodeId = actionNode.nodeId;
                break;
            default:
                break;
        }
    }
    
    /// <summary>

    
    #endregion
    
    #region Combat System
    
    /// <summary>
    /// Helper method to find the BasePivot transform for accurate targeting
    /// Handles cases where target might be a child part (armor, turret, engine) or root tank object
    /// </summary>
    Transform GetTargetBasePivot(GameObject targetObject)
    {
        if (targetObject == null) return null;
        
        // First, try to find BasePivot directly as a child
        Transform basePivot = targetObject.transform.Find("BasePivot");
        if (basePivot != null) return basePivot;
        
        // If not found, target might be a child part - get the root tank object
        TankMan targetTankMan = targetObject.GetComponent<TankMan>();
        if (targetTankMan == null)
        {
            targetTankMan = targetObject.GetComponentInParent<TankMan>();
        }
        
        // Now search for BasePivot from the root tank object
        if (targetTankMan != null)
        {
            basePivot = targetTankMan.transform.Find("BasePivot");
            if (basePivot != null) return basePivot;
        }
        
        // Fallback: return the original transform if BasePivot not found
        return targetObject.transform;
    }
    
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
        // Range check removed - fire whenever aimed, bullet will explode after traveling its max range
        if (turretTransform != null)
        {
            if (turretType == TurretType.Artillery)
            {
                // For artillery, check if turret matches the calculated trajectory angle
                Transform basePivot = GetTargetBasePivot(currentTarget);
                Vector3 targetPosition = basePivot.position;
                
                float launchAngle;
                Vector3 horizontalDirection = CalculateArtilleryDirection(out launchAngle, targetPosition);
                
                // Calculate what the turret rotation should be
                Vector3 horizontalDir = Vector3.ProjectOnPlane(horizontalDirection, Vector3.up);
                if (horizontalDir.magnitude > 0.1f)
                {
                    Quaternion horizontalRotation = Quaternion.LookRotation(horizontalDir);
                    float adjustedAngle = launchAngle - 60f; // Compensate for model's 60-degree default tilt
                    Quaternion targetRotation = horizontalRotation * Quaternion.Euler(-adjustedAngle, 0f, 0f);
                    
                    // Check if turret is within 2 degrees of target rotation
                    float angleDifference = Quaternion.Angle(turretTransform.rotation, targetRotation);
                    if (angleDifference > 2f)
                    {
                        return false;
                    }
                }
            }
            else
            {
                // Direct fire - check if aimed at lead point
                Vector3 aimPoint = CalculateLeadPoint(currentTarget, currentLeadDistance);
                
                Vector3 turretForward = turretTransform.forward;
                Vector3 directionToTarget = (aimPoint - turretTransform.position).normalized;
                float angleToTarget = Vector3.Angle(turretForward, directionToTarget);
                if (angleToTarget > 2f)
                {
                    return false;
                }
            }
        }
        else
        {
        }
        return true;
    }
    
    /// <summary>
    /// Checks if turret is aimed at a specific point within specified tolerance
    /// </summary>
    bool IsTurretAimedAtPoint(Vector3 targetPoint, float angleTolerance = 1f)
    {
        if (turretTransform == null)
        {
            return false;
        }
        
        Vector3 turretForward = turretTransform.forward;
        Vector3 directionToTarget = (targetPoint - turretTransform.position).normalized;
        float angleToTarget = Vector3.Angle(turretForward, directionToTarget);
        
        return angleToTarget <= angleTolerance;
    }
    
    /// <summary>
    /// Calculates the lead point for targeting - adds lead distance in direction of target's movement
    /// LeadDistance = 0: aims at target center (replaces CenterTarget)
    /// LeadDistance > 0: aims ahead of target's movement
    /// </summary>
    Vector3 CalculateLeadPoint(GameObject target, float leadDistance)
    {
        if (target == null)
        {
            return Vector3.zero;
        }
        
        // Find the BasePivot for accurate center position
        Transform basePivot = GetTargetBasePivot(target);
        Vector3 targetPosition = basePivot.position;
        
        // If lead distance is 0, just return center position
        if (Mathf.Abs(leadDistance) < 0.01f)
        {
            return targetPosition;
        }
        
        // Get target's velocity
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb == null)
        {
            targetRb = target.GetComponentInParent<Rigidbody>();
        }
        
        if (targetRb != null && targetRb.linearVelocity.magnitude > 0.1f)
        {
            // Target is moving - lead it
            Vector3 targetVelocityDirection = targetRb.linearVelocity.normalized;
            Vector3 leadPoint = targetPosition + (targetVelocityDirection * leadDistance);
            return leadPoint;
        }
        else
        {
            // Target is stationary - just aim at center
            return targetPosition;
        }
    }
    
    void Fire()
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
                // Artillery: Calculate ballistic trajectory to target base pivot
                Transform basePivot = GetTargetBasePivot(currentTarget);
                Vector3 targetPosition = basePivot.position;
                direction = CalculateArtilleryDirection(out launchAngle, targetPosition);
            }
            else
            {
                // Direct fire: Shoot straight along turret's forward axis
                // The turret is already aimed at the lead point by LeadTargetAction
                direction = turretTransform.forward;
            }
            
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(direction));
            
            // Make artillery bullets twice as fat (wider and taller)
            if (turretType == TurretType.Artillery)
            {
                bullet.transform.localScale = new Vector3(2f, 2f, 1f);
            }
            
            // Give bullet velocity based on turret's bullet speed
            Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
            if (bulletRb != null)
            {
                // Configure physics based on turret type
                if (turretType == TurretType.Artillery)
                {
                    // Artillery uses Unity physics with gravity
                    bulletRb.isKinematic = false;
                    bulletRb.useGravity = true;
                    
                    // Calculate launch velocity with proper angle
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
            
            // Pass combat stats to bullet (bullet prefab has its own explosion effect)
            BulletScript bulletScript = bullet.GetComponent<BulletScript>();
            if (bulletScript != null)
            {
                // Hammer uses AOE like artillery but with custom radius of 20
                if (turretType == TurretType.Hammer)
                {
                    bulletScript.Initialize(damage, range, myTeamInfo.teamId, true, ParseKnockback(), 20f);
                }
                else
                {
                    bulletScript.Initialize(damage, range, myTeamInfo.teamId, turretType == TurretType.Artillery, ParseKnockback());
                }
            }
            else
            {
            }
            
            // Start hammer swing animation when firing
            if (turretType == TurretType.Hammer)
            {
                if (hammerSwingCoroutine != null)
                {
                    StopCoroutine(hammerSwingCoroutine);
                }
                hammerSwingCoroutine = StartCoroutine(SwingHammer());
            }
            
            // Fire second barrel if firePoint1 exists (for double-barrel shotguns)
            if (firePoint1 != null)
            {
                GameObject bullet2 = Instantiate(bulletPrefab, firePoint1.position, Quaternion.LookRotation(direction));
                
                // Make artillery bullets twice as fat (wider and taller)
                if (turretType == TurretType.Artillery)
                {
                    bullet2.transform.localScale = new Vector3(2f, 2f, 1f);
                }
                
                // Give bullet velocity based on turret's bullet speed
                Rigidbody bulletRb2 = bullet2.GetComponent<Rigidbody>();
                if (bulletRb2 != null)
                {
                    // Configure physics based on turret type
                    if (turretType == TurretType.Artillery)
                    {
                        // Artillery uses Unity physics with gravity
                        bulletRb2.isKinematic = false;
                        bulletRb2.useGravity = true;
                        
                        // Calculate launch velocity with proper angle
                        Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                        Vector3 launchVelocity = Quaternion.AngleAxis(launchAngle, Vector3.Cross(horizontalDirection, Vector3.up)) * horizontalDirection * bulletSpeed;
                        bulletRb2.linearVelocity = launchVelocity;
                    }
                    else
                    {
                        bulletRb2.useGravity = false;
                        bulletRb2.linearVelocity = direction * bulletSpeed;
                    }
                }
                
                // Pass combat stats to second bullet
                BulletScript bulletScript2 = bullet2.GetComponent<BulletScript>();
                if (bulletScript2 != null)
                {
                    // Hammer uses AOE like artillery but with custom radius of 20
                    if (turretType == TurretType.Hammer)
                    {
                        bulletScript2.Initialize(damage, range, myTeamInfo.teamId, true, ParseKnockback(), 20f);
                    }
                    else
                    {
                        bulletScript2.Initialize(damage, range, myTeamInfo.teamId, turretType == TurretType.Artillery, ParseKnockback());
                    }
                }
            }
        }
        
    }
    
    /// <summary>
    /// Coroutine to animate hammer swinging down and back up
    /// Swaps entire turret from HammerUp to HammerDown for 0.15s, then back
    /// Uses pre-instantiated models for performance
    /// </summary>
    IEnumerator SwingHammer()
    {
        // Check if animation prefab is assigned and pre-instantiated
        if (hammerDownInstance == null)
        {
            Debug.LogError($"[TankMan] No hammer animation instance available! Make sure TankAssembly loaded it.");
            yield break;
        }

        if (turretTransform == null)
        {
            Debug.LogError($"[TankMan] turretTransform is null, cannot swing hammer");
            yield break;
        }

        Debug.Log($"[TankMan] SwingHammer - Hiding turret: {turretTransform.name}");

        // Sync hammerDown transform with current turret transform
        hammerDownInstance.transform.position = turretTransform.position;
        hammerDownInstance.transform.rotation = turretTransform.rotation;
        hammerDownInstance.transform.localScale = turretTransform.localScale;

        // Hide the hammer up model (entire turret)
        turretTransform.gameObject.SetActive(false);

        // Show hammer down model
        hammerDownInstance.SetActive(true);

        Debug.Log($"[TankMan] SwingHammer - Showing HammerDown: {hammerDownInstance.name}");

        // Wait for 0.15 seconds (hammer down)
        yield return new WaitForSeconds(0.15f);

        // Hide hammer down model
        hammerDownInstance.SetActive(false);

        // Show hammer up model again
        turretTransform.gameObject.SetActive(true);

        Debug.Log($"[TankMan] SwingHammer - Restored HammerUp: {turretTransform.name}");

        // Calculate remaining time based on fire rate
        float fireRate = 1f / shotsPerSec;
        float remainingTime = Mathf.Max(0f, fireRate - 0.15f);

        // Wait for remaining time
        yield return new WaitForSeconds(remainingTime);

        hammerSwingCoroutine = null;
    }
    
    /// <summary>
    /// Helper method to find TurretData ScriptableObject by instance ID
    /// </summary>
    private TurretData FindTurretDataByInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            return null;
            
        // Search in Resources/Workshop/ComponentData/Turrets
        TurretData[] turrets = Resources.LoadAll<TurretData>("Workshop/ComponentData/Turrets");
        foreach (TurretData turret in turrets)
        {
            if (turret.instanceId == instanceId)
                return turret;
        }
        
        return null;
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
    Vector3 CalculateArtilleryDirection(out float launchAngle, Vector3 targetPos)
    {
        Vector3 firePos = firePoint.position;
        
        // Calculate horizontal distance and height difference
        Vector3 horizontalDisplacement = Vector3.ProjectOnPlane(targetPos - firePos, Vector3.up);
        float horizontalDistance = horizontalDisplacement.magnitude;
        
        // Apply exponential distance compensation - the further the target, the more we aim short
        // This compensates for consistent overshooting that increases with distance
        float distanceCompensation = 7f + (horizontalDistance * 0.001f);
        horizontalDistance = Mathf.Max(10f, horizontalDistance - distanceCompensation);
        
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
        
        // Apply manual knockback force if we have stored bullet data
        if (lastBulletVelocity != Vector3.zero && lastBulletKnockback > 0f && rb != null)
        {
            // Calculate knockback force based on bullet velocity direction and knockback value
            Vector3 knockbackDirection = lastBulletVelocity.normalized;
            float forceMagnitude = lastBulletKnockback; // Direct force value from ParseKnockback()
            
            // Apply force in the next FixedUpdate to ensure friction is fully reduced
            StartCoroutine(ApplyKnockbackForceNextFrame(knockbackDirection * forceMagnitude));
            
            Debug.Log($"[{gameObject.name}] Applying manual knockback force: {forceMagnitude:F1} in direction {knockbackDirection}");
            
            // Clear stored values
            lastBulletVelocity = Vector3.zero;
            lastBulletKnockback = 0f;
        }
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    /// <summary>
    /// Applies knockback force after waiting one physics frame
    /// </summary>
    private System.Collections.IEnumerator ApplyKnockbackForceNextFrame(Vector3 force)
    {
        yield return new WaitForFixedUpdate();
        if (rb != null)
        {
            rb.AddForce(force, ForceMode.Impulse);
        }
    }
    
    /// <summary>
    /// Reduces wheel collider friction to allow knockback to slide the tank
    /// </summary>
    private void ReduceWheelFriction()
    {
        if (frictionReduced) return; // Already reduced
        
        Debug.Log($"[{gameObject.name}] Zero friction applied at {Time.time:F2}");
        
        // Create zero-friction material
        PhysicsMaterial zeroFriction = new PhysicsMaterial("ZeroFriction");
        zeroFriction.dynamicFriction = 0f;
        zeroFriction.staticFriction = 0f;
        zeroFriction.frictionCombine = PhysicsMaterialCombine.Minimum;
        
        // Apply zero friction to all wheel colliders
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            if (wheelColliders[i] != null)
            {
                wheelColliders[i].material = zeroFriction;
            }
        }
        
        frictionReduced = true;
    }
    
    /// <summary>
    /// Restores wheel collider friction after knockback effect
    /// </summary>
    private void RestoreWheelFriction()
    {
        if (!frictionReduced) return;
        
        // Create default friction material
        PhysicsMaterial defaultFriction = new PhysicsMaterial("DefaultFriction");
        defaultFriction.dynamicFriction = 0.6f;
        defaultFriction.staticFriction = 0.6f;
        
        // Restore friction to all wheel colliders
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            if (wheelColliders[i] != null)
            {
                wheelColliders[i].material = defaultFriction;
            }
        }
        
        frictionReduced = false;
    }
    
    void Die()
    {
        StopAI();

        // Play explosion sound at tank position
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayExplosionAtPosition(transform.position);

        // Remove/destroy the turret if it exists
        if (turretTransform != null)
        {
            // Spawn fire explosion at turret position
            if (deathExplosionPrefab != null)
            {
                Vector3 explosionPosition = turretTransform.position + new Vector3(0f, -2f, 0f); // Lower by 2 units
                GameObject explosion = Instantiate(deathExplosionPrefab, explosionPosition, turretTransform.rotation);
                
                // Parent explosion to tank so it moves with the tank
                explosion.transform.SetParent(transform);
                
                // Apply custom scale to explosion (X, Y, Z)
                explosion.transform.localScale = new Vector3(3f, 1f, 3f); // Adjust X, Y, Z values as needed
                
                // Make explosion last 20 seconds with fade-out
                ParticleSystem[] particleSystems = explosion.GetComponentsInChildren<ParticleSystem>();
                foreach (var ps in particleSystems)
                {
                    // Stop the system first to allow property changes
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    
                    var main = ps.main;
                    main.duration = 20f;
                    main.startLifetime = 20f;
                    main.simulationSpeed = 2f; // Speed up particle animation
                    
                    // Enable size over lifetime for fade effect
                    var sizeOverLifetime = ps.sizeOverLifetime;
                    sizeOverLifetime.enabled = true;
                    AnimationCurve curve = new AnimationCurve();
                    curve.AddKey(0f, 1f); // Start at full size
                    curve.AddKey(0.8f, 1f); // Stay full size for 80% of lifetime
                    curve.AddKey(1f, 0f); // Shrink to nothing at end
                    sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
                    
                    // Stop emitting new particles after 15 seconds to create fade effect
                    StartCoroutine(StopEmissionAfterDelay(ps, 15f));
                    
                    // Play the system after setting properties
                    ps.Play();
                }
                
                // Destroy explosion after 25 seconds (5 extra seconds for particles to finish)
                Destroy(explosion, 25f);
            }
            Destroy(turretTransform.gameObject);
        }

        // Disable the tank (but keep it for visual reference)
        enabled = false;
    }
    
    /// <summary>
    /// Coroutine to stop particle emission after a delay for smooth fade-out
    /// </summary>
    private IEnumerator StopEmissionAfterDelay(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        ps.Stop();
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
            // Commented out to reduce log noise - only show AI action changes
            // Debug.Log($"[TankMan] {gameObject.name} NavState: {newState}");
            lastNavState = newState;
        }
    }

    // Helper to log sub-state transitions for MoveToWaypoint only when changed
    private void LogMoveSubState(NavMoveSubState newSubState)
    {
        if (lastMoveSubState != newSubState)
        {
            // Commented out to reduce log noise - only show AI action changes
            // Debug.Log($"[TankMan] {gameObject.name} MoveToWaypoint: {newSubState}");
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
    IEnumerator WanderAction(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
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
        
        // Unstuck the tank before starting movement
        UnstuckTank();
        yield return new WaitForFixedUpdate();

        // Pick a new wander target if needed
        if (!isWandering || ShouldPickNewWanderTarget())
        {
            SetNewWanderTarget();
            wanderStartTime = Time.time;
        }

        // Stuck detection variables for this wander action
        Vector3 lastWanderPosition = transform.position;
        float lastWanderPositionCheckTime = Time.time;
        float wanderStuckCheckInterval = 3f; // Check every 3 seconds
        float wanderStuckDistanceThreshold = 1.0f; // Must move at least 1 unit to not be considered stuck

        // Move towards wander target using force-driven system
        while (true)
        {
            if (!isGrounded)
            {
                yield return null;
                continue;
            }

            // Check for stuck - only when grounded since movement commands only work when grounded
            if (Time.time - lastWanderPositionCheckTime >= wanderStuckCheckInterval)
            {
                float distanceMoved = Vector3.Distance(
                    new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(lastWanderPosition.x, 0, lastWanderPosition.z));
                
                // If we barely moved, we're stuck - apply upward force
                if (distanceMoved < wanderStuckDistanceThreshold)
                {
                    float upwardForce = rb.mass * 50f; // Strong impulse to lift tank
                    rb.AddForce(Vector3.up * upwardForce, ForceMode.Impulse);
                    Debug.Log($"[{gameObject.name}] Unstuck force applied (moved {distanceMoved:F2}m in {wanderStuckCheckInterval}s)");
                }
                
                // Update position tracking
                lastWanderPosition = transform.position;
                lastWanderPositionCheckTime = Time.time;
            }

            Vector3 diff = currentWanderTarget - transform.position;
            diff.y = 0;
            float distance = diff.magnitude;
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
    
    IEnumerator MoveToTarget(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
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
        
        // Unstuck the tank before starting movement
        UnstuckTank();
        yield return new WaitForFixedUpdate();

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
    
    IEnumerator ChaseTarget(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
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
        
        // Unstuck the tank before starting movement
        UnstuckTank();
        yield return new WaitForFixedUpdate();

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

            // Chase continuously - AI tree conditions (like IfRange) decide when to stop
            // Use NavState_MoveToWaypoint for smooth movement toward target
            NavState_MoveToWaypoint(targetPosition);

            yield return new WaitForFixedUpdate();
        }
    }
    
    IEnumerator FleeFromTarget(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
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
        
        // Unstuck the tank before starting movement
        UnstuckTank();
        yield return new WaitForFixedUpdate();

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

            // Flee continuously - AI tree conditions (like IfRange) decide when to stop
            // Use NavState_MoveToWaypoint to move toward flee position (away from target)
            NavState_MoveToWaypoint(fleeTarget);

            yield return new WaitForFixedUpdate();
        }
    }
    
    IEnumerator MapCenterAction(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
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
        
        // Unstuck the tank before starting movement
        UnstuckTank();
        yield return new WaitForFixedUpdate();

        // Get the parent cycle node if this action is part of a cycle
        AiExecutableNode parentCycle = null;
        if (currentNavActionNode != null)
        {
            parentCycle = GetParentCycleNode(currentNavActionNode, runtimeNavAI);
            if (parentCycle == null && runtimeTurretAI != null)
            {
                parentCycle = GetParentCycleNode(currentNavActionNode, runtimeTurretAI);
            }
        }

        // Calculate map center from terrain bounds
        Vector3 mapCenter = GetMapCenter();
        
        // Navigate to map center
        while (true)
        {
            if (!isGrounded)
            {
                yield return null;
                continue;
            }

            float distance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                             new Vector3(mapCenter.x, 0, mapCenter.z));
            
            if (distance < wanderReachDistance)
            {
                // Reached map center - stop
                StopMovement();
                break;
            }

            // Use NavState_MoveToWaypoint to move toward map center
            NavState_MoveToWaypoint(mapCenter);

            yield return new WaitForFixedUpdate();
        }
    }
    
    IEnumerator HomeAction(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
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
        
        // Unstuck the tank before starting movement
        UnstuckTank();
        yield return new WaitForFixedUpdate();

        // Get the parent cycle node if this action is part of a cycle
        AiExecutableNode parentCycle = null;
        if (currentNavActionNode != null)
        {
            parentCycle = GetParentCycleNode(currentNavActionNode, runtimeNavAI);
            if (parentCycle == null && runtimeTurretAI != null)
            {
                parentCycle = GetParentCycleNode(currentNavActionNode, runtimeTurretAI);
            }
        }

        // Navigate to spawn position
        while (true)
        {
            if (!isGrounded)
            {
                yield return null;
                continue;
            }

            float distance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                             new Vector3(spawnPosition.x, 0, spawnPosition.z));
            
            if (distance < wanderReachDistance)
            {
                // Reached home - stop
                StopMovement();
                break;
            }

            // Use NavState_MoveToWaypoint to move toward home/spawn position
            NavState_MoveToWaypoint(spawnPosition);

            yield return new WaitForFixedUpdate();
        }
    }
    
            
        
    

    /// <summary>
    /// Calculates the center of the map from terrain bounds
    /// </summary>
    private Vector3 GetMapCenter()
    {
        // Try to find terrain in the scene
        Terrain terrain = Terrain.activeTerrain;
        
        if (terrain != null)
        {
            // Get terrain bounds and calculate center
            Vector3 terrainSize = terrain.terrainData.size;
            Vector3 terrainPos = terrain.transform.position;
            Vector3 center = terrainPos + new Vector3(terrainSize.x * 0.5f, 0, terrainSize.z * 0.5f);
            return center;
        }
        else
        {
            // Fallback: Use hardcoded map boundaries (30-770 range suggests 800x800 map)
            // Center would be at 400, 400
            return new Vector3(400f, 0f, 400f);
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
    
    IEnumerator WaitAction(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
        // Apply both brakes for 0.2 seconds (tank sits still, turret can still track)
        float waitDuration = 0.2f;
        float startTime = Time.time;
        while (Time.time - startTime < waitDuration)
        {
            NavState_Wait();
            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>
    /// Lead target action - aims at a point ahead of the target based on lead distance
    /// When leadDistance = 0, aims directly at target center (replaces CenterTarget)
    /// When leadDistance > 0, aims ahead of target's movement direction
    /// </summary>
    IEnumerator LeadTargetAction(float leadDistance, string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
        // Reset turret rotation ramp-up when starting to track
        turretRotationStartTime = Time.time;
        previousTurretRotation = turretTransform != null ? turretTransform.rotation : Quaternion.identity;
        currentTurretRotationSpeed = 0f;
        
        // Continuously rotate turret to face the lead point
        // This keeps running until the action is stopped by the AI system
        while (currentTarget != null && turretTransform != null)
        {
            Quaternion targetRotation;
            
            // Artillery turrets need special handling - aim at elevation angle
            if (turretType == TurretType.Artillery)
            {
                // Get target position (use BasePivot for accuracy)
                Transform basePivot = GetTargetBasePivot(currentTarget);
                Vector3 targetPosition = basePivot.position;
                
                // Calculate the artillery trajectory and launch angle
                float launchAngle;
                Vector3 horizontalDirection = CalculateArtilleryDirection(out launchAngle, targetPosition);
                
                // Apply the elevation angle to the turret
                // First rotate to face the target horizontally
                Vector3 horizontalDir = Vector3.ProjectOnPlane(horizontalDirection, Vector3.up);
                if (horizontalDir.magnitude > 0.1f)
                {
                    // Create rotation that faces target horizontally, then tilt up by launch angle
                    Quaternion horizontalRotation = Quaternion.LookRotation(horizontalDir);
                    // Apply elevation by rotating around the right axis
                    // Subtract 60 degrees to compensate for artillery model's default 60-degree upward tilt
                    float adjustedAngle = launchAngle - 60f;
                    targetRotation = horizontalRotation * Quaternion.Euler(-adjustedAngle, 0f, 0f);
                }
                else
                {
                    targetRotation = turretTransform.rotation;
                }
            }
            else
            {
                // Direct fire turrets - aim directly at lead point
                Vector3 leadPoint = CalculateLeadPoint(currentTarget, leadDistance);
                Vector3 targetDirection = leadPoint - turretTransform.position;
                
                if (targetDirection.magnitude > 0.1f)
                {
                    targetDirection.Normalize();
                    targetRotation = Quaternion.LookRotation(targetDirection);
                }
                else
                {
                    targetRotation = turretTransform.rotation;
                }
            }
            
            // Gradual ramp-up for turret rotation to prevent jumpiness
            float timeSinceRotationStart = Time.time - turretRotationStartTime;
            float rampProgress = Mathf.Clamp01(timeSinceRotationStart / turretRampUpTime);
            
            // Smoothly ramp up from 0 to full speed
            float targetSpeed = TurnSpeed * turretRotationSpeed;
            currentTurretRotationSpeed = Mathf.Lerp(0f, targetSpeed, rampProgress);
            
            // Apply rotation with ramped speed
            turretTransform.rotation = Quaternion.RotateTowards(
                turretTransform.rotation,
                targetRotation,
                currentTurretRotationSpeed * Time.deltaTime
            );
            
            previousTurretRotation = turretTransform.rotation;
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Legacy TrackTarget action for backward compatibility - calls LeadTargetAction with 0 lead
    /// </summary>
    IEnumerator TrackTargetAction(string nodeId, bool isNavAI)
    {
        yield return StartCoroutine(LeadTargetAction(0f, nodeId, isNavAI));
    }
    
    #region Turret Alignment Actions
    
    /// <summary>
    /// Aligns turret to face forward (Y=0) relative to the tank body
    /// </summary>
    IEnumerator AlignFrontAction(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
        if (turretTransform == null)
        {
            Debug.LogWarning("[AlignFront] No turret transform assigned!");
            yield break;
        }
        
        // Get the parent cycle node if this action is part of a cycle
        AiExecutableNode parentCycle = null;
        if (currentTurretActionNode != null)
        {
            parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeTurretAI);
            if (parentCycle == null && runtimeNavAI != null)
            {
                parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeNavAI);
            }
        }
        
        float targetYRotation = 0f; // Front = 0 degrees
        float threshold = 2f; // Consider aligned when within 2 degrees
        
        while (turretTransform != null)
        {
            // Get the tank's (parent) world rotation
            float tankWorldY = transform.eulerAngles.y;
            
            // Calculate target world rotation for turret
            float targetWorldY = tankWorldY + targetYRotation;
            
            // Get current turret world rotation
            float currentTurretY = turretTransform.eulerAngles.y;
            
            // Calculate shortest angle difference
            float angleDiff = Mathf.DeltaAngle(currentTurretY, targetWorldY);
            
            // Check if we're aligned
            if (Mathf.Abs(angleDiff) <= threshold)
            {
                // Aligned!
                yield break;
            }
            
            // Rotate towards target at 1/4 speed
            float rotationSpeed = TurnSpeed * 0.5f * Time.deltaTime;
            float newY = Mathf.MoveTowardsAngle(currentTurretY, targetWorldY, rotationSpeed);
            
            // Set X=0 (level pitch) and lock Z to parent tank's Z rotation
            float tankZ = transform.eulerAngles.z;
            turretTransform.rotation = Quaternion.Euler(0f, newY, tankZ);
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Aligns turret to face right (Y=90) relative to the tank body
    /// </summary>
    IEnumerator AlignRightAction(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
        if (turretTransform == null)
        {
            Debug.LogWarning("[AlignRight] No turret transform assigned!");
            yield break;
        }
        
        // Get the parent cycle node if this action is part of a cycle
        AiExecutableNode parentCycle = null;
        if (currentTurretActionNode != null)
        {
            parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeTurretAI);
            if (parentCycle == null && runtimeNavAI != null)
            {
                parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeNavAI);
            }
        }
        
        float targetYRotation = 90f; // Right = 90 degrees
        float threshold = 2f; // Consider aligned when within 2 degrees
        
        while (turretTransform != null)
        {
            // Get the tank's (parent) world rotation
            float tankWorldY = transform.eulerAngles.y;
            
            // Calculate target world rotation for turret
            float targetWorldY = tankWorldY + targetYRotation;
            
            // Get current turret world rotation
            float currentTurretY = turretTransform.eulerAngles.y;
            
            // Calculate shortest angle difference
            float angleDiff = Mathf.DeltaAngle(currentTurretY, targetWorldY);
            
            // Check if we're aligned
            if (Mathf.Abs(angleDiff) <= threshold)
            {
                // Aligned!
                yield break;
            }
            
            // Rotate towards target at 1/4 speed
            float rotationSpeed = TurnSpeed * 0.5f * Time.deltaTime;
            float newY = Mathf.MoveTowardsAngle(currentTurretY, targetWorldY, rotationSpeed);
            
            // Set X=0 (level pitch) and lock Z to parent tank's Z rotation
            float tankZ = transform.eulerAngles.z;
            turretTransform.rotation = Quaternion.Euler(0f, newY, tankZ);
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Aligns turret to face left (Y=-90) relative to the tank body
    /// </summary>
    IEnumerator AlignLeftAction(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
        if (turretTransform == null)
        {
            Debug.LogWarning("[AlignLeft] No turret transform assigned!");
            yield break;
        }
        
        // Get the parent cycle node if this action is part of a cycle
        AiExecutableNode parentCycle = null;
        if (currentTurretActionNode != null)
        {
            parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeTurretAI);
            if (parentCycle == null && runtimeNavAI != null)
            {
                parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeNavAI);
            }
        }
        
        float targetYRotation = -90f; // Left = -90 degrees
        float threshold = 2f; // Consider aligned when within 2 degrees
        
        while (turretTransform != null)
        {
            // Get the tank's (parent) world rotation
            float tankWorldY = transform.eulerAngles.y;
            
            // Calculate target world rotation for turret
            float targetWorldY = tankWorldY + targetYRotation;
            
            // Get current turret world rotation
            float currentTurretY = turretTransform.eulerAngles.y;
            
            // Calculate shortest angle difference
            float angleDiff = Mathf.DeltaAngle(currentTurretY, targetWorldY);
            
            // Check if we're aligned
            if (Mathf.Abs(angleDiff) <= threshold)
            {
                // Aligned!
                yield break;
            }
            
            // Rotate towards target at 1/4 speed
            float rotationSpeed = TurnSpeed * 0.5f * Time.deltaTime;
            float newY = Mathf.MoveTowardsAngle(currentTurretY, targetWorldY, rotationSpeed);
            
            // Set X=0 (level pitch) and lock Z to parent tank's Z rotation
            float tankZ = transform.eulerAngles.z;
            turretTransform.rotation = Quaternion.Euler(0f, newY, tankZ);
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Aligns turret to face back (Y=180) relative to the tank body
    /// </summary>
    IEnumerator AlignBackAction(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
        if (turretTransform == null)
        {
            Debug.LogWarning("[AlignBack] No turret transform assigned!");
            yield break;
        }
        
        // Get the parent cycle node if this action is part of a cycle
        AiExecutableNode parentCycle = null;
        if (currentTurretActionNode != null)
        {
            parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeTurretAI);
            if (parentCycle == null && runtimeNavAI != null)
            {
                parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeNavAI);
            }
        }
        
        float targetYRotation = 180f; // Back = 180 degrees
        float threshold = 2f; // Consider aligned when within 2 degrees
        
        while (turretTransform != null)
        {
            // Get the tank's (parent) world rotation
            float tankWorldY = transform.eulerAngles.y;
            
            // Calculate target world rotation for turret
            float targetWorldY = tankWorldY + targetYRotation;
            
            // Get current turret world rotation
            float currentTurretY = turretTransform.eulerAngles.y;
            
            // Calculate shortest angle difference
            float angleDiff = Mathf.DeltaAngle(currentTurretY, targetWorldY);
            
            // Check if we're aligned
            if (Mathf.Abs(angleDiff) <= threshold)
            {
                // Aligned!
                yield break;
            }
            
            // Rotate towards target at 1/4 speed
            float rotationSpeed = TurnSpeed * 0.5f * Time.deltaTime;
            float newY = Mathf.MoveTowardsAngle(currentTurretY, targetWorldY, rotationSpeed);
            
            // Set X=0 (level pitch) and lock Z to parent tank's Z rotation
            float tankZ = transform.eulerAngles.z;
            turretTransform.rotation = Quaternion.Euler(0f, newY, tankZ);
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Rotates turret up by specified degrees relative to current pitch
    /// </summary>
    IEnumerator RotateUpAction(float degrees, string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
        if (turretTransform == null)
        {
            Debug.LogWarning("[RotateUp] No turret transform assigned!");
            yield break;
        }
        
        // Get the parent cycle node if this action is part of a cycle
        AiExecutableNode parentCycle = null;
        if (currentTurretActionNode != null)
        {
            parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeTurretAI);
            if (parentCycle == null && runtimeNavAI != null)
            {
                parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeNavAI);
            }
        }
        
        // Get current X rotation (pitch)
        float currentX = turretTransform.eulerAngles.x;
        // Convert from 0-360 to -180 to 180 range
        if (currentX > 180f) currentX -= 360f;
        
        // Calculate target X rotation (subtract degrees for pitch up)
        float targetX = currentX - degrees;
        
        // Clamp pitch to reasonable limits (e.g., -45 to 45 degrees)
        targetX = Mathf.Clamp(targetX, -45f, 45f);
        
        float threshold = 2f; // Consider reached when within 2 degrees
        
        while (turretTransform != null)
        {
            // Get current pitch
            currentX = turretTransform.eulerAngles.x;
            if (currentX > 180f) currentX -= 360f;
            
            // Calculate difference
            float angleDiff = targetX - currentX;
            
            // Check if we've reached the target
            if (Mathf.Abs(angleDiff) <= threshold)
            {
                // Reached!
                yield break;
            }
            
            // Rotate towards target
            float rotationSpeed = TurnSpeed * 0.5f * Time.deltaTime;
            float newX = Mathf.MoveTowards(currentX, targetX, rotationSpeed);
            
            // Maintain current Y (yaw) and lock Z to parent tank's Z rotation
            float currentY = turretTransform.eulerAngles.y;
            float tankZ = transform.eulerAngles.z;
            turretTransform.rotation = Quaternion.Euler(newX, currentY, tankZ);
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Rotates turret down by specified degrees relative to current pitch
    /// </summary>
    IEnumerator RotateDownAction(float degrees, string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
        if (turretTransform == null)
        {
            Debug.LogWarning("[RotateDown] No turret transform assigned!");
            yield break;
        }
        
        // Get the parent cycle node if this action is part of a cycle
        AiExecutableNode parentCycle = null;
        if (currentTurretActionNode != null)
        {
            parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeTurretAI);
            if (parentCycle == null && runtimeNavAI != null)
            {
                parentCycle = GetParentCycleNode(currentTurretActionNode, runtimeNavAI);
            }
        }
        
        // Get current X rotation (pitch)
        float currentX = turretTransform.eulerAngles.x;
        // Convert from 0-360 to -180 to 180 range
        if (currentX > 180f) currentX -= 360f;
        
        // Calculate target X rotation (add degrees for pitch down)
        float targetX = currentX + degrees;
        
        // Clamp pitch to reasonable limits (e.g., -45 to 45 degrees)
        targetX = Mathf.Clamp(targetX, -45f, 45f);
        
        float threshold = 2f; // Consider reached when within 2 degrees
        
        while (turretTransform != null)
        {
            // Get current pitch
            currentX = turretTransform.eulerAngles.x;
            if (currentX > 180f) currentX -= 360f;
            
            // Calculate difference
            float angleDiff = targetX - currentX;
            
            // Check if we've reached the target
            if (Mathf.Abs(angleDiff) <= threshold)
            {
                // Reached!
                yield break;
            }
            
            // Rotate towards target
            float rotationSpeed = TurnSpeed * 0.5f * Time.deltaTime;
            float newX = Mathf.MoveTowards(currentX, targetX, rotationSpeed);
            
            // Maintain current Y (yaw) and lock Z to parent tank's Z rotation
            float currentY = turretTransform.eulerAngles.y;
            float tankZ = transform.eulerAngles.z;
            turretTransform.rotation = Quaternion.Euler(newX, currentY, tankZ);
            
            yield return null;
        }
    }
    
    #region Navigation Actions
    
    /// <summary>
    /// Propels the tank forward continuously while active
    /// </summary>
    IEnumerator ForwardAction(string nodeId, bool isNavAI)
    {
        // Update the last used node ID to track that we've moved to a different action
        if (isNavAI)
            lastUsedNavNodeId = nodeId;
        else
            lastUsedTurretNodeId = nodeId;
        
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
        
        // Unstuck the tank before starting movement
        UnstuckTank();
        yield return new WaitForFixedUpdate();
        
        // Continuously move forward while this action is active
        while (true)
        {
            if (!isGrounded)
            {
                yield return null;
                continue;
            }
            
            // Move forward at full speed
            SetMovementInput(1f, 0f);
            yield return new WaitForFixedUpdate();
        }
    }
    
    /// <summary>
    /// Rotates the tank right by the specified number of degrees
    /// </summary>
    IEnumerator RotateRightAction(float degrees, string nodeId, bool isNavAI)
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
        
        // Unstuck the tank before starting rotation
        UnstuckTank();
        yield return new WaitForFixedUpdate();
        
        // If degrees is 0, rotate continuously (no target angle)
        if (degrees == 0f)
        {
            while (true)
            {
                if (!isGrounded)
                {
                    yield return null;
                    continue;
                }
                
                // Continuous right rotation
                SetMovementInput(0f, 1f);
                yield return new WaitForFixedUpdate();
            }
        }
        
        // Get the parent cycle node if this action is part of a cycle
        AiExecutableNode parentCycle = null;
        if (currentNavActionNode != null)
        {
            parentCycle = GetParentCycleNode(currentNavActionNode, runtimeNavAI);
            if (parentCycle == null && runtimeTurretAI != null)
            {
                parentCycle = GetParentCycleNode(currentNavActionNode, runtimeTurretAI);
            }
        }
        
        // Check if this is the same node as last time - if so, reuse the target angle
        string lastUsedNodeId = isNavAI ? lastUsedNavNodeId : lastUsedTurretNodeId;
        float targetYRotation;
        
        if (lastUsedNodeId == nodeId && storedRotationTarget != 0f)
        {
            // Same node - reuse the stored target
            targetYRotation = storedRotationTarget;
            Debug.Log($"[{gameObject.name}] RotateRight reusing target for node {nodeId}: targetY={targetYRotation:F1}");
        }
        else
        {
            // Different node or first time - calculate new target
            float startYRotation = transform.eulerAngles.y;
            targetYRotation = startYRotation + degrees; // Positive Y = right turn (clockwise from above)
            
            // Store for this specific node
            if (isNavAI)
                lastUsedNavNodeId = nodeId;
            else
                lastUsedTurretNodeId = nodeId;
            
            storedRotationTarget = targetYRotation;
            
            Debug.Log($"[{gameObject.name}] RotateRight NEW target for node {nodeId}: startY={startYRotation:F1}, degrees={degrees}, targetY={targetYRotation:F1}");
        }
        
        // Continuously try to face the target direction
        while (true)
        {
            if (!isGrounded)
            {
                yield return null;
                continue;
            }
            
            // Get current rotation
            float currentY = transform.eulerAngles.y;
            
            // Calculate the shortest angle difference to target
            float angleDiff = Mathf.DeltaAngle(currentY, targetYRotation);
            
            Debug.Log($"[{gameObject.name}] RotateRight: currentY={currentY:F1}, targetY={targetYRotation:F1}, angleDiff={angleDiff:F1}");
            
            // Rotate towards target (never stop trying)
            float turnDirection = Mathf.Sign(angleDiff);
            float turnIntensity = Mathf.Clamp01(Mathf.Abs(angleDiff) / 45f); // Scale turn intensity
            SetMovementInput(0f, turnDirection * turnIntensity);
            
            yield return new WaitForFixedUpdate();
        }
    }
    
    /// <summary>
    /// Rotates the tank left by the specified number of degrees
    /// </summary>
    IEnumerator RotateLeftAction(float degrees, string nodeId, bool isNavAI)
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
        
        // Unstuck the tank before starting rotation
        UnstuckTank();
        yield return new WaitForFixedUpdate();
        
        // If degrees is 0, rotate continuously (no target angle)
        if (degrees == 0f)
        {
            while (true)
            {
                if (!isGrounded)
                {
                    yield return null;
                    continue;
                }
                
                // Continuous left rotation
                SetMovementInput(0f, -1f);
                yield return new WaitForFixedUpdate();
            }
        }
        
        // Get the parent cycle node if this action is part of a cycle
        AiExecutableNode parentCycle = null;
        if (currentNavActionNode != null)
        {
            parentCycle = GetParentCycleNode(currentNavActionNode, runtimeNavAI);
            if (parentCycle == null && runtimeTurretAI != null)
            {
                parentCycle = GetParentCycleNode(currentNavActionNode, runtimeTurretAI);
            }
        }
        
        // Check if this is the same node as last time - if so, reuse the target angle
        string lastUsedNodeId = isNavAI ? lastUsedNavNodeId : lastUsedTurretNodeId;
        float targetYRotation;
        
        if (lastUsedNodeId == nodeId && storedRotationTarget != 0f)
        {
            // Same node - reuse the stored target
            targetYRotation = storedRotationTarget;
            Debug.Log($"[{gameObject.name}] RotateLeft reusing target for node {nodeId}: targetY={targetYRotation:F1}");
        }
        else
        {
            // Different node or first time - calculate new target
            float startYRotation = transform.eulerAngles.y;
            targetYRotation = startYRotation - degrees; // Negative Y = left turn (counter-clockwise from above)
            
            // Store for this specific node
            if (isNavAI)
                lastUsedNavNodeId = nodeId;
            else
                lastUsedTurretNodeId = nodeId;
            
            storedRotationTarget = targetYRotation;
            
            Debug.Log($"[{gameObject.name}] RotateLeft NEW target for node {nodeId}: startY={startYRotation:F1}, degrees={degrees}, targetY={targetYRotation:F1}");
        }
        
        // Continuously try to face the target direction
        while (true)
        {
            if (!isGrounded)
            {
                yield return null;
                continue;
            }
            
            // Get current rotation
            float currentY = transform.eulerAngles.y;
            
            // Calculate the shortest angle difference to target
            float angleDiff = Mathf.DeltaAngle(currentY, targetYRotation);
            
            // Rotate towards target (never stop trying)
            float turnDirection = Mathf.Sign(angleDiff);
            float turnIntensity = Mathf.Clamp01(Mathf.Abs(angleDiff) / 45f); // Scale turn intensity
            SetMovementInput(0f, turnDirection * turnIntensity);
            
            yield return new WaitForFixedUpdate();
        }
    }
    
    #endregion
    
    #endregion
    
    #region Private Tag System
    
    /// <summary>
    /// Sets or updates a private tag for a target.
    /// Private tags are only visible to this tank.
    /// </summary>
    /// <param name="targetGameObject">The target to tag</param>
    /// <param name="tagValue">The tag value to assign</param>
    private void SetPrivateTag(GameObject targetGameObject, int tagValue)
    {
        if (targetGameObject == null) return;
        
        int targetId = targetGameObject.GetInstanceID();
        
        if (privateTagList.ContainsKey(targetId))
        {
            privateTagList[targetId] = tagValue;
        }
        else
        {
            privateTagList.Add(targetId, tagValue);
        }
    }
    
    /// <summary>
    /// Gets the private tag for a target.
    /// </summary>
    /// <param name="targetGameObject">The target to get the tag for</param>
    /// <returns>The tag value, or -1 if no tag exists</returns>
    private int GetPrivateTag(GameObject targetGameObject)
    {
        if (targetGameObject == null) return -1;
        
        int targetId = targetGameObject.GetInstanceID();
        
        if (privateTagList.ContainsKey(targetId))
        {
            return privateTagList[targetId];
        }
        
        return -1; // No tag exists
    }
    
    /// <summary>
    /// Checks if a private tag exists for a target.
    /// </summary>
    /// <param name="targetGameObject">The target to check</param>
    /// <returns>True if a tag exists</returns>
    private bool HasPrivateTag(GameObject targetGameObject)
    {
        if (targetGameObject == null) return false;
        
        int targetId = targetGameObject.GetInstanceID();
        return privateTagList.ContainsKey(targetId);
    }
    
    /// <summary>
    /// Removes a private tag for a target.
    /// </summary>
    /// <param name="targetGameObject">The target to remove the tag for</param>
    private void RemovePrivateTag(GameObject targetGameObject)
    {
        if (targetGameObject == null) return;
        
        int targetId = targetGameObject.GetInstanceID();
        privateTagList.Remove(targetId);
    }
    
    /// <summary>
    /// Clears all private tags.
    /// </summary>
    private void ClearPrivateTags()
    {
        privateTagList.Clear();
    }
    
    #endregion
}
