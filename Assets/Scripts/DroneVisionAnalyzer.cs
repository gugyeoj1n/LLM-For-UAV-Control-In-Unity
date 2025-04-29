using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;
using System.Threading.Tasks;

public class DroneVisionAnalyzer : MonoBehaviour
{
    [SerializeField] private Camera droneCamera;
    [SerializeField] private RawImage displayImage;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private int captureWidth = 416;
    [SerializeField] private int captureHeight = 416;
    [SerializeField] private float captureInterval = 0.5f;
    [SerializeField] private string websocketUrl = "ws://localhost:8000/analyze";
    
    private WebSocket websocket;
    private RenderTexture renderTexture;
    private Texture2D captureTexture;
    private bool isConnected = false;
    
    // 바운딩 박스 관련 변수들 (클래스 내부로 이동)
    private List<GameObject> boundingBoxes = new List<GameObject>();
    private Dictionary<string, Color> classColors = new Dictionary<string, Color>();
    private int nextColorIndex = 0;
    private Color[] predefinedColors = new Color[] {
        new Color(1, 0, 0, 0.5f),    // 빨강
        new Color(0, 1, 0, 0.5f),    // 초록
        new Color(0, 0, 1, 0.5f),    // 파랑
        new Color(1, 1, 0, 0.5f),    // 노랑
        new Color(1, 0, 1, 0.5f),    // 마젠타
        new Color(0, 1, 1, 0.5f)     // 시안
    };
    
