using UnityEngine;
using System;
using System.IO;

[Serializable] 
public class DroneCommand 
{
    public enum DroneAction { Move, Hover, Altitude, Rotate, Return }

    [NonSerialized] 
    public DroneAction actionEnum;

    public string Action;
    public float Altitude;
    public Vector3 Direction;
    public float Speed;

    public static DroneCommand FromJson(string json) 
    {
        DroneCommand command = JsonUtility.FromJson<DroneCommand>(json);

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
    }

    public void ConvertCommandFromJson()
    {
        try
        {
            TextAsset jsonFile = Resources.Load<TextAsset>("command");
            if (jsonFile != null)
            {
                DroneCommand command = DroneCommand.FromJson(jsonFile.text);
                currentCommand = command;
                
                // 명령을 DroneController에 전달
                if (droneController != null)
                {
                    droneController.OnCommand(currentCommand);
                    Debug.Log($"명령 실행: {currentCommand.actionEnum}");
                }
            }
            else
            {
                Debug.LogError("test.json 파일을 찾을 수 없습니다. Resources 폴더에 파일이 있는지 확인하세요.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"JSON 파일 처리 중 오류 발생: {e.Message}");
        }
    }
}

/*
public class DroneCommandHandler : MonoBehaviour
{
    private DroneMovement droneMovement;
    private Vector3 startPosition;
    private Quaternion startRotation;

    void Start()
    {
        droneMovement = GetComponent<DroneMovement>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public void ProcessCommand(DroneCommand command)
    {
        switch (command.action.ToLower())
        {
            case "move":
                HandleMoveCommand(command);
                break;
            case "hover":
                HandleHoverCommand();
                break;
            case "altitude":
                HandleAltitudeCommand(command);
                break;
            case "rotate":
                HandleRotateCommand(command);
                break;
            case "return":
                HandleReturnCommand();
                break;
            default:
                Debug.LogWarning($"Unknown command action: {command.action}");
                break;
        }
    }

    private void HandleMoveCommand(DroneCommand command)
    {
        // 드론의 현재 방향을 기준으로 이동 방향 계산
        Vector3 moveDirection = transform.TransformDirection(command.direction);
        droneMovement.SetMovement(moveDirection, command.speed);
    }

    private void HandleHoverCommand()
    {
        // 현재 위치 유지
        droneMovement.SetMovement(Vector3.zero, 0f);
    }

    private void HandleAltitudeCommand(DroneCommand command)
    {
        // 고도 변경
        float altitudeChange = command.altitude - transform.position.y;
        Vector3 verticalMovement = new Vector3(0, Mathf.Sign(altitudeChange), 0);
        droneMovement.SetMovement(verticalMovement, Mathf.Abs(altitudeChange));
    }

    private void HandleRotateCommand(DroneCommand command)
    {
        // 회전 처리
        float rotationAmount = command.direction.y * command.speed * Time.deltaTime;
        transform.Rotate(Vector3.up, rotationAmount);
    }

    private void HandleReturnCommand()
    {
        // 시작 위치로 복귀
        Vector3 returnDirection = (startPosition - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, startPosition);
        droneMovement.SetMovement(returnDirection, distance);
    }
} 

*/