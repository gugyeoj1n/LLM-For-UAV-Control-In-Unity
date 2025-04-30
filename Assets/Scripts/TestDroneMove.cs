using UnityEngine;

public class TestDroneMove : MonoBehaviour
{
    public Vector3 targetPosition = new Vector3(0, 0, 5); // 이동 목표 지점
    public float moveSpeed = 2f;
    public float rotationSpeed = 90f; // 회전 속도 (도/초)

    private Vector3 startPosition;
    private bool movingToTarget = true;
    private bool isRotating = false;
    private Quaternion targetRotation;

    void Start()
    {
        startPosition = transform.position;
        SetNextRotation();
    }

    void Update()
    {
        if (isRotating)
        {
            RotateTowardsTarget();
        }
        else
        {
            Move();
        }
    }

    void Move()
    {
        Vector3 destination = movingToTarget ? targetPosition : startPosition;
        float step = moveSpeed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, destination, step);

        if (Vector3.Distance(transform.position, destination) < 0.001f)
        {
            isRotating = true;
            movingToTarget = !movingToTarget;
            SetNextRotation();
        }
    }

    void RotateTowardsTarget()
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        if (Quaternion.Angle(transform.rotation, targetRotation) < 0.5f)
        {
            transform.rotation = targetRotation;
            isRotating = false;
        }
    }

    void SetNextRotation()
    {
        // 목표 회전 방향: 현재 회전에서 Y축 기준으로 180도
        targetRotation = Quaternion.Euler(0, transform.eulerAngles.y + 180f, 0);
    }
}
