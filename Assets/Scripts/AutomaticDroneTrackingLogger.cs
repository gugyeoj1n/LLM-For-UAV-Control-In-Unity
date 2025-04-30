using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Linq;

public class AutomaticDroneTrackingLogger : MonoBehaviour
{
    public static AutomaticDroneTrackingLogger instance;
    
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
        logFilePath = Path.Combine(Application.persistentDataPath, "drone_tracking_log.txt");
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
    /// 현재 드론 추적 데이터 수집
    /// </summary>
    private void CollectTrackingData()
    {
        // 드론 컨트롤러 참조 얻기
        DroneController droneController = FindObjectOfType<DroneController>();
        if (droneController == null || droneController.trackingTarget == null) return;
        
        Transform target = droneController.trackingTarget;
        
        // 드론과 타겟 사이의 거리와 방향 계산
        Vector3 dronePos = droneController.transform.position;
        Vector3 targetPos = target.position;
        Vector3 directionToTarget = targetPos - dronePos;
        float distance = directionToTarget.magnitude;
        
        // 상대적 방향 (드론 기준)
        Vector3 localDirection = droneController.transform.InverseTransformDirection(directionToTarget);
        
        // 움직임 패턴 분석
        string movementPattern = DetermineMovementPattern(localDirection);
        
        // 로그 메시지 생성
        string logMessage = $"[거리: {distance:F1}m] 대상이 드론 기준 {movementPattern} " +
                          $"(X: {localDirection.x:F1}, Y: {localDirection.y:F1}, Z: {localDirection.z:F1})";
        
        // 드론 속도 관련 정보
        if (droneController.GetComponent<Rigidbody>() != null)
        {
            float speed = droneController.GetComponent<Rigidbody>().linearVelocity.magnitude;
            logMessage += $", 드론 속도: {speed:F1}m/s";
        }
        
        // 로그 기록
        LogTrackingInfo(logMessage);
    }
    
    /// <summary>
    /// 방향 데이터를 기반으로 움직임 패턴 결정
    /// </summary>
    private string DetermineMovementPattern(Vector3 localDir)
    {
        StringBuilder pattern = new StringBuilder();
        
        // 좌우 움직임
        if (Mathf.Abs(localDir.x) > 1.0f)
        {
            pattern.Append(localDir.x > 0 ? "오른쪽" : "왼쪽");
        }
        
        // 상하 움직임
        if (Mathf.Abs(localDir.y) > 1.0f)
        {
            if (pattern.Length > 0) pattern.Append(" 및 ");
            pattern.Append(localDir.y > 0 ? "위쪽" : "아래쪽");
        }
        
        // 전후 움직임
        if (Mathf.Abs(localDir.z) > 1.0f)
        {
            if (pattern.Length > 0) pattern.Append("으로 ");
            pattern.Append(localDir.z > 0 ? "접근 중" : "멀어지는 중");
        }
        
        // 움직임이 미미한 경우
        if (pattern.Length == 0)
        {
            pattern.Append("제자리에 정지해 있음");
        }
        
        return pattern.ToString();
    }
    
    /// <summary>
    /// 드론 트래킹 상태를 로그 파일에 기록
    /// </summary>
    public void LogTrackingInfo(string logMessage)
    {
        try
        {
            // 타임스탬프와 함께 로그 추가
            string timestampedLog = $"[{DateTime.Now.ToString("HH:mm:ss")}] {logMessage}";
            File.AppendAllText(logFilePath, timestampedLog + "\n");
            
            // 큐에 로그 추가
            recentLogs.Enqueue(timestampedLog);
            if (recentLogs.Count > maxRecentLogs)
            {
                recentLogs.Dequeue(); // 가장 오래된 로그 제거
            }
            
            // UI에 최신 로그 표시 (선택적)
            if (uiManager != null)
                uiManager.SetDroneResultText(timestampedLog);
            
            Debug.Log($"[트래킹 로그] {logMessage}");
        }
        catch (Exception e)
        {
            Debug.LogError($"로그 기록 오류: {e.Message}");
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
        Debug.LogFormat("로그 요약 요청 시작... {0}개의 로그 수집됨", recentLogs.Count);
        LogTrackingInfo("---로그 요약 요청 중---");
        
        // 요약용 시스템 프롬프트
        string systemPrompt = @"
다음 드론 추적 시스템 로그를 분석하여 표적의 움직임 패턴을 3-4문장으로 간결하게 요약해주세요:

1. 주요 움직임 패턴(좌우 이동, 상하 이동, 접근, 후퇴 등)을 식별하세요
2. 움직임의 빈도나 규칙성을 분석하세요
3. 비정상적인 움직임(급격한 방향 전환, 일관된 패턴, 예측 불가능한 움직임 등)을 강조하세요
4. 표적의 전반적인 이동 경로와 의도를 추론해보세요

요약은 간결하면서도 정보가 풍부해야 하며, 드론 운영자가 표적의 행동 패턴을 신속하게 이해할 수 있도록 작성해주세요.
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
                string logEntry = "=== 추적 요약 ===\n" + summary + "\n=================";
                LogTrackingInfo(logEntry);
                
                // UI 업데이트
                if (uiManager != null)
                    foreach (string line in logEntry.Split('\n'))
                        uiManager.SetDroneResultText(line);
                
                Debug.Log($"요약 완료: {summary}");
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
    /// 저장된 로그 파일 삭제 (초기화)
    /// </summary>
    public void ClearLogFile()
    {
        try
        {
            File.WriteAllText(logFilePath, "드론 트래킹 로그 초기화: " + DateTime.Now.ToString() + "\n");
            recentLogs.Clear();
            
            if (uiManager != null)
            {
                uiManager.SetDroneResultText("로그가 초기화되었습니다.");
            }
            
            Debug.Log("로그 파일이 초기화되었습니다.");
        }
        catch (Exception e)
        {
            Debug.LogError($"로그 파일 초기화 오류: {e.Message}");
        }
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
