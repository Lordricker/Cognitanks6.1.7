using UnityEngine;
using UnityEngine.UI;
using System;

[RequireComponent(typeof(Slider))]
public class RainbowColorSlider : MonoBehaviour
{
    public Image colorPreview; // Assign in inspector for color preview
    public Action<Color> onColorChanged;

    private Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        Color color = Color.HSVToRGB(value, 1f, 1f);
        if (colorPreview != null)
            colorPreview.color = color;
        onColorChanged?.Invoke(color);
    }

    public void SetColor(Color color)
    {
        Color.RGBToHSV(color, out float h, out _, out _);
        slider.SetValueWithoutNotify(h);
        if (colorPreview != null)
            colorPreview.color = color;
    }

    public Color GetColor()
    {
        return Color.HSVToRGB(slider.value, 1f, 1f);
    }
}
