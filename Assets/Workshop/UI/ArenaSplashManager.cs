using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Video;

/// <summary>
/// Manages arena/story selection buttons and displays corresponding splash images with fade effects
/// </summary>
public class ArenaSplashManager : MonoBehaviour
{
    [System.Serializable]
    public class ArenaButton
    {
        public Button button;
        public GameObject splashImage;
        
        [Header("Video Settings (Optional)")]
        public VideoClip videoClip;
        public VideoPlayer videoPlayer;
        public GameObject videoPanel;
    }

    [Header("UI References")]
    [SerializeField] private GameObject splashPanel;
    
    [Header("Video Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float videoVolume = 0.5f; // Volume for all splash videos (0-1)
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float expandDuration = 0.5f;
    [SerializeField] private float videoDelay = 2f;
    
    [Header("Arena Buttons and Splash Images")]
    [SerializeField] private List<ArenaButton> arenaButtons = new List<ArenaButton>();

    private CanvasGroup splashCanvasGroup;
    private Coroutine fadeCoroutine;
    private Dictionary<GameObject, float> originalVideoHeights = new Dictionary<GameObject, float>();

    void Start()
    {
        Debug.Log($"[ArenaSplashManager] Starting setup. Arena buttons count: {arenaButtons.Count}");
        Debug.Log($"[ArenaSplashManager] Splash panel assigned: {splashPanel != null}");
        
        // Get or add CanvasGroup component
        if (splashPanel != null)
        {
            splashCanvasGroup = splashPanel.GetComponent<CanvasGroup>();
            if (splashCanvasGroup == null)
            {
                Debug.LogWarning("[ArenaSplashManager] No CanvasGroup found on splash panel, adding one...");
                splashCanvasGroup = splashPanel.AddComponent<CanvasGroup>();
            }
            Debug.Log($"[ArenaSplashManager] CanvasGroup found/created: {splashCanvasGroup != null}");
        }
        else
        {
            Debug.LogError("[ArenaSplashManager] Splash Panel GameObject is not assigned!");
        }
        
        // Setup button listeners
        for (int i = 0; i < arenaButtons.Count; i++)
        {
            int index = i; // Capture index for closure
            if (arenaButtons[i].button != null)
            {
                arenaButtons[i].button.onClick.AddListener(() => ShowSplash(index));
                Debug.Log($"[ArenaSplashManager] Added listener to button {index}");
            }
            else
            {
                Debug.LogWarning($"[ArenaSplashManager] Button at index {i} is null!");
            }
            
            // Add click handler to splash image
            if (arenaButtons[i].splashImage != null)
            {
                AddClickHandler(arenaButtons[i].splashImage);
            }
            else
            {
                Debug.LogWarning($"[ArenaSplashManager] Splash image at index {i} is null!");
            }
            
            // Setup video panel if present
            if (arenaButtons[i].videoPanel != null)
            {
                RectTransform rt = arenaButtons[i].videoPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    originalVideoHeights[arenaButtons[i].videoPanel] = rt.sizeDelta.y;
                    arenaButtons[i].videoPanel.SetActive(false);
                }
                
                // Setup video player
                if (arenaButtons[i].videoPlayer != null)
                {
                    RawImage rawImage = arenaButtons[i].videoPanel.GetComponentInChildren<RawImage>();
                    if (rawImage != null && arenaButtons[i].videoClip != null)
                    {
                        // Create RenderTexture matching video dimensions
                        int width = (int)arenaButtons[i].videoClip.width;
                        int height = (int)arenaButtons[i].videoClip.height;
                        arenaButtons[i].videoPlayer.targetTexture = new RenderTexture(width, height, 0);
                        rawImage.texture = arenaButtons[i].videoPlayer.targetTexture;
                        
                        // Set RawImage to stretch to fill its RectTransform
                        RectTransform rawImageRT = rawImage.GetComponent<RectTransform>();
                        if (rawImageRT != null)
                        {
                            // Anchor to stretch to fill parent
                            rawImageRT.anchorMin = Vector2.zero;
                            rawImageRT.anchorMax = Vector2.one;
                            rawImageRT.offsetMin = Vector2.zero;
                            rawImageRT.offsetMax = Vector2.zero;
                        }
                        
                        // Set UV rect to display full video
                        rawImage.uvRect = new Rect(0, 0, 1, 1);
                    }
                    arenaButtons[i].videoPlayer.clip = arenaButtons[i].videoClip;
                    
                    // Add completion event listener
                    ArenaButton capturedButton = arenaButtons[i];
                    arenaButtons[i].videoPlayer.loopPointReached += (vp) => OnVideoFinished(capturedButton);
                }
            }
        }

        // Setup video volume
        SetAllVideoVolumes(videoVolume);
        Debug.Log($"[ArenaSplashManager] Video volume set to: {videoVolume}");
    }

    /// <summary>
    /// Adds click handler to splash image
    /// </summary>
    private void AddClickHandler(GameObject splashImage)
    {
        // Try to get existing Button component
        Button button = splashImage.GetComponent<Button>();
        if (button == null)
        {
            button = splashImage.AddComponent<Button>();
            // Make button fill the entire image
            button.targetGraphic = splashImage.GetComponent<Image>();
            if (button.targetGraphic != null)
            {
                button.targetGraphic.raycastTarget = true;
            }
        }
        
        // Remove existing listeners to avoid duplicates
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(HideSplash);
    }

    /// <summary>
    /// Shows the splash panel with fade in and the selected arena image
    /// </summary>
    private void ShowSplash(int index)
    {
        Debug.Log($"[ArenaSplashManager] ShowSplash called with index: {index}");
        
        if (index < 0 || index >= arenaButtons.Count || splashCanvasGroup == null)
        {
            Debug.LogWarning($"[ArenaSplashManager] ShowSplash failed - index: {index}, arenaButtons count: {arenaButtons.Count}, canvasGroup null: {splashCanvasGroup == null}");
            return;
        }

        Debug.Log($"[ArenaSplashManager] Activating splash panel and fading in...");
        
        // Stop any existing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // Activate and fade in
        splashCanvasGroup.gameObject.SetActive(true);
        fadeCoroutine = StartCoroutine(FadeCanvasGroup(splashCanvasGroup, 0f, 1f, fadeDuration));

        // Deactivate all splash images
        foreach (var arenaButton in arenaButtons)
        {
            if (arenaButton.splashImage != null)
            {
                arenaButton.splashImage.SetActive(false);
            }
        }

        // Activate the selected splash image
        if (arenaButtons[index].splashImage != null)
        {
            arenaButtons[index].splashImage.SetActive(true);
            Debug.Log($"[ArenaSplashManager] Activated splash image at index {index}");
        }
        else
        {
            Debug.LogWarning($"[ArenaSplashManager] Splash image at index {index} is null!");
        }

        // If this button has a video, start video playback after delay
        if (arenaButtons[index].videoClip != null && arenaButtons[index].videoPanel != null)
        {
            StartCoroutine(PlayStoryVideo(arenaButtons[index]));
        }
    }

    /// <summary>
    /// Hides the splash panel with fade out
    /// </summary>
    public void HideSplash()
    {
        if (splashCanvasGroup == null)
            return;

        // Stop any existing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // Stop all videos and hide video panels
        foreach (var arenaButton in arenaButtons)
        {
            if (arenaButton.videoPlayer != null)
            {
                arenaButton.videoPlayer.Stop();
            }
            if (arenaButton.videoPanel != null)
            {
                arenaButton.videoPanel.SetActive(false);
            }
        }

        // Fade out and deactivate
        fadeCoroutine = StartCoroutine(FadeOutAndDeactivate());
    }

    /// <summary>
    /// Coroutine to fade canvas group
    /// </summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration)
    {
        canvasGroup.alpha = startAlpha;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }
        
        canvasGroup.alpha = endAlpha;
    }

