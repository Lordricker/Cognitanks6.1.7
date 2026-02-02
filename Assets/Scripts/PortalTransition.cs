using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Portal Warp Transition Effect - UI-Based Version
/// Uses UI elements for guaranteed visibility.
/// 
/// SETUP:
/// 1. Create empty ROOT GameObject in Shop scene, attach this script
/// 2. Create a Canvas child (Screen Space - Overlay)
/// 3. Add a full-screen Image to Canvas (black, alpha=0)
/// 4. Assign Image to fadeToBlackImage
/// 5. Drag into LeagueDropdownManager's portalTransition field
/// </summary>
public class PortalTransition : MonoBehaviour
{
    [Header("=== REQUIRED ===")]
    public Image fadeToBlackImage;
    
    [Header("=== SETTINGS ===")]
    public float transitionDuration = 5f;
    public float delayBeforeSceneLoad = 0.5f;

    [Header("=== COLORS ===")]
    public Color portalColor1 = new Color(0f, 0.8f, 1f, 1f);  // Cyan
    public Color portalColor2 = new Color(0.5f, 0f, 1f, 1f);  // Purple
    public Color sparkColor = new Color(1f, 1f, 1f, 1f);      // White

    [Header("=== EFFECT ===")]
    public int numberOfRings = 3;
    public int numberOfSparkles = 20;
    public float maxRingSize = 2000f;
    
    [Header("=== OPTIONAL RING IMAGES ===")]
    [Tooltip("Leave empty to use default colored rings. Assign 1-3 images for custom rings.")]
    public Sprite[] ringImages = new Sprite[0];
    
    [Tooltip("Optional alpha values for each ring. If provided, will override default alpha. Should match number of rings or ring images.")]
    public float[] ringAlphas = new float[0];

    private string sceneToLoad;
    private bool isTransitioning = false;
    private Canvas portalCanvas;
    private List<GameObject> portalElements = new List<GameObject>();

    private void Start()
    {
        if (fadeToBlackImage != null)
        {
            fadeToBlackImage.color = new Color(0, 0, 0, 0);
            fadeToBlackImage.raycastTarget = false;
            
            Canvas canvas = fadeToBlackImage.canvas;
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 9999;
                canvas.overrideSorting = true;
                portalCanvas = canvas;
                
                RectTransform rt = fadeToBlackImage.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }
    }

    public void StartTransition(string sceneName)
    {
        if (isTransitioning) return;
        
        isTransitioning = true;
        sceneToLoad = sceneName;
        
        if (fadeToBlackImage != null)
        {
            fadeToBlackImage.raycastTarget = true;
        }
        
        StartCoroutine(TransitionCoroutine());
    }

    private IEnumerator TransitionCoroutine()
    {
        if (portalCanvas == null)
        {
            Debug.LogError("PortalTransition: No canvas found!");
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
            yield break;
        }

        // Create portal effect elements
        CreatePortalRings();
        CreateSparkles();

        // Animate everything - fade FIRST, then portal
        float elapsed = 0f;
        float fadeDuration = transitionDuration * 0.4f;     // Fade takes first 40%
        float portalStartTime = transitionDuration * 0.5f;  // Portal starts at 50% (after fade)
        float portalDuration = transitionDuration * 0.5f;   // Portal lasts remaining 50%
        float totalDuration = transitionDuration + delayBeforeSceneLoad;  // Total time including delay

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / totalDuration;

            // Fade to black FIRST (happens early)
            if (elapsed <= fadeDuration && fadeToBlackImage != null)
            {
                float fadeT = Mathf.Clamp01(elapsed / fadeDuration);
                fadeToBlackImage.color = new Color(0, 0, 0, fadeT);
            }
            else if (fadeToBlackImage != null)
            {
                // Keep it black
                fadeToBlackImage.color = Color.black;
            }

            // Animate rings expanding (AFTER fade completes)
            if (elapsed > portalStartTime)
            {
                float portalT = Mathf.Clamp01((elapsed - portalStartTime) / portalDuration);
                AnimateRings(portalT, elapsed);
            }

            // Animate sparkles (AFTER fade completes)
            if (elapsed > portalStartTime)
            {
                float portalT = Mathf.Clamp01((elapsed - portalStartTime) / portalDuration);
                AnimateSparkles(portalT);
            }

            yield return null;
        }

