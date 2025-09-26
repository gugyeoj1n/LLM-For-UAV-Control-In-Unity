using UnityEngine;

public class SatelliteOrbitController : MonoBehaviour
{
    [Header("궤도 설정")]
    public float orbitRadius = 30f; // 궤도 반지름
    public float orbitSpeed = 10f;  // 궤도 속도 (도/초)
    public Vector3 earthPosition = Vector3.zero; // 지구 위치
    
    [Header("궤도 축")]
    public Vector3 orbitAxis = Vector3.up; // 궤도 회전축 (기본: Y축)
    public bool alwaysLookAtEarth = true; // 항상 지구를 바라보기
    public float lookAtEarthSpeed = 5f; // 지구 바라보기 회전 속도
    
    [Header("시작 위치")]
    public float startAngle = 0f; // 시작 각도 (도)
    public bool useCustomStartPosition = false; // 커스텀 시작 위치 사용
    public Vector3 customStartPosition = Vector3.zero; // 커스텀 시작 위치 (x, y, z)
    public bool useCustomAsOrbitCenter = true; // 커스텀 위치를 궤도 중심으로 사용
    
    [Header("궤도 정보 (읽기 전용)")]
    [SerializeField] private float currentAngle; // 현재 각도
    [SerializeField] private Vector3 currentOrbitPosition; // 현재 궤도상 위치
    
    [Header("레이저 시스템")]
    public bool enableLaser = true; // 레이저 활성화
    public float laserRange = 100f; // 레이저 사거리
    public float laserDamage = 10f; // 레이저 데미지
    public float laserCooldown = 0.1f; // 레이저 쿨다운 시간 (더 빠른 발사)
    public Transform laserOrigin; // 레이저 발사점 (비어있으면 위성 중심)
    public LineRenderer laserLineRenderer; // 레이저 시각화용
    public Color laserColor = Color.green; // 레이저 색상
    public float laserWidth = 0.05f; // 레이저 두께 (얇게)
    public LayerMask collisionLayers = -1; // 충돌 감지 레이어 마스크
    public bool showCollisionDebug = true; // 충돌 디버그 표시
    public bool useAdvancedCollision = true; // 고급 충돌 감지 사용
    public float collisionCheckRadius = 0.5f; // 충돌 감지 반지름
    
    [Header("레이저 타겟팅")]
    public Transform defaultTarget; // 기본 타겟 오브젝트 (Inspector에서 선택)
    public bool autoTargetEarth = true; // 자동으로 지구를 타겟팅
    public float earthTargetingRange = 50f; // 지구 타겟팅 범위
    public float targetSwitchCooldown = 3f; // 타겟 전환 쿨다운
    public float autoTargetDelay = 10f; // 자동 지구 타겟팅 지연 시간 (초)
    
    [Header("텀블링 시스템")]
    public bool enableTumbling = false; // 텀블링 기능 활성화 (Inspector에서 체크)
    public float tumblingTriggerTime = 5f; // 텀블링 시작 시간 (초)
    public float tumblingDuration = 3f; // 텀블링 지속 시간 (초)
    public float tumblingSpeed = 90f; // 텀블링 회전 속도 (도/초)
    public bool useRightDirectionLaser = true; // 오른쪽 방향 레이저 사용
    
    [Header("레이저 시뮬레이션")]
    public bool enableLaserSimulation = false; // 레이저 시뮬레이션 활성화
    public Transform lazerTarget; // Lazer 오브젝트 (Inspector에서 드래그)
    public float simulationStartTime = 5f; // 시뮬레이션 시작 시간 (초)
    public float angleDriftDuration = 5f; // 각도 틀어짐 지속 시간 (초)
    public float maxAngleDrift = 20f; // 최대 각도 틀어짐 (도)
    public bool stopLaserAtCollision = true; // 충돌 시 레이저 중단
    public AnimationCurve angleDriftCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 각도 틀어짐 곡선
    
    private float timeElapsed = 0f;
    private float lastLaserTime = 0f;
    private float lastTargetSwitchTime = 0f;
    private bool isTargetingEarth = false;
    private Vector3 currentLaserDirection;
    private float gameStartTime = 0f; // 게임 시작 시간
    private bool hasAutoTargeted = false; // 자동 타겟팅이 실행되었는지 여부
    
