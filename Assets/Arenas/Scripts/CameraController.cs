using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float lerpSpeed = 5f;
    public List<Transform> cameraAnchors = new List<Transform>();
    private int currentAnchorIndex = 0;
    private Transform targetAnchor;

    // Separate tracking for turret cameras
    private List<Transform> turretAnchors = new List<Transform>();
    private int currentTurretIndex = 0;

    [Header("UI Buttons")]
    public Button globalCamButton;
    public Button cycleTankCamButton;

    private Vector3 dragOrigin;
    private bool isDragging = false;
    public float orbitSensitivity = 0.005f;

    public Transform globalAnchor; // Assign in inspector for global view

    /// <summary>Returns true when the camera is currently on the global overview anchor.</summary>
    public bool IsGlobalCamera => targetAnchor != null && targetAnchor == globalAnchor;

    [Header("Global Camera Zoom (FOV)")]
    public float zoomSpeed = 15f;
    public float minFOV = 10f;
    public float maxFOV = 60f;
    private float targetFOV = 60f;
    private Camera cam;

    private Vector3 smoothVelocity = Vector3.zero;
    private float smoothTime = 0.15f; // Smoothing time for camera follow
    private Quaternion targetRotation;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        targetFOV = maxFOV;

        RefreshAnchors();
        if (cameraAnchors.Count > 0)
            SetTargetAnchor(cameraAnchors[0]);
        if (globalCamButton != null)
            globalCamButton.onClick.AddListener(MoveToGlobalAnchor);
        // Removed cycleTankCamButton listener to avoid double calls - handled by ArenaUIManager
    }

    public void RefreshAnchors()
    {
        cameraAnchors.Clear();
        turretAnchors.Clear();
        // Add both tank anchors and turret anchors to the cycle list
        foreach (var anchor in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (anchor.name == "CameraAnchor")
                cameraAnchors.Add(anchor);
            else if (anchor.name == "TurretCameraAnchor")
                turretAnchors.Add(anchor);
        }
        Debug.Log($"[CameraController] Found {cameraAnchors.Count} camera anchors and {turretAnchors.Count} turret anchors");
        currentAnchorIndex = 0;
        currentTurretIndex = 0;
        if (globalAnchor != null)
            SetTargetAnchor(globalAnchor);
        else if (cameraAnchors.Count > 0)
            SetTargetAnchor(cameraAnchors[0]);
    }

    void LateUpdate()
    {
        if (cameraAnchors.Count == 0) return;

        // Handle scroll wheel zoom via FOV (only for global camera)
        bool isGlobalCamera = targetAnchor != null && targetAnchor == globalAnchor;
        if (isGlobalCamera && !EventSystem.current.IsPointerOverGameObject())
        {
            float scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                // Scroll up = zoom in (lower FOV), scroll down = zoom out (higher FOV)
                targetFOV -= scrollDelta * zoomSpeed * Time.unscaledDeltaTime;
                targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
            }
        }

        // Reset FOV when switching away from global camera
        if (!isGlobalCamera)
            targetFOV = maxFOV;

        // Smoothly lerp the camera FOV
        if (cam != null)
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.unscaledDeltaTime * 10f);

        // Smooth follow position and rotation
        if (targetAnchor != null)
        {
            // SmoothDamp for position
            transform.position = Vector3.SmoothDamp(transform.position, targetAnchor.position, ref smoothVelocity, smoothTime);
            // Slerp for rotation
            targetRotation = targetAnchor.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.unscaledDeltaTime * lerpSpeed);
        }        // Mouse drag to orbit
        if (Mouse.current.leftButton.wasPressedThisFrame && !EventSystem.current.IsPointerOverGameObject())
        {
            dragOrigin = Mouse.current.position.ReadValue();
            isDragging = true;
        }
        
        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }        // Only check for UI interference at the start of drag, not during
        if (isDragging && targetAnchor != null)
        {
            Vector3 currentMousePos = Mouse.current.position.ReadValue();
            Vector3 delta = currentMousePos - dragOrigin;
            dragOrigin = currentMousePos;
            
            // Apply rotation for any mouse movement
            if (delta.magnitude > 0f)
            {
                // Calculate sensitivity so full screen width = 45 degrees
                // Screen.width pixels should equal 90 degrees of rotation
                float screenToDegreesRatio = 90f / Screen.width;
                float yaw = delta.x * screenToDegreesRatio;
                float pitch = -delta.y * screenToDegreesRatio;
                
                targetAnchor.Rotate(Vector3.up, yaw, Space.World);
                targetAnchor.Rotate(Vector3.right, pitch, Space.Self);
                
                // Lock Z-axis rotation to prevent camera roll
                Vector3 euler = targetAnchor.localEulerAngles;
                targetAnchor.localEulerAngles = new Vector3(euler.x, euler.y, 0f);
            }
        }
    }    public void SetTargetAnchor(Transform anchor)
    {
        targetAnchor = anchor;
    }

    public void MoveToGlobalAnchor()
    {
        SetTargetAnchor(globalAnchor);
    }    public void CycleTankAnchor()
    {
        if (cameraAnchors.Count == 0) return;
        currentAnchorIndex = (currentAnchorIndex + 1) % cameraAnchors.Count;
        SetTargetAnchor(cameraAnchors[currentAnchorIndex]);
        
        // Use the anchor rotation as set by TankAssembly (no override needed)
        Debug.Log($"[CameraController] Switched to tank anchor: {cameraAnchors[currentAnchorIndex].name} ({currentAnchorIndex + 1}/{cameraAnchors.Count})");
    }

    public void SwitchToTurretCamera()
    {
        if (turretAnchors.Count == 0)
        {
            Debug.LogWarning("[CameraController] No turret anchors found! Cannot switch to turret camera.");
            return;
        }
        
        // Cycle through turret cameras like CycleTankAnchor does for all cameras
        currentTurretIndex = (currentTurretIndex + 1) % turretAnchors.Count;
        SetTargetAnchor(turretAnchors[currentTurretIndex]);
        
        Debug.Log($"[CameraController] Switched to TURRET camera: {turretAnchors[currentTurretIndex].name} (Parent: {turretAnchors[currentTurretIndex].parent.name}) ({currentTurretIndex + 1}/{turretAnchors.Count})");
    }
}
