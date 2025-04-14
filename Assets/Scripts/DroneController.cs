using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float verticalSpeed = 3f;
    public float rotationSpeed = 100f;

    private Rigidbody rb;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    
    // 상태 관리를 위한 열거형 추가
    private enum DroneState { Idle, Moving, Hovering, ChangingAltitude, Rotating, Returning }
    private DroneState currentState = DroneState.Idle;
    
    private Vector3 returnPosition = new Vector3(0, 1, 0);
    private float targetAltitude;
    private float originalMoveSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        originalMoveSpeed = moveSpeed;
    }

    void Update()
    {
        switch (currentState)
        {
            case DroneState.ChangingAltitude:
                HandleAltitudeChange();
                break;
            case DroneState.Rotating:
                HandleRotation();
                break;
            case DroneState.Moving:
                HandleMovement();
                break;
            case DroneState.Hovering:
                HandleHovering();
                break;
            case DroneState.Returning:
                HandleReturn();
                break;
        }
    }

    private void HandleAltitudeChange()
    {
        float currentAltitude = transform.position.y;
        float altitudeDifference = targetAltitude - currentAltitude;
        
        if (Mathf.Abs(altitudeDifference) > 0.1f)
        {
            // 현재 속도 유지하면서 고도만 변경
            Vector3 currentVelocity = rb.linearVelocity;
            currentVelocity.y = Mathf.Sign(altitudeDifference) * verticalSpeed;
            rb.linearVelocity = currentVelocity;
        }
        else
        {
            // 목표 고도에 도달
            Vector3 pos = transform.position;
            pos.y = targetAltitude;
            transform.position = pos;
            
            // 수직 속도만 0으로 설정
            Vector3 currentVelocity = rb.linearVelocity;
            currentVelocity.y = 0;
            rb.linearVelocity = currentVelocity;
            
            // 고도 변경 완료 후 Idle 상태로 전환
            currentState = DroneState.Idle;
        }
    }

    private void HandleRotation()
    {
        Quaternion currentRotation = transform.rotation;
        transform.rotation = Quaternion.RotateTowards(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);
        
        // 회전이 완료되었는지 확인
        if (Quaternion.Angle(currentRotation, targetRotation) < 0.1f)
        {
            currentState = DroneState.Idle;
        }
    }

    private void HandleMovement()
    {
        Vector3 moveDirection = transform.TransformDirection(targetPosition);
        Debug.Log($"이동 중: 방향벡터={targetPosition}, 변환된 방향={moveDirection}, 속도={moveSpeed}, 현재위치={transform.position}");
        rb.linearVelocity = moveDirection * moveSpeed;
    }

    private void HandleHovering()
    {
        // 호버링 상태 유지 - moveSpeed를 0으로 설정하고 속도도 0으로 설정
        moveSpeed = 0f;
        rb.linearVelocity = Vector3.zero;
    }

    private void HandleReturn()
    {
        // 복귀 명령 실행
        Vector3 returnDirection = (returnPosition - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, returnPosition);
        
        if (distance > 0.1f)
        {
            rb.linearVelocity = returnDirection * moveSpeed;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
            currentState = DroneState.Idle;
        }
    }

    public void OnCommand(DroneCommand command)
    {
        Debug.Log($"[DroneController] 명령 수신: {command.actionEnum}, 속도: {command.Speed}, 방향: {command.DirectionVector}");
        
        // 모든 상태 초기화 로직 추가
        ResetAllStates();

        switch (command.actionEnum)
        {
            case DroneCommand.DroneAction.Move:
                currentState = DroneState.Moving;
                
                // 방향 및 속도 설정
                targetPosition = command.DirectionVector;
                moveSpeed = command.Speed > 0 ? command.Speed : 5f;
                
                // 고도 설정
                targetAltitude = command.Altitude;
                if (command.Altitude != 0)
                {
                    currentState = DroneState.ChangingAltitude;
                }
                break;
                
            case DroneCommand.DroneAction.Hover:
                currentState = DroneState.Hovering;
                moveSpeed = 0f;
                rb.linearVelocity = Vector3.zero;
                break;
                
            case DroneCommand.DroneAction.Altitude:
                currentState = DroneState.ChangingAltitude;
                targetAltitude = command.Altitude;
                break;
                
            case DroneCommand.DroneAction.Rotate:
                currentState = DroneState.Rotating;
                targetRotation = Quaternion.Euler(command.DirectionVector);
                break;
                
            case DroneCommand.DroneAction.Return:
                currentState = DroneState.Returning;
                moveSpeed = originalMoveSpeed;
                break;
                
            default:
                Debug.LogWarning($"알 수 없는 명령: {command.actionEnum}");
                currentState = DroneState.Idle;
                break;
        }
    }

    // 모든 상태를 초기화하는 메서드 추가
    private void ResetAllStates()
    {
        // 모든 상태 관련 변수 초기화
        rb.linearVelocity = Vector3.zero;
        moveSpeed = originalMoveSpeed;
    }
}