    // 텀블링 시스템 변수들
    private bool isTumbling = false; // 현재 텀블링 중인지
    private bool hasTumbled = false; // 텀블링이 완료되었는지
    private float tumblingStartTime = 0f; // 텀블링 시작 시간
    private Quaternion initialRotation; // 초기 회전값
    private Quaternion targetRotation; // 목표 회전값 (180도 회전 후)
    private Vector3 rightDirectionLaser = Vector3.right; // 오른쪽 방향 레이저
    
    // 레이저 시뮬레이션 변수들
    private bool isLaserSimulationActive = false; // 레이저 시뮬레이션 활성화 상태
    private bool hasLaserSimulationStarted = false; // 시뮬레이션 시작됨
    private bool isAngleDrifting = false; // 각도 틀어짐 중
    private float simulationStartTimeElapsed = 0f; // 시뮬레이션 시작 시간
    private float angleDriftStartTime = 0f; // 각도 틀어짐 시작 시간
    private Vector3 originalLaserDirection; // 원래 레이저 방향
    private Vector3 driftedLaserDirection; // 틀어진 레이저 방향

    void Start()
    {
        // 시작 위치 설정
        if (useCustomStartPosition)
        {
            if (useCustomAsOrbitCenter)
            {
                // 커스텀 시작 위치를 궤도 중심으로 설정
                earthPosition = customStartPosition;
                currentAngle = startAngle;
                UpdateOrbitPosition();
                Debug.Log($"커스텀 궤도 중심 설정: {customStartPosition}");
            }
            else
            {
                // 커스텀 시작 위치에 고정 (궤도 회전 없음)
                transform.position = customStartPosition;
                currentAngle = startAngle;
                Debug.Log($"커스텀 고정 위치 설정: {customStartPosition}");
            }
        }
        else
        {
            // 기본 궤도 위치 사용
            currentAngle = startAngle;
            UpdateOrbitPosition();
        }
        
        // 게임 시작 시간 기록
        gameStartTime = Time.time;
        
        // 레이저 시스템 초기화
        InitializeLaserSystem();
        
        // 텀블링 시스템 초기화
        if (enableTumbling)
        {
            InitializeTumblingSystem();
        }
        
        // 레이저 시뮬레이션 초기화
        if (enableLaserSimulation)
        {
            InitializeLaserSimulation();
        }
        
        Debug.Log($"위성 궤도 시작 - 반지름: {orbitRadius}, 속도: {orbitSpeed}도/초");
        Debug.Log($"자동 지구 타겟팅 예정 시간: {autoTargetDelay}초 후");
        if (enableTumbling)
        {
            Debug.Log($"텀블링 시작 예정 시간: {tumblingTriggerTime}초 후");
        }
        if (enableLaserSimulation)
        {
            Debug.Log($"레이저 시뮬레이션 시작 예정 시간: {simulationStartTime}초 후");
        }
    }

    void Update()
    {
        // 시간 누적
        timeElapsed += Time.deltaTime;
        
        // 각도 업데이트 (시계방향으로 회전)
        currentAngle = startAngle + (orbitSpeed * timeElapsed);
        
        // 360도를 넘어가면 0으로 리셋
        if (currentAngle >= 360f)
        {
            currentAngle -= 360f;
            timeElapsed = 0f;
        }
        
        // 궤도 위치 업데이트
        if (!useCustomStartPosition || useCustomAsOrbitCenter)
        {
            UpdateOrbitPosition();
        }
        
        // 위성을 지구 방향으로 향하게 회전 (선택사항)
        LookAtEarth();
        
        // 레이저 시스템 업데이트
        if (enableLaser)
        {
            UpdateLaserSystem();
        }
        
        // 텀블링 시스템 업데이트
        if (enableTumbling)
        {
            UpdateTumblingSystem();
        }
        
        // 레이저 시뮬레이션 업데이트
        if (enableLaserSimulation)
        {
            UpdateLaserSimulation();
        }
    }

