using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float verticalSpeed = 3f;
    public float rotationSpeed = 100f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // 드론은 자체 부력으로 떠있다고 가정
    }

    void Update()
    {
        // 수평 이동
        float horizontal = Input.GetAxis("Horizontal"); // A, D
        float vertical = Input.GetAxis("Vertical");     // W, S

        Vector3 moveDirection = (transform.forward * vertical + transform.right * horizontal) * moveSpeed;

        // 고도 조절
        float altitude = 0f;
        if (Input.GetKey(KeyCode.Space))
        {
            altitude = verticalSpeed; // 상승
        }
        else if (Input.GetKey(KeyCode.LeftShift))
        {
            altitude = -verticalSpeed; // 하강
        }

        moveDirection.y = altitude;

        rb.linearVelocity = moveDirection;

        // 방향 회전 (선택사항)
        if (Input.GetKey(KeyCode.Q))
        {
            transform.Rotate(Vector3.up, -rotationSpeed * Time.deltaTime);
        }
        else if (Input.GetKey(KeyCode.E))
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
}