    /// <summary>
    /// Coroutine to fade out and deactivate
    /// </summary>
    private IEnumerator FadeOutAndDeactivate()
    {
        yield return StartCoroutine(FadeCanvasGroup(splashCanvasGroup, splashCanvasGroup.alpha, 0f, fadeDuration));
        splashCanvasGroup.gameObject.SetActive(false);
    }

    /// <summary>
    /// Coroutine to play story video after delay
    /// </summary>
    private IEnumerator PlayStoryVideo(ArenaButton arenaButton)
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(videoDelay);

        // Expand the video window vertically
        if (arenaButton.videoPanel != null)
        {
            RectTransform videoRectTransform = arenaButton.videoPanel.GetComponent<RectTransform>();
            if (videoRectTransform != null)
            {
                arenaButton.videoPanel.SetActive(true);
                float targetHeight = originalVideoHeights[arenaButton.videoPanel];
                StartCoroutine(ExpandVideoWindow(videoRectTransform, targetHeight));
            }
        }

        // Play the video
        if (arenaButton.videoPlayer != null)
        {
            arenaButton.videoPlayer.Play();
        }
    }

    /// <summary>
    /// Coroutine to expand video window vertically
    /// </summary>
    private IEnumerator ExpandVideoWindow(RectTransform rectTransform, float targetHeight)
    {
        float elapsed = 0f;
        Vector2 startSize = new Vector2(rectTransform.sizeDelta.x, 0f);
        Vector2 endSize = new Vector2(rectTransform.sizeDelta.x, targetHeight);

        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            rectTransform.sizeDelta = Vector2.Lerp(startSize, endSize, elapsed / expandDuration);
            yield return null;
        }

        rectTransform.sizeDelta = endSize;
    }

    /// <summary>
    /// Called when video finishes playing
    /// </summary>
    private void OnVideoFinished(ArenaButton arenaButton)
    {
        if (arenaButton.videoPanel != null)
        {
            StartCoroutine(CollapseVideoPanel(arenaButton));
        }
    }

    /// <summary>
    /// Coroutine to collapse video panel and deactivate
    /// </summary>
    private IEnumerator CollapseVideoPanel(ArenaButton arenaButton)
    {
        RectTransform rectTransform = arenaButton.videoPanel.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            float elapsed = 0f;
            float startHeight = originalVideoHeights[arenaButton.videoPanel];
            Vector2 startSize = new Vector2(rectTransform.sizeDelta.x, startHeight);
            Vector2 endSize = new Vector2(rectTransform.sizeDelta.x, 0f);

            while (elapsed < expandDuration)
            {
                elapsed += Time.deltaTime;
                rectTransform.sizeDelta = Vector2.Lerp(startSize, endSize, elapsed / expandDuration);
                yield return null;
            }

            rectTransform.sizeDelta = endSize;
        }
        
        arenaButton.videoPanel.SetActive(false);
    }

    /// <summary>
    /// Sets the volume for all video players
    /// </summary>
    private void SetAllVideoVolumes(float volume)
    {
        foreach (var arenaButton in arenaButtons)
        {
            if (arenaButton.videoPlayer != null)
            {
                arenaButton.videoPlayer.SetDirectAudioVolume(0, volume);
            }
        }
        Debug.Log($"[ArenaSplashManager] Set volume to {volume} for all video players");
    }

    /// <summary>
    /// Public method to update video volume at runtime
    /// </summary>
    public void UpdateVideoVolume(float newVolume)
    {
        videoVolume = Mathf.Clamp01(newVolume);
        SetAllVideoVolumes(videoVolume);
    }
}