    void UpdateOrbitPosition()
    {
        // 라디안으로 변환
        float angleInRadians = currentAngle * Mathf.Deg2Rad;
        
        // 원형 궤도 계산 (XZ 평면에서)
        float x = earthPosition.x + orbitRadius * Mathf.Cos(angleInRadians);
        float z = earthPosition.z + orbitRadius * Mathf.Sin(angleInRadians);
        float y = transform.position.y; // Y축은 그대로 유지
        
        // 궤도 축이 Y축이 아닌 경우를 대비한 확장 가능한 코드
        if (orbitAxis != Vector3.up)
        {
            // 커스텀 축 주위로 회전하는 경우
            Vector3 localPosition = new Vector3(orbitRadius * Mathf.Cos(angleInRadians), 0, orbitRadius * Mathf.Sin(angleInRadians));
            Quaternion axisRotation = Quaternion.FromToRotation(Vector3.up, orbitAxis.normalized);
            Vector3 rotatedPosition = axisRotation * localPosition;
            currentOrbitPosition = earthPosition + rotatedPosition;
        }
        else
        {
            // 기본 Y축 궤도
            currentOrbitPosition = new Vector3(x, y, z);
        }
        
        // 위성 위치 업데이트
        transform.position = currentOrbitPosition;
    }

