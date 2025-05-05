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

    // 드론 추적을 위한 클래스
    private class DroneTracker
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
        
        public DroneTracker(Vector2 pos, Vector2 sz, string lbl)
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
    
    // 드론 추적기 딕셔너리 - 화면에 여러 드론이 있을 경우 각각 추적
    private Dictionary<string, DroneTracker> droneTrackers = new Dictionary<string, DroneTracker>();
    
    // 가장 최근에 감지된 드론 정보
    private DroneTracker latestDetectedDrone = null;
    private float lastDroneDetectionTime = 0f;

    void Start()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        labels = classesAsset.text.Split('\n');
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
        Camera droneCamera = GameObject.Find("DroneCamera").GetComponent<Camera>();
        droneCamera.targetTexture = targetRT;
    }

    private void Update()
    {
        ExecuteML();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        
        // 일정 시간 동안 드론이 감지되지 않으면 상태 메시지 업데이트
        if (latestDetectedDrone != null && Time.time - lastDroneDetectionTime > 3.0f)
        {
            UIManager.instance.SetDroneResultText("적 드론이 시야에서 사라졌습니다.");
            latestDetectedDrone = null;
        }
    }

    public void ExecuteML()
    {
        currentFrameBoxCount = 0;
        ClearAnnotations();

        displayImage.texture = targetRT;

        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
        TextureConverter.ToTensor(targetRT, inputTensor, default);
        worker.Schedule(inputTensor);

        using var output = (worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone();
        using var labelIDs = (worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone();

        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;

        float scaleX = displayWidth / imageWidth;
        float scaleY = displayHeight / imageHeight;

        // 이 프레임에 감지된 드론 ID 추적
        List<string> detectedDroneIds = new List<string>();
        bool droneDetected = false;

        int boxesFound = output.shape[0];
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
            
            // 드론 감지 처리
            if (!string.IsNullOrEmpty(box.label) && box.label.Trim().Equals("drone", StringComparison.OrdinalIgnoreCase))
            {
                droneDetected = true;

                // 드론 위치와 크기 계산
                Vector2 dronePos = new Vector2(box.centerX + displayWidth / 2, box.centerY + displayHeight / 2); // 화면 좌표로 변환
                Vector2 droneSize = new Vector2(box.width, box.height);

                // 드론 ID 생성 (현재는 단순히 인덱스 사용)
                string droneId = "drone_" + n;
                detectedDroneIds.Add(droneId);

                bool isNewDrone = !droneTrackers.ContainsKey(droneId);

                // 위치 업데이트 또는 새로 추가
                if (isNewDrone)
                {
                    droneTrackers[droneId] = new DroneTracker(dronePos, droneSize, box.label);
                }
                else
                {
                    droneTrackers[droneId].UpdatePosition(dronePos, droneSize);
                }

                // 가장 최근 감지된 드론으로 업데이트
                latestDetectedDrone = droneTrackers[droneId];
                lastDroneDetectionTime = Time.time;

                if (isNewDrone)
                {
                    // 새 드론일 때만 수행할 작업들
                    UpdateDroneStatusUI(droneId);
                    Debug.Log("드론 감지! 움직임: " + latestDetectedDrone.movementPattern);

                    DroneController controller = FindFirstObjectByType<DroneController>();
                    if (controller != null)
                    {
                        DroneCommand hoverCommand = new DroneCommand
                        {
                            actionEnum = DroneCommand.DroneAction.Hover
                        };
                        controller.OnCommand(hoverCommand);

                        controller.trackingTarget = FindClosestDroneToBox(box);
                        // controller.StartTracking();

                        UIManager.instance.SetDroneResultText("드론이 감지되었습니다.");
                    }
                }
            }

            
            DrawBox(box, n, displayHeight * 0.05f);
            currentFrameBoxCount++;
        }
        
        // 이번 프레임에 감지되지 않은 드론 제거
        List<string> droneIdsToRemove = new List<string>();
        foreach (var kvp in droneTrackers)
        {
            if (!detectedDroneIds.Contains(kvp.Key))
            {
                if (Time.time - kvp.Value.lastUpdateTime > 3.0f) // 3초 이상 감지되지 않으면 제거
                {
                    droneIdsToRemove.Add(kvp.Key);
                }
            }
        }
        
        foreach (string id in droneIdsToRemove)
        {
            droneTrackers.Remove(id);
        }
    }
    
    // UI에 드론 움직임 정보 업데이트
    private void UpdateDroneStatusUI(string droneId)
    {
        if (!droneTrackers.ContainsKey(droneId) || UIManager.instance == null)
            return;
            
        DroneTracker drone = droneTrackers[droneId];
        
        // UI에 표시할 메시지 생성
        StringBuilder message = new StringBuilder();
        message.AppendLine("===== 적 드론 감지 =====");
        
        // 움직임 패턴 설명
        message.AppendLine($"적 드론이 {drone.movementPattern}");
        
        // 움직임 분석
        if (drone.positionHistory.Count > 5)
        {
            // 이동 방향 추정
            Vector2 recentMovement = drone.position - drone.positionHistory[drone.positionHistory.Count - 5];
            
            // 좀 더 구체적인 분석
            if (recentMovement.magnitude > 20f) // 화면 좌표 기준 임계값
            {
                if (Mathf.Abs(recentMovement.x) > Mathf.Abs(recentMovement.y))
                {
                    // 수평 이동이 더 큼
                    string direction = recentMovement.x > 0 ? "오른쪽" : "왼쪽";
                    message.AppendLine($"최근 {direction}으로 이동하는 경향이 있습니다.");
                }
                else
                {
                    // 수직 이동이 더 큼
                    string direction = recentMovement.y > 0 ? "위쪽" : "아래쪽";
                    message.AppendLine($"최근 {direction}으로 이동하는 경향이 있습니다.");
                }
            }
            
            // 크기 변화로 거리 추정
            float initialSize = drone.positionHistory.Count > 9 
                ? Vector2.Distance(Vector2.zero, drone.positionHistory[0]) 
                : 0;
            float currentSize = Vector2.Distance(Vector2.zero, drone.size);
            
            if (initialSize > 0 && Mathf.Abs(currentSize - initialSize) / initialSize > 0.2f)
            {
                if (currentSize > initialSize)
                {
                    message.AppendLine("드론이 접근하고 있습니다.");
                }
                else
                {
                    message.AppendLine("드론이 멀어지고 있습니다.");
                }
            }
        }
        
        // 화면상 위치 설명
        float screenCenterX = displayImage.rectTransform.rect.width / 2;
        float screenCenterY = displayImage.rectTransform.rect.height / 2;
        
        if (Mathf.Abs(drone.position.x - screenCenterX) > screenCenterX * 0.3f ||
            Mathf.Abs(drone.position.y - screenCenterY) > screenCenterY * 0.3f)
        {
            // 화면 중앙에서 멀리 있음
            string horizontalPos = drone.position.x < screenCenterX * 0.7f ? "왼쪽" : "오른쪽";
            string verticalPos = drone.position.y < screenCenterY * 0.7f ? "아래쪽" : "위쪽";
            
            message.AppendLine($"화면 {horizontalPos} {verticalPos}에 위치하고 있습니다.");
        }
        else
        {
            message.AppendLine("화면 중앙 부근에 위치하고 있습니다.");
        }
        
        // 위험 평가
        if (drone.size.magnitude > screenCenterX * 0.3f) // 드론 크기가 화면의 30% 이상
        {
            message.AppendLine("경고: 드론이 매우 가까이 있습니다!");
        }
        
        // UI 업데이트
        UIManager.instance.SetDroneResultText(message.ToString());
    }
    
    private Transform FindClosestDroneToBox(BoundingBox box)
    {
        GameObject[] drones = GameObject.FindGameObjectsWithTag("Drone"); // 또는 FindObjectsByType<DroneIdentifier>()

        Vector2 screenCenter = new Vector2(displayImage.rectTransform.rect.width / 2, displayImage.rectTransform.rect.height / 2);
        Vector2 boxCenter = screenCenter + new Vector2(box.centerX, -box.centerY); // UGUI 기준 위치 보정

        float minDistance = float.MaxValue;
        Transform closest = null;

        foreach (var drone in drones)
        {
            Vector3 worldPos = drone.transform.position;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            Vector2 localPoint;

            // 화면 좌표를 RawImage 내부 로컬 좌표로 변환
            RectTransformUtility.ScreenPointToLocalPointInRectangle(displayImage.rectTransform, screenPos, null, out localPoint);

            float dist = Vector2.Distance(localPoint, boxCenter);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = drone.transform;
            }
        }

        return closest;
    }

    public void DrawBox(BoundingBox box, int id, float fontSize)
    {
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.yellow);
        }

        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);

        var label = panel.GetComponentInChildren<Text>();
        
        // 드론의 경우 움직임 패턴도 함께 표시
        if (!string.IsNullOrEmpty(box.label) && box.label.Trim().Equals("drone", StringComparison.OrdinalIgnoreCase))
        {
            string droneId = "drone_" + id;
            if (droneTrackers.ContainsKey(droneId) && droneTrackers[droneId].positionHistory.Count > 3)
            {
                label.text = $"{box.label}: {droneTrackers[droneId].movementPattern}";
            }
            else
            {
                label.text = box.label;
            }
        }
        else
        {
            label.text = box.label;
        }
        
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

        boxPool.Add(panel);
        return panel;
    }

    public void ClearAnnotations()
    {
        for (int i = 0; i < boxPool.Count; i++)
        {
            boxPool[i].SetActive(i < currentFrameBoxCount);
        }
    }

    private void OnDestroy()
    {
        centersToCorners?.Dispose();
        worker?.Dispose();
    }
}