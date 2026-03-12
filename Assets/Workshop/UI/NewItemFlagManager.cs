using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages "New Item" flag images shown next to category toggle buttons in the Workshop.
/// When a new component is unlocked in a category the corresponding flag becomes visible.
/// When the player clicks the category toggle the flag is hidden and the state is persisted.
/// </summary>
public class NewItemFlagManager : MonoBehaviour
{
    public static NewItemFlagManager Instance { get; private set; }

    [Header("New Item Flag Images")]
    [Tooltip("Flag image displayed next to the Turret category toggle")]
    public Image turretFlag;
    [Tooltip("Flag image displayed next to the Armor category toggle")]
    public Image armorFlag;
    [Tooltip("Flag image displayed next to the Engine Frame category toggle")]
    public Image engineFrameFlag;
    [Tooltip("Flag image displayed next to the Turret AI category toggle")]
    public Image turretAIFlag;
    [Tooltip("Flag image displayed next to the Nav AI category toggle")]
    public Image navAIFlag;

    [Header("Category Toggles")]
    [Tooltip("The Turret category toggle button")]
    public Toggle turretToggle;
    [Tooltip("The Armor category toggle button")]
    public Toggle armorToggle;
    [Tooltip("The Engine Frame category toggle button")]
    public Toggle engineFrameToggle;
    [Tooltip("The Turret AI category toggle button")]
    public Toggle turretAIToggle;
    [Tooltip("The Nav AI category toggle button")]
    public Toggle navAIToggle;

    private const string FLAG_KEY_PREFIX = "NewItemFlag_";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Restore flag visibility from PlayerPrefs (handles returning to shop after an arena)
        RefreshAllFlags();

        // Hide each flag when the player opens its category
        if (turretToggle != null)
            turretToggle.onValueChanged.AddListener(isOn => { if (isOn) ClearFlag(ComponentCategory.Turret); });
        if (armorToggle != null)
            armorToggle.onValueChanged.AddListener(isOn => { if (isOn) ClearFlag(ComponentCategory.Armor); });
        if (engineFrameToggle != null)
            engineFrameToggle.onValueChanged.AddListener(isOn => { if (isOn) ClearFlag(ComponentCategory.EngineFrame); });
        if (turretAIToggle != null)
            turretAIToggle.onValueChanged.AddListener(isOn => { if (isOn) ClearFlag(ComponentCategory.TurretAI); });
        if (navAIToggle != null)
            navAIToggle.onValueChanged.AddListener(isOn => { if (isOn) ClearFlag(ComponentCategory.NavAI); });
    }

    /// <summary>
    /// Called by WorkshopUIManager when a new component is successfully unlocked.
    /// Shows the flag for the corresponding category and saves the state.
    /// </summary>
    public void NotifyNewItemUnlocked(ComponentCategory category)
    {
        Image flag = GetFlag(category);
        if (flag == null) return; // Category has no flag (e.g. AITree)

        PlayerPrefs.SetInt(FLAG_KEY_PREFIX + category.ToString(), 1);
        PlayerPrefs.Save();
        flag.gameObject.SetActive(true);
    }

    private void ClearFlag(ComponentCategory category)
    {
        PlayerPrefs.SetInt(FLAG_KEY_PREFIX + category.ToString(), 0);
        PlayerPrefs.Save();

        Image flag = GetFlag(category);
        if (flag != null)
            flag.gameObject.SetActive(false);
    }

    public void RefreshAllFlags()
    {
        RefreshFlag(ComponentCategory.Turret);
        RefreshFlag(ComponentCategory.Armor);
        RefreshFlag(ComponentCategory.EngineFrame);
        RefreshFlag(ComponentCategory.TurretAI);
        RefreshFlag(ComponentCategory.NavAI);
    }

    private void RefreshFlag(ComponentCategory category)
    {
        Image flag = GetFlag(category);
        if (flag == null) return;
        bool active = PlayerPrefs.GetInt(FLAG_KEY_PREFIX + category.ToString(), 0) == 1;
        flag.gameObject.SetActive(active);
    }

    private Image GetFlag(ComponentCategory category)
    {
        switch (category)
        {
            case ComponentCategory.Turret:      return turretFlag;
            case ComponentCategory.Armor:       return armorFlag;
            case ComponentCategory.EngineFrame: return engineFrameFlag;
            case ComponentCategory.TurretAI:    return turretAIFlag;
            case ComponentCategory.NavAI:       return navAIFlag;
            default:                            return null;
        }
    }
}
