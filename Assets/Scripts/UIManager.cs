using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    public TMP_InputField promptInput;
    public TMP_Text SatelliteResultText;
    public Button button;

    public TMP_Text distanceText;
    
    public Transform SatelliteResultContent;
    public GameObject SatelliteResultTextPrefab;
    public ScrollRect SatelliteResultScrollRect;

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

    public void SetSatelliteResultText( string target )
    {
        if (SatelliteResultText != null)
        {
            SatelliteResultText.text = target;
            return;
        }
        
        GameObject newResult = Instantiate( SatelliteResultTextPrefab, SatelliteResultContent );
        TMP_Text text = newResult.GetComponent<TMP_Text>( );
        text.text = target;
        RectTransform rect = newResult.GetComponent<RectTransform>( );
        rect.sizeDelta = new Vector2( rect.sizeDelta.x, text.preferredHeight );
    }

    public void SetDistanceText(float value)
    {
        distanceText.text = "Distance : " + Mathf.RoundToInt(value) + "m";
    }
}