using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public TMP_InputField promptInput;
    public Button button;

    void Start()
    {
        button.onClick.AddListener(OnSubmitButtonClicked);
    }

    void OnSubmitButtonClicked()
    {
        Program.instance.LLMProcess(promptInput.text);
    }
}