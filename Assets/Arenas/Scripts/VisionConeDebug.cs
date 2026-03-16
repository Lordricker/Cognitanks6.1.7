using UnityEngine;

/// <summary>
/// Renders a debug vision cone visualization for a tank.
/// Only visible when the global camera is active.
/// Purely visual — no colliders or physics impact.
/// </summary>
public class VisionConeDebug : MonoBehaviour
{
    private TankMan tankMan;
    private GameObject coneObject;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private CameraController cameraController;

    private float lastVisionCone;
    private float lastVisionRange;

    private const int segments = 30;

    public void Initialize(TankMan tank)
    {
        tankMan = tank;

        // Create child object for the cone visual — no collider, visual only
        coneObject = new GameObject("DebugVisionCone");
        coneObject.transform.SetParent(transform, false);
        coneObject.layer = 6; // Shadow layer (same as other tank visuals, no physics interaction)

        meshFilter = coneObject.AddComponent<MeshFilter>();
        meshRenderer = coneObject.AddComponent<MeshRenderer>();

        // Create transparent grey material using Sprites/Default (always available in builds)
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("[VisionConeDebug] Sprites/Default shader not found! Vision cone will not render.");
            return;
        }
        Material coneMaterial = new Material(shader);
        coneMaterial.color = new Color(0.5f, 0.5f, 0.5f, 0.12f);
        coneMaterial.renderQueue = 3000;

        meshRenderer.material = coneMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        // Find camera controller in scene
        cameraController = Object.FindFirstObjectByType<CameraController>();

        // Start hidden
        coneObject.SetActive(false);

        RegenerateMesh();
    }

    void Update()
    {
        if (tankMan == null || coneObject == null) return;

        // Only show in global camera view
        bool showCone = cameraController != null && cameraController.IsGlobalCamera;
        coneObject.SetActive(showCone);

        if (!showCone) return;

        // Follow the turret position and Y-rotation (flat projection)
        Transform turret = tankMan.turretPivot;
        if (turret != null)
        {
            coneObject.transform.position = turret.position;
            coneObject.transform.rotation = Quaternion.Euler(0f, turret.eulerAngles.y, 0f);
        }
        else
        {
            coneObject.transform.position = transform.position;
            coneObject.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        // Compensate for tank root scale so the mesh always displays at correct world-space size.
        // The actual detection range (OverlapSphere) uses raw float values unaffected by transform scale,
        // so the visual must match by undoing the parent's lossy scale.
        Vector3 ps = transform.lossyScale;
        coneObject.transform.localScale = new Vector3(
            ps.x > 0f ? 1f / ps.x : 1f,
            1f,
            ps.z > 0f ? 1f / ps.z : 1f);

        // Regenerate mesh if vision parameters changed
        if (!Mathf.Approximately(tankMan.VisionCone, lastVisionCone) ||
            !Mathf.Approximately(tankMan.VisionRange, lastVisionRange))
        {
            RegenerateMesh();
        }
    }

    void RegenerateMesh()
    {
        if (tankMan == null) return;

        float coneAngle = tankMan.VisionCone;
        float range = tankMan.VisionRange;
        lastVisionCone = coneAngle;
        lastVisionRange = range;

        if (coneAngle <= 0f || range <= 0f)
        {
            meshFilter.mesh = null;
            return;
        }

        Mesh mesh = new Mesh();
        float halfAngle = coneAngle * 0.5f;

        // Vertices: origin point + arc points
        Vector3[] vertices = new Vector3[segments + 2];
        vertices[0] = Vector3.zero; // Origin at turret position

        for (int i = 0; i <= segments; i++)
        {
            float angle = -halfAngle + (coneAngle * i / segments);
            float rad = angle * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(
                Mathf.Sin(rad) * range,
                0f,
                Mathf.Cos(rad) * range
            );
        }

        // Triangles: fan from origin to arc
        int[] triangles = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
    }

    void OnDestroy()
    {
        if (coneObject != null)
            Destroy(coneObject);
    }
}
