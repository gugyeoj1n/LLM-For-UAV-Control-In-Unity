using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;

namespace DroneCommandParser
{
    class Program
    {
        static DroneCommand currentDroneState = new DroneCommand
        {
            Action = "hover",
            Altitude = 0.0f,
            Direction = new float[] { 0.0f, 0.0f, 0.0f },
            Speed = 0.0f
        };

        static readonly HttpClient client = new HttpClient();
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("UAV 명령 테스트 애플리케이션");
            Console.WriteLine("=============================================");
            Console.WriteLine("명령 파일을 읽어서 처리합니다.");

            // 초기 상태 출력
            Console.WriteLine("\n=== 초기 드론 상태 ===");
            PrintDroneState();
            
            string apiUrl = "http://localhost:11434/api/chat";
            string modelName = "llama3:8b";
            Console.WriteLine($"\nOllama API 서버에 연결합니다: {apiUrl}");
            Console.WriteLine($"사용 모델: {modelName}");
            
            // 시스템 프롬프트 설정
            string systemPrompt = @"
You are an LLM that converts drone commands into structured JSON.

The drone has a current state, and your output should only change the parameters mentioned in the command.

Input: User inputs a natural language drone command.
Output: Convert to JSON with the following structure:
{
  ""action"": ""[action type: move, hover, altitude, rotate, return]"",
  ""altitude"": [altitude value (meters)],
  ""direction"": [x, y, z vector - each element between -1.0 and 1.0],
  ""speed"": [speed value (m/s)]
}

Action Types:
- move: Move in a specific direction
- hover: Maintain position at current location
- altitude: Change altitude
- rotate: Rotate
- return: Return to starting point

Direction Vector Examples:
- [1.0, 0.0, 0.0]: East/Right
- [-1.0, 0.0, 0.0]: West/Left
- [0.0, 1.0, 0.0]: Upward
- [0.0, -1.0, 0.0]: Downward
- [0.0, 0.0, 1.0]: North/Forward
- [0.0, 0.0, -1.0]: South/Backward

Analyze the input command and return only the JSON object. Do not include any explanations or other text.
";

            // 명령 파일 경로 설정
            string commandFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "commands.txt");
            Console.WriteLine($"명령 파일 경로: {commandFilePath}");
            if (!File.Exists(commandFilePath))
            {
                Console.WriteLine($"\n오류: {commandFilePath} 파일이 존재하지 않습니다.");
                // 프로젝트 디렉토리에서도 시도
                string projectPath = Path.Combine(Directory.GetCurrentDirectory(), "commands.txt");
                Console.WriteLine($"프로젝트 디렉토리에서 시도: {projectPath}");
                if (File.Exists(projectPath))
                {
                    commandFilePath = projectPath;
                    Console.WriteLine("프로젝트 디렉토리에서 파일을 찾았습니다.");
                }
                else
                {
                    return;
                }
            }

            // 명령 파일 읽기
            string[] commands = File.ReadAllLines(commandFilePath);
            Console.WriteLine($"\n{commands.Length}개의 명령을 처리합니다.");

