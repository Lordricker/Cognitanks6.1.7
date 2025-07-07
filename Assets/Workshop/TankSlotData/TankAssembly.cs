using UnityEngine;
using AiEditor;

public class TankAssembly : MonoBehaviour
{
    public Transform basePivot; // Where engine frame and armor won't instantiated
    public Transform turretPivot; // Where turret will be instantiated
    
    [Header("Tank Faction")]
    [SerializeField] private bool isEnemyTank = true; // Set this in inspector or through code
    
    // TankMan component handles all AI and stats - no local references needed
    private TankMan tankMan;

    /// <summary>
    /// Get the current turret AI from TankMan component
    /// </summary>
    public AiTreeAsset GetTurretAI() 
    {
        if (tankMan == null) tankMan = GetComponent<TankMan>();
        return tankMan?.AssignedTurretAI;
    }
    
    /// <summary>
    /// Get the current nav AI from TankMan component
    /// </summary>
    public AiTreeAsset GetNavAI() 
    {
        if (tankMan == null) tankMan = GetComponent<TankMan>();
        return tankMan?.AssignedNavAI;
    }

    public void Assemble(TankSlotDataJson data)
    {
        Debug.Log($"TankAssembly.Assemble() called on {gameObject.name} with data: {(data != null ? data.displayName : "NULL")}");
        if (data == null) 
        {
            Debug.LogError($"TankAssembly.Assemble: data is null for {gameObject.name}!");
            return;
        }
        
        Debug.Log($"TankAssembly.Assemble: Tank data - turretInstanceId: '{data.turretInstanceId}', turretAIInstanceId: '{data.turretAIInstanceId}', isActive: {data.isActive}");
        
        // Ensure NavMeshAgent is present for smooth movement
        var navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent == null)
        {
            navAgent = gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
        }
        
        // Configure NavMeshAgent for tank movement
        navAgent.speed = Mathf.Max(1f, data.enginePower - (data.totalWeight * 0.1f)); // Use calculated move speed
        navAgent.angularSpeed = Mathf.Max(30f, 90f - (data.totalWeight * 0.5f)); // Use calculated turn speed
        navAgent.acceleration = 8f; // Reasonable acceleration
        navAgent.stoppingDistance = 1f; // Stop close to destination
        navAgent.radius = 2f; // Tank size
        navAgent.height = 3f; // Tank height
        navAgent.baseOffset = 0f; // Keep agent at NavMesh level
        navAgent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        
        // CRITICAL: Configure NavMeshAgent for proper tank movement
        navAgent.updatePosition = true; // NavMeshAgent controls position
        navAgent.updateRotation = false; // Disable NavMeshAgent rotation so we can handle terrain following manually
        navAgent.updateUpAxis = false; // Prevent NavMeshAgent from forcing upright orientation
        
        Debug.Log($"[TankAssembly] NavMeshAgent configured - Speed: {navAgent.speed}, AngularSpeed: {navAgent.angularSpeed}");
        
        // Ensure basePivot and turretPivot are positioned correctly above the NavMesh surface
        // The tank model should sit ON the ground, not IN it
        if (basePivot != null)
        {
            basePivot.localPosition = new Vector3(0f, 4f, 0f); // Lift base components 4 units above ground
            Debug.Log($"[TankAssembly] Set basePivot local position to: {basePivot.localPosition}");
        }
        if (turretPivot != null)
        {
            turretPivot.localPosition = new Vector3(0f, 4.5f, 0f); // Lift turret above base
            Debug.Log($"[TankAssembly] Set turretPivot local position to: {turretPivot.localPosition}");
        }
        
        // Configure component placement relative to NavMesh surface
        Debug.Log($"[TankAssembly] Using stat-based approach - component stats are stored directly in TankSlotData");
              // Ensure TankMan component is present and configured
        tankMan = GetComponent<TankMan>();
        if (tankMan == null)
            tankMan = gameObject.AddComponent<TankMan>();
        
        // Set the tank slot data so TankMan can calculate stats
        tankMan.SetTankSlotData(data);
        
