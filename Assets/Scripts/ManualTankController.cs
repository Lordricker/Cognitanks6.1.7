using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manual tank physics controller for testing and tuning tank movement feel.
/// This script provides direct player control with configurable physics parameters.
/// Uses component-based system where engine power/torque determine movement.
/// 
/// Instructions:
/// 1. Add this script to a GameObject with a Rigidbody
/// 2. Configure the engine and component weights in the inspector
/// 3. Use WASD to control the tank
/// 4. Enable "Show Debug Info" to see real-time values
/// 
/// Physics Model:
/// - Engine power (N) / Total mass (kg) = Acceleration
/// - Higher mass = slower acceleration with same engine
/// - Top speeds are mechanical limits (not affected by mass)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ManualTankController : MonoBehaviour
{
    [Header("Component Weights")]
    [SerializeField] private float chassisWeight = 50f;   // kg
    [SerializeField] private float armorWeight = 20f;     // kg
    [SerializeField] private float turretWeight = 15f;    // kg
    [SerializeField] private float engineWeight = 15f;    // kg
    
    [Header("Engine Parameters")]
    [SerializeField] private float enginePower = 15000f;      // N (Newtons) - force output for forward movement
    [SerializeField] private float topSpeed = 15f;            // m/s - maximum forward speed (mechanical limit)
    [SerializeField] private float turningPower = 3000f;      // N·m (Newton-meters) - torque for rotation
    [SerializeField] private float maxTurnRate = 120f;        // deg/s - maximum turn speed (mechanical limit)
    [SerializeField] private float turnRampUpTime = 1.0f;     // seconds - time to reach full turning power
    [SerializeField] private float turnStartPowerPercent = 0.5f;  // 0-1 - starting power percent (0.5 = 50%)
    
    [Header("Physics Settings")]
    [SerializeField] private float dragCoefficient = 0.5f;         // Rolling resistance
    [SerializeField] private float angularDragCoefficient = 2.0f;  // Turn resistance
    
    [Header("Input System Keys")]
    [SerializeField] private Key forwardKey = Key.W;
    [SerializeField] private Key leftKey = Key.A;
    [SerializeField] private Key rightKey = Key.D;
    
    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField, ReadOnly] private float totalMass;
    [SerializeField, ReadOnly] private float currentSpeed;
    [SerializeField, ReadOnly] private float currentAngularSpeed;
    [SerializeField, ReadOnly] private float currentAcceleration;
    [SerializeField, ReadOnly] private float currentAngularAcceleration;
    
    // Components
    private Rigidbody rb;
    
    // Input state
    private float forwardInput;
    private float turnInput;
    
    // Turning ramp-up tracking
    private float currentTurningPower = 0f;
    private float turnInputStartTime = 0f;
    
    void Start()
    {
        // Get or create rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // Calculate total mass and configure physics
        CalculateTotalMass();
        ApplyPhysicsSettings();
        
        Debug.Log($"Tank ready! Mass: {totalMass}kg, Power: {enginePower}N, Torque: {turningPower}N·m");
    }
    
    // Called when values change in the editor
    void OnValidate()
    {
        // Calculate total mass from all components
        totalMass = chassisWeight + armorWeight + turretWeight + engineWeight;
        
        // Sync mass to rigidbody when changed in inspector (editor only)
        if (rb != null && !Application.isPlaying)
        {
            rb.mass = totalMass;
        }
    }
    
    void Update()
    {
        // Handle input
        HandleInput();
        
        // Recalculate mass if component weights changed during runtime
        float newTotalMass = chassisWeight + armorWeight + turretWeight + engineWeight;
        if (rb != null && Mathf.Abs(rb.mass - newTotalMass) > 0.01f)
        {
            totalMass = newTotalMass;
            rb.mass = totalMass;
        }
        
        // Update debug info
        if (showDebugInfo)
        {
            UpdateDebugInfo();
        }
    }
    
    void FixedUpdate()
    {
        // Apply physics-based movement
        ApplyMovementForces();
        ApplyTurningForces();
        
        // Limit speed to prevent unrealistic behavior
        LimitSpeeds();
    }
    
    private void HandleInput()
    {
        // Get the current keyboard state
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        
        // Forward input only
        forwardInput = keyboard[forwardKey].isPressed ? 1f : 0f;
        
        // Turning input
        float previousTurnInput = turnInput;
        turnInput = 0f;
        if (keyboard[rightKey].isPressed)
            turnInput = 1f;
        else if (keyboard[leftKey].isPressed)
            turnInput = -1f;
        
        // Reset timer when starting to turn from stopped
        if (Mathf.Abs(previousTurnInput) < 0.1f && Mathf.Abs(turnInput) > 0.1f)
        {
            turnInputStartTime = Time.time;
        }
    }
    
    private void ApplyMovementForces()
    {
        if (forwardInput > 0.1f)
        {
            // Apply engine power as force
            // Heavy tanks accelerate slower with same engine (F = ma, so a = F/m)
            float force = enginePower * forwardInput;
            rb.AddForce(transform.forward * force);
            
            // Calculate actual acceleration for debug display
            currentAcceleration = force / rb.mass;
        }
        else
        {
            currentAcceleration = 0f;
        }
    }
    
    private void ApplyTurningForces()
    {
        if (Mathf.Abs(turnInput) > 0.1f)
        {
            // Gradually ramp up turning power over turnRampUpTime
            float timeSinceTurnStart = Time.time - turnInputStartTime;
            float rampProgress = Mathf.Clamp01(timeSinceTurnStart / turnRampUpTime);
            
            // Start at configured power percent, ramp linearly to 100%
            // This gives immediate response while still having gradual build-up
            float powerPercent = Mathf.Lerp(turnStartPowerPercent, 1.0f, rampProgress);
            
            // Calculate target turning power with ramp-up
            float targetPower = turningPower * powerPercent;
            currentTurningPower = Mathf.Lerp(currentTurningPower, targetPower, Time.fixedDeltaTime * 10f);
            
            // Apply torque with current (ramped) power
            float torque = currentTurningPower * turnInput;
            rb.AddTorque(Vector3.up * torque);
            
            // Calculate actual angular acceleration for debug
            currentAngularAcceleration = (torque / rb.mass) * Mathf.Rad2Deg;
        }
        else
        {
            // Reset when not turning
            currentTurningPower = 0f;
            currentAngularAcceleration = 0f;
            turnInputStartTime = Time.time; // Reset timer for next turn
        }
    }
    
    private void LimitSpeeds()
    {
        // Limit linear velocity to engine's top speed
        if (rb.linearVelocity.magnitude > topSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * topSpeed;
        }
        
        // Limit angular velocity to max turn rate
        float maxAngularSpeedRad = maxTurnRate * Mathf.Deg2Rad;
        if (rb.angularVelocity.magnitude > maxAngularSpeedRad)
        {
            rb.angularVelocity = rb.angularVelocity.normalized * maxAngularSpeedRad;
        }
    }
    
    private void CalculateTotalMass()
    {
        totalMass = chassisWeight + armorWeight + turretWeight + engineWeight;
    }
    
    private void ApplyPhysicsSettings()
    {
        // Calculate and set mass from components
        CalculateTotalMass();
        rb.mass = totalMass;
        
        // Apply drag coefficients (simulates rolling resistance and turn friction)
        rb.linearDamping = dragCoefficient;
        rb.angularDamping = angularDragCoefficient;
        
        // Tank-specific settings
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Prevent tank from tipping over (tanks don't flip easily)
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        
        // Lower center of mass for stability
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }
    
    private void UpdateDebugInfo()
    {
        // Calculate current speeds for inspector display
        currentSpeed = rb.linearVelocity.magnitude;
        currentAngularSpeed = rb.angularVelocity.magnitude * Mathf.Rad2Deg;
    }
    
    // Public methods for runtime tuning (can be called from other scripts or inspector events)
    public void SetEnginePower(float power) => enginePower = power;
    public void SetTopSpeed(float speed) => topSpeed = speed;
    public void SetTurningPower(float power) => turningPower = power;
    public void SetMaxTurnRate(float rate) => maxTurnRate = rate;
    
    public void SetComponentWeights(float chassis, float armor, float turret, float engine)
    {
        chassisWeight = chassis;
        armorWeight = armor;
        turretWeight = turret;
        engineWeight = engine;
        CalculateTotalMass();
        if (rb != null) rb.mass = totalMass;
    }
    
    // Debug display (only shows if showDebugInfo is true)
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 350, 500));
        
        GUILayout.Label("=== TANK PHYSICS DEBUG ===");
        GUILayout.Label($"Speed: {currentSpeed:F1} / {topSpeed:F1} m/s");
        GUILayout.Label($"Turn Speed: {currentAngularSpeed:F1} / {maxTurnRate:F1} deg/s");
        GUILayout.Space(10);
        
        GUILayout.Label("=== COMPONENT MASS ===");
        GUILayout.Label($"Chassis: {chassisWeight:F0} kg");
        GUILayout.Label($"Armor: {armorWeight:F0} kg");
        GUILayout.Label($"Turret: {turretWeight:F0} kg");
        GUILayout.Label($"Engine: {engineWeight:F0} kg");
        GUILayout.Label($"TOTAL: {totalMass:F0} kg");
        GUILayout.Space(10);
        
        GUILayout.Label("=== ENGINE PERFORMANCE ===");
        GUILayout.Label($"Power: {enginePower:F0} N");
        GUILayout.Label($"Current Accel: {currentAcceleration:F1} m/s²");
        GUILayout.Label($"Power/Weight Ratio: {(enginePower / totalMass):F1} N/kg");
        GUILayout.Label($"Max Torque: {turningPower:F0} N·m");
        GUILayout.Label($"Current Torque: {currentTurningPower:F0} N·m ({(currentTurningPower/turningPower*100f):F0}%)");
        GUILayout.Label($"Current Angular Accel: {currentAngularAcceleration:F1} deg/s²");
        GUILayout.Label($"Turn Ramp Time: {turnRampUpTime:F2}s");
        GUILayout.Space(10);
        
        GUILayout.Label("=== PHYSICS SETTINGS ===");
        GUILayout.Label($"Linear Drag: {rb.linearDamping:F2}");
        GUILayout.Label($"Angular Drag: {rb.angularDamping:F2}");
        GUILayout.Space(10);
        
        GUILayout.Label("=== INPUT ===");
        GUILayout.Label($"Forward: {forwardInput:F1}");
        GUILayout.Label($"Turn: {turnInput:F1}");
        
        GUILayout.EndArea();
    }
}

/// <summary>
/// ReadOnly attribute for inspector display (makes fields visible but not editable)
/// </summary>
public class ReadOnlyAttribute : PropertyAttribute { }