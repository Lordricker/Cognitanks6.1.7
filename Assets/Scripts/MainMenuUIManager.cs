using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the main menu panel and its sub-panels (Settings, Credits).
/// The main menu panel slides up off-screen when dismissed (e.g. via Workshop button).
/// Settings and Credits panels slide in/out independently.
/// 
/// Setup:
///   1. Attach this script to a GameObject in your main menu scene/canvas.
///   2. Assign the Main Menu Panel, Workshop button, Settings button, Credits button,
///      Settings Panel, and Credits Panel in the Inspector.
///   3. Position the Settings and Credits panels at their desired on-screen anchored positions.
///      They will be moved off-screen on Start and slid in when their buttons are pressed.
/// </summary>
public class MainMenuUIManager : MonoBehaviour
{
    [Header("Main Menu Panel")]
    [Tooltip("The root panel that contains Workshop / Settings / Credits buttons.")]
    public GameObject mainMenuPanel;
    [Tooltip("Speed at which the main menu panel slides up off-screen (pixels per second).")]
    public float mainMenuSlideSpeed = 2000f;

    [Header("Main Menu Buttons")]
    [Tooltip("Slides the main menu panel up and hides it (enters the Workshop).")]
    public Button workshopButton;
    public Button settingsButton;
    public Button creditsButton;

    [Header("Settings Panel")]
    [Tooltip("Shown when the Settings button is pressed. Position it on-screen in the Inspector.")]
    public GameObject settingsPanel;
    [Tooltip("Speed at which the settings panel slides in/out (pixels per second).")]
    public float settingsPanelSlideSpeed = 2000f;

    [Header("Credits Panel")]
    [Tooltip("Shown when the Credits button is pressed. Position it on-screen in the Inspector.")]
    public GameObject creditsPanel;
    [Tooltip("Speed at which the credits panel slides in/out (pixels per second).")]
    public float creditsPanelSlideSpeed = 2000f;

    // ── Main Menu Panel state ──────────────────────────────────────────────
    private RectTransform mainMenuRect;
    private Vector2 mainMenuOnScreenPos;
    private Vector2 mainMenuOffScreenPos;   // above the top edge
    private bool isMainMenuVisible = true;
    private Coroutine mainMenuCoroutine;

    // ── Settings Panel state ───────────────────────────────────────────────
    private RectTransform settingsRect;
    private Vector2 settingsOnScreenPos;
    private Vector2 settingsOffScreenPos;
    private bool isSettingsVisible = false;
    private Coroutine settingsCoroutine;

    // ── Credits Panel state ────────────────────────────────────────────────
    private RectTransform creditsRect;
    private Vector2 creditsOnScreenPos;
    private Vector2 creditsOffScreenPos;
    private bool isCreditsVisible = false;
    private Coroutine creditsCoroutine;

