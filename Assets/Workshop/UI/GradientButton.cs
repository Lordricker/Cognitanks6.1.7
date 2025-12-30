using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class GradientButton : MonoBehaviour
{
    [Header("Gradient Colors")]
    public Color topColor = Color.white;
    public Color bottomColor = Color.gray;

    [Header("Gradient Direction")]
    public bool vertical = true; // true = vertical gradient, false = horizontal

    private Image image;

    void Start()
    {
        image = GetComponent<Image>();
        ApplyGradient();
    }

    void ApplyGradient()
    {
        // Create a gradient texture (2x2 for better quality)
        Texture2D gradientTexture = new Texture2D(2, 2);
        gradientTexture.wrapMode = TextureWrapMode.Clamp;
        gradientTexture.filterMode = FilterMode.Bilinear;

        if (vertical)
        {
            // Vertical gradient
            gradientTexture.SetPixel(0, 0, bottomColor);
            gradientTexture.SetPixel(1, 0, bottomColor);
            gradientTexture.SetPixel(0, 1, topColor);
            gradientTexture.SetPixel(1, 1, topColor);
        }
        else
        {
            // Horizontal gradient
            gradientTexture.SetPixel(0, 0, bottomColor);
            gradientTexture.SetPixel(1, 0, topColor);
            gradientTexture.SetPixel(0, 1, bottomColor);
            gradientTexture.SetPixel(1, 1, topColor);
        }

        gradientTexture.Apply();

        // Create a sprite from the texture
        Sprite gradientSprite = Sprite.Create(gradientTexture,
            new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

        image.sprite = gradientSprite;
        image.type = Image.Type.Sliced;
    }

    // Call this in editor to preview changes
    void OnValidate()
    {
        if (Application.isPlaying) return;
        image = GetComponent<Image>();
        if (image != null) ApplyGradient();
    }

    // Public method to update gradient colors programmatically
    public void SetGradientColors(Color top, Color bottom)
    {
        topColor = top;
        bottomColor = bottom;
        ApplyGradient();
    }

    // Public method to change gradient direction
    public void SetGradientDirection(bool isVertical)
    {
        vertical = isVertical;
        ApplyGradient();
    }
}