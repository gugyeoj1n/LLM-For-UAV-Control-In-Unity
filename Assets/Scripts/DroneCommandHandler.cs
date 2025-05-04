using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public class DroneCommand
{
    public enum DroneAction { Move, Hover, Altitude, Rotate, Return, Reconnaissance, Tracking }

    [NonSerialized]
    public DroneAction actionEnum;

    public string Action;
    public float Altitude;
    public float[] Direction; // Vector3 대신 배열로 받음
    public float Speed;
    public float TrackingDistance; // 트래킹 거리 추가

    [NonSerialized]
    public Vector3 DirectionVector; // 수동으로 파싱한 Vector3

    public static DroneCommand FromJson(string json)
    {
        DroneCommand command = JsonUtility.FromJson<DroneCommand>(json);

        // 수동으로 Vector3로 변환
        if (command.Direction != null && command.Direction.Length == 3)
        {
            command.DirectionVector = new Vector3(command.Direction[0], command.Direction[1], command.Direction[2]);
        }
        else
        {
            Debug.LogWarning("Direction 값이 유효하지 않습니다. Vector3(0,0,0)으로 설정합니다.");
            command.DirectionVector = Vector3.zero;
        }

        // 문자열 액션을 enum으로 변환
        switch (command.Action.ToLower())
        {
            case "move":
                command.actionEnum = DroneAction.Move;
                break;
            case "hover":
                command.actionEnum = DroneAction.Hover;
                command.Speed = 0;
                break;
            case "altitude":
                command.actionEnum = DroneAction.Altitude;
                break;
            case "rotate":
                command.actionEnum = DroneAction.Rotate;
                break;
            case "return":
                command.actionEnum = DroneAction.Return;
                break;
            case "reconnaissance":
                command.actionEnum = DroneAction.Reconnaissance;
                break;
            case "tracking":
                command.actionEnum = DroneAction.Tracking;
                break;
            default:
                Debug.LogWarning($"알 수 없는 액션: {command.Action}. 기본값 Move로 설정합니다.");
                command.actionEnum = DroneAction.Move;
                break;
        }

        return command;
    }
}


public class DroneCommandHandler : MonoBehaviour
{
    public DroneCommand currentCommand;
    private DroneController droneController;
    private Queue<DroneCommand> commandQueue = new Queue<DroneCommand>();
    private bool isProcessingCommand = false;

    public static DroneCommandHandler instance;
    
    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        droneController = GetComponent<DroneController>();
        if (droneController == null)
        {
            Debug.LogError("DroneController 컴포넌트를 찾을 수 없습니다.");
        }
        CheckForExistingCommand(); // 기존 json 자동로드
    }

    private void CheckForExistingCommand() // 기존 json 자동로드
    {
        try
        {
            TextAsset jsonFile = Resources.Load<TextAsset>("command");
            if (jsonFile != null)
            {
                Debug.Log("기존 command.json 파일을 발견했습니다. 자동으로 로드합니다.");
                ConvertCommandFromJson();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"기존 명령 확인 중 오류: {e.Message}");
        }
    }
    
    public void ConvertCommandFromJson()
    {
        try
        {
            TextAsset jsonFile = Resources.Load<TextAsset>("command");
            if (jsonFile != null)
            {
                Debug.Log($"명령 파일 내용: {jsonFile.text}");
                DroneCommand command = DroneCommand.FromJson(jsonFile.text);
                
                // 큐에 명령 추가
                commandQueue.Enqueue(command);
                Debug.Log($"명령이 큐에 추가됨: {command.actionEnum}. 현재 큐 크기: {commandQueue.Count}");
                
                // 처리 중이 아니면 처리 시작
                if (!isProcessingCommand)
                {
                    ProcessNextCommand();
                }
            }
            else
            {
                Debug.LogError("command.json 파일을 찾을 수 없습니다.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON 파일 처리 중 오류 발생: {e.Message}");
        }
    }

    private void ProcessNextCommand()
    {
        if (commandQueue.Count > 0)
        {
            isProcessingCommand = true;
            DroneCommand cmd = commandQueue.Dequeue();
            currentCommand = cmd;
            
            Debug.Log($"명령 처리 시작: {cmd.actionEnum}");
            
            if (droneController != null)
            {
                droneController.OnCommand(currentCommand);
                Debug.Log($"명령이 드론에 전달됨: {currentCommand.actionEnum}");
                
                // 명령 처리 완료 후 일정 시간 대기
                StartCoroutine(WaitAndProcessNext(2.0f));
            }
            else
            {
                Debug.LogError("DroneController가 null입니다.");
                isProcessingCommand = false;
            }
        }
        else
        {
            isProcessingCommand = false;
            Debug.Log("처리할 명령이 없습니다.");
        }
    }

    private IEnumerator WaitAndProcessNext(float delay)
    {
        yield return new WaitForSeconds(delay);
        isProcessingCommand = false;
        ProcessNextCommand();
    }
    // DroneCommandHandler.cs에 다음 메서드 추가
    public void AddCommand(DroneCommand command)
    {
        // 큐에 명령 추가
        commandQueue.Enqueue(command);
        Debug.Log($"명령이 직접 추가됨: {command.actionEnum}. 현재 큐 크기: {commandQueue.Count}");
        
        // 처리 중이 아니면 처리 시작
        if (!isProcessingCommand)
        {
            ProcessNextCommand();
        }
    }
}