using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    public TMP_InputField promptInput;
    public TMP_Text droneResultText;
    public Button button;

    private void Awake( )
    {
        instance = this;
    }

    void Start()
    {
        button.onClick.AddListener(OnSubmitButtonClicked);
    }

    void OnSubmitButtonClicked()
    {
        Program.instance.LLMProcess(promptInput.text);
    }

    public void SetDroneResultText( string target )
    {
        droneResultText.text = target;
    }
}