using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    public TMP_InputField promptInput;
    public TMP_Text droneResultText;
    public Button button;

    public TMP_Text distanceText;
    
    public Transform droneResultContent;
    public GameObject droneResultTextPrefab;
    public ScrollRect droneResultScrollRect;

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
        GameObject newResult = Instantiate( droneResultTextPrefab, droneResultContent );
        newResult.GetComponent<TMP_Text>( ).text = target;
        
        // 하단 코드는 새 텍스트가 추가될 시 자동으로 스크롤을 최하단으로 조정하는 코드
        // droneResultScrollRect.verticalNormalizedPosition = 0f;
    }

    public void SetDistanceText(float value)
    {
        distanceText.text = "대상과의 거리 : " + Mathf.RoundToInt(value) + "m";
    }
}