            foreach (string userInput in commands)
            {
                if (string.IsNullOrWhiteSpace(userInput))
                {
                    continue;
                }

                Console.WriteLine($"\n처리 중인 명령: {userInput}");

                try
                {
                    // Ollama API를 통한 명령 처리시 현재 상태 정보도 함께 전달
                    string currentStateJson = JsonSerializer.Serialize(currentDroneState);
                    string fullUserInput = $"현재 드론 상태: {currentStateJson}\n\n사용자 명령: {userInput}";
                    
                    string jsonResult = await ProcessWithOllama(apiUrl, modelName, systemPrompt, fullUserInput);
                    
                    Console.WriteLine("\n=== 드론 명령 JSON ===");
                    Console.WriteLine(jsonResult);
                    
                    // JSON 결과를 드론 상태에 적용
                    try {
                        DroneCommand newCommand = JsonSerializer.Deserialize<DroneCommand>(jsonResult);
                        if (newCommand != null) {
                            UpdateDroneState(newCommand);
                        }
                    }
                    catch (JsonException ex) {
                        Console.WriteLine($"JSON 파싱 오류: {ex.Message}");
                        Console.WriteLine("규칙 기반 파서로 대체합니다...");
                        UpdateDroneState(RuleBasedParser(userInput));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n오류 발생: {ex.Message}");
                    Console.WriteLine("Ollama 서버가 실행 중인지 확인하세요.");
                    
                    // 규칙 기반 파서로 폴백
                    Console.WriteLine("\n규칙 기반 파서로 대체합니다...");
                    try
                    {
                        UpdateDroneState(RuleBasedParser(userInput));
                        string response = JsonSerializer.Serialize(currentDroneState, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });

                        Console.WriteLine("\n=== 드론 명령 JSON (규칙 기반) ===");
                        Console.WriteLine(response);
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"\n규칙 기반 파서 오류: {ex2.Message}");
                    }
                }
            }

            Console.WriteLine("\n모든 명령 처리가 완료되었습니다.");
        }

        // 드론 상태를 업데이트하는 메서드
        static void UpdateDroneState(DroneCommand newCommand)
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
            if (newCommand.Action == "move" && newCommand.Direction != null && newCommand.Direction.Length == 3)
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
                Console.WriteLine("\n=== 드론 상태 업데이트됨 ===");
                PrintDroneState();
            }
        }
        
        // 현재 드론 상태를 출력하는 메서드
        static void PrintDroneState()
        {
            Console.WriteLine($"액션: {currentDroneState.Action}");
            Console.WriteLine($"고도: {currentDroneState.Altitude}m");
            Console.WriteLine($"방향: [{string.Join(", ", currentDroneState.Direction)}]");
            Console.WriteLine($"속도: {currentDroneState.Speed}m/s");
        }

        static async Task<string> ProcessWithOllama(string apiUrl, string modelName, string systemPrompt, string userInput)
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

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json"
            );

            // API 호출
            var response = await client.PostAsync(apiUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"API 요청 실패 (상태 코드: {response.StatusCode}): {errorContent}");
            }
            
            string responseBody = await response.Content.ReadAsStringAsync();
            
            // Ollama API 응답 파싱
            using (JsonDocument doc = JsonDocument.Parse(responseBody))
            {
                JsonElement root = doc.RootElement;
                
                // Ollama API 응답 형식
                if (root.TryGetProperty("message", out JsonElement message) && 
                    message.TryGetProperty("content", out JsonElement contentText))
                {
                    string llmResponse = contentText.GetString() ?? "";
                    
                    // JSON 추출 (LLM이 설명 텍스트와 함께 JSON을 반환할 수 있음)
                    int startIdx = llmResponse.IndexOf('{');
                    int endIdx = llmResponse.LastIndexOf('}');
                    
                    if (startIdx >= 0 && endIdx >= 0 && endIdx > startIdx)
                    {
                        string jsonPart = llmResponse.Substring(startIdx, endIdx - startIdx + 1);
                        
                        try
                        {
                            // JSON 형식 유효성 검사 및 포맷팅
                            var parsedJson = JsonSerializer.Deserialize<object>(jsonPart);
                            return JsonSerializer.Serialize(parsedJson, new JsonSerializerOptions 
                            { 
                                WriteIndented = true,
                                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            });
                        }
                        catch (JsonException)
                        {
                            // JSON 파싱 실패 시 원본 텍스트 반환
                            return llmResponse;
                        }
                    }
                    
                    // JSON이 감지되지 않은 경우 전체 응답 반환
                    return llmResponse;
                }
                
                // 기본 응답
                return responseBody;
            }
        }

        static List<string> ListAvailableModels(string localDir)
        {
            var models = new List<string>();

            if (!Directory.Exists(localDir))
            {
                Console.WriteLine("로컬 모델 디렉토리가 없습니다.");
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
                    var parsedJson = JsonSerializer.Deserialize<object>(jsonPart);
                    // 정렬된 JSON 반환
                    return JsonSerializer.Serialize(parsedJson, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                }
                catch (JsonException)
                {
                    return "유효한 JSON 형식이 아닙니다: " + jsonPart;
                }
            }
            else
            {
                return "JSON 형식을 찾을 수 없습니다.";
            }
        }

        static DroneCommand RuleBasedParser(string command)
        {
            var result = new DroneCommand
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
            else if (result.Action == "move" && currentDroneState.Speed <= 0)
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
            else if ((command.Contains("날아") || command.Contains("이동") || command.Contains("비행")) 
                    && string.IsNullOrEmpty(result.Action))
            {
                result.Action = "move";
                if (result.Speed <= 0 && !speedDown.Success) {
                    result.Speed = 5.0f;
                }
            }

            return result;
        }
    }

    // 드론 명령을 표현하는 클래스
    public class DroneCommand
    {
        public string Action { get; set; } = "hover";
        public float Altitude { get; set; }
        public float[] Direction { get; set; } = new float[3];
        public float Speed { get; set; }
    }
}
