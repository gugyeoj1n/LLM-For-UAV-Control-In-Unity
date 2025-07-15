using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Linq;

public class AutomaticSatelliteTrackingLogger : MonoBehaviour
{
    public static AutomaticSatelliteTrackingLogger instance;
    
    // 로그 파일 경로
    private string logFilePath;
    
    // UI 참조
    private UIManager uiManager;
    
    // 자동 요약 타이머
    public float autoSummarizeInterval = 10.0f; // 10초 간격으로 자동 요약
    private float lastSummarizeTime = 0f;
    private bool isProcessingSummary = false; // API 요청 처리 중 여부
    
    // 로그 수집 시간
    private float lastLogTime = 0f;
    [SerializeField]
    private float logInterval; // 0.5초 간격으로 로그 수집
    
    // 트래킹 활성화 여부
    public bool isTrackingActive = false;
    
    // 최근 로그 저장용 (큐 형식)
    private Queue<string> recentLogs = new Queue<string>();
    private int maxRecentLogs = 15; // 최대 저장할 최근 로그 수
    
    // LLM API 설정
    private string apiUrl = "http://localhost:11434/api/chat";
    private string modelName = "llama3:8b";
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        
        // 로그 파일 경로 설정
        logFilePath = Path.Combine(Application.persistentDataPath, "Satellite_tracking_log.txt");
        Debug.Log($"로그 파일 경로: {logFilePath}");
        
