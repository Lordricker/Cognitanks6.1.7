using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple HP bar that follows a tank and displays its health as a red/green bar
/// </summary>
public class TankHPBar : MonoBehaviour
{
    [Header("HP Bar Settings")]
    [SerializeField] private Canvas hpCanvas;
    [SerializeField] private Image hpBarBackground; // Red background
    [SerializeField] private Image hpBarFill;       // Green fill
    [SerializeField] private TextMeshProUGUI hpText; // Health text display
    [SerializeField] private float barWidth = 200f;
    [SerializeField] private float barHeight = 10f; // Half the previous height (was 20f)
    [SerializeField] private float heightOffset = 25f; // How high above tank to display
    
    private TankMan targetTank;
    private Camera mainCamera;
    
    void Start()
    {
        // Find the main camera
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindFirstObjectByType<Camera>();
            
        CreateHPBar();
    }
    
    void CreateHPBar()
    {
        // Create canvas for world space UI
        GameObject canvasGO = new GameObject("HPBarCanvas");
        canvasGO.transform.SetParent(transform);
        
        hpCanvas = canvasGO.AddComponent<Canvas>();
        hpCanvas.renderMode = RenderMode.WorldSpace;
        hpCanvas.worldCamera = mainCamera;
        hpCanvas.sortingOrder = 100; // Ensure it renders on top
        
        // Add CanvasScaler for consistent sizing
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        
        // Add GraphicRaycaster (required for UI)
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // Set canvas size and position
        RectTransform canvasRect = hpCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(barWidth, barHeight);
        canvasRect.localPosition = new Vector3(0, heightOffset, 0);
        canvasRect.localScale = Vector3.one * 0.1f; // Adjusted scale
        canvasRect.localRotation = Quaternion.identity;
        
        // Create border (white outline) - FIRST (back layer)
        GameObject borderGO = new GameObject("Border");
        borderGO.transform.SetParent(canvasGO.transform, false);
        Image hpBarBorder = borderGO.AddComponent<Image>();
        hpBarBorder.color = Color.white;
        hpBarBorder.sprite = CreateRoundedRectSprite(barWidth + 4, barHeight + 4, 6f); // Doubled corner radius for border
        hpBarBorder.type = Image.Type.Sliced;
        
        RectTransform borderRect = borderGO.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2, -2); // Extend 2 pixels outside
        borderRect.offsetMax = new Vector2(2, 2);   // Extend 2 pixels outside
        borderRect.anchoredPosition = Vector2.zero;
        
        // Create background (red) - SECOND (middle layer) - ALWAYS FULL WIDTH
        GameObject backgroundGO = new GameObject("Background");
        backgroundGO.transform.SetParent(canvasGO.transform, false);
        hpBarBackground = backgroundGO.AddComponent<Image>();
        hpBarBackground.color = Color.red;
        hpBarBackground.sprite = CreateRoundedRectSprite(barWidth, barHeight, 4f); // Doubled corner radius
        hpBarBackground.type = Image.Type.Sliced;
        
        RectTransform bgRect = backgroundGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        // Create fill (green) - THIRD (front layer) - PARTIAL WIDTH BASED ON HEALTH
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(canvasGO.transform, false);
        hpBarFill = fillGO.AddComponent<Image>();
        hpBarFill.color = Color.green;
        hpBarFill.sprite = CreateRoundedRectSprite(barWidth, barHeight, 4f); // Doubled corner radius
        hpBarFill.type = Image.Type.Sliced; // Use sliced to maintain rounded corners when scaling
        
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(1, 1); // This will be modified by fillAmount
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        
        // Ensure proper rendering order (last created = on top)
        borderGO.transform.SetSiblingIndex(0);     // Back
        backgroundGO.transform.SetSiblingIndex(1); // Middle  
        fillGO.transform.SetSiblingIndex(2);       // Front
        
        // Create text display - FOURTH (top layer)
        GameObject textGO = new GameObject("HPText");
        textGO.transform.SetParent(canvasGO.transform, false);
        hpText = textGO.AddComponent<TextMeshProUGUI>();
        hpText.text = "100/100"; // Default text
        hpText.fontSize = 14;
        hpText.color = Color.white;
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.textWrappingMode = TextWrappingModes.NoWrap;
        
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;
        
