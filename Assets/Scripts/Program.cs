using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Program : MonoBehaviour
{
    public static Program instance;

    static DroneCommandData currentDroneState = new DroneCommandData
    {
        Action = "hover",
        Altitude = 0.0f,
        Direction = new float[] { 0.0f, 0.0f, 0.0f },
        Speed = 0.0f
    };

    void Start()
    {
        instance = this;
    }

    public void LLMProcess(string promptInput)
    {
        // 새 명령을 처리하기 전에 상태 확인
        Debug.Log("새 명령 처리 전 - 현재 드론 상태:");
        PrintDroneState();
        
        StartCoroutine(ProcessCommands(promptInput));
    }
    
    // 명령 처리를 위한 코루틴
    private System.Collections.IEnumerator ProcessCommands(string promptInput)
    {
        Debug.Log("UAV 명령 테스트 애플리케이션");
        Debug.Log("=============================================");
        Debug.Log("입력된 명령을 처리합니다.");

        // 초기 상태 출력
        Debug.Log("\n=== 초기 드론 상태 ===");
        PrintDroneState();
        
        string apiUrl = "http://localhost:11434/api/chat";
        string modelName = "llama3:8b";
        Debug.Log($"\nOllama API 서버에 연결합니다: {apiUrl}");
        Debug.Log($"사용 모델: {modelName}");
        
        // 시스템 프롬프트 설정
        string systemPrompt = @"
You are an LLM that converts drone commands into structured JSON.

The drone has a current state, and your output should only change the parameters mentioned in the command.

Input: User inputs a natural language drone command.
Output: Convert to JSON with the following structure:
{
  ""action"": ""[action type: move, hover, altitude, rotate, return, reconnaissance, tracking]"",
  ""altitude"": [altitude value (meters)],
  ""direction"": [x, y, z vector - each element between -1.0 and 1.0],
  ""speed"": [speed value (m/s)],
  ""trackingDistance"": [distance value for tracking (meters)]
}

Action Types:
- move: Move in a specific direction
- hover: Maintain position at current location
- altitude: Change altitude
- rotate: Rotate
- return: Return to starting point
- reconnaissance: Perform area reconnaissance/scouting
- tracking: Track a detected target

Tracking Distance:
- When the user wants to set tracking distance (e.g., ""10미터 거리에서 추적""), set action to ""tracking"" and trackingDistance to the specified value (e.g., 10.0)
- For relative changes:
  - To increase distance (e.g., ""3미터 더 멀리""): use positive value (e.g., 3.0)
  - To decrease distance (e.g., ""2미터 더 가까이""): use negative value (e.g., -2.0)

Direction Vector Examples:
- [1.0, 0.0, 0.0]: East/Right
- [-1.0, 0.0, 0.0]: West/Left
- [0.0, 1.0, 0.0]: Upward
- [0.0, -1.0, 0.0]: Downward
- [0.0, 0.0, 1.0]: North/Forward
- [0.0, 0.0, -1.0]: South/Backward

Analyze the input command and return only the JSON object. Do not include any explanations or other text.
";

        if (string.IsNullOrWhiteSpace(promptInput))
        {
            Debug.LogError("입력된 명령이 없습니다.");
            yield break;
        }

        Debug.Log($"\n처리 중인 명령: {promptInput}");

        // Ollama API를 통한 명령 처리시 현재 상태 정보도 함께 전달
        string currentStateJson = JsonConvert.SerializeObject(currentDroneState);
        string fullUserInput = $"현재 드론 상태: {currentStateJson}\n\n사용자 명령: {promptInput}";
        
        // 코루틴으로 API 요청 처리
        yield return StartCoroutine(ProcessWithOllamaCoroutine(apiUrl, modelName, systemPrompt, fullUserInput, (jsonResult) => {
            Debug.Log("\n=== 드론 명령 JSON ===");
            Debug.Log(jsonResult);
            
            // JSON 결과를 드론 상태에 적용
            try {
                JObject responseObj = JObject.Parse(jsonResult);
                string contentJson = responseObj["message"]["content"].ToString();
                DroneCommandData newCommand = JsonConvert.DeserializeObject<DroneCommandData>(contentJson);
                if (newCommand != null) {
                    UpdateDroneState(newCommand);
                }
            }
            catch (Exception ex) {
                Debug.Log($"JSON 파싱 오류: {ex.Message}");
                Debug.Log("규칙 기반 파서로 대체합니다...");
                UpdateDroneState(RuleBasedParser(promptInput));
            }
        }));

        Debug.Log("\n명령 처리가 완료되었습니다.");
        
        // 최종 JSON 결과를 파일로 저장
        SaveJsonToFile();
    }
    
    // JSON 결과를 파일로 저장하는 메서드
    private void SaveJsonToFile()
    {
        try
        {
            // JSON 문자열 생성
            string jsonResult = JsonConvert.SerializeObject(currentDroneState, Formatting.Indented);
            
            // 파일 경로 설정
            string filePath = Path.Combine(Application.dataPath, "Resources", "command.json");
            
            // 파일 저장
            File.WriteAllText(filePath, jsonResult);
            
            Debug.Log($"JSON 결과가 성공적으로 저장되었습니다: {filePath}");

            // 중요! 현재 명령 데이터를 바로 DroneCommand 객체로 변환하여 전달
            DroneCommand command = new DroneCommand();
            command.Action = currentDroneState.Action;
            command.Altitude = currentDroneState.Altitude;
            command.Direction = currentDroneState.Direction;
            command.Speed = currentDroneState.Speed;
            command.TrackingDistance = currentDroneState.TrackingDistance;
            
            // 추적 거리 값도 전달 (여기를 추가)
            if (currentDroneState.Action == "tracking" && currentDroneState.TrackingDistance != 0)
            {
                // Direction 벡터의 크기로 추적 거리를 표현
                // 추적 거리가 양수면 기존 방향에 거리 적용, 음수면 반대 방향으로 설정
                float distance = Mathf.Abs(currentDroneState.TrackingDistance);
                Vector3 dir = new Vector3(0, 0, 1); // 기본 전방 방향
                
                if (command.Direction != null && command.Direction.Length == 3)
                {
                    Vector3 temp = new Vector3(command.Direction[0], command.Direction[1], command.Direction[2]);
                    if (temp.magnitude > 0)
                    {
                        dir = temp.normalized;
                    }
                }
                
                // 추적 거리를 방향 벡터의 크기로 표현
                command.Direction = new float[] { 
                    dir.x * distance, 
                    dir.y * distance, 
                    dir.z * distance 
                };
            }
            
            // FromJson 메서드에 있는 코드와 동일하게 처리
            if (command.Direction != null && command.Direction.Length == 3)
            {
                command.DirectionVector = new Vector3(command.Direction[0], command.Direction[1], command.Direction[2]);
            }
            
            // 문자열 액션을 enum으로 변환
            switch (command.Action.ToLower())
            {
                case "move":
                    command.actionEnum = DroneCommand.DroneAction.Move;
                    break;
                case "hover":
                    command.actionEnum = DroneCommand.DroneAction.Hover;
                    command.Speed = 0;
                    break;
                case "altitude":
                    command.actionEnum = DroneCommand.DroneAction.Altitude;
                    break;
                case "rotate":
                    command.actionEnum = DroneCommand.DroneAction.Rotate;
                    break;
                case "return":
                    command.actionEnum = DroneCommand.DroneAction.Return;
                    break;
                case "reconnaissance":
                    command.actionEnum = DroneCommand.DroneAction.Reconnaissance;
                    break;
                case "tracking":
                    command.actionEnum = DroneCommand.DroneAction.Tracking;
                    break;
                default:
                    Debug.LogWarning($"알 수 없는 액션: {command.Action}. 기본값 Move로 설정합니다.");
                    command.actionEnum = DroneCommand.DroneAction.Move;
                    break;
            }
            
            // 명령을 직접 전달
            if (DroneCommandHandler.instance != null)
            {
                DroneCommandHandler.instance.AddCommand(command);
                Debug.Log("현재 명령이 직접 전달되었습니다!");
            }
            else
            {
                Debug.LogError("DroneCommandHandler.instance가 null입니다!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON 파일 저장 중 오류 발생: {ex.Message}");
        }
    }
    private System.Collections.IEnumerator DelayedExecuteCommand()
    {
        // 짧은 지연을 주어 파일 I/O 작업이 완료될 시간 확보
        
        yield return new WaitForSeconds(1.0f);
        
        if (DroneCommandHandler.instance != null)
        {
            Debug.Log("지연 후 명령 실행");
            DroneCommandHandler.instance.ConvertCommandFromJson();
        }
        else
        {
            Debug.LogError("DroneCommandHandler.instance가 null입니다.");
        }
    }

    // 드론 상태를 업데이트하는 메서드
    static void UpdateDroneState(DroneCommandData newCommand)
    {
        bool stateChanged = false;
        
        // 액션이 지정된 경우에만 변경
        if (newCommand.Action != null && newCommand.Action != "")
        {
            if (currentDroneState.Action != newCommand.Action)
            {
                currentDroneState.Action = newCommand.Action;
                stateChanged = true;
            }
        }
        
        // 고도 변경이 있는 경우
        if (newCommand.Action == "altitude")
        {
            float oldAltitude = currentDroneState.Altitude;
            
            // 방향에 따라 상대적 고도 변경 또는 절대 고도 설정
            if (newCommand.Direction != null && newCommand.Direction.Length == 3)
            {
                if (newCommand.Direction[1] > 0) // 상승
                {
                    currentDroneState.Altitude += newCommand.Altitude;
                }
                else if (newCommand.Direction[1] < 0) // 하강
                {
                    currentDroneState.Altitude -= newCommand.Altitude;
                    if (currentDroneState.Altitude < 0)
                        currentDroneState.Altitude = 0; // 고도는 0 미만이 될 수 없음
                }
                else // 절대 고도 설정
                {
                    currentDroneState.Altitude = newCommand.Altitude;
                }
            }
            else // 방향이 없으면 절대 고도로 설정
            {
                currentDroneState.Altitude = newCommand.Altitude;
            }
            
            if (oldAltitude != currentDroneState.Altitude)
            {
                stateChanged = true;
            }
        }
        
        // 이동 명령이 있는 경우 방향 설정
        if ((newCommand.Action == "move" || newCommand.Action == "reconnaissance") && newCommand.Direction != null && newCommand.Direction.Length == 3)
        {
            if (!currentDroneState.Direction.SequenceEqual(newCommand.Direction))
            {
                currentDroneState.Direction = newCommand.Direction.ToArray();
                stateChanged = true;
            }
        }
        
        // 속도 변경이 있는 경우
        if (newCommand.Speed > 0 && currentDroneState.Speed != newCommand.Speed)
        {
            currentDroneState.Speed = newCommand.Speed;
            stateChanged = true;
        }
        
        // 상태 변경이 있을 때만 출력
        if (stateChanged)
        {
            Debug.Log("\n=== 드론 상태 업데이트됨 ===");
            PrintDroneState();
        }
        
        if (newCommand.Action == "tracking" && newCommand.TrackingDistance != 0)
            {
                currentDroneState.TrackingDistance = newCommand.TrackingDistance;
                stateChanged = true;
                
                if (newCommand.TrackingDistance > 0)
                {
                    Debug.Log($"추적 거리 설정/증가: {newCommand.TrackingDistance}m");
                }
                else
                {
                    Debug.Log($"추적 거리 감소: {Math.Abs(newCommand.TrackingDistance)}m");
                }
            }
        

    }
    
    // 현재 드론 상태를 출력하는 메서드
    static void PrintDroneState()
    {
        Debug.Log($"액션: {currentDroneState.Action}");
        Debug.Log($"고도: {currentDroneState.Altitude}m");
        Debug.Log($"방향: [{string.Join(", ", currentDroneState.Direction)}]");
        Debug.Log($"속도: {currentDroneState.Speed}m/s");
    }

    // UnityWebRequest를 사용하는 코루틴
    private System.Collections.IEnumerator ProcessWithOllamaCoroutine(string apiUrl, string modelName, string systemPrompt, string userInput, System.Action<string> callback)
    {
        // Ollama API 요청 형식 구성
        var requestData = new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userInput }
            },
            stream = false,
            options = new
            {
                temperature = 0.1,
                num_predict = 500
            }
        };

        string jsonData = JsonConvert.SerializeObject(requestData);
        Debug.Log($"API 요청 데이터: {jsonData}");
        
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
                Debug.LogError($"API 요청 실패: {request.error}");
                Debug.LogError($"응답 코드: {request.responseCode}");
                Debug.LogError($"응답 내용: {request.downloadHandler.text}");
                callback("{}"); // 오류 발생 시 빈 JSON 반환
                yield break;
            }

            string responseBody = request.downloadHandler.text;
            
            // JSON 추출 (LLM이 설명 텍스트와 함께 JSON을 반환할 수 있음)
            int startIdx = responseBody.IndexOf('{');
            int endIdx = responseBody.LastIndexOf('}');
            
            if (startIdx >= 0 && endIdx >= 0 && endIdx > startIdx)
            {
                string jsonPart = responseBody.Substring(startIdx, endIdx - startIdx + 1);
                
                try
                {
                    // JSON 형식 유효성 검사 및 포맷팅
                    var parsedJson = JObject.Parse(jsonPart);
                    string formattedJson = parsedJson.ToString(Formatting.Indented);
                    callback(formattedJson);
                }
                catch (Exception)
                {
                    // JSON 파싱 실패 시 원본 텍스트 반환
                    callback(responseBody);
                }
            }
            else
            {
                // JSON이 감지되지 않은 경우 전체 응답 반환
                callback(responseBody);
            }
        }
    }

    static List<string> ListAvailableModels(string localDir)
    {
        var models = new List<string>();

        if (!Directory.Exists(localDir))
        {
            Debug.Log("로컬 모델 디렉토리가 없습니다.");
            return models;
        }

        foreach (var directory in Directory.GetDirectories(localDir))
        {
            if (File.Exists(Path.Combine(directory, "tokenizer_config.json")) &&
                (File.Exists(Path.Combine(directory, "pytorch_model.bin")) ||
                 File.Exists(Path.Combine(directory, "model.safetensors"))))
            {
                models.Add(Path.GetFileName(directory));
            }
        }

        return models;
    }

    static string FormatJson(string jsonStr)
    {
        // JSON 패턴 찾기 (중괄호로 둘러싸인 부분)
        var match = Regex.Match(jsonStr, @"\{.*\}", RegexOptions.Singleline);

        if (match.Success)
        {
            string jsonPart = match.Value;
            try
            {
                // JSON 파싱 시도
                var parsedJson = JObject.Parse(jsonPart);
                // 정렬된 JSON 반환
                return parsedJson.ToString(Formatting.Indented);
            }
            catch (Exception)
            {
                return "유효한 JSON 형식이 아닙니다: " + jsonPart;
            }
        }
        else
        {
            return "JSON 형식을 찾을 수 없습니다.";
        }
    }

    static DroneCommandData RuleBasedParser(string command)
    {
        var result = new DroneCommandData
        {
            Action = "",
            Altitude = 0.0f,
            Direction = new float[] { 0.0f, 0.0f, 0.0f },
            Speed = 0.0f
        };

        // 고도 파싱 - 절대값과 상대값 모두 처리
        var altitudeExact = Regex.Match(command, @"고도.{0,5}(\d+)\s*(미터|m)");
        var altitudeUp = Regex.Match(command, @"고도.{0,5}(\d+).{0,5}(올|높|증가|상승)");
        var altitudeDown = Regex.Match(command, @"고도.{0,5}(\d+).{0,5}(내|낮|감소|하강)");

        if (altitudeExact.Success)
        {
            float altitude = float.Parse(altitudeExact.Groups[1].Value);
            result.Altitude = altitude;
            result.Action = "altitude";
        }
        else if (altitudeUp.Success)
        {
            float altitude = float.Parse(altitudeUp.Groups[1].Value);
            result.Altitude = altitude;
            result.Action = "altitude";
            result.Direction = new float[] { 0.0f, 1.0f, 0.0f };  // 상승 방향
        }
        else if (altitudeDown.Success)
        {
            float altitude = float.Parse(altitudeDown.Groups[1].Value);
            result.Altitude = altitude;
            result.Action = "altitude";
            result.Direction = new float[] { 0.0f, -1.0f, 0.0f };  // 하강 방향
        }

        var directionKeywords = new Dictionary<string, float[]>
        {
            { "위", new float[] { 0.0f, 1.0f, 0.0f } },
            { "아래", new float[] { 0.0f, -1.0f, 0.0f } },
            { "앞", new float[] { 0.0f, 0.0f, 1.0f } },
            { "뒤", new float[] { 0.0f, 0.0f, -1.0f } },
            { "왼쪽", new float[] { -1.0f, 0.0f, 0.0f } },
            { "오른쪽", new float[] { 1.0f, 0.0f, 0.0f } },
            { "북", new float[] { 0.0f, 0.0f, 1.0f } },
            { "남", new float[] { 0.0f, 0.0f, -1.0f } },
            { "동", new float[] { 1.0f, 0.0f, 0.0f } },
            { "서", new float[] { -1.0f, 0.0f, 0.0f } }
        };

        foreach (var keyword in directionKeywords.Keys)
        {
            if (command.Contains(keyword))
            {
                result.Direction = directionKeywords[keyword];
                if (string.IsNullOrEmpty(result.Action)) {
                    result.Action = "move";
                }
                break;
            }
        }

        var speedExact = Regex.Match(command, @"(\d+)\s*(속도|스피드|m/s)");
        var speedUp = Regex.Match(command, @"속(도|력).{0,5}(\d+).{0,5}(올|높|증가)");
        var speedDown = Regex.Match(command, @"속(도|력).{0,5}(\d+).{0,5}(내|낮|감소)");

        if (speedExact.Success)
        {
            float speed = float.Parse(speedExact.Groups[1].Value);
            result.Speed = speed;
        }
        else if (speedUp.Success)
        {
            float speed = float.Parse(speedUp.Groups[2].Value);
            result.Speed = speed;
        }
        else if (speedDown.Success)
        {
            float speed = float.Parse(speedDown.Groups[2].Value);
            result.Speed = speed;
        }
        else if ((result.Action == "move" || result.Action == "reconnaissance") && currentDroneState.Speed <= 0)
        {
            // 이동 명령시 현재 속도가 0이면 기본 속도 설정
            result.Speed = 5.0f;
        }

        // 동작 타입 파싱
        if (command.Contains("회전"))
        {
            result.Action = "rotate";
        }
        else if (command.Contains("정지") || command.Contains("제자리"))
        {
            result.Action = "hover";
            result.Speed = 0.0f;
        }
        else if (command.Contains("복귀") || command.Contains("돌아"))
        {
            result.Action = "return";
        }
                else if (command.Contains("정찰") || command.Contains("스캔") || command.Contains("탐색") || command.Contains("감시") || command.Contains("살펴") || command.Contains("reconnaissance"))
        {
            result.Action = "reconnaissance";
            if (result.Speed <= 0) {
                result.Speed = 3.0f; // 정찰은 조금 더 느린 속도로 설정
            }
        }
        else if ((command.Contains("날아") || command.Contains("이동") || command.Contains("비행")) 
                && string.IsNullOrEmpty(result.Action))
        {
            result.Action = "move";
            if (result.Speed <= 0 && !speedDown.Success) {
                result.Speed = 5.0f;
            }
        }

        // 추적 명령 인식 로직 추가
        if (command.Contains("추적") || command.Contains("따라가") || command.Contains("tracking") || command.Contains("follow"))
        {
            result.Action = "tracking";
            
            // 거리 파싱 (예: "10미터 거리에서 추적")
            var distanceMatch = Regex.Match(command, @"(\d+)\s*(미터|m|거리)");
            if (distanceMatch.Success)
            {
                float distance = float.Parse(distanceMatch.Groups[1].Value);
                result.TrackingDistance = distance;
            }
            else
            {
                // 기본 추적 거리 설정
                result.TrackingDistance = 5.0f;
            }
        }

        return result;
    }
}

[System.Serializable]
public class DroneCommandData
{
    public string Action { get; set; } = "hover";
    public float Altitude { get; set; }
    public float[] Direction { get; set; } = new float[3];
    public float Speed { get; set; }
    public float TrackingDistance { get; set; } // 추적 거리 필드 추가
}