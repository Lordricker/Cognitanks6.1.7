using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TitleName : MonoBehaviour, IPointerClickHandler
{
    public TMP_Text titleText;
    public TMP_InputField inputField; // Assign in inspector, overlay on top of titleText

    void Start()
    {
        if (inputField != null)
        {
            inputField.gameObject.SetActive(false);
            inputField.onEndEdit.AddListener(OnInputEndEdit);
        }
    }

    public void SetTitle(string newTitle)
    {
        if (titleText != null)
            titleText.text = newTitle;
        if (inputField != null)
            inputField.text = newTitle;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inputField != null && titleText != null)
        {
            inputField.text = titleText.text;
            inputField.gameObject.SetActive(true);
            inputField.Select();
            inputField.ActivateInputField();
            titleText.gameObject.SetActive(false);
        }
    }

    private void OnInputEndEdit(string newText)
    {
        if (titleText != null)
            titleText.text = newText;
        if (inputField != null)
            inputField.gameObject.SetActive(false);
        if (titleText != null)
            titleText.gameObject.SetActive(true);
    }
}