        textGO.transform.SetSiblingIndex(3); // On top
    }
    
    void Update()
    {
        if (targetTank == null)
        {
            // Try to find TankMan on this GameObject
            targetTank = GetComponent<TankMan>();
            if (targetTank == null)
                return;
        }
        
        // Update HP bar fill based on tank's health
        float healthPercent = targetTank.CurrentHealth / targetTank.TotalHP;
        if (hpBarFill != null)
        {
            // MANUAL WIDTH CONTROL instead of fillAmount
            RectTransform fillRect = hpBarFill.GetComponent<RectTransform>();
            healthPercent = Mathf.Clamp01(healthPercent);
            
            // Set the anchorMax.x to control the width (0 = no width, 1 = full width)
            fillRect.anchorMax = new Vector2(healthPercent, 1);
        }
        
        // Update HP text
        if (hpText != null)
        {
            hpText.text = $"{Mathf.RoundToInt(targetTank.CurrentHealth)}/{Mathf.RoundToInt(targetTank.TotalHP)}";
        }
        
        // Make HP bar face the camera
        if (mainCamera != null && hpCanvas != null)
        {
            // Always face the camera
            Vector3 directionToCamera = hpCanvas.transform.position - mainCamera.transform.position;
            hpCanvas.transform.rotation = Quaternion.LookRotation(directionToCamera);
            
            // Keep the HP bar at the correct height above the tank
            Vector3 targetPosition = transform.position + Vector3.up * heightOffset;
            hpCanvas.transform.position = targetPosition;
        }
        
        // Hide HP bar if tank is destroyed
        if (targetTank.CurrentHealth <= 0 && hpCanvas != null)
        {
            hpCanvas.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Manually set the target tank (useful if HP bar is on a different GameObject)
    /// </summary>
    public void SetTargetTank(TankMan tank)
    {
        targetTank = tank;
    }
    
    /// <summary>
    /// Update HP bar colors
    /// </summary>
    public void SetColors(Color backgroundColor, Color fillColor)
    {
        if (hpBarBackground != null)
            hpBarBackground.color = backgroundColor;
        if (hpBarFill != null)
            hpBarFill.color = fillColor;
    }
    
    /// <summary>
    /// Creates a sprite with rounded corners for the HP bar
    /// </summary>
    private Sprite CreateRoundedRectSprite(float width, float height, float cornerRadius)
    {
        // Create a small texture for the rounded rectangle
        int textureWidth = Mathf.RoundToInt(width);
        int textureHeight = Mathf.RoundToInt(height);
        
        // Ensure minimum size for proper rendering
        textureWidth = Mathf.Max(textureWidth, 20);
        textureHeight = Mathf.Max(textureHeight, 10);
        
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[textureWidth * textureHeight];
        
        // Create rounded rectangle shape
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                float normalizedX = (float)x / textureWidth;
                float normalizedY = (float)y / textureHeight;
                
                // Check if pixel is inside rounded rectangle
                bool insideRect = IsInsideRoundedRect(normalizedX, normalizedY, cornerRadius / width, cornerRadius / height);
                
                int index = y * textureWidth + x;
                pixels[index] = insideRect ? Color.white : Color.clear;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        // Create sprite with proper pivot and borders for 9-slice
        Vector4 border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, textureWidth, textureHeight), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        
        return sprite;
    }
    
    /// <summary>
    /// Helper method to check if a normalized point is inside a rounded rectangle
    /// </summary>
    private bool IsInsideRoundedRect(float normalizedX, float normalizedY, float normalizedCornerRadiusX, float normalizedCornerRadiusY)
    {
        // Convert to centered coordinates (-0.5 to 0.5)
        float x = normalizedX - 0.5f;
        float y = normalizedY - 0.5f;
        
        // Half dimensions
        float halfWidth = 0.5f;
        float halfHeight = 0.5f;
        
        // Check if point is in corner regions
        float cornerX = halfWidth - normalizedCornerRadiusX;
        float cornerY = halfHeight - normalizedCornerRadiusY;
        
        // If in corner region, check if inside circle
        if (Mathf.Abs(x) > cornerX && Mathf.Abs(y) > cornerY)
        {
            float dx = Mathf.Abs(x) - cornerX;
            float dy = Mathf.Abs(y) - cornerY;
            float distanceSquared = (dx / normalizedCornerRadiusX) * (dx / normalizedCornerRadiusX) + 
                                  (dy / normalizedCornerRadiusY) * (dy / normalizedCornerRadiusY);
            return distanceSquared <= 1.0f;
        }
        
        // If not in corner region, just check if inside rectangle
        return Mathf.Abs(x) <= halfWidth && Mathf.Abs(y) <= halfHeight;
    }
}
