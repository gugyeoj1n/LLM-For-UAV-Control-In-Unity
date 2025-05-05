using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class DroneController : MonoBehaviour
{
    [Header("Speed Settings")]
    public float moveSpeed = 5f;
    public float verticalSpeed = 3f;
    public float rotationSpeed = 100f;

    [Header("Tracking Settings")]
    public Transform trackingTarget;
    public float trackingDistance = 5f;
    public float trackingSpeedMultiplier = 0.8f;

    [Header("Internal State (ReadOnly)")]
    public bool isHovering = false;
    public bool isMoving = false;
    public bool isChangingAltitude = false;
    public bool isRotating = false;
    public bool isReturning = false;
    public bool isReconnaissance = false;
    public bool isTracking = false;

    [Header("Positions")]
    public Vector3 returnPosition = new Vector3(0, 1, 0);
    public Vector3 targetPosition;
    public Quaternion targetRotation;
    public float targetAltitude;

    [Header("References")]
    public LidarScanner lidar;
    public RunYOLO yolo;
    public AutomaticDroneTrackingLogger trackingLogger;

    private Rigidbody rb;
    private float originalMoveSpeed;

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
        HandleMovement();
        HandleAltitudeChange();
        HandleRotation();
        HandleReconnaissance();
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

        Debug.Log($"{gameObject.name} [Altitude] 현재 고도: {currentAltitude}");
        Debug.Log($"{gameObject.name} [Altitude] 목표 고도: {targetAltitude}");
        Debug.Log($"{gameObject.name} [Altitude] 고도 차이: {altitudeDifference}");

        if (Mathf.Abs(altitudeDifference) > 0.1f)
        {
            Debug.Log($"{gameObject.name} [Altitude] 이동 중 - 차이가 큼, 상승 또는 하강 시작");

            Vector3 currentVelocity = rb.linearVelocity;
            Debug.Log($"{gameObject.name} [Altitude] 현재 속도(before): {currentVelocity}");

            currentVelocity.y = Mathf.Sign(altitudeDifference) * verticalSpeed;

            Debug.Log($"{gameObject.name} [Altitude] Mathf.Sign 결과: {Mathf.Sign(altitudeDifference)}");
            Debug.Log($"{gameObject.name} [Altitude] 새로운 Y속도: {currentVelocity.y}");

            rb.linearVelocity = currentVelocity;

            Debug.Log($"{gameObject.name} [Altitude] 속도 적용 완료: {rb.linearVelocity}");
        }
        else
        {
            Debug.Log($"{gameObject.name} [Altitude] 목표 고도 도달 - 고도 설정 및 속도 정지");

            Vector3 pos = transform.position;
            pos.y = targetAltitude;
            transform.position = pos;

            Vector3 currentVelocity = rb.linearVelocity;
            Debug.Log($"{gameObject.name} [Altitude] 기존 속도(before): {currentVelocity}");

            currentVelocity.y = 0;
            rb.linearVelocity = currentVelocity;

            Debug.Log($"{gameObject.name} [Altitude] 속도 after Y 제거: {rb.linearVelocity}");
            Debug.Log($"{gameObject.name} [Altitude] 위치 보정 완료: {transform.position}");

            isChangingAltitude = false;
            Debug.Log($"{gameObject.name} [Altitude] isChangingAltitude → false");
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
            Color rayColor = dist < lidar.safeDistance ? Color.red : Color.green;
            Debug.DrawRay(center, dir * dist, rayColor);

            if (dist < lidar.safeDistance)
            {
                dangerDetected = true;
                dangerDirection = dir;
                break;
            }
        }

        if (dangerDetected)
        {
            Vector3 oppositeDirection = -dangerDirection;
            Quaternion targetRotation = Quaternion.LookRotation(oppositeDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            transform.position += transform.forward * moveSpeed * Time.deltaTime * 0.5f;
        }
        else
        {
            Vector3 bestDir = Vector3.zero;
            float bestScore = -1;

            foreach (var entry in lidar.scanResults)
            {
                Vector3 dir = entry.Key;
                float dist = entry.Value;
                float angle = Vector3.Angle(transform.forward, dir);
                float score = dist - angle * 0.1f;

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

    private void HandleTracking()
    {
        if (trackingTarget == null) return;

        Vector3 directionToTarget = trackingTarget.position - transform.position;
        float distanceToTarget = directionToTarget.magnitude;
        
        // 디버깅 로그 추가
        UIManager.instance.SetDistanceText( distanceToTarget );
        Debug.Log($"[Tracking] 대상과의 거리: {distanceToTarget}m, 설정된 추적 거리: {trackingDistance}m");
        Debug.Log($"[Tracking] 최소 허용 거리: {trackingDistance * 0.7f}m, 최대 허용 거리: {trackingDistance * 1.3f}m");

        if (distanceToTarget < trackingDistance * 0.7f)
        {
            Debug.Log("[Tracking] 너무 가까움 - 뒤로 물러남");
            Vector3 backwardDirection = -directionToTarget.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(backwardDirection), Time.deltaTime * 2f);
            rb.linearVelocity = transform.forward * moveSpeed * 0.5f;
        }
        else if (distanceToTarget < trackingDistance * 1.3f)
        {
            Debug.Log("[Tracking] 적절한 거리 - 제자리에서 대상 주시");
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(directionToTarget), Time.deltaTime * 3f);
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            Debug.Log("[Tracking] 너무 멈 - 대상을 향해 접근");
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(directionToTarget), Time.deltaTime * 3f);
            rb.linearVelocity = transform.forward * moveSpeed * trackingSpeedMultiplier;
        }

        float targetY = trackingTarget.position.y;
        float currentY = transform.position.y;
        float yDiff = targetY - currentY;

        if (Mathf.Abs(yDiff) > 1.0f)
        {
            Debug.Log($"[Tracking] Y축 조정: 차이 = {yDiff}m");
            Vector3 velocity = rb.linearVelocity;
            velocity.y = Mathf.Sign(yDiff) * verticalSpeed * 0.7f;
            rb.linearVelocity = velocity;
        }
    }


    public void StartTracking()
    {
        if (trackingTarget != null)
        {
            Debug.Log($"[DroneController] 추적 시작: 대상과의 거리 = {Vector3.Distance(transform.position, trackingTarget.position)}m, 설정된 추적 거리 = {trackingDistance}m");
            
            // 다른 모드는 모두 비활성화
            isMoving = false;
            isHovering = false;
            isReturning = false;
            isReconnaissance = false;
            
            // 추적 모드 활성화
            isTracking = true;
            moveSpeed = originalMoveSpeed * trackingSpeedMultiplier;
            
            UIManager.instance.distanceText.gameObject.SetActive( true );
            
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
        switch (command.actionEnum)
        {
            case DroneCommand.DroneAction.Move:
                isHovering = false;
                isReturning = false;
                isReconnaissance = false;
                isMoving = true;
                yolo.enabled = false;
                targetPosition = command.DirectionVector;
                moveSpeed = command.Speed > 0 ? command.Speed : originalMoveSpeed;
                break;

            case DroneCommand.DroneAction.Hover:
                isMoving = false;
                isReturning = false;
                isReconnaissance = false;
                isHovering = true;
                yolo.enabled = true;
                moveSpeed = 0f;
                rb.linearVelocity = Vector3.zero;
                break;

            case DroneCommand.DroneAction.Altitude:
                isHovering = false;
                isChangingAltitude = true;
                yolo.enabled = false;
                targetAltitude = command.Altitude;
                break;

            case DroneCommand.DroneAction.Rotate:
                isHovering = false;
                isRotating = true;
                yolo.enabled = false;
                targetRotation = Quaternion.Euler(command.DirectionVector);
                break;

            case DroneCommand.DroneAction.Return:
                isMoving = false;
                isHovering = false;
                isReconnaissance = false;
                isReturning = true;
                yolo.enabled = false;
                moveSpeed = originalMoveSpeed;
                break;

            case DroneCommand.DroneAction.Reconnaissance:
                isMoving = false;
                isHovering = false;
                isReturning = false;
                isReconnaissance = true;
                yolo.enabled = true;
                moveSpeed = command.Speed > 0 ? command.Speed : 2f;
                break;
                
            case DroneCommand.DroneAction.Tracking:
                if (command.TrackingDistance > 0)
                    trackingDistance = command.TrackingDistance;
                StartTracking();
                break;

            default:
                Debug.LogWarning($"알 수 없는 명령: {command.actionEnum}");
                break;
        }
    }
}