    void LookAtEarth()
    {
        // 항상 지구를 바라보기 옵션이 활성화된 경우에만 실행
        if (!alwaysLookAtEarth) return;
        
        // 위성이 항상 지구를 바라보도록 회전
        Vector3 directionToEarth = (earthPosition - transform.position).normalized;
        
        // 방향 벡터가 유효한지 확인
        if (directionToEarth.magnitude > 0.001f)
        {
            // 부드러운 회전을 위해 Slerp 사용
            Quaternion targetRotation = Quaternion.LookRotation(directionToEarth);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookAtEarthSpeed);
        }
    }

    void OnDrawGizmos()
    {
        // Scene 뷰에서 궤도를 시각화
        DrawOrbitGizmo();
    }

    void DrawOrbitGizmo()
    {
        // 궤도 원 그리기
        Gizmos.color = Color.cyan;
        
        Vector3 previousPoint = Vector3.zero;
        int segments = 64; // 원의 세그먼트 수
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * 360f * Mathf.Deg2Rad;
            Vector3 point;
            
            if (orbitAxis == Vector3.up)
            {
                // Y축 궤도
                point = earthPosition + new Vector3(
                    orbitRadius * Mathf.Cos(angle),
                    0,
                    orbitRadius * Mathf.Sin(angle)
                );
            }
            else
            {
                // 커스텀 축 궤도
                Vector3 localPoint = new Vector3(orbitRadius * Mathf.Cos(angle), 0, orbitRadius * Mathf.Sin(angle));
                Quaternion axisRotation = Quaternion.FromToRotation(Vector3.up, orbitAxis.normalized);
                point = earthPosition + axisRotation * localPoint;
            }
            
            if (i > 0)
            {
                Gizmos.DrawLine(previousPoint, point);
            }
            previousPoint = point;
        }
        
        // 지구 위치 표시
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(earthPosition, 1f);
        
        // 현재 위성 위치 표시
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, 0.5f);
            
            // 지구와 위성을 잇는 선
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(earthPosition, transform.position);
        }
    }

    // 런타임에서 궤도 설정을 변경할 수 있는 메서드들
    public void SetOrbitRadius(float newRadius)
    {
        orbitRadius = newRadius;
        UpdateOrbitPosition();
    }

    public void SetOrbitSpeed(float newSpeed)
    {
        orbitSpeed = newSpeed;
    }

    public void SetEarthPosition(Vector3 newEarthPosition)
    {
        earthPosition = newEarthPosition;
        UpdateOrbitPosition();
    }

    // 궤도 정보 가져오기
    public float GetCurrentAngle()
    {
        return currentAngle;
    }

    public float GetOrbitProgress()
    {
        return currentAngle / 360f;
    }

    public Vector3 GetOrbitCenter()
    {
        return earthPosition;
    }

    // 레이저 시스템 초기화
    void InitializeLaserSystem()
    {
        // 텀블링이 활성화된 경우 오른쪽 방향 레이저 사용
        if (enableTumbling && useRightDirectionLaser)
        {
            currentLaserDirection = rightDirectionLaser;
            Debug.Log("텀블링 모드: 오른쪽 방향 레이저 설정");
        }
        // 기본 타겟 설정
        else if (defaultTarget != null)
        {
            currentLaserDirection = (defaultTarget.position - transform.position).normalized;
            Debug.Log($"기본 타겟 설정: {defaultTarget.name}");
        }
        else
        {
            currentLaserDirection = Vector3.forward;
            Debug.Log("기본 타겟이 설정되지 않음. 전방 방향으로 설정");
        }
        
        // LineRenderer가 없으면 자동으로 생성
        if (laserLineRenderer == null)
        {
            GameObject laserObject = new GameObject("Laser");
            laserObject.transform.SetParent(transform);
            laserLineRenderer = laserObject.AddComponent<LineRenderer>();
        }
        
        // LineRenderer 설정
        laserLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        laserLineRenderer.startColor = laserColor;
        laserLineRenderer.endColor = laserColor;
        laserLineRenderer.startWidth = laserWidth;
        laserLineRenderer.endWidth = laserWidth;
        laserLineRenderer.positionCount = 2;
        laserLineRenderer.enabled = true; // 시작부터 레이저 활성화
    }

    // 레이저 시스템 업데이트
    void UpdateLaserSystem()
    {
        // 10초 후 자동 지구 타겟팅 체크
        if (autoTargetEarth && !hasAutoTargeted && Time.time - gameStartTime >= autoTargetDelay)
        {
            ForceTargetEarth();
            hasAutoTargeted = true;
            Debug.Log($"{autoTargetDelay}초 경과! 자동으로 지구를 타겟팅합니다.");
        }
        
        // 기본 타겟이 움직이는 경우 방향 업데이트
        if (!isTargetingEarth && defaultTarget != null)
        {
            currentLaserDirection = (defaultTarget.position - transform.position).normalized;
        }
        
        // 연속적인 레이저 업데이트
        UpdateContinuousLaser();
        
        // 레이저 발사 (자동)
        if (Time.time - lastLaserTime > laserCooldown)
        {
            FireLaser();
        }
    }

    // 지구 타겟팅 체크
    void CheckForEarthTargeting()
    {
        float distanceToEarth = Vector3.Distance(transform.position, earthPosition);
        
        // 지구가 사거리 내에 있고, 현재 지구를 타겟팅하지 않는 경우
        if (distanceToEarth <= earthTargetingRange && !isTargetingEarth)
        {
            SwitchToEarthTarget();
        }
        // 지구가 사거리 밖에 있고, 현재 지구를 타겟팅하는 경우
        else if (distanceToEarth > earthTargetingRange && isTargetingEarth)
        {
            SwitchToDefaultTarget();
        }
    }

    // 지구 타겟으로 전환
    void SwitchToEarthTarget()
    {
        isTargetingEarth = true;
        currentLaserDirection = (earthPosition - transform.position).normalized;
        lastTargetSwitchTime = Time.time;
        Debug.Log("레이저 타겟을 지구로 전환!");
    }

    // 기본 타겟으로 전환
    void SwitchToDefaultTarget()
    {
        isTargetingEarth = false;
        
        // 기본 타겟이 있으면 그 방향으로, 없으면 전방으로
        if (defaultTarget != null)
        {
            currentLaserDirection = (defaultTarget.position - transform.position).normalized;
            Debug.Log($"레이저 타겟을 기본 타겟 {defaultTarget.name}으로 전환!");
        }
        else
        {
            currentLaserDirection = Vector3.forward;
            Debug.Log("레이저 타겟을 전방 방향으로 전환!");
        }
        
        lastTargetSwitchTime = Time.time;
    }

    // 레이저 발사
    void FireLaser()
    {
        Vector3 laserStart = laserOrigin != null ? laserOrigin.position : transform.position;
        Vector3 laserEnd = laserStart + currentLaserDirection * laserRange;
        
        // 레이캐스트로 실제 충돌 감지
        RaycastHit hit;
        if (Physics.Raycast(laserStart, currentLaserDirection, out hit, laserRange))
        {
            laserEnd = hit.point;
            
            // 충돌한 오브젝트에 데미지 적용
            if (hit.collider.CompareTag("Earth"))
            {
                Debug.Log($"지구에 레이저 명중! 데미지: {laserDamage}");
            }
            else
            {
                Debug.Log($"레이저가 {hit.collider.name}에 명중! 데미지: {laserDamage}");
            }
        }
        
        // 레이저 시각화는 UpdateContinuousLaser()에서 처리
        // ShowLaser(laserStart, laserEnd);
        
        lastLaserTime = Time.time;
    }

    // 레이저 시각화
    void ShowLaser(Vector3 start, Vector3 end)
    {
        if (laserLineRenderer != null)
        {
            laserLineRenderer.enabled = true;
            laserLineRenderer.SetPosition(0, start);
            laserLineRenderer.SetPosition(1, end);
            
            // 연속적인 레이저를 위해 숨기지 않음
            // StartCoroutine(HideLaserAfterDelay(0.5f));
        }
    }

    // 연속적인 레이저 업데이트
    void UpdateContinuousLaser()
    {
        if (laserLineRenderer != null && laserLineRenderer.enabled)
        {
            Vector3 laserStart = laserOrigin != null ? laserOrigin.position : transform.position;
            Vector3 laserEnd = laserStart + currentLaserDirection * laserRange;
            
            // 더 강력한 충돌 감지
            RaycastHit hit;
            bool hasHit = false;
            
            // 여러 레이캐스트로 충돌 감지 강화
            if (Physics.Raycast(laserStart, currentLaserDirection, out hit, laserRange, collisionLayers, QueryTriggerInteraction.Collide))
            {
                // 충돌한 지점에서 레이저 중단
                laserEnd = hit.point;
                hasHit = true;
                
                if (showCollisionDebug)
                {
                    Debug.Log($"레이저 충돌 감지: {hit.collider.name} at {hit.point}, 거리: {hit.distance:F2}");
                }
            }
            else if (useAdvancedCollision)
            {
                // 추가 충돌 감지: 더 짧은 거리로 여러 번 체크
                float checkDistance = laserRange;
                int checkCount = 10; // 더 많은 체크 포인트
                
                for (int i = 1; i <= checkCount; i++)
                {
                    float currentDistance = (laserRange / checkCount) * i;
                    Vector3 checkPoint = laserStart + currentLaserDirection * currentDistance;
                    
                    // 구체 충돌 감지
                    Collider[] colliders = Physics.OverlapSphere(checkPoint, collisionCheckRadius, collisionLayers);
                    if (colliders.Length > 0)
                    {
                        laserEnd = checkPoint;
                        hasHit = true;
                        
                        if (showCollisionDebug)
                        {
                            Debug.Log($"레이저 구체 충돌 감지: {colliders[0].name} at {checkPoint}");
                        }
                        break;
                    }
                }
            }
            
            // 색상 설정
            if (hasHit)
            {
                laserLineRenderer.startColor = Color.red;
                laserLineRenderer.endColor = Color.red;
            }
            else
            {
                laserLineRenderer.startColor = laserColor;
                laserLineRenderer.endColor = laserColor;
            }
            
            // 레이저 위치 지속적으로 업데이트
            laserLineRenderer.SetPosition(0, laserStart);
            laserLineRenderer.SetPosition(1, laserEnd);
        }
    }

    // 레이저 숨기기 (코루틴) - 사용하지 않음
    System.Collections.IEnumerator HideLaserAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (laserLineRenderer != null)
        {
            laserLineRenderer.enabled = false;
        }
    }

    // 수동으로 레이저 발사
    public void FireLaserManually()
    {
        if (enableLaser && Time.time - lastLaserTime > laserCooldown)
        {
            FireLaser();
        }
    }

    // 레이저 방향 수동 설정
    public void SetLaserDirection(Vector3 direction)
    {
        currentLaserDirection = direction.normalized;
        isTargetingEarth = false;
        hasAutoTargeted = false; // 수동 설정 시 자동 타겟팅 리셋
    }

    // 지구 타겟팅 강제 전환
    public void ForceTargetEarth()
    {
        SwitchToEarthTarget();
    }

    // 기본 타겟팅으로 강제 전환
    public void ForceTargetDefault()
    {
        SwitchToDefaultTarget();
    }

    // 기본 타겟 설정
    public void SetDefaultTarget(Transform target)
    {
        defaultTarget = target;
        if (!isTargetingEarth)
        {
            if (defaultTarget != null)
            {
                currentLaserDirection = (defaultTarget.position - transform.position).normalized;
                Debug.Log($"기본 타겟 변경: {defaultTarget.name}");
            }
        }
    }

    // 자동 타겟팅 리셋 (다시 10초 후에 지구 타겟팅)
    public void ResetAutoTargeting()
    {
        hasAutoTargeted = false;
        gameStartTime = Time.time;
        Debug.Log("자동 타겟팅 리셋! 10초 후 다시 지구를 타겟팅합니다.");
    }

    // 현재 타겟 정보 가져오기
    public string GetCurrentTargetInfo()
    {
        if (isTargetingEarth)
        {
            return "현재 타겟: 지구";
        }
        else if (defaultTarget != null)
        {
            return $"현재 타겟: {defaultTarget.name}";
        }
        else
        {
            return "현재 타겟: 없음 (전방 방향)";
        }
    }

    // 남은 자동 타겟팅 시간 가져오기
    public float GetRemainingAutoTargetTime()
    {
        if (hasAutoTargeted)
        {
            return 0f;
        }
        float elapsed = Time.time - gameStartTime;
        return Mathf.Max(0f, autoTargetDelay - elapsed);
    }

    // 텀블링 시스템 초기화
    void InitializeTumblingSystem()
    {
        // 초기 회전값 저장
        initialRotation = transform.rotation;
        
        // 180도 회전 후 목표 회전값 계산
        targetRotation = initialRotation * Quaternion.Euler(0, 180, 0);
        
        Debug.Log("텀블링 시스템 초기화 완료");
    }

    // 텀블링 시스템 업데이트
    void UpdateTumblingSystem()
    {
        float elapsedTime = Time.time - gameStartTime;
        
        // 텀블링 트리거 시간 체크
        if (!hasTumbled && elapsedTime >= tumblingTriggerTime)
        {
            StartTumbling();
        }
        
        // 텀블링 중인 경우 회전 처리
        if (isTumbling)
        {
            ProcessTumbling();
        }
    }

    // 텀블링 시작
    void StartTumbling()
    {
        isTumbling = true;
        hasTumbled = true;
        tumblingStartTime = Time.time;
        
        Debug.Log("텀블링 시작! 180도 회전하여 지구를 바라봅니다.");
    }

    // 텀블링 처리
    void ProcessTumbling()
    {
        float tumblingElapsed = Time.time - tumblingStartTime;
        float tumblingProgress = tumblingElapsed / tumblingDuration;
        
        // 텀블링 진행률에 따라 회전
        if (tumblingProgress < 1f)
        {
            // Slerp를 사용하여 부드러운 회전
            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, tumblingProgress);
        }
        else
        {
            // 텀블링 완료
            isTumbling = false;
            transform.rotation = targetRotation;
            
            // 지구 타겟팅으로 전환
            ForceTargetEarth();
            
            Debug.Log("텀블링 완료! 지구를 타겟팅합니다.");
        }
    }

    // 텀블링 상태 확인
    public bool IsTumbling()
    {
        return isTumbling;
    }

    // 텀블링 진행률 가져오기 (0~1)
    public float GetTumblingProgress()
    {
        if (!isTumbling)
        {
            return hasTumbled ? 1f : 0f;
        }
        
        float tumblingElapsed = Time.time - tumblingStartTime;
        return Mathf.Clamp01(tumblingElapsed / tumblingDuration);
    }

    // 텀블링 강제 시작
    public void ForceStartTumbling()
    {
        if (!hasTumbled)
        {
            StartTumbling();
        }
    }

    // 텀블링 리셋
    public void ResetTumbling()
    {
        isTumbling = false;
        hasTumbled = false;
        transform.rotation = initialRotation;
        Debug.Log("텀블링 리셋 완료");
    }

    // 커스텀 시작 위치 설정
    public void SetCustomStartPosition(Vector3 position, bool asOrbitCenter = true)
    {
        customStartPosition = position;
        useCustomStartPosition = true;
        useCustomAsOrbitCenter = asOrbitCenter;
        
        if (asOrbitCenter)
        {
            earthPosition = position;
            UpdateOrbitPosition();
            Debug.Log($"커스텀 궤도 중심 설정: {position}");
        }
        else
        {
            transform.position = position;
            Debug.Log($"커스텀 고정 위치 설정: {position}");
        }
    }

    // 커스텀 시작 위치 비활성화 (궤도 모드로 전환)
    public void DisableCustomStartPosition()
    {
        useCustomStartPosition = false;
        currentAngle = startAngle;
        UpdateOrbitPosition();
        Debug.Log("커스텀 시작 위치 비활성화, 궤도 모드로 전환");
    }

    // 현재 위치 가져오기
    public Vector3 GetCurrentPosition()
    {
        return transform.position;
    }

    // 시작 위치 모드 확인
    public bool IsUsingCustomStartPosition()
    {
        return useCustomStartPosition;
    }

    // 레이저 시뮬레이션 초기화
    void InitializeLaserSimulation()
    {
        if (lazerTarget != null)
        {
            // 원래 레이저 방향을 Lazer 오브젝트로 설정
            originalLaserDirection = (lazerTarget.position - transform.position).normalized;
            currentLaserDirection = originalLaserDirection;
            isLaserSimulationActive = true;
            Debug.Log($"레이저 시뮬레이션 초기화: {lazerTarget.name} 타겟팅");
            Debug.Log($"원래 레이저 방향: {originalLaserDirection}");
            Debug.Log($"현재 레이저 방향: {currentLaserDirection}");
        }
        else
        {
            Debug.LogWarning("Lazer 타겟이 설정되지 않았습니다!");
        }
    }

    // 레이저 시뮬레이션 업데이트
    void UpdateLaserSimulation()
    {
        if (!isLaserSimulationActive || lazerTarget == null) 
        {
            return;
        }

        float elapsedTime = Time.time - gameStartTime;

        // 시뮬레이션 시작 시간 체크
        if (!hasLaserSimulationStarted && elapsedTime >= simulationStartTime)
        {
            StartLaserSimulation();
        }

        // 각도 틀어짐 처리
        if (isAngleDrifting)
        {
            ProcessAngleDrift();
        }
    }

    // 레이저 시뮬레이션 시작
    void StartLaserSimulation()
    {
        hasLaserSimulationStarted = true;
        isAngleDrifting = true;
        angleDriftStartTime = Time.time;
        
        Debug.Log("레이저 시뮬레이션 시작! 각도 틀어짐 시작");
    }

    // 각도 틀어짐 처리
    void ProcessAngleDrift()
    {
        float driftElapsed = Time.time - angleDriftStartTime;
        float driftProgress = driftElapsed / angleDriftDuration;

        if (driftProgress < 1f)
        {
            // AnimationCurve를 사용한 부드러운 각도 틀어짐
            float curveValue = angleDriftCurve.Evaluate(driftProgress);
            float currentDriftAngle = maxAngleDrift * curveValue;
            
            // 아래쪽으로 틀어짐 (X축 회전)
            Vector3 driftDirection = Quaternion.Euler(currentDriftAngle, 0, 0) * originalLaserDirection;
            currentLaserDirection = driftDirection;
            
            // 1초마다만 로그 출력 (과도한 로그 방지)
            if (Mathf.FloorToInt(driftElapsed) != Mathf.FloorToInt(driftElapsed - Time.deltaTime))
            {
                Debug.Log($"각도 틀어짐 진행: {currentDriftAngle:F1}도 (진행률: {driftProgress:F2})");
            }
        }
        else
        {
            // 최대 각도 틀어짐 완료
            Vector3 finalDriftDirection = Quaternion.Euler(maxAngleDrift, 0, 0) * originalLaserDirection;
            currentLaserDirection = finalDriftDirection;
            isAngleDrifting = false;
            
            Debug.Log($"각도 틀어짐 완료: {maxAngleDrift}도 틀어짐");
        }
    }

    // 레이저 시뮬레이션 리셋
    public void ResetLaserSimulation()
    {
        isLaserSimulationActive = false;
        hasLaserSimulationStarted = false;
        isAngleDrifting = false;
        currentLaserDirection = originalLaserDirection;
        Debug.Log("레이저 시뮬레이션 리셋");
    }

    // 레이저 시뮬레이션 강제 시작
    public void ForceStartLaserSimulation()
    {
        if (lazerTarget != null)
        {
            StartLaserSimulation();
        }
    }

    // 현재 레이저 시뮬레이션 상태 확인
    public bool IsLaserSimulationActive()
    {
        return isLaserSimulationActive;
    }

    // 각도 틀어짐 진행률 가져오기 (0~1)
    public float GetAngleDriftProgress()
    {
        if (!isAngleDrifting)
        {
            return 1f;
        }
        
        float driftElapsed = Time.time - angleDriftStartTime;
        return Mathf.Clamp01(driftElapsed / angleDriftDuration);
    }

    // 지구 바라보기 강제 실행
    public void ForceLookAtEarth()
    {
        Vector3 directionToEarth = (earthPosition - transform.position).normalized;
        if (directionToEarth.magnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(directionToEarth);
            Debug.Log("지구 바라보기 강제 실행");
        }
    }

    // 지구 바라보기 설정 변경
    public void SetLookAtEarth(bool enable, float speed = 5f)
    {
        alwaysLookAtEarth = enable;
        lookAtEarthSpeed = speed;
        Debug.Log($"지구 바라보기 설정: {enable}, 속도: {speed}");
    }

    // 현재 지구 바라보기 상태 확인
    public bool IsLookingAtEarth()
    {
        return alwaysLookAtEarth;
    }
}