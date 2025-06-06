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
        // SatelliteResultText가 할당되어 있다면 직접 텍스트 업데이트
        if (SatelliteResultText != null)
        {
            SatelliteResultText.text = target;
            return;
        }
        
        // 기존 방식: 기존 텍스트들을 모두 제거하고 새로 생성
        foreach (Transform child in SatelliteResultContent)
        {
            if (child.gameObject != null)
            {
                DestroyImmediate(child.gameObject);
            }
        }
        
        // 새로운 텍스트 생성
        GameObject newResult = Instantiate( SatelliteResultTextPrefab, SatelliteResultContent );
        newResult.GetComponent<TMP_Text>( ).text = target;
        
        // 새 텍스트가 추가될 시 자동으로 스크롤을 최상단으로 조정
        if (SatelliteResultScrollRect != null)
        {
            SatelliteResultScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void SetDistanceText(float value)
    {
        distanceText.text = "대상과의 거리 : " + Mathf.RoundToInt(value) + "m";
    }
}