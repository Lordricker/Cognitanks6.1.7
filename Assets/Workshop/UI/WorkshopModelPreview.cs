using UnityEngine;
using System.Collections.Generic;


public class WorkshopModelPreview : MonoBehaviour
{
    public Transform previewAnchor; // Assign in inspector
    public int previewLayer = 8; // Set to your "ModelPreview" layer number
    public float spinSpeed = 50f;

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

            currentModels.Add(model);
        }
    }
    public void ShowTank(Dictionary<ComponentCategory, ComponentData> equipped)
    {
        ClearPreview();        // Engine Frame (base)
        if (equipped.TryGetValue(ComponentCategory.EngineFrame, out var engineFrame) && engineFrame.modelPrefab != null)
        {
            var model = Instantiate(engineFrame.modelPrefab, previewAnchor);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(model, previewLayer);
            ApplyColorToTreadMount(model, engineFrame.customColor);
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
            currentModels.Add(model);
        }

        // Armor
        if (equipped.TryGetValue(ComponentCategory.Armor, out var armor) && armor.modelPrefab != null)
        {
            var model = Instantiate(armor.modelPrefab, previewAnchor);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(model, previewLayer);
            ApplyColorToModel(model, armor.customColor);
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
}
