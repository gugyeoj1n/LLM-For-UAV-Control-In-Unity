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
    private bool isHovering = false;
    private bool isMoving = false;
    private bool isRotating = false;
    private bool isReturning = false;
    private Vector3 returnPosition = new Vector3(0, 1, 0);
    private float targetAltitude;
    private bool isChangingAltitude = false;
    private float originalMoveSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        originalMoveSpeed = moveSpeed;
    }

    void Update()
    {
        // 고도 변경 처리 (다른 명령과 독립적으로 작동)
        if (isChangingAltitude)
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
                
                isChangingAltitude = false;
            }
        }
        
        // 회전 처리 (다른 명령과 독립적으로 작동)
        if (isRotating)
        {
            Quaternion currentRotation = transform.rotation;
            transform.rotation = Quaternion.RotateTowards(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);
            
            // 회전이 완료되었는지 확인
            if (Quaternion.Angle(currentRotation, targetRotation) < 0.1f)
            {
                isRotating = false;
            }
        }
        
        // 이동 및 호버링 처리
        if (isHovering)
        {
            // 호버링 상태 유지 - moveSpeed를 0으로 설정하고 속도도 0으로 설정
            moveSpeed = 0f;
            rb.linearVelocity = Vector3.zero;
        }
        else if (isMoving)
        {
            // 이동 명령 실행
            Vector3 moveDirection = transform.TransformDirection(targetPosition);
            rb.linearVelocity = moveDirection * moveSpeed;
        }
        else if (isReturning)
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
                isReturning = false;
            }
        }
    }

    public void OnCommand(DroneCommand command)
    {
        switch (command.actionEnum)
        {
            case DroneCommand.DroneAction.Move:
                // 이동 명령 처리 - 방향, 속도, 고도만 사용
                isHovering = false;
                isMoving = true;
                isReturning = false;
                
                // 방향 및 속도 설정
                targetPosition = command.Direction;
                moveSpeed = command.Speed;
                
                // 고도 설정 (고도 변경 명령과 독립적으로 작동)
                targetAltitude = command.Altitude;
                isChangingAltitude = true;
                break;
                
            case DroneCommand.DroneAction.Hover:
                // 호버링 명령 처리 - 다른 모든 이동 명령 중지
                isHovering = true;
                isMoving = false;
                isReturning = false;
                moveSpeed = 0f;
                rb.linearVelocity = Vector3.zero; // 즉시 속도 0으로 설정
                break;
                
            case DroneCommand.DroneAction.Altitude:
                // 고도 변경 명령 처리 - 고도만 사용
                targetAltitude = command.Altitude;
                isChangingAltitude = true;
                break;
                
            case DroneCommand.DroneAction.Rotate:
                // 회전 명령 처리 - 방향만 사용
                isRotating = true;
                targetRotation = Quaternion.Euler(command.Direction);
                break;
                
            case DroneCommand.DroneAction.Return:
                // 복귀 명령 처리 - 다른 모든 이동 명령 중지
                isHovering = false;
                isMoving = false;
                isReturning = true;
                moveSpeed = originalMoveSpeed; // 원래 속도로 복원
                break;
                
            default:
                Debug.LogWarning($"알 수 없는 명령: {command.actionEnum}");
                break;
        }
    }
}
