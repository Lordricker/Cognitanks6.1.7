using UnityEngine;
using System.Collections.Generic;


public class WorkshopModelPreview : MonoBehaviour
{
    public Transform previewAnchor; // Assign in inspector
    public int previewLayer = 8; // Set to your "ModelPreview" layer number
    public float spinSpeed = 50f;
    [SerializeField] private float armorYOffset = -1.25f; // Armor vertical offset to align properly

    private List<GameObject> currentModels = new List<GameObject>();

    void Update()
    {
        if (previewAnchor.childCount > 0)
            previewAnchor.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }
    public void ShowModel(GameObject prefab)
    {
        ClearPreview();
        if (prefab != null)
        {
            var model = Instantiate(prefab, previewAnchor);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(model, previewLayer);
            currentModels.Add(model);
        }
    }
    public void ShowModel(ComponentData componentData)
    {
        ClearPreview();
        if (componentData != null && componentData.modelPrefab != null)
        {
            var model = Instantiate(componentData.modelPrefab, previewAnchor);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(model, previewLayer);

            // Use specific coloring method for EngineFrame to only color TreadMount
            if (componentData.category == ComponentCategory.EngineFrame)
                ApplyColorToTreadMount(model, componentData.customColor);
            else
                ApplyColorToModel(model, componentData.customColor);

            // Apply skin if available (from ComponentCustomizationManager)
            string skinPath = ComponentCustomizationManager.Instance?.GetSkin(componentData.instanceId);
            if (!string.IsNullOrEmpty(skinPath))
            {
                ApplySkinToModel(model, skinPath);
            }
            
            // Apply decal if available (turrets only, from ComponentCustomizationManager)
            if (componentData.category == ComponentCategory.Turret)
            {
                string decalPath = ComponentCustomizationManager.Instance?.GetDecal(componentData.instanceId);
                if (!string.IsNullOrEmpty(decalPath))
                {
                    ApplyDecalToModel(model, decalPath);
                }
            }

            currentModels.Add(model);
        }
    }
    public void ShowTank(Dictionary<ComponentCategory, ComponentData> equipped, TankSlotDataJson slotData = null)
    {
        ClearPreview();        // Engine Frame (base)
        if (equipped.TryGetValue(ComponentCategory.EngineFrame, out var engineFrame) && engineFrame.modelPrefab != null)
        {
            var model = Instantiate(engineFrame.modelPrefab, previewAnchor);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(model, previewLayer);
            ApplyColorToTreadMount(model, engineFrame.customColor);
            
            // Apply skin if available (from ComponentCustomizationManager)
            string engineSkinPath = ComponentCustomizationManager.Instance?.GetSkin(engineFrame.instanceId);
            if (!string.IsNullOrEmpty(engineSkinPath))
            {
                ApplySkinToModel(model, engineSkinPath);
            }
            
            currentModels.Add(model);
        }

        // Turret
        if (equipped.TryGetValue(ComponentCategory.Turret, out var turret) && turret.modelPrefab != null)
        {
            var model = Instantiate(turret.modelPrefab, previewAnchor);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(model, previewLayer);
            ApplyColorToModel(model, turret.customColor);
            
            // Apply skin if available (from ComponentCustomizationManager)
            string turretSkinPath = ComponentCustomizationManager.Instance?.GetSkin(turret.instanceId);
            if (!string.IsNullOrEmpty(turretSkinPath))
            {
                ApplySkinToModel(model, turretSkinPath);
            }
            
            // Apply decal if available (from ComponentCustomizationManager)
            string turretDecalPath = ComponentCustomizationManager.Instance?.GetDecal(turret.instanceId);
            if (!string.IsNullOrEmpty(turretDecalPath))
            {
                ApplyDecalToModel(model, turretDecalPath);
            }
            
            currentModels.Add(model);
        }

        // Armor
        if (equipped.TryGetValue(ComponentCategory.Armor, out var armor) && armor.modelPrefab != null)
        {
            var model = Instantiate(armor.modelPrefab, previewAnchor);
            model.transform.localPosition = new Vector3(0f, armorYOffset, 0f);
            model.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(model, previewLayer);
            ApplyColorToModel(model, armor.customColor);
            
            // Apply skin if available (from ComponentCustomizationManager)
            string armorSkinPath = ComponentCustomizationManager.Instance?.GetSkin(armor.instanceId);
            if (!string.IsNullOrEmpty(armorSkinPath))
            {
                ApplySkinToModel(model, armorSkinPath);
            }
            
            currentModels.Add(model);
        }

        // Add more categories as needed (e.g., AIModule)
    }

    public void ClearPreview()
    {
        foreach (Transform child in previewAnchor)
            Destroy(child.gameObject);
        currentModels.Clear();
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

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

    // Helper: Only color the TreadMount child for EngineFrame components
    private void ApplyColorToTreadMount(GameObject engineFrame, Color color)
    {
        var treadMount = engineFrame.transform.Find("TreadMount");
        if (treadMount != null)
        {
            var renderers = treadMount.GetComponentsInChildren<Renderer>();
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
    }
    
    /// <summary>
    /// Applies a skin texture to a model by loading from Resources and setting as main texture
    /// Also loads additional maps (normal, height, metallic/roughness) from a subfolder with the same name
    /// </summary>
    private void ApplySkinToModel(GameObject model, string skinPath)
    {
        if (string.IsNullOrEmpty(skinPath))
            return;
            
        // Load texture from Resources
        Texture2D skinTexture = Resources.Load<Texture2D>(skinPath);
        if (skinTexture == null)
        {
            Debug.LogWarning($"Could not load skin texture from: {skinPath}");
            return;
        }
        
        Debug.Log($"Applying skin texture: {skinPath}");
        
        // Try to load additional maps from a subfolder with the same name as the texture
        // e.g., if skinPath is "KritaArt/Skins/MySkin", look in "KritaArt/Skins/MySkin/" for additional maps
        Texture2D normalMap = null;
        Texture2D heightMap = null;
        Texture2D metallicMap = null;
        
        string additionalMapsPath = skinPath; // Folder has same name as the texture file
        Texture2D[] additionalTextures = Resources.LoadAll<Texture2D>(additionalMapsPath);
        
        if (additionalTextures != null && additionalTextures.Length > 0)
        {
            foreach (var tex in additionalTextures)
            {
                string nameLower = tex.name.ToLower();
                if (nameLower.Contains("nor"))
                {
                    normalMap = tex;
                    Debug.Log($"Found normal map: {tex.name}");
                }
                else if (nameLower.Contains("disp"))
                {
                    heightMap = tex;
                    Debug.Log($"Found height map: {tex.name}");
                }
                else if (nameLower.Contains("rough"))
                {
                    metallicMap = tex;
                    Debug.Log($"Found metallic/roughness map: {tex.name}");
                }
            }
        }
        
        // Apply to all renderers
        var renderers = model.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            // Skip SpriteRenderers (used for decals)
            if (renderer is SpriteRenderer) continue;
            
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
            Debug.LogWarning($"Could not load decal texture from: {decalPath}");
            return;
        }
        
        Debug.Log($"Applying decal texture: {decalPath}");
        
        // Find the "Decal" child object by name
        Transform decalTransform = model.transform.Find("Decal");
        if (decalTransform == null)
        {
            Debug.LogWarning($"No child named 'Decal' found on turret model {model.name}");
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
            Debug.Log($"Applied decal sprite to {decalTransform.name}");
        }
        else
        {
            Debug.LogWarning($"No SpriteRenderer component found on 'Decal' child of {model.name}");
        }
    }
}