        // Ensure fully black
        if (fadeToBlackImage != null)
        {
            fadeToBlackImage.color = Color.black;
        }

        // Clean up
        foreach (var element in portalElements)
        {
            if (element != null) Destroy(element);
        }
        portalElements.Clear();

        // Load scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
    }

    private void CreatePortalRings()
    {
        // Create default ring sprite only if no custom images provided
        Sprite defaultRingSprite = (ringImages == null || ringImages.Length == 0) ? CreateRingSprite() : null;
        
        for (int i = 0; i < numberOfRings; i++)
        {
            GameObject ringObj = new GameObject($"PortalRing_{i}");
            ringObj.transform.SetParent(portalCanvas.transform, false);
            
            // Position AFTER fade image so rings render on top of black screen
            ringObj.transform.SetAsLastSibling();

            Image ringImage = ringObj.AddComponent<Image>();
            
            // Calculate color blend value (used for timing even with custom images)
            float colorT = (float)i / Mathf.Max(1, numberOfRings - 1);
            
            // Use custom sprite if available, otherwise use default colored ring
            if (ringImages != null && ringImages.Length > 0)
            {
                // Cycle through provided images
                int imageIndex = i % ringImages.Length;
                ringImage.sprite = ringImages[imageIndex];
                float alpha = (ringAlphas.Length > 0) ? ringAlphas[i % ringAlphas.Length] : 1f;
                ringImage.color = new Color(1f, 1f, 1f, alpha);
            }
            else
            {
                // Use procedurally generated colored rings
                ringImage.sprite = defaultRingSprite;
                Color baseColor = Color.Lerp(portalColor1, portalColor2, colorT);
                float alpha = (ringAlphas.Length > 0) ? ringAlphas[i % ringAlphas.Length] : 1f;
                ringImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }
            
            ringImage.type = Image.Type.Simple;
            ringImage.preserveAspect = true;

            RectTransform rt = ringObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            // Store delay value for animation
            RingData data = ringObj.AddComponent<RingData>();
            data.delay = colorT * 0.3f;
            data.colorT = colorT;
            data.ringIndex = i;
            
            portalElements.Add(ringObj);
        }
    }

    private void CreateSparkles()
    {
        Sprite sparkSprite = CreateSparkSprite();
        
        for (int i = 0; i < numberOfSparkles; i++)
        {
            GameObject sparkObj = new GameObject($"Sparkle_{i}");
            sparkObj.transform.SetParent(portalCanvas.transform, false);
            sparkObj.transform.SetAsLastSibling(); // Render on top of black screen

            Image sparkImage = sparkObj.AddComponent<Image>();
            sparkImage.sprite = sparkSprite;
            sparkImage.color = new Color(sparkColor.r, sparkColor.g, sparkColor.b, 0f); // Start invisible

            RectTransform rt = sparkObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30, 30);
            rt.anchoredPosition = Vector2.zero;

            // Store animation data
            SparkData data = sparkObj.AddComponent<SparkData>();
            data.angle = Random.Range(0f, 360f);
            data.speed = Random.Range(400f, 1000f);
            data.delay = Random.Range(0f, 0.4f);
            
            rt.localRotation = Quaternion.Euler(0, 0, data.angle);

            portalElements.Add(sparkObj);
        }
    }

    private void AnimateRings(float t, float elapsedTime)
    {
        foreach (var element in portalElements)
        {
            if (element == null) continue;
            
            RingData data = element.GetComponent<RingData>();
            if (data == null) continue;

            float ringT = Mathf.Clamp01((t - data.delay) / (1f - data.delay));
            
            // Ease out with exponential slowdown in last 10%
            float easedT;
            if (ringT < 0.9f)
            {
                // Normal ease out for first 90%
                float t90 = ringT / 0.9f;
                easedT = (1f - Mathf.Pow(1f - t90, 3f)) * 0.9f;
            }
            else
            {
                // Exponential slowdown for last 10%
                float tLast = (ringT - 0.9f) / 0.1f;
                easedT = 0.9f + (0.1f * (1f - Mathf.Exp(-tLast * 5f)));
            }

            RectTransform rt = element.GetComponent<RectTransform>();
            float size = Mathf.Lerp(0, maxRingSize, easedT);
            
            // In last 20% of TOTAL animation, expand all rings together
            if (t > 0.8f)
            {
                float expansionT = (t - 0.8f) / 0.2f; // 0 to 1 over last 20%
                float expansionMultiplier = 1f + (expansionT * 2f); // Scale up to 1.5x
                size *= expansionMultiplier;
            }
            
            rt.sizeDelta = new Vector2(size, size);

            // Fade out as it expands
            Image img = element.GetComponent<Image>();
            if (img != null)
            {
                Color c = img.color;
                c.a = 1f - (ringT * 0.6f); // Fade to 40% alpha
                img.color = c;
            }

            // Constant rotation speed based on ring index
            // Ring 0: 180 deg/sec clockwise
            // Ring 1: 144 deg/sec counter-clockwise (80% speed)
            // Ring 2: 108 deg/sec clockwise (60% speed)
            int ringIndex = data.ringIndex;
            float baseSpeed = 180f; // degrees per second
            float rotationSpeed;
            
            if (ringIndex % 3 == 0)
            {
                // Ring 1, 4, 7... clockwise at 100% speed
                rotationSpeed = baseSpeed;
            }
            else if (ringIndex % 3 == 1)
            {
                // Ring 2, 5, 8... counter-clockwise at 80% speed
                rotationSpeed = -baseSpeed * 0.8f;
            }
            else
            {
                // Ring 3, 6, 9... clockwise at 60% speed
                rotationSpeed = baseSpeed * 0.6f;
            }
            
            float rotation = elapsedTime * rotationSpeed;
            rt.localRotation = Quaternion.Euler(0, 0, rotation);
        }
    }

    private void AnimateSparkles(float t)
    {
        foreach (var element in portalElements)
        {
            if (element == null) continue;
            
            SparkData data = element.GetComponent<SparkData>();
            if (data == null) continue;

            float sparkT = Mathf.Clamp01((t - data.delay) / (1f - data.delay));
            if (sparkT <= 0) continue;

            RectTransform rt = element.GetComponent<RectTransform>();
            
            // Start halfway out and spiral outward
            float startDist = data.speed * 0.5f;
            float distance = Mathf.Lerp(startDist, data.speed, sparkT);
            
            // Add spiral rotation (360 degrees over the animation)
            float spiralAngle = data.angle + (sparkT * 360f);
            float rad = spiralAngle * Mathf.Deg2Rad;
            
            rt.anchoredPosition = new Vector2(
                Mathf.Cos(rad) * distance,
                Mathf.Sin(rad) * distance
            );

            // Fade and shrink
            Image img = element.GetComponent<Image>();
            if (img != null)
            {
                Color c = img.color;
                c.a = 1f - sparkT;
                img.color = c;
            }

            float scale = Mathf.Lerp(1f, 0.2f, sparkT);
            rt.localScale = Vector3.one * scale;
        }
    }

    private Sprite CreateRingSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        
        float center = size / 2f;
        float outerRadius = size / 2f - 2f;
        float innerRadius = outerRadius - 14f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                
                if (dist >= innerRadius && dist <= outerRadius)
                {
                    float edgeDist = Mathf.Min(dist - innerRadius, outerRadius - dist);
                    float alpha = Mathf.Clamp01(edgeDist / 4f);
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply();
        
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateSparkSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        
        float center = size / 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(1f - (dist / center));
                alpha = alpha * alpha;
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply();
        
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}

// Helper components to store animation data
public class RingData : MonoBehaviour
{
    public float delay;
    public float colorT;
    public int ringIndex;
}

public class SparkData : MonoBehaviour
{
    public float angle;
    public float speed;
    public float delay;
}
