using UnityEngine;

public class SimpleTankController : MonoBehaviour
{
    [Header("Movement Forces")]
    [SerializeField] private float linearForceMultiplier = 1000f;
    [SerializeField] private float angularForceMultiplier = 1500f;
    
    [Header("Speed Limits")]
    [SerializeField] private float maxLinearSpeed = 20f;
    [SerializeField] private float maxAngularSpeed = 90f; // degrees per second
    
    [Header("Physics Settings")]
    [SerializeField] private float gravityScale = 1f;
    [SerializeField] private float linearDamping = 0.5f;
    [SerializeField] private float angularDamping = 2f;
    [SerializeField] private float tankMass = 10f;
    
    [Header("Navigation")]
    [SerializeField] private float stoppingDistance = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    
    private Rigidbody rb;
    private Vector3 targetWaypoint;
    private Vector3 startPosition;
    
    void Start()
    {
        // Get or add rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // Configure rigidbody
        SetupRigidbody();
        
        // Set initial waypoint 100 units to the right at current Y level
        startPosition = transform.position;
        targetWaypoint = new Vector3(startPosition.x + 100f, transform.position.y, startPosition.z);
        
        Debug.Log($"SimpleTankController: Starting at {startPosition}, target waypoint: {targetWaypoint}");
    }
    
    void SetupRigidbody()
    {
        Debug.Log($"Before setup: Mass={rb.mass}, setting to tankMass={tankMass}");
        
        rb.mass = tankMass;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.useGravity = true;
        
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Constrain rotation to prevent flipping
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        
        Debug.Log($"After setup: Mass={rb.mass}, LinearDamping={rb.linearDamping}, AngularDamping={rb.angularDamping}");
    }
    
    void FixedUpdate()
    {
        // Apply custom gravity scale if different from 1
        if (gravityScale != 1f)
        {
            Vector3 customGravity = Physics.gravity * (gravityScale - 1f);
            rb.AddForce(customGravity, ForceMode.Acceleration);
        }
        
        NavigateToWaypoint();
    }
    
    void NavigateToWaypoint()
    {
        // Calculate direction to target
        Vector3 directionToTarget = (targetWaypoint - transform.position);
        directionToTarget.y = 0f; // Keep movement on horizontal plane
        float distanceToTarget = directionToTarget.magnitude;
        
        // Check if we've reached the waypoint
        if (distanceToTarget <= stoppingDistance)
        {
            LoopWaypoint();
            return;
        }
        
        // Calculate desired rotation
        if (directionToTarget.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.y;
            float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);
            float absAngleDifference = Mathf.Abs(angleDifference);
            
            // Movement logic based on angle to target
            if (absAngleDifference > 40f)
            {
                // Only apply angular force (turn in place)
                float turnDirection = Mathf.Sign(angleDifference);
                float turnIntensity = Mathf.Clamp01(absAngleDifference / 45f);
                ApplyTurn(turnDirection * turnIntensity);
            }
            else if (absAngleDifference > 5f)
            {
                // Apply both linear and angular force
                float turnDirection = Mathf.Sign(angleDifference);
                float turnIntensity = Mathf.Clamp01(absAngleDifference / 45f);
                ApplyTurn(turnDirection * turnIntensity);
                
                // Reduce forward movement while turning
                float forwardMultiplier = 1f - (absAngleDifference / 90f);
                ApplyForward(forwardMultiplier);
            }
            else
            {
                // Only apply linear force (move straight)
                ApplyForward(1f);
            }
        }
    }
    
    void ApplyForward(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);
        
        // Check speed limit
        float currentSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (currentSpeed < maxLinearSpeed)
        {
            Vector3 forwardForce = transform.forward * (linearForceMultiplier * intensity);
            rb.AddForce(forwardForce, ForceMode.Force);
        }
    }
    
    void ApplyTurn(float intensity)
    {
        intensity = Mathf.Clamp(intensity, -1f, 1f);
        
        // Check angular speed limit
        float currentAngularSpeed = rb.angularVelocity.y * Mathf.Rad2Deg;
        if (Mathf.Abs(currentAngularSpeed) < maxAngularSpeed)
        {
            Vector3 torque = Vector3.up * (angularForceMultiplier * intensity);
            rb.AddTorque(torque, ForceMode.Force);
        }
    }
    
    void LoopWaypoint()
    {
        // Calculate distances ignoring Y value (only X and Z)
        Vector3 currentPosXZ = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 startPosXZ = new Vector3(startPosition.x, 0, startPosition.z);
        Vector3 rightWaypointXZ = new Vector3(startPosition.x + 100f, 0, startPosition.z);
        
        float distanceToStart = Vector3.Distance(currentPosXZ, startPosXZ);
        float distanceToRight = Vector3.Distance(currentPosXZ, rightWaypointXZ);
        
        // Switch between start position and 100 units right
        // Set waypoints at the tank's current Y level to avoid Y-axis issues
        if (distanceToStart < distanceToRight)
        {
            // Currently near start, go to right waypoint
            targetWaypoint = new Vector3(startPosition.x + 100f, transform.position.y, startPosition.z);
        }
        else
        {
            // Currently near right waypoint, go back to start
            targetWaypoint = new Vector3(startPosition.x, transform.position.y, startPosition.z);
        }
        
        Debug.Log($"Reached waypoint! New target: {targetWaypoint}");
    }
    
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Draw waypoint
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(targetWaypoint, stoppingDistance);
        
        // Draw line to target
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, targetWaypoint);
        
        // Draw forward direction
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 5f);
        
        // Draw start and end positions
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(startPosition, Vector3.one);
            Gizmos.DrawWireCube(startPosition + Vector3.right * 100f, Vector3.one);
        }
    }
    
    void OnGUI()
    {
        if (!showDebugGizmos) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Distance to waypoint: {Vector3.Distance(transform.position, targetWaypoint):F1}");
        GUILayout.Label($"Current speed: {rb.linearVelocity.magnitude:F1}");
        GUILayout.Label($"Angular speed: {(rb.angularVelocity.y * Mathf.Rad2Deg):F1}°/s");
        GUILayout.Label($"Current waypoint: {targetWaypoint}");
        GUILayout.EndArea();
    }
}