    // ──────────────────────────────────────────────────────────────────────
    private void Start()
    {
        // ── Main Menu Panel ──
        if (mainMenuPanel != null)
        {
            mainMenuRect = mainMenuPanel.GetComponent<RectTransform>();
            if (mainMenuRect != null)
            {
                mainMenuOnScreenPos  = mainMenuRect.anchoredPosition;
                float panelHeight    = mainMenuRect.rect.height;
                mainMenuOffScreenPos = mainMenuOnScreenPos + new Vector2(0f, (panelHeight + 100f) * 2f);

                // Panel starts on-screen
                mainMenuRect.anchoredPosition = mainMenuOnScreenPos;
                isMainMenuVisible = true;
            }
        }

        // ── Settings Panel ──
        if (settingsPanel != null)
        {
            settingsRect = settingsPanel.GetComponent<RectTransform>();
            if (settingsRect != null)
            {
                settingsOnScreenPos  = settingsRect.anchoredPosition;
                float panelHeight    = settingsRect.rect.height;
                // Slide from/to below the bottom edge
                settingsOffScreenPos = settingsOnScreenPos - new Vector2(0f, (panelHeight + 100f) * 2f);

                // Start hidden off-screen
                settingsRect.anchoredPosition = settingsOffScreenPos;
                isSettingsVisible = false;
            }
        }

        // ── Credits Panel ──
        if (creditsPanel != null)
        {
            creditsRect = creditsPanel.GetComponent<RectTransform>();
            if (creditsRect != null)
            {
                creditsOnScreenPos  = creditsRect.anchoredPosition;
                float panelHeight   = creditsRect.rect.height;
                // Slide from/to below the bottom edge
                creditsOffScreenPos = creditsOnScreenPos - new Vector2(0f, (panelHeight + 100f) * 2f);

                // Start hidden off-screen
                creditsRect.anchoredPosition = creditsOffScreenPos;
                isCreditsVisible = false;
            }
        }

        // ── Button Listeners ──
        if (workshopButton != null)
            workshopButton.onClick.AddListener(OnWorkshopButtonPressed);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsButtonPressed);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCreditsButtonPressed);
    }

    // ──────────────────────────────────────────────────────────────────────
    #region Button Handlers

    /// <summary>
    /// Workshop button pressed — slide the main menu panel UP off-screen.
    /// </summary>
    public void OnWorkshopButtonPressed()
    {
        HideMainMenu();
    }

    /// <summary>
    /// Settings button pressed — toggle the settings panel.
    /// Closes Credits panel if it is open.
    /// </summary>
    public void OnSettingsButtonPressed()
    {
        if (isCreditsVisible)
            HideCredits();

        if (isSettingsVisible)
            HideSettings();
        else
            ShowSettings();
    }

    /// <summary>
    /// Credits button pressed — toggle the credits panel.
    /// Closes Settings panel if it is open.
    /// </summary>
    public void OnCreditsButtonPressed()
    {
        if (isSettingsVisible)
            HideSettings();

        if (isCreditsVisible)
            HideCredits();
        else
            ShowCredits();
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────
    #region Main Menu Panel

    /// <summary>Slides the main menu panel upward off-screen.</summary>
    public void HideMainMenu()
    {
        if (mainMenuRect == null) return;

        if (mainMenuCoroutine != null)
            StopCoroutine(mainMenuCoroutine);

        isMainMenuVisible = false;
        mainMenuCoroutine = StartCoroutine(SlidePanel(mainMenuRect, mainMenuOffScreenPos, mainMenuSlideSpeed,
            () => mainMenuCoroutine = null));
    }

    /// <summary>Slides the main menu panel back down to its on-screen position.</summary>
    public void ShowMainMenu()
    {
        if (mainMenuRect == null) return;

        if (mainMenuCoroutine != null)
            StopCoroutine(mainMenuCoroutine);

        isMainMenuVisible = true;
        mainMenuCoroutine = StartCoroutine(SlidePanel(mainMenuRect, mainMenuOnScreenPos, mainMenuSlideSpeed,
            () => mainMenuCoroutine = null));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────
    #region Settings Panel

    public void ShowSettings()
    {
        if (settingsRect == null) return;

        if (settingsCoroutine != null)
            StopCoroutine(settingsCoroutine);

        isSettingsVisible = true;
        settingsCoroutine = StartCoroutine(SlidePanel(settingsRect, settingsOnScreenPos, settingsPanelSlideSpeed,
            () => settingsCoroutine = null));
    }

    public void HideSettings()
    {
        if (settingsRect == null) return;

        if (settingsCoroutine != null)
            StopCoroutine(settingsCoroutine);

        isSettingsVisible = false;
        settingsCoroutine = StartCoroutine(SlidePanel(settingsRect, settingsOffScreenPos, settingsPanelSlideSpeed,
            () => settingsCoroutine = null));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────
    #region Credits Panel

    public void ShowCredits()
    {
        if (creditsRect == null) return;

        if (creditsCoroutine != null)
            StopCoroutine(creditsCoroutine);

        isCreditsVisible = true;
        creditsCoroutine = StartCoroutine(SlidePanel(creditsRect, creditsOnScreenPos, creditsPanelSlideSpeed,
            () => creditsCoroutine = null));
    }

    public void HideCredits()
    {
        if (creditsRect == null) return;

        if (creditsCoroutine != null)
            StopCoroutine(creditsCoroutine);

        isCreditsVisible = false;
        creditsCoroutine = StartCoroutine(SlidePanel(creditsRect, creditsOffScreenPos, creditsPanelSlideSpeed,
            () => creditsCoroutine = null));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────────
    #region Shared Slide Coroutine

    /// <summary>
    /// Smoothly moves a RectTransform to <paramref name="targetPos"/> at <paramref name="speed"/> px/s.
    /// Calls <paramref name="onComplete"/> when finished.
    /// Uses unscaled time so it works even when Time.timeScale = 0.
    /// </summary>
    private IEnumerator SlidePanel(RectTransform rect, Vector2 targetPos, float speed, System.Action onComplete)
    {
        if (rect == null) yield break;

        while (Vector2.Distance(rect.anchoredPosition, targetPos) > 1f)
        {
            rect.anchoredPosition = Vector2.MoveTowards(
                rect.anchoredPosition,
                targetPos,
                speed * Time.unscaledDeltaTime
            );
            yield return null;
        }

        rect.anchoredPosition = targetPos;
        onComplete?.Invoke();
    }

    #endregion
}
