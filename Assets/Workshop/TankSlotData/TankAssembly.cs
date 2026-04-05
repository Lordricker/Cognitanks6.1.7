using UnityEngine;
using System.Collections.Generic;
using AiEditor;

public class TankAssembly : MonoBehaviour
{
    public Transform basePivot; // Where engine frame and armor are instantiated
    public Transform turretPivot; // Where turret will be instantiated

    [Header("Tank Visual Offsets")]
    [Tooltip("Vertical offset for armor relative to engine frame (in local Y units)")]
    //[SerializeField] private float armorYOffset = 0f;

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
        if (data == null) 
        {
            Debug.LogError($"TankAssembly.Assemble: data is null for {gameObject.name}!");
            return;
        }

        // Ensure BoxCollider is present for tank physics Ground Grounded detection
        var boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }
        boxCollider.center = new Vector3(0f, -3f, 0f);
        boxCollider.size = new Vector3(9f, 1.5f, 20f);
        boxCollider.isTrigger = true; // Ensure collider is set as trigger for ground detection
        
        // Setup 4-sphere physics contact system for normalized friction
        SetupWheelColliders();
        
        // positioning components for tank
        if (basePivot != null)
        {
            basePivot.localPosition = new Vector3(0f, 4f, 0f); // Lift base components 3 units above ground
        }
        if (turretPivot != null)
        {
            turretPivot.localPosition = new Vector3(0f, 4.5f, 0f); // Lift turret above base
        }
        
              // Ensure TankMan component is present and configured
        tankMan = GetComponent<TankMan>();
        if (tankMan == null)
            tankMan = gameObject.AddComponent<TankMan>();

        // Pull stats directly from ScriptableObjects so the JSON only needs instanceId references.
        // Editing an SO propagates automatically to every tank that uses it.
        if (!string.IsNullOrEmpty(data.engineFrameInstanceId))
        {
            string efName = data.engineFrameInstanceId.Contains("_")
                ? data.engineFrameInstanceId.Substring(0, data.engineFrameInstanceId.LastIndexOf("_"))
                : data.engineFrameInstanceId;
            EngineFrameData efData = Resources.Load<EngineFrameData>($"Workshop/ComponentData/EngineFrames/{efName}");
            if (efData != null)
            {
                data.engineForce = efData.enginePower;
                data.engineTorque = efData.turningPower;
                data.engineWeightCapacity = efData.weightCapacity;
                Debug.Log($"[TankAssembly] Engine stats from SO '{efName}': force={data.engineForce}, torque={data.engineTorque}");
            }
            else
            {
                Debug.LogWarning($"[TankAssembly] EngineFrameData SO not found for '{efName}' — falling back to JSON values");
            }
        }

        if (!string.IsNullOrEmpty(data.armorInstanceId))
        {
            string armorName = data.armorInstanceId.Contains("_")
                ? data.armorInstanceId.Substring(0, data.armorInstanceId.LastIndexOf("_"))
                : data.armorInstanceId;
            ArmorData armorData = Resources.Load<ArmorData>($"Workshop/ComponentData/Armors/{armorName}");
            if (armorData != null)
            {
                data.armorHP = armorData.HP;
                Debug.Log($"[TankAssembly] Armor stats from SO '{armorName}': HP={data.armorHP}");
            }
            else
            {
                Debug.LogWarning($"[TankAssembly] ArmorData SO not found for '{armorName}' — falling back to JSON values");
            }
        }

        if (!string.IsNullOrEmpty(data.turretInstanceId))
        {
            string turretName = data.turretInstanceId.Contains("_")
                ? data.turretInstanceId.Substring(0, data.turretInstanceId.LastIndexOf("_"))
                : data.turretInstanceId;
            TurretData turretData = Resources.Load<TurretData>($"Workshop/ComponentData/Turrets/{turretName}");
            if (turretData != null)
            {
                data.turretDamage = turretData.damage;
                data.turretRange = turretData.range;
                data.turretShotsPerSec = turretData.shotspersec;
                data.turretBulletSpeed = turretData.bulletSpeed;
                data.turretKnockback = turretData.knockback;
                data.turretVisionRange = turretData.visionRange;
                data.turretVisionCone = turretData.visionCone;
                Debug.Log($"[TankAssembly] Turret stats from SO '{turretName}': dmg={data.turretDamage}, range={data.turretRange}");
            }
            else
            {
                Debug.LogWarning($"[TankAssembly] TurretData SO not found for '{turretName}' — falling back to JSON values");
            }
        }
        
        // Set the tank slot data so TankMan can calculate stats
        tankMan.SetTankSlotData(data);
        
        // Load and assign the universal bullet prefab
        GameObject bulletPrefab = Resources.Load<GameObject>("Prefabs/BulletObject");
        if (bulletPrefab != null)
        {
            tankMan.SetBulletPrefab(bulletPrefab);
            Debug.Log($"TankAssembly: Successfully loaded and assigned bullet prefab to {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"TankAssembly: Could not load bullet prefab from Resources/Prefabs/BulletObject for {gameObject.name}");
        }
        
        // Load and assign the heal bullet prefab (used by Healer turret type)
        GameObject healBulletPrefab = Resources.Load<GameObject>("Prefabs/HealObject");
        if (healBulletPrefab != null)
        {
            tankMan.SetHealBulletPrefab(healBulletPrefab);
            Debug.Log($"TankAssembly: Successfully loaded and assigned heal bullet prefab to {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"TankAssembly: Could not load heal bullet prefab from Resources/Prefabs/HealObject for {gameObject.name}");
        }
        
        // Load and assign the death explosion prefab
        GameObject deathExplosionPrefab = Resources.Load<GameObject>("Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Big");
        if (deathExplosionPrefab != null)
        {
            tankMan.SetDeathExplosionPrefab(deathExplosionPrefab);
            Debug.Log($"TankAssembly: Successfully loaded and assigned death explosion prefab to {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"TankAssembly: Could not load death explosion prefab from Resources/Vefects/Free Fire VFX URP/Particles/VFX_Fire_01_Big for {gameObject.name}");
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
            GameObject engineFramePrefab = FindComponentPrefabByInstanceId(data.engineFrameInstanceId, ComponentCategory.EngineFrame);
            if (engineFramePrefab != null)
            {
                GameObject engineFrame = Instantiate(engineFramePrefab, basePivot.position, basePivot.rotation, basePivot);
                // Force reset local rotation to fix rotation issues in builds
                engineFrame.transform.localRotation = Quaternion.identity;
                ApplyColorToTreadMount(engineFrame, data.engineFrameColor.ToUnityColor());
                
                // Apply skin: use JSON path first, fallback to ComponentCustomizationManager
                string engineSkinPath = !string.IsNullOrEmpty(data.engineFrameSkinPath) 
                    ? data.engineFrameSkinPath
                    : ComponentCustomizationManager.Instance?.GetSkin(data.engineFrameInstanceId);
                ApplySkinToModel(engineFrame, engineSkinPath);

                // Apply per-part rust overlay
                string efRustName = data.engineFrameInstanceId.Contains("_")
                    ? data.engineFrameInstanceId.Substring(0, data.engineFrameInstanceId.LastIndexOf("_"))
                    : data.engineFrameInstanceId;
                ApplyRustToModel(engineFrame, GetRustBaseName(efRustName));

                SetLayerRecursively(engineFrame, 6); // Set to Shadow layer
            }
            else
            {
                Debug.LogWarning($"TankAssembly: Could not find engine frame prefab for instanceId: {data.engineFrameInstanceId}");
            }
        }
        if (!string.IsNullOrEmpty(data.armorInstanceId))
        {
            GameObject armorPrefab = FindComponentPrefabByInstanceId(data.armorInstanceId, ComponentCategory.Armor);
            if (armorPrefab != null)
            {
                GameObject armor = Instantiate(armorPrefab, basePivot.position, basePivot.rotation, basePivot);
                // Force local position to zero (ignores any baked FBX offset)
                armor.transform.localPosition = Vector3.zero;
                armor.transform.localRotation = Quaternion.identity;
                ApplyColorToModel(armor, data.armorColor.ToUnityColor());
                
                // Apply skin: use JSON path first, fallback to ComponentCustomizationManager
                string armorSkinPath = !string.IsNullOrEmpty(data.armorSkinPath) 
                    ? data.armorSkinPath
                    : ComponentCustomizationManager.Instance?.GetSkin(data.armorInstanceId);
                ApplySkinToModel(armor, armorSkinPath);

                // Apply per-part rust overlay
                string armorRustName = data.armorInstanceId.Contains("_")
                    ? data.armorInstanceId.Substring(0, data.armorInstanceId.LastIndexOf("_"))
                    : data.armorInstanceId;
                ApplyRustToModel(armor, GetRustBaseName(armorRustName));

                SetLayerRecursively(armor, 6); // Set to Shadow layer
            }
            else
            {
                Debug.LogWarning($"TankAssembly: Could not find armor prefab for instanceId: {data.armorInstanceId}");
            }
        }
        
        // Instantiate turret as child of turretPivot
        GameObject turretInstance = null;
        if (!string.IsNullOrEmpty(data.turretInstanceId))
        {
            GameObject turretPrefab = FindComponentPrefabByInstanceId(data.turretInstanceId, ComponentCategory.Turret);
            if (turretPrefab != null)
            {
                    turretInstance = Instantiate(turretPrefab, turretPivot.position, turretPivot.rotation, turretPivot);
                ApplyColorToModel(turretInstance, data.turretColor.ToUnityColor());
                
                // Apply skin: use JSON path first, fallback to ComponentCustomizationManager
                string turretSkinPath = !string.IsNullOrEmpty(data.turretSkinPath) 
                    ? data.turretSkinPath
                    : ComponentCustomizationManager.Instance?.GetSkin(data.turretInstanceId);
                ApplySkinToModel(turretInstance, turretSkinPath);

                // Apply per-part rust overlay
                string turretRustName = data.turretInstanceId.Contains("_")
                    ? data.turretInstanceId.Substring(0, data.turretInstanceId.LastIndexOf("_"))
                    : data.turretInstanceId;
                ApplyRustToModel(turretInstance, GetRustBaseName(turretRustName));

                // Apply decal: use JSON path first, fallback to ComponentCustomizationManager
                string turretDecalPath = !string.IsNullOrEmpty(data.turretDecalPath) 
                    ? data.turretDecalPath
                    : ComponentCustomizationManager.Instance?.GetDecal(data.turretInstanceId);
                if (!string.IsNullOrEmpty(turretDecalPath))
                {
                    ApplyDecalToModel(turretInstance, turretDecalPath);
                }
                
                SetLayerRecursively(turretInstance, 6); // Set to Shadow layer
                
                // Find all fire points for turret (matches any child containing "firepoint" in its name)
                List<Transform> firePointsList = FindAllFirePointsRecursive(turretInstance.transform);
                if (firePointsList.Count > 0)
                {
                    Debug.Log($"TankAssembly: Found {firePointsList.Count} FirePoint(s) for turret {turretInstance.name}");
                }
                else
                {
                    Debug.LogWarning($"TankAssembly: No FirePoints found in turret {turretInstance.name}");
                }
                tankMan.SetTurretComponents(turretInstance.transform, firePointsList);
                
                // Get skin and decal paths (use JSON path first, fallback to ComponentCustomizationManager)
                string skinPathForModels = !string.IsNullOrEmpty(data.turretSkinPath) 
                    ? data.turretSkinPath
                    : ComponentCustomizationManager.Instance?.GetSkin(data.turretInstanceId);
                string decalPathForModels = !string.IsNullOrEmpty(data.turretDecalPath) 
                    ? data.turretDecalPath
                    : ComponentCustomizationManager.Instance?.GetDecal(data.turretInstanceId);
                
                // Load and assign hammer animation prefab if specified
                if (!string.IsNullOrEmpty(data.turretAnimationPrefabPath))
                {
                    GameObject animationPrefab = Resources.Load<GameObject>(data.turretAnimationPrefabPath);
                    if (animationPrefab != null)
                    {
                        tankMan.SetHammerAnimationPrefab(animationPrefab, data.turretColor.ToUnityColor(), skinPathForModels, decalPathForModels);
                        Debug.Log($"TankAssembly: Loaded and assigned hammer animation prefab: {animationPrefab.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"TankAssembly: Could not load animation prefab from path: {data.turretAnimationPrefabPath}");
                    }
                }
                
                // Load and assign death model prefab if specified
                if (!string.IsNullOrEmpty(data.turretDeathModelPrefabPath))
                {
                    GameObject deathModelPrefab = Resources.Load<GameObject>(data.turretDeathModelPrefabPath);
                    if (deathModelPrefab != null)
                    {
                        tankMan.SetTurretDeathModelPrefab(deathModelPrefab, data.turretColor.ToUnityColor(), skinPathForModels, decalPathForModels);
                        Debug.Log($"TankAssembly: Loaded and assigned turret death model prefab: {deathModelPrefab.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"TankAssembly: Could not load death model prefab from path: {data.turretDeathModelPrefabPath}");
                    }
                }
                
            }
            else
            {
                Debug.LogWarning($"TankAssembly: Could not find turret prefab for instanceId: {data.turretInstanceId}");
            }
        }
        
        // AI references are now loaded by TankMan.SetTankSlotData() using instance IDs

        // Add CameraAnchor if not present (tank body camera)
        Transform anchor = transform.Find("CameraAnchor");
        if (anchor == null)
        {
            GameObject anchorObj = new GameObject("CameraAnchor");
            anchorObj.transform.SetParent(transform);
            anchorObj.transform.localPosition = new Vector3(0f, 15f, -30f); // Behind tank (negative Z), elevated
            anchorObj.transform.localRotation = Quaternion.identity; // Y rotation = 0 degrees
        }

        // Add TurretCameraAnchor if not present (turret following camera)
        if (turretInstance != null)
        {
            Transform turretAnchor = turretInstance.transform.Find("TurretCameraAnchor");
            if (turretAnchor == null)
            {
                GameObject turretAnchorObj = new GameObject("TurretCameraAnchor");
                turretAnchorObj.transform.SetParent(turretInstance.transform);
                turretAnchorObj.transform.localPosition = new Vector3(0f, 15f, -30f); // Same offset as tank camera
                turretAnchorObj.transform.localRotation = Quaternion.identity;
            }
        }
        
        // Add dirt emitter anchors and instantiate dirt emitters
        GameObject dirtEmitterPrefab = Resources.Load<GameObject>("Vefects/DirtEmitter");
        if (dirtEmitterPrefab != null)
        {
            // Left tread dirt emitter
            Transform leftAnchor = transform.Find("LeftDirtEmitterAnchor");
            if (leftAnchor == null)
            {
                GameObject leftAnchorObj = new GameObject("LeftDirtEmitterAnchor");
                leftAnchorObj.transform.SetParent(transform);
                leftAnchorObj.transform.localPosition = new Vector3(-5f, -3f, -8f);
                leftAnchorObj.transform.localRotation = Quaternion.identity;
                leftAnchor = leftAnchorObj.transform;
            }
            
            // Right tread dirt emitter
            Transform rightAnchor = transform.Find("RightDirtEmitterAnchor");
            if (rightAnchor == null)
            {
                GameObject rightAnchorObj = new GameObject("RightDirtEmitterAnchor");
                rightAnchorObj.transform.SetParent(transform);
                rightAnchorObj.transform.localPosition = new Vector3(5f, -2f, -6f);
                rightAnchorObj.transform.localRotation = Quaternion.identity;
                rightAnchor = rightAnchorObj.transform;
            }
            
            // Instantiate dirt emitters at anchors
            GameObject leftEmitterObj = Instantiate(dirtEmitterPrefab, leftAnchor.position, leftAnchor.rotation, leftAnchor);
            GameObject rightEmitterObj = Instantiate(dirtEmitterPrefab, rightAnchor.position, rightAnchor.rotation, rightAnchor);
            
            // Rotate emitters -120 degrees on X axis to adjust particle direction
            leftEmitterObj.transform.localRotation = Quaternion.Euler(-120f, 0f, 0f);
            rightEmitterObj.transform.localRotation = Quaternion.Euler(-120f, 0f, 0f);
            
            // Get particle systems and pass to TankMan
            ParticleSystem leftPS = leftEmitterObj.GetComponent<ParticleSystem>();
            ParticleSystem rightPS = rightEmitterObj.GetComponent<ParticleSystem>();
            
            if (leftPS != null && rightPS != null)
            {
                tankMan.SetDirtEmitters(leftPS, rightPS);
                Debug.Log($"TankAssembly: Successfully added dirt emitters to {gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"TankAssembly: Dirt emitter prefab missing ParticleSystem component");
            }
        }
        else
        {
            Debug.LogWarning($"TankAssembly: Could not load dirt emitter prefab from Resources/Vefects/DirtEmitter");
        }

        // Add debug vision cone visualization
        VisionConeDebug visionCone = GetComponent<VisionConeDebug>();
        if (visionCone == null)
            visionCone = gameObject.AddComponent<VisionConeDebug>();
        visionCone.Initialize(tankMan);

        // Rigidbody configuration is now handled by TankMan.Start() and CalculateStats()
        // This ensures physics parameters from TankSlotDataJson are properly applied
        
        // Old wheel markers removed - using 4-sphere collider system instead

        // Scale the tank root after full assembly so all colliders/children were built at scale 1
        // and Unity scales them correctly - mirrors setting scale manually in the inspector
        transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
    }
      /// <summary>
    /// Recursively searches for a FirePoint transform in the hierarchy
    /// </summary>
    private List<Transform> FindAllFirePointsRecursive(Transform parent)
    {
        List<Transform> results = new List<Transform>();
        CollectFirePoints(parent, results);
        return results;
    }

    private void CollectFirePoints(Transform t, List<Transform> results)
    {
        if (t.name.IndexOf("firepoint", System.StringComparison.OrdinalIgnoreCase) >= 0)
            results.Add(t);
        foreach (Transform child in t)
            CollectFirePoints(child, results);
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

    // Helper: Only color the TreadMount child, falls back to whole model
    private void ApplyColorToTreadMount(GameObject engineFrame, Color color)
    {
        var treadMount = engineFrame.transform.Find("TreadMount");
        var renderers = treadMount != null
            ? treadMount.GetComponentsInChildren<Renderer>()
            : engineFrame.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            // Skip SpriteRenderers (used for decals) - they should not be affected by color
            if (renderer is SpriteRenderer) continue;
            
            foreach (var mat in renderer.materials)
            {
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                else if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);
            }
        }
    }
    // Helper: Color all renderers in a model
    private void ApplyColorToModel(GameObject model, Color color)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            // Skip SpriteRenderers (used for decals) - they should not be affected by color
            if (renderer is SpriteRenderer) continue;
            
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
    /// Applies a skin texture to a model by loading from Resources and setting as main texture
    /// Also loads additional maps (normal, height, metallic/roughness) from a subfolder with the same name
    /// </summary>
    private void ApplySkinToModel(GameObject model, string skinPath)
    {
        // Fall back to the default skin if none is set
        if (string.IsNullOrEmpty(skinPath))
            skinPath = "KritaArt/Skins/TankPaint";
            
        // Load texture from Resources
        Texture2D skinTexture = Resources.Load<Texture2D>(skinPath);
        if (skinTexture == null)
        {
            Debug.LogWarning($"TankAssembly: Could not load skin texture from: {skinPath}");
            return;
        }
        
        // Try to load additional maps from a subfolder with the same name as the texture
        // e.g., if skinPath is "KritaArt/Skins/MySkin", look in "KritaArt/Skins/MySkin/" for additional maps
        Texture2D normalMap = null;
        Texture2D heightMap = null;
        Texture2D metallicMap = null;
        
        string additionalMapsPath = skinPath; // Folder has same name as the texture file
        Texture2D[] additionalTextures = Resources.LoadAll<Texture2D>(additionalMapsPath);
        Debug.Log($"[TankAssembly] Skin subfolder scan: path='{additionalMapsPath}' found {(additionalTextures?.Length ?? 0)} texture(s)");
        if (additionalTextures != null)
            foreach (var t in additionalTextures)
                Debug.Log($"[TankAssembly]   - {t.name}");
        
        if (additionalTextures != null && additionalTextures.Length > 0)
        {
            foreach (var tex in additionalTextures)
            {
                string nameLower = tex.name.ToLower();
                if (nameLower.Contains("nor"))
                {
                    normalMap = tex;
                    Debug.Log($"TankAssembly: Found normal map: {tex.name}");
                }
                else if (nameLower.Contains("disp"))
                {
                    heightMap = tex;
                    Debug.Log($"TankAssembly: Found height map: {tex.name}");
                }
                else if (nameLower.Contains("rough"))
                {
                    metallicMap = tex;
                    Debug.Log($"TankAssembly: Found metallic/roughness map: {tex.name}");
                }
            }
        }
        
        // Apply to all renderers (except SpriteRenderer which is for decals)
        var renderers = model.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer is SpriteRenderer) continue; // Skip sprite renderers
            
            foreach (var mat in renderer.materials)
            {
                // Apply base map / main texture
                if (mat.HasProperty("_MainTex") || mat.HasProperty("_BaseMap"))
                {
                    if (mat.HasProperty("_MainTex"))
                        mat.SetTexture("_MainTex", skinTexture);
                    if (mat.HasProperty("_BaseMap"))
                        mat.SetTexture("_BaseMap", skinTexture);
                }
                
                // Apply normal map
                if (normalMap != null && mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.EnableKeyword("_NORMALMAP");
                }
                
                // Apply height/displacement map (parallax)
                if (heightMap != null && mat.HasProperty("_ParallaxMap"))
                {
                    mat.SetTexture("_ParallaxMap", heightMap);
                    mat.EnableKeyword("_PARALLAXMAP");
                }
                
                // Apply metallic/roughness map
                // In URP, roughness is stored in the alpha of the metallic map, or use _SmoothnessTextureChannel
                if (metallicMap != null && mat.HasProperty("_MetallicGlossMap"))
                {
                    mat.SetTexture("_MetallicGlossMap", metallicMap);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
            }
        }
    }
    
    /// Extracts the first "word" from a component name to use as the rust mask family key.
    /// Splits on the first space (for names like "Velocity Chassis" → "Velocity"),
    /// or on the first PascalCase boundary after a lowercase letter
    /// (for names like "HammerDown" → "Hammer", "CarbonWeaveArmor" → "Carbon").
    /// This means HammerDown, HammerDeath etc. all share HammerRustMask.png.
    private static string GetRustBaseName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        // Space-separated: "Velocity Chassis" → "Velocity"
        int spaceIdx = name.IndexOf(' ');
        if (spaceIdx > 0) return name.Substring(0, spaceIdx);
        // PascalCase: find first uppercase letter that follows a lowercase letter
        // "HammerDown" → 'D' at index 6 follows 'r' → return "Hammer"
        for (int i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && char.IsLower(name[i - 1]))
                return name.Substring(0, i);
        }
        return name;
    }

    /// <summary>
    /// Loads the shared rust texture and a per-part mask, then applies them to the TankPaint shader.
    /// Naming convention (all in Assets/Resources/KritaArt/RustTextures/):
    ///   rust             → shared tileable rust color RGBA      (_RustTex)   — one file for all parts
    ///   {Name}RustMask   → per-part B/W mask painted in Blender (_RustMask)  — e.g. RifleRustMask
    ///   {Name}RustNormal → per-part rust normal map (optional)  (_RustNormalMap)
    /// </summary>
    private void ApplyRustToModel(GameObject model, string componentName)
    {
        if (model == null || string.IsNullOrEmpty(componentName)) return;

        // Shared rust color texture — same file used by every part
        Texture2D rustTex = Resources.Load<Texture2D>("KritaArt/RustTextures/rust");

        // Per-part files
        string basePath    = $"KritaArt/RustTextures/{componentName}";
        Texture2D rustMask     = Resources.Load<Texture2D>($"{basePath}RustMask");
        Texture2D rustNormal   = Resources.Load<Texture2D>($"{basePath}RustNormal");
        Texture2D rustMetallic = Resources.Load<Texture2D>($"{basePath}RustMetallic");
        Texture2D rustHeight   = Resources.Load<Texture2D>($"{basePath}RustHeight");

        // Nothing to do if there's no mask for this part (rust color alone doesn't help)
        if (rustMask == null) return;

        var renderers = model.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer is SpriteRenderer) continue;
            foreach (var mat in renderer.materials)
            {
                if (rustTex      != null && mat.HasProperty("_RustTex"))         mat.SetTexture("_RustTex",         rustTex);
                if (rustMask     != null && mat.HasProperty("_RustMask"))        mat.SetTexture("_RustMask",        rustMask);
                if (rustNormal   != null && mat.HasProperty("_RustNormalMap"))   mat.SetTexture("_RustNormalMap",   rustNormal);
                if (rustMetallic != null && mat.HasProperty("_RustMetallicMap")) mat.SetTexture("_RustMetallicMap", rustMetallic);
                if (rustHeight   != null && mat.HasProperty("_RustHeightMap"))   mat.SetTexture("_RustHeightMap",   rustHeight);
            }
        }
    }

    /// <summary>
    /// Applies a decal texture to a turret model's SpriteRenderer child
    /// </summary>
    private void ApplyDecalToModel(GameObject model, string decalPath)
    {
        if (string.IsNullOrEmpty(decalPath))
            return;
            
        // Load texture from Resources
        Texture2D decalTexture = Resources.Load<Texture2D>(decalPath);
        if (decalTexture == null)
        {
            Debug.LogWarning($"TankAssembly: Could not load decal texture from: {decalPath}");
            return;
        }
        
        // Find the "Decal" child object by name
        Transform decalTransform = model.transform.Find("Decal");
        if (decalTransform == null)
        {
            Debug.LogWarning($"TankAssembly: No child named 'Decal' found on turret model {model.name}");
            return;
        }
        
        // Get the SpriteRenderer component on the Decal object
        SpriteRenderer spriteRenderer = decalTransform.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            // Create a sprite from the texture
            Sprite decalSprite = Sprite.Create(
                decalTexture,
                new Rect(0, 0, decalTexture.width, decalTexture.height),
                new Vector2(0.5f, 0.5f),
                100f // pixels per unit
            );
            spriteRenderer.sprite = decalSprite;
            Debug.Log($"TankAssembly: Applied decal sprite to {decalTransform.name} on {model.name}");
        }
        else
        {
            Debug.LogWarning($"TankAssembly: No SpriteRenderer component found on 'Decal' child of {model.name}");
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
            // ...existing code if you want to create a placeholder...
            // return CreatePlaceholderPrefab(componentName, category);
        }
        return null;
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
                if (componentName == "Accelerator Frame" || componentName.Contains("Accelerator Frame"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/AcceleratorFrame");
                    Debug.Log($"[TankAssembly] Loaded engine frame: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/AcceleratorFrame");
                }
                else if (componentName == "Velocity Chassis" || componentName.Contains("Velocity Chassis"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/VelocityChassis");
                    Debug.Log($"[TankAssembly] Loaded engine frame: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/VelocityChassis");
                }
                else if (componentName == "Vortex Engine" || componentName.Contains("Vortex Engine"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/VortexEngine");
                    Debug.Log($"[TankAssembly] Loaded engine frame: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/VortexEngine");
                }
                else if (componentName == "Titan Core" || componentName.Contains("Titan Core"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/TitanCore");
                    Debug.Log($"[TankAssembly] Loaded engine frame: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/TitanCore");
                }
                else if (componentName.Contains("Heavy Engine") || componentName == "Heavy Engine")
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/cengineframe");
                    Debug.Log($"[TankAssembly] Loaded engine frame: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/cengineframe");
                }
                break;
                
            case ComponentCategory.Armor:
                // Map component names to armor prefabs
                if (componentName == "Carbon Weave Armor" || componentName.Contains("Carbon Weave"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Armors/CarbonWeaveArmor");
                    Debug.Log($"[TankAssembly] Loaded armor: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Armors/CarbonWeaveArmor");
                }
                else if (componentName == "Ceramic Laminate Plating" || componentName.Contains("Ceramic Laminate"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Armors/CeramicLaminatePlating");
                    Debug.Log($"[TankAssembly] Loaded armor: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Armors/CeramicLaminatePlating");
                }
                else if (componentName == "MK-VI Alloy Shell" || componentName.Contains("MK-VI") || componentName.Contains("Alloy Shell"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Armors/MKVIAlloyShell");
                    Debug.Log($"[TankAssembly] Loaded armor: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Armors/MKVIAlloyShell");
                }
                else if (componentName.Contains("Light Plate") || componentName == "Light Plate")
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Armors/barmor");
                    Debug.Log($"[TankAssembly] Loaded armor: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Armors/barmor");
                }
                break;
                
            case ComponentCategory.Turret:
                // Map component names to turret prefabs
                if (componentName == "Rifle" || componentName.Contains("Rifle"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Turrets/Rifle");
                    Debug.Log($"[TankAssembly] Loaded turret: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Turrets/Rifle");
                }
                else if (componentName == "Artillery" || componentName.Contains("Artillery"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Turrets/Artillery");
                    Debug.Log($"[TankAssembly] Loaded turret: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Turrets/Artillery");
                }
                else if (componentName == "Shotgun" || componentName.Contains("Shotgun"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Turrets/Shotgun");
                    Debug.Log($"[TankAssembly] Loaded turret: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Turrets/Shotgun");
                }
                else if (componentName == "Hammer" || componentName.Contains("Hammer"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Turrets/Hammer");
                    Debug.Log($"[TankAssembly] Loaded turret: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Turrets/Hammer");
                }
                else if (componentName == "Laser" || componentName.Contains("Laser"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Turrets/Laser");
                    Debug.Log($"[TankAssembly] Loaded turret: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Turrets/Laser");
                }
                else if (componentName == "Sniper" || componentName.Contains("Sniper"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Turrets/Sniper");
                    Debug.Log($"[TankAssembly] Loaded turret: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Turrets/Sniper");
                }
                else if (componentName == "Caduceus" || componentName.Contains("Caduceus"))
                {
                    prefab = Resources.Load<GameObject>("Models/Prefabs/Turrets/Caduceus");
                    Debug.Log($"[TankAssembly] Loaded turret: {(prefab != null ? "SUCCESS" : "FAILED")} - Models/Prefabs/Turrets/Caduceus");
                }
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
                if (componentName == "Accelerator Frame" || componentName.Contains("Accelerator Frame"))
                    return "Assets/Resources/Models/Prefabs/AcceleratorFrame.prefab";
                else if (componentName == "Velocity Chassis" || componentName.Contains("Velocity Chassis"))
                    return "Assets/Resources/Models/Prefabs/VelocityChassis.prefab";
                else if (componentName == "Vortex Engine" || componentName.Contains("Vortex Engine"))
                    return "Assets/Resources/Models/Prefabs/VortexEngine.prefab";
                else if (componentName == "Titan Core" || componentName.Contains("Titan Core"))
                    return "Assets/Resources/Models/Prefabs/TitanCore.prefab";
                else if (componentName.Contains("Heavy Engine") || componentName == "Heavy Engine")
                    return "Assets/Resources/Models/Prefabs/cengineframe.prefab";
                break;
                
            case ComponentCategory.Armor:
                if (componentName == "Carbon Weave Armor" || componentName.Contains("Carbon Weave"))
                    return "Assets/Resources/Models/Prefabs/Armors/CarbonWeaveArmor.prefab";
                else if (componentName == "Ceramic Laminate Plating" || componentName.Contains("Ceramic Laminate"))
                    return "Assets/Resources/Models/Prefabs/Armors/CeramicLaminatePlating.prefab";
                else if (componentName == "MK-VI Alloy Shell" || componentName.Contains("MK-VI") || componentName.Contains("Alloy Shell"))
                    return "Assets/Resources/Models/Prefabs/Armors/MKVIAlloyShell.prefab";
                else if (componentName.Contains("Light Plate") || componentName == "Light Plate")
                    return "Assets/Resources/Models/Prefabs/Armors/barmor.prefab";
                break;
                
            case ComponentCategory.Turret:
                if (componentName == "Rifle" || componentName.Contains("Rifle"))
                    return "Assets/Resources/Models/Prefabs/Turrets/Rifle.prefab";
                else if (componentName == "Artillery" || componentName.Contains("Artillery"))
                    return "Assets/Resources/Models/Prefabs/Turrets/Artillery.prefab";
                else if (componentName == "Shotgun" || componentName.Contains("Shotgun"))
                    return "Assets/Resources/Models/Prefabs/Turrets/Shotgun.prefab";
                else if (componentName == "Hammer" || componentName.Contains("Hammer"))
                    return "Assets/Resources/Models/Prefabs/Turrets/Hammer.prefab";
                else if (componentName == "Laser" || componentName.Contains("Laser"))
                    return "Assets/Resources/Models/Prefabs/Turrets/Laser.prefab";
                else if (componentName == "Sniper" || componentName.Contains("Sniper"))
                    return "Assets/Resources/Models/Prefabs/Turrets/Sniper.prefab";
                else if (componentName == "Caduceus" || componentName.Contains("Caduceus"))
                    return "Assets/Resources/Models/Prefabs/Turrets/Caduceus.prefab";
                break;
        }
        
        return "";
    }
    #endif
    
    /// <summary>
    /// Creates 4 sphere colliders at the corners for consistent physics contact
    /// Positioned just above the ground detection trigger box
    /// </summary>
    private void SetupWheelColliders()
    {
        // Remove any existing wheel colliders
        Transform existingWheels = transform.Find("WheelColliders");
        if (existingWheels != null)
        {
            Destroy(existingWheels.gameObject);
        }
        
        // Create container for wheel colliders
        GameObject wheelContainer = new GameObject("WheelColliders");
        wheelContainer.transform.SetParent(transform);
        wheelContainer.transform.localPosition = Vector3.zero;
        wheelContainer.transform.localRotation = Quaternion.identity;
        
        // Sphere positioning - raised slightly to prevent sinking into terrain
        float wheelYPosition = -1.25f; // Raised from -1.36f
        float wheelRadius = 2f; // Increased from 0.4f to provide more contact area
        
        // Positioning based on tank dimensions (matching ground detection box size)
        float frontBack = 8f;  // Front/back distance (half of 14 length minus margin)
        float leftRight = 3.8f; // Left/right distance (half of 7 width minus margin)
        
        // Create 4 sphere colliders at corners
        CreateWheelSphere("WheelFL", wheelContainer.transform, new Vector3(-leftRight, wheelYPosition, frontBack), wheelRadius);
        CreateWheelSphere("WheelFR", wheelContainer.transform, new Vector3(leftRight, wheelYPosition, frontBack), wheelRadius);
        CreateWheelSphere("WheelBL", wheelContainer.transform, new Vector3(-leftRight, wheelYPosition, -frontBack), wheelRadius);
        CreateWheelSphere("WheelBR", wheelContainer.transform, new Vector3(leftRight, wheelYPosition, -frontBack), wheelRadius);
    }
    
    /// <summary>
    /// Creates a single sphere collider for wheel contact
    /// </summary>
    private void CreateWheelSphere(string name, Transform parent, Vector3 localPosition, float radius)
    {
        GameObject wheel = new GameObject(name);
        wheel.transform.SetParent(parent);
        wheel.transform.localPosition = localPosition;
        wheel.transform.localRotation = Quaternion.identity;
        
        SphereCollider sphereCollider = wheel.AddComponent<SphereCollider>();
        sphereCollider.radius = radius;
        sphereCollider.material = null; // Use default physics material (can be customized later)
        
        // Set layer to match parent (tank layer)
        wheel.layer = gameObject.layer;
    }

}
