using UnityEngine;
using System.Collections.Generic;

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
    private bool isChangingAltitude = false;
    private bool isRotating = false;
    private bool isReturning = false;
    private bool isReconnaissance = false;
    private bool isTracking = false;

    private Vector3 returnPosition = new Vector3(0, 1, 0);
    private float targetAltitude;
    private float originalMoveSpeed;

    private LidarScanner lidar;

    private RunYOLO yolo;
    
    // 드론 추적 관련 속성 추가
    public Transform trackingTarget;
    public float trackingDistance = 5f; // 추적 대상과 유지할 거리
    public float trackingSpeedMultiplier = 0.8f; // 추적 시 속도 계수

    private AutomaticDroneTrackingLogger trackingLogger;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        originalMoveSpeed = moveSpeed;

        lidar = GetComponent<LidarScanner>();
        yolo = FindFirstObjectByType<RunYOLO>();
        yolo.enabled = false;

        trackingLogger = FindObjectOfType<AutomaticDroneTrackingLogger>();
        if (trackingLogger == null)
        {
            Debug.LogWarning("AutomaticDroneTrackingLogger를 찾을 수 없습니다. 자동 요약 기능이 비활성화됩니다.");
        }
    }

    void Update()
    {
        HandleAltitudeChange();
        HandleRotation();
        HandleMovement();
        HandleReconnaissance(); // 자율 이동 수행
        
        // 추적 동작 처리
        if (isTracking && trackingTarget != null)
        {
            HandleTracking();
        }
    }

    private void HandleAltitudeChange()
    {
        if (!isChangingAltitude) return;

        float currentAltitude = transform.position.y;
        float altitudeDifference = targetAltitude - currentAltitude;

        if (Mathf.Abs(altitudeDifference) > 0.1f)
        {
            Vector3 currentVelocity = rb.linearVelocity;
            currentVelocity.y = Mathf.Sign(altitudeDifference) * verticalSpeed;
            rb.linearVelocity = currentVelocity;
        }
        else
        {
            Vector3 pos = transform.position;
            pos.y = targetAltitude;
            transform.position = pos;

            Vector3 currentVelocity = rb.linearVelocity;
            currentVelocity.y = 0;
            rb.linearVelocity = currentVelocity;

            isChangingAltitude = false;
        }
    }

    private void HandleRotation()
    {
        if (!isRotating) return;

        Quaternion currentRotation = transform.rotation;
        transform.rotation = Quaternion.RotateTowards(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);

        if (Quaternion.Angle(currentRotation, targetRotation) < 0.1f)
        {
            isRotating = false;
        }
    }

    private void HandleMovement()
    {
        if (isHovering)
        {
            moveSpeed = 0f;
            rb.linearVelocity = Vector3.zero;
        }
        else if (isMoving)
        {
            Vector3 moveDirection = transform.TransformDirection(targetPosition);
            rb.linearVelocity = moveDirection * moveSpeed;
        }
        else if (isReturning)
        {
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

    private void HandleReconnaissance()
    {
        if (!isReconnaissance || lidar == null || lidar.scanResults.Count == 0)
            return;

        Vector3 center = transform.position;
        Vector3 dangerDirection = Vector3.zero;
        bool dangerDetected = false;

        foreach (var entry in lidar.scanResults)
        {
            Vector3 dir = entry.Key;
            float dist = entry.Value;

            // ✅ 디버그 선 추가 (안전 거리 기준 색상)
            Color rayColor = dist < lidar.safeDistance ? Color.red : Color.green;
            Debug.DrawRay(center, dir * dist, rayColor);

            if (dist < lidar.safeDistance)
            {
                dangerDetected = true;
                dangerDirection = dir;
                break; // 하나만 감지하면 바로
            }
        }

        if (dangerDetected)
        {
            // 🚨 위험 감지 시: 위험 방향의 반대 방향으로 회전
            Vector3 oppositeDirection = -dangerDirection;
            Quaternion targetRotation = Quaternion.LookRotation(oppositeDirection, Vector3.up);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f); // 빠르게 회전
            transform.position += transform.forward * moveSpeed * Time.deltaTime * 0.5f; // 반대 방향으로 천천히 이동
        }
        else
        {
            // 👍 위험이 없을 때: 가장 좋은 방향 찾아 이동
            Vector3 bestDir = Vector3.zero;
            float bestScore = -1;

            foreach (var entry in lidar.scanResults)
            {
                Vector3 dir = entry.Key;
                float dist = entry.Value;

                float angle = Vector3.Angle(transform.forward, dir);
                float score = dist - angle * 0.1f; // 거리 우선 + 각도 고려

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = dir;
                }
            }

            if (bestScore > 0)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(bestDir), Time.deltaTime * 2f);
                transform.position += transform.forward * moveSpeed * Time.deltaTime;
            }
        }
    }
    
    // 드론 추적 처리 메서드
    private void HandleTracking()
    {
        if (trackingTarget == null) return;
        
        // 추적 대상을 향한 방향 계산
        Vector3 directionToTarget = trackingTarget.position - transform.position;
        float distanceToTarget = directionToTarget.magnitude;
        
        // 대상과의 거리가 너무 가까우면 뒤로 물러남
        if (distanceToTarget < trackingDistance * 0.7f)
        {
            // 약간 뒤로 이동
            Vector3 backwardDirection = -directionToTarget.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, 
                                                  Quaternion.LookRotation(backwardDirection), 
                                                  Time.deltaTime * 2f);
            rb.linearVelocity = transform.forward * moveSpeed * 0.5f;
        }
        // 적정 거리면 유지
        else if (distanceToTarget < trackingDistance * 1.3f)
        {
            // 대상을 바라보되 제자리 유지
            transform.rotation = Quaternion.Slerp(transform.rotation, 
                                                  Quaternion.LookRotation(directionToTarget), 
                                                  Time.deltaTime * 3f);
            rb.linearVelocity = Vector3.zero;
        }
        // 대상과 거리가 멀면 접근
        else
        {
            // 추적 대상을 향해 이동
            transform.rotation = Quaternion.Slerp(transform.rotation, 
                                                  Quaternion.LookRotation(directionToTarget), 
                                                  Time.deltaTime * 3f);
            rb.linearVelocity = transform.forward * moveSpeed * trackingSpeedMultiplier;
        }
        
        // 고도 유지 (추적 대상과 비슷한 고도 유지)
        float targetY = trackingTarget.position.y;
        float currentY = transform.position.y;
        float yDiff = targetY - currentY;
        
        if (Mathf.Abs(yDiff) > 1.0f)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.y = Mathf.Sign(yDiff) * verticalSpeed * 0.7f;
            rb.linearVelocity = velocity;
        }
    }
    
    // 추적 시작 메서드
    public void StartTracking()
    {
        if (trackingTarget != null)
        {
            ResetAllStates();
            isTracking = true;
            moveSpeed = originalMoveSpeed * trackingSpeedMultiplier;
            // Debug.Log("추적 시작: " + trackingTarget.name);

            if (trackingLogger != null)
            {
            trackingLogger.SetTrackingActive(true);
            }
        }
        else
        {
            Debug.LogWarning("추적 대상이 없습니다.");
        }
    }
    
    // 추적 중지 메서드
    public void StopTracking()
    {
        isTracking = false;
        Debug.Log("추적 중지");

        if (trackingLogger != null)
        {
            trackingLogger.SetTrackingActive(false);
        }
    }

    public void OnCommand(DroneCommand command)
    {
        Debug.Log($"[DroneController] 명령 수신: {command.actionEnum}, 속도: {command.Speed}, 방향: {command.DirectionVector}");

        ResetAllStates();

        switch (command.actionEnum)
        {
            case DroneCommand.DroneAction.Move:
                isMoving = true;
                yolo.enabled = false;
                targetPosition = command.DirectionVector;
                moveSpeed = command.Speed > 0 ? command.Speed : 5f;

                targetAltitude = command.Altitude;
                isChangingAltitude = command.Altitude != 0;
                break;

            case DroneCommand.DroneAction.Hover:
                isHovering = true;
                yolo.enabled = true; // YOLO 활성화하여 드론 감지 가능하도록 수정
                moveSpeed = 0f;
                rb.linearVelocity = Vector3.zero;
                break;

            case DroneCommand.DroneAction.Altitude:
                targetAltitude = command.Altitude;
                yolo.enabled = false;
                isChangingAltitude = true;
                break;

            case DroneCommand.DroneAction.Rotate:
                isRotating = true;
                yolo.enabled = false;
                targetRotation = Quaternion.Euler(command.DirectionVector);
                break;

            case DroneCommand.DroneAction.Return:
                isReturning = true;
                yolo.enabled = false;
                moveSpeed = originalMoveSpeed;
                break;

            case DroneCommand.DroneAction.Reconnaissance:
                isReconnaissance = true;
                yolo.enabled = true;
                moveSpeed = command.Speed > 0 ? command.Speed : 2f;
                break;

            default:
                Debug.LogWarning($"알 수 없는 명령: {command.actionEnum}");
                break;
        }
    }

    private void ResetAllStates()
    {
        isHovering = false;
        isMoving = false;
        isRotating = false;
        isReturning = false;
        isReconnaissance = false;
        isTracking = false;

        rb.linearVelocity = Vector3.zero;
        moveSpeed = originalMoveSpeed;
    }
}