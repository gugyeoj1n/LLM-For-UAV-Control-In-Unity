using UnityEngine;

public class SatelliteOrbitController : MonoBehaviour
{
    [Header("궤도 설정")]
    public float orbitRadius = 30f; // 궤도 반지름
    public float orbitSpeed = 10f;  // 궤도 속도 (도/초)
    public Vector3 earthPosition = Vector3.zero; // 지구 위치
    
    [Header("궤도 축")]
    public Vector3 orbitAxis = Vector3.up; // 궤도 회전축 (기본: Y축)
    
    [Header("시작 위치")]
    public float startAngle = 0f; // 시작 각도 (도)
    
    [Header("궤도 정보 (읽기 전용)")]
    [SerializeField] private float currentAngle; // 현재 각도
    [SerializeField] private Vector3 currentOrbitPosition; // 현재 궤도상 위치
    
    private float timeElapsed = 0f;

    void Start()
    {
        // 시작 위치 설정
        currentAngle = startAngle;
        UpdateOrbitPosition();
        
        Debug.Log($"위성 궤도 시작 - 반지름: {orbitRadius}, 속도: {orbitSpeed}도/초");
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
        UpdateOrbitPosition();
        
        // 위성을 지구 방향으로 향하게 회전 (선택사항)
        LookAtEarth();
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
        // 위성이 항상 지구를 바라보도록 회전
        Vector3 directionToEarth = (earthPosition - transform.position).normalized;
        if (directionToEarth != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(directionToEarth);
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
}