        // 초기화
        ClearLogFile();
    }
    
    void Start()
    {
        uiManager = UIManager.instance;
        if (uiManager == null)
        {
            Debug.LogWarning("UIManager를 찾을 수 없습니다.");
        }
    }
    
    void Update()
    {
        // 트래킹이 활성화된 경우만 로그 처리
        if (!isTrackingActive) return;
        
        // 로그 수집 간격 관리
        if (Time.time - lastLogTime >= logInterval)
        {
            lastLogTime = Time.time;
            CollectTrackingData();
        }

        // 자동 요약 간격 관리
        if (Time.time - lastSummarizeTime >= autoSummarizeInterval)
        {
            lastSummarizeTime = Time.time;
            RequestLogSummary();
        }
    }
    
    /// <summary>
    /// 트래킹 활성화 설정
    /// </summary>
    public void SetTrackingActive(bool active)
    {
        isTrackingActive = active;
        
        if (active)
        {
            // Debug.Log("자동 트래킹 로그 시작");
            lastSummarizeTime = Time.time; // 타이머 초기화
            // LogTrackingInfo("자동 트래킹 로그가 시작되었습니다.");
        }
        else
        {
            // Debug.Log("자동 트래킹 로그 종료");
            // LogTrackingInfo("자동 트래킹 로그가 종료되었습니다.");
            
            // 로그 종료 시 마지막 요약 수행
            RequestLogSummary();
        }
    }
    
    /// <summary>
    /// Collect current satellite tracking data
    /// </summary>
    private void CollectTrackingData()
    {
        // Get reference to SatelliteController
        SatelliteController SatelliteController = FindObjectOfType<SatelliteController>();
        if (SatelliteController == null || SatelliteController.trackingTarget == null) return;
        
        Transform target = SatelliteController.trackingTarget;
        
        // Calculate distance and direction between satellite and target
        Vector3 SatellitePos = SatelliteController.transform.position;
        Vector3 targetPos = target.position;
        Vector3 directionToTarget = targetPos - SatellitePos;
        float distance = directionToTarget.magnitude;
        
        // Relative direction (from satellite's perspective)
        Vector3 localDirection = SatelliteController.transform.InverseTransformDirection(directionToTarget);
        
        // Analyze movement pattern
        string movementPattern = DetermineMovementPattern(localDirection);
        
        // Create log message
        string logMessage = $"[Distance: {distance:F1}m] Target is {movementPattern} relative to the satellite " +
                          $"(X: {localDirection.x:F1}, Y: {localDirection.y:F1}, Z: {localDirection.z:F1})";
        
        // Satellite speed info
        if (SatelliteController.GetComponent<Rigidbody>() != null)
        {
            float speed = SatelliteController.GetComponent<Rigidbody>().linearVelocity.magnitude;
            logMessage += $", Satellite speed: {speed:F1}m/s";
        }
        
        // Log
        LogTrackingInfo(logMessage);
    }
    
    /// <summary>
    /// Determine movement pattern based on direction data
    /// </summary>
    private string DetermineMovementPattern(Vector3 localDir)
    {
        StringBuilder pattern = new StringBuilder();
        
        // Left/right movement
        if (Mathf.Abs(localDir.x) > 1.0f)
        {
            pattern.Append(localDir.x > 0 ? "moving right" : "moving left");
        }
        
        // Up/down movement
        if (Mathf.Abs(localDir.y) > 1.0f)
        {
            if (pattern.Length > 0) pattern.Append(" and ");
            pattern.Append(localDir.y > 0 ? "moving up" : "moving down");
        }
        
        // Forward/backward movement
        if (Mathf.Abs(localDir.z) > 1.0f)
        {
            if (pattern.Length > 0) pattern.Append(", ");
            pattern.Append(localDir.z > 0 ? "approaching" : "moving away");
        }
        
        // If movement is minimal
        if (pattern.Length == 0)
        {
            pattern.Append("stationary");
        }
        
        return pattern.ToString();
    }
    
    /// <summary>
    /// Record satellite tracking status to log file
    /// </summary>
    public void LogTrackingInfo(string logMessage)
    {
        try
        {
            // Add log with timestamp
            string timestampedLog = $"[{DateTime.Now.ToString("HH:mm:ss")}] {logMessage}";
            File.AppendAllText(logFilePath, timestampedLog + "\n");
            
            // Add to queue
            recentLogs.Enqueue(timestampedLog);
            if (recentLogs.Count > maxRecentLogs)
            {
                recentLogs.Dequeue(); // Remove oldest log
            }
            
            // Show latest log on UI (optional)
            if (uiManager != null)
                uiManager.SetSatelliteResultText(timestampedLog);
            
            Debug.Log($"[Tracking Log] {logMessage}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Log write error: {e.Message}");
        }
    }
    
    /// <summary>
    /// 로그 파일을 읽고 LLM에 요약 요청
    /// </summary>
    public void RequestLogSummary()
    {
        // 이미 요약 처리 중이면 스킵
        if (isProcessingSummary)
        {
            Debug.Log("이전 요약 처리가 아직 진행 중입니다.");
            return;
        }

        string logPath = Path.Combine(Application.persistentDataPath, "LLMWorkflowLog.txt");
        string nowStr = DateTime.Now.ToString("HH:mm:ss.fff");
        Debug.Log($"[TIME] LLM 요약 요청 시작: {nowStr}");
        File.AppendAllText(logPath, $"[TIME] LLM 요약 요청 시작: {nowStr}\n");

        try
        {
            // 로그 파일 읽기
            if (!File.Exists(logFilePath))
            {
                Debug.LogWarning("요약할 로그 파일이 없습니다.");
                return;
            }
            
            string logContent = File.ReadAllText(logFilePath);
            if (string.IsNullOrEmpty(logContent))
            {
                Debug.LogWarning("로그 내용이 비어 있습니다.");
                return;
            }
            
            // API 요청 시작
            isProcessingSummary = true;
            StartCoroutine(ProcessSummaryRequest(logContent));
        }
        catch (Exception e)
        {
            isProcessingSummary = false;
            Debug.LogError($"로그 요약 요청 오류: {e.Message}");
        }
    }
    
    /// <summary>
    /// LLM에 요약 요청을 보내는 코루틴
    /// </summary>
    private IEnumerator ProcessSummaryRequest(string logContent)
    {
        Debug.LogFormat("Starting log summary request... {0} logs collected", recentLogs.Count);
        LogTrackingInfo("---Requesting log summary---");
        
        // System prompt for summary
        string systemPrompt = @"
Analyze the following satellite tracking system logs and concisely summarize the detected satellite movement patterns in 3-4 sentences:

1. Identify major movement patterns (left/right, up/down, approaching, moving away, etc.)
2. Analyze the frequency and regularity of satellite movements or orbits
3. Highlight any abnormal satellite behavior (sudden direction changes, consistent patterns, unpredictable movements, etc.)
4. Infer the overall movement path and intent of the satellite

The summary should be concise yet informative, enabling a satellite tracking operator to quickly understand the satellite's behavior patterns.
";
        
        // Ollama API 요청 형식 구성
        var requestData = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = logContent }
            },
            stream = false,
            options = new
            {
                temperature = 0.1,
                num_predict = 300 // 짧은 요약을 위해 토큰 제한
            }
        };
        
        string jsonData = JsonConvert.SerializeObject(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        
        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            // 요청 보내기
            var operation = request.SendWebRequest();
            
            // 요청이 완료될 때까지 대기
            while (!operation.isDone)
            {
                yield return null;
            }
            
            string logPath = Path.Combine(Application.persistentDataPath, "LLMWorkflowLog.txt");
            string nowStr = DateTime.Now.ToString("HH:mm:ss.fff");
            Debug.Log($"[TIME] LLM 요약 응답 수신: {nowStr}");
            File.AppendAllText(logPath, $"[TIME] LLM 요약 응답 수신: {nowStr}\n");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"요약 API 요청 실패: {request.error}");
                LogTrackingInfo($"요약 실패: {request.error}");
                isProcessingSummary = false;
                yield break;
            }
            
            string responseBody = request.downloadHandler.text;
            Debug.Log($"요약 API 응답 수신 완료");
            
            try
            {
                // JSON 파싱
                var response = JsonConvert.DeserializeObject<OllamaResponse>(responseBody);
                string summary = response.Message.Content;
                
                // 요약 내용 로그에 추가
                string logEntry = "=== Tracking Summary ===\n" + summary + "\n=================";
                LogTrackingInfo(logEntry);
                
                // UI 업데이트
                if (uiManager != null)
                    foreach (string line in logEntry.Split('\n'))
                        uiManager.SetSatelliteResultText(line);
                
                Debug.Log($"요약 완료: {summary}");
                string nowStr2 = DateTime.Now.ToString("HH:mm:ss.fff");
                Debug.Log($"[TIME] LLM 요약 처리 완료: {nowStr2}");
                File.AppendAllText(logPath, $"[TIME] LLM 요약 처리 완료: {nowStr2}\n");
            }
            catch (Exception e)
            {
                Debug.LogError($"요약 응답 처리 오류: {e.Message}");
                LogTrackingInfo($"요약 처리 실패: {e.Message}");
            }
            finally
            {
                isProcessingSummary = false;
            }
        }
    }
    
    /// <summary>
    /// Delete saved log file (initialize)
    /// </summary>
    public void ClearLogFile()
    {
        try
        {
            File.WriteAllText(logFilePath, "Satellite tracking log initialized: " + DateTime.Now.ToString() + "\n");
            recentLogs.Clear();
            
            if (uiManager != null)
            {
                uiManager.SetSatelliteResultText("Log has been initialized.");
            }
            
            Debug.Log("Log file has been initialized.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Log file initialization error: {e.Message}");
        }
    }

    public class ProcessTimingInfo
    {
        public DateTime CommandInputStart, CommandInputEnd;
        public DateTime LLMStart, LLMEnd;
        public DateTime ControlStart, ControlEnd;
        public DateTime VisionStart, VisionEnd;
        public DateTime LogStart, LogEnd;

        public void PrintAllTimings()
        {
            Debug.Log($"[TIME] 1. 명령 입력: {CommandInputStart:HH:mm:ss.fff} ~ {CommandInputEnd:HH:mm:ss.fff} ({(CommandInputEnd-CommandInputStart).TotalSeconds:F3}초)");
            Debug.Log($"[TIME] 2. LLM 변환: {LLMStart:HH:mm:ss.fff} ~ {LLMEnd:HH:mm:ss.fff} ({(LLMEnd-LLMStart).TotalSeconds:F3}초)");
            Debug.Log($"[TIME] 3. 위성 제어: {ControlStart:HH:mm:ss.fff} ~ {ControlEnd:HH:mm:ss.fff} ({(ControlEnd-ControlStart).TotalSeconds:F3}초)");
            Debug.Log($"[TIME] 4. 비전 분석: {VisionStart:HH:mm:ss.fff} ~ {VisionEnd:HH:mm:ss.fff} ({(VisionEnd-VisionStart).TotalSeconds:F3}초)");
            Debug.Log($"[TIME] 5. 로그/요약: {LogStart:HH:mm:ss.fff} ~ {LogEnd:HH:mm:ss.fff} ({(LogEnd-LogStart).TotalSeconds:F3}초)");
            Debug.Log($"[TIME] 전체 소요 시간: {(LogEnd-CommandInputStart).TotalSeconds:F3}초");
        }
    }

    // 로그/요약 단계에서 전체 타이밍을 출력하는 메서드 추가
    public void LogSummaryWithTiming(ProcessTimingInfo timingInfo)
    {
        timingInfo.LogStart = DateTime.Now;
        // ... 로그/요약 처리 ...
        timingInfo.LogEnd = DateTime.Now;
        timingInfo.PrintAllTimings(); // 전체 시간 및 각 파트별 시간 출력
    }
}

public class OllamaResponse
{
    [JsonProperty("message")]
    public Message Message { get; set; }
}

public class Message
{
    [JsonProperty("content")]
    public string Content { get; set; }
}
