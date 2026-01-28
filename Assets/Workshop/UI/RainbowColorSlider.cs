using UnityEngine;
using UnityEngine.UI;
using System;

[RequireComponent(typeof(Slider))]
public class RainbowColorSlider : MonoBehaviour
{
    public Image colorPreview; // Assign in inspector for color preview
    public Action<Color> onColorChanged;

    private Slider slider;
    
    // Define the ranges for the slider:
    // 0.0 = White (default)
    // 0.05 - 0.95 = Rainbow colors (hue 0 to 1)
    // 1.0 = Black
    private const float WHITE_THRESHOLD = 0.05f;
    private const float BLACK_THRESHOLD = 0.95f;

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
        Color color = GetColorFromSliderValue(value);
        if (colorPreview != null)
            colorPreview.color = color;
        onColorChanged?.Invoke(color);
    }
    
    private Color GetColorFromSliderValue(float value)
    {
        if (value <= WHITE_THRESHOLD)
        {
            // Far left = White (default)
            return Color.white;
        }
        else if (value >= BLACK_THRESHOLD)
        {
            // Far right = Black
            return Color.black;
        }
        else
        {
            // Middle section = Rainbow colors
            // Map the value from (WHITE_THRESHOLD to BLACK_THRESHOLD) to (0 to 1) for hue
            float hue = (value - WHITE_THRESHOLD) / (BLACK_THRESHOLD - WHITE_THRESHOLD);
            return Color.HSVToRGB(hue, 1f, 1f);
        }
    }

    public void SetColor(Color color)
    {
        float sliderValue;
        
        // Check for white
        if (color == Color.white || (color.r >= 0.99f && color.g >= 0.99f && color.b >= 0.99f))
        {
            sliderValue = 0f;
        }
        // Check for black
        else if (color == Color.black || (color.r <= 0.01f && color.g <= 0.01f && color.b <= 0.01f))
        {
            sliderValue = 1f;
        }
        else
        {
            // Convert color to hue and map to slider range
            Color.RGBToHSV(color, out float h, out _, out _);
            sliderValue = WHITE_THRESHOLD + h * (BLACK_THRESHOLD - WHITE_THRESHOLD);
        }
        
        slider.SetValueWithoutNotify(sliderValue);
        if (colorPreview != null)
            colorPreview.color = color;
    }

    public Color GetColor()
    {
        return GetColorFromSliderValue(slider.value);
    }
}
