using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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
    }

    [Header("UI References")]
    [SerializeField] private GameObject splashPanel;
    
    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 0.3f;
    
    [Header("Arena Buttons and Splash Images")]
    [SerializeField] private List<ArenaButton> arenaButtons = new List<ArenaButton>();

    private CanvasGroup splashCanvasGroup;
    private Coroutine fadeCoroutine;

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
        }

        // Hide splash panel at start
        if (splashCanvasGroup != null)
        {
            splashCanvasGroup.alpha = 0f;
            splashCanvasGroup.gameObject.SetActive(false);
            Debug.Log($"[ArenaSplashManager] Initialized splash panel to hidden");
        }
        else
        {
            Debug.LogError("[ArenaSplashManager] Splash Canvas Group is not assigned!");
        }
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
}
