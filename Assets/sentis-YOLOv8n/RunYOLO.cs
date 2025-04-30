using System;
using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.IO;
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

    [SerializeField, Range(0, 1)] float iouThreshold = 0.7f;
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
    }

    public void ExecuteML()
    {
        currentFrameBoxCount = 0;

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
            
            if (!string.IsNullOrEmpty(box.label) && box.label.Trim().Equals("drone", StringComparison.Ordinal))
            {
                Debug.Log("드론 감지!");
                DroneController controller = FindFirstObjectByType<DroneController>( );
                DroneCommand hoverCommand = new DroneCommand
                {
                    actionEnum = DroneCommand.DroneAction.Hover
                };
                controller.OnCommand( hoverCommand );
                UIManager.instance.SetDroneResultText( "드론이 감지되었습니다." );
                controller.trackingTarget = FindClosestDroneToBox(box);
                controller.StartTracking( );
            }
            
            DrawBox(box, n, displayHeight * 0.05f);
            ClearAnnotations();
        }
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
        currentFrameBoxCount++;

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
        label.text = box.label;
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