        // Load and assign the universal bullet prefab
        GameObject bulletPrefab = Resources.Load<GameObject>("Prefabs/BulletObject");
        if (bulletPrefab != null)
        {
            tankMan.SetBulletPrefab(bulletPrefab);
            Debug.Log($"TankAssembly: Assigned bullet prefab to {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"TankAssembly: Could not load bullet prefab from Resources/Prefabs/BulletObject for {gameObject.name}");
        }
        
        Debug.Log($"TankAssembly: Added and configured TankMan for {gameObject.name}");
        
        // Add HP bar component
        TankHPBar hpBar = GetComponent<TankHPBar>();
        if (hpBar == null)
        {
            hpBar = gameObject.AddComponent<TankHPBar>();
            Debug.Log($"TankAssembly: Added HP bar to {gameObject.name}");
        }
        
        // Set the appropriate layer for tank faction (only on root object)
        SetTankLayer();
        
        // Remove old children
        foreach (Transform child in basePivot) Destroy(child.gameObject);
        foreach (Transform child in turretPivot) Destroy(child.gameObject);
        
        // Instantiate engine frame and armor as children of basePivot
        if (!string.IsNullOrEmpty(data.engineFrameInstanceId))
        {
            Debug.Log($"TankAssembly: Looking up engine frame with instanceId: {data.engineFrameInstanceId}");
            GameObject engineFramePrefab = FindComponentPrefabByInstanceId(data.engineFrameInstanceId, ComponentCategory.EngineFrame);
            if (engineFramePrefab != null)
            {
                GameObject engineFrame = Instantiate(engineFramePrefab, basePivot.position, basePivot.rotation, basePivot);
                ApplyColorToTreadMount(engineFrame, data.engineFrameColor.ToUnityColor());
                SetLayerRecursively(engineFrame, 0);
                Debug.Log($"TankAssembly: Instantiated engine frame: {engineFramePrefab.name}");
            }
            else
            {
                Debug.LogWarning($"TankAssembly: Could not find engine frame prefab for instanceId: {data.engineFrameInstanceId}");
            }
        }
        if (!string.IsNullOrEmpty(data.armorInstanceId))
        {
            Debug.Log($"TankAssembly: Looking up armor with instanceId: {data.armorInstanceId}");
            GameObject armorPrefab = FindComponentPrefabByInstanceId(data.armorInstanceId, ComponentCategory.Armor);
            if (armorPrefab != null)
            {
                GameObject armor = Instantiate(armorPrefab, basePivot.position, basePivot.rotation, basePivot);
                ApplyColorToModel(armor, data.armorColor.ToUnityColor());
                SetLayerRecursively(armor, 0);
                Debug.Log($"TankAssembly: Instantiated armor: {armorPrefab.name}");
            }
            else
            {
                Debug.LogWarning($"TankAssembly: Could not find armor prefab for instanceId: {data.armorInstanceId}");
            }
        }
        
        // Instantiate turret as child of turretPivot
        if (!string.IsNullOrEmpty(data.turretInstanceId))
        {
            Debug.Log($"TankAssembly: Looking up turret with instanceId: {data.turretInstanceId}");
            GameObject turretPrefab = FindComponentPrefabByInstanceId(data.turretInstanceId, ComponentCategory.Turret);
            if (turretPrefab != null)
            {
                GameObject turretInstance = Instantiate(turretPrefab, turretPivot.position, turretPivot.rotation, turretPivot);
                ApplyColorToModel(turretInstance, data.turretColor.ToUnityColor());
                SetLayerRecursively(turretInstance, 0);
                
                // Find fire point for turret
                Transform firePoint = FindFirePointRecursive(turretInstance.transform);
                tankMan.SetTurretComponents(turretInstance.transform, firePoint);
                
                Debug.Log($"TankAssembly: Instantiated turret: {turretPrefab.name}");
            }
            else
            {
                Debug.LogWarning($"TankAssembly: Could not find turret prefab for instanceId: {data.turretInstanceId}");
            }
        }
        
        // AI references are now loaded by TankMan.SetTankSlotData() using instance IDs
        Debug.Log($"TankAssembly: AI assignments handled by TankMan - TurretAI: {data.turretAIInstanceId}, NavAI: {data.navAIInstanceId}");

        // Add CameraAnchor if not present
        Transform anchor = transform.Find("CameraAnchor");
        if (anchor == null)
        {
            GameObject anchorObj = new GameObject("CameraAnchor");
            anchorObj.transform.SetParent(transform);
            anchorObj.transform.localPosition = new Vector3(0f, 15f, -30f); // Behind tank (negative Z), elevated
            anchorObj.transform.localRotation = Quaternion.identity; // Y rotation = 0 degrees
            Debug.Log($"[TankAssembly] Created CameraAnchor at position: {anchorObj.transform.localPosition}, rotation: {anchorObj.transform.localEulerAngles}");
        }
    }
      /// <summary>
    /// Recursively searches for a FirePoint transform in the hierarchy
    /// </summary>
    private Transform FindFirePointRecursive(Transform parent)
    {
        // Check if current transform is FirePoint
        if (parent.name == "FirePoint")
            return parent;
              
        // Search all children recursively
        foreach (Transform child in parent)
        {
            Transform result = FindFirePointRecursive(child);
            if (result != null)
                return result;
        }
        
        return null;
    }
    
