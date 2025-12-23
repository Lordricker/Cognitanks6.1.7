using UnityEngine;
using System.Collections.Generic;

public class TankPreview : MonoBehaviour
{
    public Transform previewAnchor; // Assign in inspector
    public int previewLayer = 8; // Set to your "ModelPreview" layer number

    private GameObject engineFrameModel;
    private GameObject turretModel;
    private GameObject armorModel;

    public float spinSpeed = 50f;

    void Update()
    {
        if (engineFrameModel != null)
            previewAnchor.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }    public void ShowTank(Dictionary<ComponentCategory, ComponentData> equipped)
    {
        ClearTank();        // Engine Frame (base)
        if (equipped.TryGetValue(ComponentCategory.EngineFrame, out var engineFrame) && engineFrame.modelPrefab != null)
        {
            engineFrameModel = Instantiate(engineFrame.modelPrefab, previewAnchor);
            engineFrameModel.transform.localPosition = Vector3.zero;
            engineFrameModel.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(engineFrameModel, previewLayer);
            ApplyColorToTreadMount(engineFrameModel, engineFrame.customColor);
        }

        // Turret
        if (equipped.TryGetValue(ComponentCategory.Turret, out var turret) && turret.modelPrefab != null)
        {
            turretModel = Instantiate(turret.modelPrefab, previewAnchor);
            turretModel.transform.localPosition = Vector3.zero;
            turretModel.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(turretModel, previewLayer);
            ApplyColorToModel(turretModel, turret.customColor);
        }

        // Armor
        if (equipped.TryGetValue(ComponentCategory.Armor, out var armor) && armor.modelPrefab != null)
        {
            armorModel = Instantiate(armor.modelPrefab, previewAnchor);
            armorModel.transform.localPosition = Vector3.zero;
            armorModel.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(armorModel, previewLayer);
            ApplyColorToModel(armorModel, armor.customColor);
        }
    }    public void ClearTank()
    {
        foreach (Transform child in previewAnchor)
            Destroy(child.gameObject);
        engineFrameModel = null;
        turretModel = null;
        armorModel = null;
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
