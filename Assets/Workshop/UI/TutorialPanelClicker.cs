using UnityEngine;

/// <summary>
/// Marker component placed on tutorial panel roots by WorkshopUIManager.UpdateTutorialSections.
/// Its presence means the Button click-to-advance wiring has already been done for that panel.
/// No logic lives here — all click handling is on the Button component added alongside this.
/// </summary>
public class TutorialPanelClicker : MonoBehaviour { }