    /// <summary>
    /// Recursively sets the layer for a GameObject and all its children
    /// </summary>
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
    
    /// <summary>
    /// Set whether this tank should be on the enemy or ally layer
    /// </summary>
    public void SetTankFaction(bool isEnemy)
    {
        isEnemyTank = isEnemy;
        
        // Apply the layer change immediately if the tank is already assembled
        SetTankLayer();
    }
    
    /// <summary>
    /// Sets the appropriate layer for the tank based on its faction
    /// </summary>
    private void SetTankLayer()
    {
        if (isEnemyTank)
        {
            gameObject.layer = 8; // Enemy layer
            Debug.Log($"[TankAssembly] Set {gameObject.name} to Enemy layer (8)");
        }
        else
        {
            gameObject.layer = 9; // Ally layer
            Debug.Log($"[TankAssembly] Set {gameObject.name} to Ally layer (9)");
        }
    }

    // Helper: Only color the TreadMount child
    private void ApplyColorToTreadMount(GameObject engineFrame, Color color)
    {
        var treadMount = engineFrame.transform.Find("TreadMount");
        if (treadMount != null)
        {
            var renderers = treadMount.GetComponentsInChildren<Renderer>();
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
    }
    // Helper: Color all renderers in a model
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
    /// Find the prefab for a component by its instance ID
    /// </summary>
    private GameObject FindComponentPrefabByInstanceId(string instanceId, ComponentCategory category)
    {
        if (string.IsNullOrEmpty(instanceId))
        {
            Debug.LogWarning($"[TankAssembly] FindComponentPrefabByInstanceId: instanceId is null or empty for category {category}");
            return null;
        }

        // Extract the component name from instanceId (format: "ComponentName_guid")
        string componentName = instanceId;
        if (instanceId.Contains("_"))
        {
            componentName = instanceId.Substring(0, instanceId.LastIndexOf("_"));
        }
        
        Debug.Log($"[TankAssembly] Looking for component '{componentName}' of category {category}");
        
        // Load prefab directly from Assets/Models/Prefabs based on component name and category
        GameObject prefab = LoadPrefabByNameAndCategory(componentName, category);
        
        if (prefab != null)
        {
            Debug.Log($"[TankAssembly] Successfully loaded prefab for {componentName}");
            return prefab;
        }
        else
        {
            Debug.LogWarning($"[TankAssembly] Could not find prefab for {componentName} of category {category}. Creating placeholder.");
            return CreatePlaceholderPrefab(componentName, category);
        }
    }
    
    /// <summary>
    /// Load prefab by component name and category from Resources folder
    /// </summary>
    private GameObject LoadPrefabByNameAndCategory(string componentName, ComponentCategory category)
    {
        GameObject prefab = null;
        
        Debug.Log($"[TankAssembly] LoadPrefabByNameAndCategory: '{componentName}' (Category: {category})");
        
        switch (category)
        {
            case ComponentCategory.EngineFrame:
                // Map component names to engine frame prefabs
                if (componentName.Contains("Heavy Engine") || componentName == "Heavy Engine")
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/cengineframe");
                    Debug.Log($"[TankAssembly] Loaded engine frame: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/cengineframe");
                }
                // Add more engine frame mappings as needed
                // else if (componentName.Contains("Light Engine"))
                // {
                //     prefab = Resources.Load<GameObject>("Models/Prefabs/lightengineframe");
                // }
                break;
                
            case ComponentCategory.Armor:
                // Map component names to armor prefabs
                if (componentName.Contains("Light Plate") || componentName == "Light Plate")
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Armors/barmor");
                    Debug.Log($"[TankAssembly] Loaded armor: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Armors/barmor");
                }
                // Add more armor mappings as needed
                // else if (componentName.Contains("Heavy Plate"))
                // {
                //     prefab = Resources.Load<GameObject>("Models/Prefabs/Armors/heavyarmor");
                // }
                break;
                
            case ComponentCategory.Turret:
                // Map component names to turret prefabs
                if (componentName == "Rifle" || componentName.Contains("Rifle"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Turrets/Rifle");
                    Debug.Log($"[TankAssembly] Loaded turret: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Turrets/Rifle");
                }
                // Add more turret mappings as needed
                // else if (componentName.Contains("Cannon"))
                // {
                //     prefab = Resources.Load<GameObject>("Models/Prefabs/Turrets/Cannon");
                // }
                break;
        }
        
        // If Resources.Load failed, try to load from direct asset path using Unity's asset database (Editor only)
        #if UNITY_EDITOR
        if (prefab == null)
        {
            string assetPath = GetAssetPathForComponent(componentName, category);
            if (!string.IsNullOrEmpty(assetPath))
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                Debug.Log($"[TankAssembly] Loaded prefab from asset path: {assetPath}");
            }
        }
        #endif
        
