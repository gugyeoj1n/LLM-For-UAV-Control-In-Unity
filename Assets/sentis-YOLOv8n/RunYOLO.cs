using System;
using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;
using System.Text;
using FF = Unity.Sentis.Functional;

public class RunYOLO : MonoBehaviour
{
    [Tooltip("Drag a YOLO model .onnx file here")]
    public ModelAsset modelAsset;

    [Tooltip("Drag the classes.txt here")]
    public TextAsset classesAsset;

    [Tooltip("Create a Raw Image in the scene and link it here")]
    public RawImage displayImage;

    [Tooltip("Drag a border box texture here")]
    public Texture2D borderTexture;

    [Tooltip("Select an appropriate font for the labels")]
    public Font font;

    [Tooltip("Change this to the name of the video you put in the Assets/StreamingAssets folder")]
    public string videoFilename = "giraffes.mp4";

    const BackendType backend = BackendType.GPUCompute;

    private Transform displayLocation;
    private Worker worker;
    [SerializeField]
    private string[] labels;
    private RenderTexture targetRT;
    private Sprite borderSprite;

    private const int imageWidth = 640;
    private const int imageHeight = 640;

    private VideoPlayer video;

    List<GameObject> boxPool = new();
    private int currentFrameBoxCount = 0;

    [Tooltip("Intersection over union threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)] float iouThreshold = 0.7f;
    
    [Tooltip("Confidence score threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)] float scoreThreshold = 0.5f;

    Tensor<float> centersToCorners;

    public struct BoundingBox
    {
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public string label;
    }

    // 위성 추적을 위한 클래스
    private class SatelliteTracker
    {
        public Vector2 position; // 화면 상의 위치 (중심점)
        public Vector2 size;     // 화면 상의 크기
        public string label;     // 감지된 라벨
        public List<Vector2> positionHistory = new List<Vector2>(); // 위치 이력
        public float lastUpdateTime;
        public string movementPattern = "감지됨";
        
        // 움직임 패턴 분석을 위한 변화량
        public float xVariation = 0f;
        public float yVariation = 0f;
        
        public SatelliteTracker(Vector2 pos, Vector2 sz, string lbl)
        {
            position = pos;
            size = sz;
            label = lbl;
            positionHistory.Add(pos);
            lastUpdateTime = Time.time;
        }
        
        public void UpdatePosition(Vector2 newPos, Vector2 newSize)
        {
            // 위치 변화량 계산
            Vector2 delta = newPos - position;
            
            // 각 축별 변화량 누적
            xVariation += Mathf.Abs(delta.x);
            yVariation += Mathf.Abs(delta.y);
            
            // 위치 업데이트
            position = newPos;
            size = newSize;
            lastUpdateTime = Time.time;
            
            // 위치 이력 저장 (최대 10개)
            positionHistory.Add(newPos);
            if (positionHistory.Count > 10)
            {
                positionHistory.RemoveAt(0);
            }
            
            // 이동 패턴 분석
            AnalyzeMovementPattern();
        }
        
        private void AnalyzeMovementPattern()
        {
            if (positionHistory.Count < 4) return;
            
            // 총 이동량
            float totalMovement = xVariation + yVariation;
            
            // 정지 상태 확인
            if (totalMovement < 10f) // 화면 픽셀 단위로 임계값 조정
            {
                movementPattern = "정지해 있음";
                return;
            }
            
            // 직선 이동 패턴 확인
            Vector2 firstDir = (positionHistory[1] - positionHistory[0]).normalized;
            Vector2 lastDir = (positionHistory[positionHistory.Count-1] - positionHistory[positionHistory.Count-2]).normalized;
            
            float dirSimilarity = Vector2.Dot(firstDir, lastDir); // 1에 가까울수록 방향이 비슷함
            
            // 직선 이동
            if (dirSimilarity > 0.85f)
            {
                // 주된 이동 방향 결정
                Vector2 avgDir = Vector2.zero;
                for (int i = 1; i < positionHistory.Count; i++)
                {
                    avgDir += (positionHistory[i] - positionHistory[i-1]);
                }
                avgDir.Normalize();
                
                // 주된 이동 방향에 따라 설명 생성
                if (Mathf.Abs(avgDir.x) > Mathf.Abs(avgDir.y))
                {
                    // x 방향 이동이 더 큼
                    movementPattern = avgDir.x > 0 ? "오른쪽으로 이동 중" : "왼쪽으로 이동 중";
                }
                else
                {
                    // y 방향 이동이 더 큼
                    movementPattern = avgDir.y > 0 ? "위쪽으로 이동 중" : "아래쪽으로 이동 중";
                }
                return;
            }
            
            // 축별 변화량으로 패턴 구분
            if (xVariation > 2f * yVariation)
            {
                movementPattern = "좌우로 움직이는 중";
            }
            else if (yVariation > 2f * xVariation)
            {
                movementPattern = "상하로 움직이는 중";
            }
            else
            {
                // 원형 패턴 확인
                bool circularPattern = IsCircularPattern();
                if (circularPattern)
                {
                    movementPattern = "원형으로 움직이는 중";
                }
                else
                {
                    movementPattern = "불규칙하게 움직이는 중";
                }
            }
        }
        
        // 원형 패턴 확인 로직
        private bool IsCircularPattern()
        {
            if (positionHistory.Count < 6) return false;
            
            // 중심점 계산
            Vector2 center = Vector2.zero;
            foreach (Vector2 pos in positionHistory)
            {
                center += pos;
            }
            center /= positionHistory.Count;
            
            // 각 점이 중심으로부터 비슷한 거리에 있는지 확인
            float avgRadius = 0;
            foreach (Vector2 pos in positionHistory)
            {
                avgRadius += Vector2.Distance(pos, center);
            }
            avgRadius /= positionHistory.Count;
            
            // 반지름 편차 계산
            float radiusVariation = 0;
            foreach (Vector2 pos in positionHistory)
            {
                float radius = Vector2.Distance(pos, center);
                radiusVariation += Mathf.Abs(radius - avgRadius);
            }
            radiusVariation /= positionHistory.Count;
            
            // 편차가 작으면 원형 패턴으로 판단
            return radiusVariation < 0.3f * avgRadius;
        }
    }
    
    // 위성 추적기 딕셔너리 - 화면에 여러 위성이 있을 경우 각각 추적
    private Dictionary<string, SatelliteTracker> SatelliteTrackers = new Dictionary<string, SatelliteTracker>();
    
    // 가장 최근에 감지된 위성 정보
    private SatelliteTracker latestDetectedSatellite = null;
    private float lastSatelliteDetectionTime = 0f;

    void Start()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        labels = classesAsset.text.Split('\n');
        
        // 디버그: 로드된 라벨 목록 출력
        Debug.Log($"총 {labels.Length}개의 라벨이 로드되었습니다:");
        for (int i = 0; i < Mathf.Min(labels.Length, 10); i++)
        {
            Debug.Log($"라벨 {i}: '{labels[i]}'");
        }
        
        LoadModel();

        targetRT = new RenderTexture(imageWidth, imageHeight, 0);
        displayLocation = displayImage.transform;

        SetupInput();
        borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, borderTexture.width, borderTexture.height), new Vector2(borderTexture.width / 2, borderTexture.height / 2));
    }

    void LoadModel()
    {
        var model1 = ModelLoader.Load(modelAsset);

        centersToCorners = new Tensor<float>(new TensorShape(4, 4),
        new float[]
        {
            1, 0, 1, 0,
            0, 1, 0, 1,
            -0.5f, 0, 0.5f, 0,
            0, -0.5f, 0, 0.5f
        });

        var graph = new FunctionalGraph();
        var inputs = graph.AddInputs(model1);
        var modelOutput = FF.Forward(model1, inputs)[0];
        var boxCoords = modelOutput[0, 0..4, ..].Transpose(0, 1);
        var allScores = modelOutput[0, 4.., ..];
        var scores = FF.ReduceMax(allScores, 0);
        var classIDs = FF.ArgMax(allScores, 0);
        var boxCorners = FF.MatMul(boxCoords, FF.Constant(centersToCorners));
        var indices = FF.NMS(boxCorners, scores, iouThreshold, scoreThreshold);
        var coords = FF.IndexSelect(boxCoords, 0, indices);
        var labelIDs = FF.IndexSelect(classIDs, 0, indices);

        worker = new Worker(graph.Compile(coords, labelIDs), backend);
    }

    void SetupInput()
    {
        Camera SatelliteCamera = GetComponent<Camera>();
        SatelliteCamera.targetTexture = targetRT;
    }

    private void Update()
    {
        ExecuteML();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        
        // 일정 시간 동안 위성이 감지되지 않으면 상태 메시지 업데이트
        if (latestDetectedSatellite != null && Time.time - lastSatelliteDetectionTime > 3.0f)
        {
            UIManager.instance.SetSatelliteResultText("적 위성이 시야에서 사라졌습니다.");
            latestDetectedSatellite = null;
        }
    }

    public void ExecuteML()
    {
        displayImage.texture = targetRT;

        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
        TextureConverter.ToTensor(targetRT, inputTensor, default);
        worker.Schedule(inputTensor);

        using var output = (worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone();
        using var labelIDs = (worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone();
        
        // 디버그: 출력 텐서 정보
        Debug.Log($"출력 텐서 크기: {output.shape}, 라벨 텐서 크기: {labelIDs.shape}");

        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;

        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        // 이 프레임에 감지된 위성 ID 추적
        List<string> detectedSatelliteIds = new List<string>();
        bool SatelliteDetected = false;
        int satelliteBoxIndex = 0; // 위성 박스를 위한 별도 인덱스

        int boxesFound = output.shape[0];
        Debug.Log($"총 {boxesFound}개의 바운딩 박스가 감지되었습니다 (처리할 개수: {Mathf.Min(boxesFound, 200)})");
        
        for (int n = 0; n < Mathf.Min(boxesFound, 200); n++)
        {
            var box = new BoundingBox
            {
                centerX = output[n, 0] * scaleX - displayWidth / 2,
                centerY = output[n, 1] * scaleY - displayHeight / 2,
                width = output[n, 2] * scaleX,
                height = output[n, 3] * scaleY,
                label = labels[labelIDs[n]],
            };
            
            // 디버그: 감지된 모든 라벨 출력
            if (!string.IsNullOrEmpty(box.label))
            {
                Debug.Log($"감지된 객체: '{box.label}' (인덱스: {labelIDs[n]}, 신뢰도: {output[n, 4]:F3})");
            }
            
            // 위성 감지 처리
            if (!string.IsNullOrEmpty(box.label) && box.label.Trim().Equals("satellite", StringComparison.OrdinalIgnoreCase))
            {
                SatelliteDetected = true;

                // 위성 위치와 크기 계산
                Vector2 SatellitePos = new Vector2(box.centerX + displayWidth / 2, box.centerY + displayHeight / 2); // 화면 좌표로 변환
                Vector2 SatelliteSize = new Vector2(box.width, box.height);

                // 위성 ID 생성 (현재는 단순히 인덱스 사용)
                string SatelliteId = "Satellite_" + n;
                detectedSatelliteIds.Add(SatelliteId);

                bool isNewSatellite = !SatelliteTrackers.ContainsKey(SatelliteId);

                // 위치 업데이트 또는 새로 추가
                if (isNewSatellite)
                {
                    SatelliteTrackers[SatelliteId] = new SatelliteTracker(SatellitePos, SatelliteSize, box.label);
                }
                else
                {
                    SatelliteTrackers[SatelliteId].UpdatePosition(SatellitePos, SatelliteSize);
                }

                // 가장 최근 감지된 위성으로 업데이트
                latestDetectedSatellite = SatelliteTrackers[SatelliteId];
                lastSatelliteDetectionTime = Time.time;

                // 위성 감지시 UI 업데이트 (매번 업데이트하여 실시간 정보 제공)
                UpdateSatelliteStatusUI(SatelliteId);
                Debug.Log("위성 감지! 움직임: " + latestDetectedSatellite.movementPattern);

                if (isNewSatellite)
                {
                    // 새 위성일 때만 수행할 작업들
                    SatelliteController controller = FindFirstObjectByType<SatelliteController>();
                    if (controller != null)
                    {
                        SatelliteCommand hoverCommand = new SatelliteCommand
                        {
                            actionEnum = SatelliteCommand.SatelliteAction.Hover
                        };
                        controller.OnCommand(hoverCommand);

                        controller.trackingTarget = FindClosestSatelliteToBox(box);
                        // controller.StartTracking();
                    }
                }
                
                satelliteBoxIndex++;
            }
        }
        
        // 이번 프레임에 감지되지 않은 위성 제거
        List<string> SatelliteIdsToRemove = new List<string>();
        foreach (var kvp in SatelliteTrackers)
        {
            if (!detectedSatelliteIds.Contains(kvp.Key))
            {
                if (Time.time - kvp.Value.lastUpdateTime > 3.0f) // 3초 이상 감지되지 않으면 제거
                {
                    SatelliteIdsToRemove.Add(kvp.Key);
                }
            }
        }
        
        foreach (string id in SatelliteIdsToRemove)
        {
            SatelliteTrackers.Remove(id);
        }
        
        // 위성이 감지되지 않았을 때 UI 업데이트
        if (!SatelliteDetected && UIManager.instance != null)
        {
            if (Time.time - lastSatelliteDetectionTime > 2.0f) // 2초 이상 감지되지 않으면
            {
                UIManager.instance.SetSatelliteResultText("현재 감지된 위성이 없습니다.\n스캔 중...");
            }
        }
        
        // 디버그: 위성 감지 상태 요약
        Debug.Log($"이번 프레임에서 {detectedSatelliteIds.Count}개의 위성이 감지되었습니다. 총 추적 중인 위성: {SatelliteTrackers.Count}개");
    }
    
    // UI에 위성 움직임 정보 업데이트
    private void UpdateSatelliteStatusUI(string SatelliteId)
    {
        if (!SatelliteTrackers.ContainsKey(SatelliteId))
        {
            Debug.LogWarning($"위성 ID '{SatelliteId}'를 찾을 수 없습니다.");
            return;
        }
        
        if (UIManager.instance == null)
        {
            Debug.LogWarning("UIManager.instance가 null입니다. UI가 제대로 설정되었는지 확인하세요.");
            return;
        }
            
        SatelliteTracker Satellite = SatelliteTrackers[SatelliteId];
        
        // UI에 표시할 메시지 생성
        StringBuilder message = new StringBuilder();
        message.AppendLine("===== 적 위성 감지 =====");
        
        // 감지된 위성 개수 표시
        message.AppendLine($"감지된 위성 수: {SatelliteTrackers.Count}개");
        message.AppendLine("");
        
        // 움직임 패턴 설명
        message.AppendLine($"• 상태: {Satellite.movementPattern}");
        
        // 화면상 위치 설명
        float screenCenterX = displayImage.rectTransform.rect.width / 2;
        float screenCenterY = displayImage.rectTransform.rect.height / 2;
        
        // 위치를 백분율로 계산
        float posX = (Satellite.position.x / displayImage.rectTransform.rect.width) * 100f;
        float posY = (Satellite.position.y / displayImage.rectTransform.rect.height) * 100f;
        
        message.AppendLine($"• 위치: 화면의 {posX:F0}%, {posY:F0}% 지점");
        
        // 방향 설명
        string horizontalPos = Satellite.position.x < screenCenterX * 0.8f ? "왼쪽" : 
                              Satellite.position.x > screenCenterX * 1.2f ? "오른쪽" : "중앙";
        string verticalPos = Satellite.position.y < screenCenterY * 0.8f ? "아래쪽" : 
                            Satellite.position.y > screenCenterY * 1.2f ? "위쪽" : "중앙";
        
        if (horizontalPos != "중앙" || verticalPos != "중앙")
        {
            message.AppendLine($"• 방향: 화면 {horizontalPos} {verticalPos}");
        }
        else
        {
            message.AppendLine("• 방향: 화면 중앙 부근");
        }
        
        // 움직임 분석
        if (Satellite.positionHistory.Count > 5)
        {
            Vector2 recentMovement = Satellite.position - Satellite.positionHistory[Satellite.positionHistory.Count - 5];
            
            if (recentMovement.magnitude > 20f)
            {
                float speed = recentMovement.magnitude / 5f; // 픽셀/프레임
                message.AppendLine($"• 이동 속도: {speed:F1} 픽셀/프레임");
                
                if (Mathf.Abs(recentMovement.x) > Mathf.Abs(recentMovement.y))
                {
                    string direction = recentMovement.x > 0 ? "오른쪽" : "왼쪽";
                    message.AppendLine($"• 주 이동 방향: {direction}");
                }
                else
                {
                    string direction = recentMovement.y > 0 ? "위쪽" : "아래쪽";
                    message.AppendLine($"• 주 이동 방향: {direction}");
                }
            }
        }
        
        // 크기 정보
        message.AppendLine($"• 크기: {Satellite.size.x:F0} × {Satellite.size.y:F0} 픽셀");
        
        // 위험 평가
        if (Satellite.size.magnitude > screenCenterX * 0.3f)
        {
            message.AppendLine("");
            message.AppendLine("⚠️ 경고: 위성이 매우 가까이 있습니다!");
        }
        
        // 추적 시간
        float trackingTime = Time.time - Satellite.lastUpdateTime;
        if (trackingTime < 1f)
        {
            message.AppendLine($"• 추적 상태: 실시간 감지");
        }
        
        // UI 업데이트
        string finalMessage = message.ToString();
        Debug.Log($"UI 텍스트 업데이트: {finalMessage.Substring(0, Mathf.Min(100, finalMessage.Length))}...");
        UIManager.instance.SetSatelliteResultText(finalMessage);
    }
    
    private Transform FindClosestSatelliteToBox(BoundingBox box)
    {
        GameObject[] Satellites = GameObject.FindGameObjectsWithTag("Satellite"); // 또는 FindObjectsByType<SatelliteIdentifier>()

        Vector2 screenCenter = new Vector2(displayImage.rectTransform.rect.width / 2, displayImage.rectTransform.rect.height / 2);
        Vector2 boxCenter = screenCenter + new Vector2(box.centerX, -box.centerY); // UGUI 기준 위치 보정

        float minDistance = float.MaxValue;
        Transform closest = null;

        foreach (var Satellite in Satellites)
        {
            Vector3 worldPos = Satellite.transform.position;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            Vector2 localPoint;

            // 화면 좌표를 RawImage 내부 로컬 좌표로 변환
            RectTransformUtility.ScreenPointToLocalPointInRectangle(displayImage.rectTransform, screenPos, null, out localPoint);

            float dist = Vector2.Distance(localPoint, boxCenter);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = Satellite.transform;
            }
        }

        return closest;
    }

    public void DrawBox(BoundingBox box, int id, float fontSize)
    {
        GameObject panel;
        
        // 박스 풀에서 재사용하거나 새로 생성
        if (id < boxPool.Count && boxPool[id] != null)
        {
            panel = boxPool[id];
        }
        else
        {
            // 새 박스 생성
            panel = CreateNewBox(Color.yellow);
            
            // 박스 풀 크기 조정
            while (boxPool.Count <= id)
            {
                boxPool.Add(null);
            }
            boxPool[id] = panel;
        }

        // 박스 활성화 및 위치/크기 설정
        panel.SetActive(true);
        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);

        // 텍스트 라벨 업데이트 (위성 전용)
        var label = panel.GetComponentInChildren<Text>();
        
        string SatelliteId = "Satellite_" + id;
        if (SatelliteTrackers.ContainsKey(SatelliteId) && SatelliteTrackers[SatelliteId].positionHistory.Count > 3)
        {
            label.text = $"{box.label}: {SatelliteTrackers[SatelliteId].movementPattern}";
        }
        else
        {
            label.text = $"{box.label}: 감지됨";
        }
        
        // 텍스트 속성 설정
        label.fontSize = (int)fontSize;
    }

    public GameObject CreateNewBox(Color color)
    {
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.color = color;
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayLocation, false);

        var text = new GameObject("ObjectLabel");
        text.AddComponent<CanvasRenderer>();
        text.transform.SetParent(panel.transform, false);
        Text txt = text.AddComponent<Text>();
        txt.font = font;
        txt.color = color;
        txt.fontSize = 40;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform rt2 = text.GetComponent<RectTransform>();
        rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
        rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
        rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
        rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
        rt2.anchorMin = new Vector2(0, 0);
        rt2.anchorMax = new Vector2(1, 1);

        // boxPool에 추가는 DrawBox에서 처리하므로 여기서는 제거
        return panel;
    }

    public void ClearAllAnnotations()
    {
        // 모든 박스를 완전히 파괴하고 풀 리셋
        for (int i = 0; i < boxPool.Count; i++)
        {
            if (boxPool[i] != null)
            {
                DestroyImmediate(boxPool[i]);
                boxPool[i] = null; // null로 설정하여 완전히 정리
            }
        }
        boxPool.Clear();
        currentFrameBoxCount = 0;
    }

    public void ClearAnnotations()
    {
        // 현재는 ClearAllAnnotations가 모든 것을 처리하므로 불필요
        // 하지만 호환성을 위해 유지
    }

    private void OnDestroy()
    {
        centersToCorners?.Dispose();
        worker?.Dispose();
    }
}