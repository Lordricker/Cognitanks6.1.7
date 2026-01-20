using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("Type of sound to play on button click.")]
    public ButtonSoundType soundType = ButtonSoundType.DefaultClick;

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayButtonSound(soundType);
        }
    }
}