        if (prefab == null)
        {
            Debug.LogWarning($"[TankAssembly] Could not load prefab for component '{componentName}' (Category: {category})");
        }
        
        return prefab;
    }
    
    #if UNITY_EDITOR
    /// <summary>
    /// Get the asset path for a component (Editor only)
    /// </summary>
    private string GetAssetPathForComponent(string componentName, ComponentCategory category)
    {
        switch (category)
        {
            case ComponentCategory.EngineFrame:
                if (componentName.Contains("Heavy Engine") || componentName == "Heavy Engine")
                    return "Assets/Resources/Models/Prefabs/cengineframe.prefab";
                break;
                
            case ComponentCategory.Armor:
                if (componentName.Contains("Light Plate") || componentName == "Light Plate")
                    return "Assets/Resources/Models/Prefabs/Armors/barmor.prefab";
                break;
                
            case ComponentCategory.Turret:
                if (componentName == "Rifle" || componentName.Contains("Rifle"))
                    return "Assets/Resources/Models/Prefabs/Turrets/Rifle.prefab";
                break;
        }
        
        return "";
    }
    #endif
    
    /// <summary>
    /// Create a placeholder prefab when the actual component can't be found
    /// </summary>
    private GameObject CreatePlaceholderPrefab(string componentName, ComponentCategory category)
    {
        GameObject placeholder;
        
        switch (category)
        {
            case ComponentCategory.EngineFrame:
                placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                placeholder.transform.localScale = new Vector3(3f, 1f, 2f);
                placeholder.name = $"Placeholder_Engine_{componentName}";
                break;
                
            case ComponentCategory.Armor:
                placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                placeholder.transform.localScale = new Vector3(2.5f, 1.5f, 2f);
                placeholder.name = $"Placeholder_Armor_{componentName}";
                break;
                
            case ComponentCategory.Turret:
                placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                placeholder.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
                placeholder.name = $"Placeholder_Turret_{componentName}";
                break;
                
            default:
                placeholder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                placeholder.name = $"Placeholder_{componentName}";
                break;
        }
        
        // Make placeholder slightly transparent and colored
        var renderer = placeholder.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(1f, 0f, 1f, 0.7f); // Magenta, slightly transparent
            renderer.material = material;
        }
        
        Debug.LogWarning($"[TankAssembly] Created placeholder for missing component: {componentName}");
        return placeholder;
    }
}