    async void Start()
    {
        Debug.Log("DroneVisionAnalyzer 초기화 중...");
        
        // 카메라가 없으면 자식 오브젝트에서 찾기
        if (droneCamera == null)
        {
            droneCamera = GetComponentInChildren<Camera>();
            if (droneCamera == null)
            {
                Debug.LogError("드론 카메라를 찾을 수 없습니다!");
                return;
            }
        }
        
        // UI 요소 확인
        if (displayImage == null || descriptionText == null)
        {
            Debug.LogWarning("UI 요소가 할당되지 않았습니다. 영상은 캡처되지만 화면에 표시되지 않습니다.");
        }
        
        // 렌더 텍스처 설정
        renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
        captureTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        droneCamera.targetTexture = renderTexture;
        
        if (displayImage != null)
        {
            displayImage.texture = renderTexture;
        }
        
        // WebSocket 초기화
        Debug.Log($"WebSocket 서버에 연결 시도: {websocketUrl}");
        websocket = new WebSocket(websocketUrl);
        
        websocket.OnOpen += () => {
            Debug.Log("WebSocket 연결 성공!");
            isConnected = true;
            StartCoroutine(CaptureAndSendRoutine());
        };
        
        websocket.OnMessage += (bytes) => {
            string message = Encoding.UTF8.GetString(bytes);
            ProcessAnalysisResult(message);
        };
        
        websocket.OnError += (e) => {
            Debug.LogError($"WebSocket 오류: {e}");
            isConnected = false;
        };
        
        websocket.OnClose += (e) => {
            Debug.Log("WebSocket 연결 종료");
            isConnected = false;
            
            // 연결 재시도 로직
            StartCoroutine(ReconnectRoutine());
        };
        
        // WebSocket 연결 시도
        try
        {
            await websocket.Connect();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"WebSocket 연결 실패: {e.Message}");
            StartCoroutine(ReconnectRoutine());
        }
    }
    
    IEnumerator ReconnectRoutine()
    {
        Debug.Log("5초 후 WebSocket 재연결 시도...");
        yield return new WaitForSeconds(5f);
        
        try
        {
            Start();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"재연결 실패: {e.Message}");
        }
    }
    
    void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
        #endif
    }
    
    IEnumerator CaptureAndSendRoutine()
    {
        Debug.Log("이미지 캡처 및 전송 루틴 시작");
        while (isConnected)
        {
            yield return new WaitForSeconds(captureInterval);
            
            try
            {
                // 카메라 이미지 캡처
                RenderTexture.active = renderTexture;
                captureTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                captureTexture.Apply();
                RenderTexture.active = null;
                
                // 이미지를 JPEG으로 변환하고 Base64 인코딩
                byte[] jpgBytes = captureTexture.EncodeToJPG(75);
                string base64Image = System.Convert.ToBase64String(jpgBytes);
                
                // WebSocket으로 전송
                if (isConnected)
                {
                    websocket.SendText(base64Image);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"이미지 캡처 및 전송 중 오류: {e.Message}");
            }
        }
    }
    
    void ProcessAnalysisResult(string jsonResult)
    {
        // 메인 스레드에서 처리
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            try {
                // 수신된 JSON 데이터 로깅 (디버그용)
                Debug.Log($"수신된 분석 결과: {jsonResult.Substring(0, Mathf.Min(100, jsonResult.Length))}...");
                
                // 서버 응답 구조에 맞게 클래스 정의
                ServerAnalysisResult result = JsonUtility.FromJson<ServerAnalysisResult>(jsonResult);
                
                // UI 텍스트 업데이트
                if (descriptionText != null)
                {
                    descriptionText.text = result.description;
                }
                
                // 객체 시각화 (필요시 활성화)
                DrawDetectedObjects(result.objects);
                
                // 드론 컨트롤러에 분석 결과 전달 (필요시 구현)
                // NotifyDroneController(result);
            }
            catch (System.Exception e) {
                Debug.LogError($"결과 처리 오류: {e.Message}\n원본 JSON: {jsonResult}");
            }
        });
    }
    
    // 바운딩 박스 시각화 함수
    private void DrawDetectedObjects(DetectedObject[] objects)
    {
        // 기존 바운딩 박스 제거
        ClearBoundingBoxes();
        
        if (objects == null || objects.Length == 0)
        {
            Debug.Log("표시할 객체가 없습니다.");
            return;
        }
        
        // UI 캔버스 확인
        if (displayImage == null)
        {
            Debug.LogError("displayImage가 할당되지 않았습니다. 바운딩 박스를 표시할 수 없습니다.");
            return;
        }

        // 바운딩 박스 생성
        for (int i = 0; i < objects.Length; i++)
        {
            DetectedObject obj = objects[i];
            
            // 클래스 이름이 없으면 'object'를 사용
            string className = string.IsNullOrEmpty(obj.class_name) ? "object" : obj.class_name;
            
            // 신뢰도가 너무 낮은 객체는 건너뛰기
            if (obj.confidence < 0.3f)
            {
                continue;
            }
            
            // 클래스별 색상 관리
            if (!classColors.ContainsKey(className))
            {
                classColors[className] = predefinedColors[nextColorIndex];
                nextColorIndex = (nextColorIndex + 1) % predefinedColors.Length;
            }
            
            // 바운딩 박스 생성
            GameObject boxObj = new GameObject($"BBox_{className}_{i}");
            boxObj.transform.SetParent(displayImage.transform, false);
            
            // RectTransform 설정
            RectTransform rectTransform = boxObj.AddComponent<RectTransform>();
            
            // 바운딩 박스 좌표 변환 (모델 출력 -> 화면 좌표)
            float x1 = obj.bbox[0];
            float y1 = obj.bbox[1];
            float x2 = obj.bbox[2];
            float y2 = obj.bbox[3];
            
            // 화면 좌표 계산 (0~captureWidth/Height -> -displayWidth/Height/2 ~ +displayWidth/Height/2)
            float displayWidth = displayImage.rectTransform.rect.width;
            float displayHeight = displayImage.rectTransform.rect.height;
            
            float normalizedX1 = x1 / captureWidth;
            float normalizedY1 = 1 - (y1 / captureHeight); // Y 축 반전
            float normalizedX2 = x2 / captureWidth;
            float normalizedY2 = 1 - (y2 / captureHeight); // Y 축 반전
            
            float screenX1 = normalizedX1 * displayWidth - (displayWidth / 2);
            float screenY1 = normalizedY1 * displayHeight - (displayHeight / 2);
            float screenX2 = normalizedX2 * displayWidth - (displayWidth / 2);
            float screenY2 = normalizedY2 * displayHeight - (displayHeight / 2);
            
            // 위치와 크기 설정
            float width = Mathf.Abs(screenX2 - screenX1);
            float height = Mathf.Abs(screenY2 - screenY1);
            float centerX = (screenX1 + screenX2) / 2;
            float centerY = (screenY1 + screenY2) / 2;
            
            rectTransform.anchoredPosition = new Vector2(centerX, centerY);
            rectTransform.sizeDelta = new Vector2(width, height);
            
            // 이미지 컴포넌트 추가 (바운딩 박스)
            Image boxImage = boxObj.AddComponent<Image>();
            boxImage.color = classColors[className];
            
            // 레이블 추가
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(boxObj.transform, false);
            
            RectTransform labelRT = labelObj.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 1);
            labelRT.anchorMax = new Vector2(1, 1);
            labelRT.pivot = new Vector2(0.5f, 1f);
            labelRT.anchoredPosition = new Vector2(0, 0);
            labelRT.sizeDelta = new Vector2(0, 20);
            
            // 레이블 배경
            Image labelBg = labelObj.AddComponent<Image>();
            labelBg.color = new Color(0, 0, 0, 0.7f);
            
            // 텍스트 추가
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(labelObj.transform, false);
            
            RectTransform textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(5, 0);
            textRT.offsetMax = new Vector2(-5, 0);
            
            Text labelText = textObj.AddComponent<Text>();
            labelText.text = $"{className} ({obj.confidence:F2})";
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize = 12;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            
            // 리스트에 추가
            boundingBoxes.Add(boxObj);
            
            Debug.Log($"바운딩 박스 생성: {className}, 신뢰도: {obj.confidence:F2}, 위치: ({centerX}, {centerY}), 크기: {width}x{height}");
        }
    }

    // 바운딩 박스 제거 함수
    private void ClearBoundingBoxes()
    {
        foreach (GameObject box in boundingBoxes)
        {
            if (box != null)
            {
                Destroy(box);
            }
        }
        boundingBoxes.Clear();
    }
    
    // 서버에서 오는 JSON 구조에 맞게 클래스 정의
    [System.Serializable]
    public class ServerAnalysisResult
    {
        public string description;
        public DetectedObject[] objects;
    }
    
    [System.Serializable]
    public class DetectedObject
    {
        // FastAPI 서버 응답 형식에 맞게 필드 수정
        public string class_name;
        public float confidence;
        public float[] bbox; // [x1, y1, x2, y2]
    }
    
    private async void OnApplicationQuit()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.Close();
        }
    }
    
    // 테스트용 함수 - 인스펙터에서 호출 가능
    public void TestCapture()
    {
        Debug.Log("테스트 캡처 수행");
        StartCoroutine(TestCaptureRoutine());
    }
    
    IEnumerator TestCaptureRoutine()
    {
        // 카메라 이미지 캡처
        RenderTexture.active = renderTexture;
        captureTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        captureTexture.Apply();
        RenderTexture.active = null;
        
        // 테스트 이미지를 저장
        byte[] pngBytes = captureTexture.EncodeToPNG();
        System.IO.File.WriteAllBytes(Application.persistentDataPath + "/drone_capture_test.png", pngBytes);
        
        Debug.Log($"테스트 이미지 저장 완료: {Application.persistentDataPath}/drone_capture_test.png");
        
        yield return null;
    }
}