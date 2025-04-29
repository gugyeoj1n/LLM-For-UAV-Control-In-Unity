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
    private bool isChangingAltitude = false;
    private bool isRotating = false;
    private bool isReturning = false;

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
        HandleAltitudeChange();
        HandleRotation();
        HandleMovement();
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

    public void OnCommand(DroneCommand command)
    {
        Debug.Log($"[DroneController] 명령 수신: {command.actionEnum}, 속도: {command.Speed}, 방향: {command.DirectionVector}");

        ResetAllStates();

        switch (command.actionEnum)
        {
            case DroneCommand.DroneAction.Move:
                isMoving = true;
                targetPosition = command.DirectionVector;
                moveSpeed = command.Speed > 0 ? command.Speed : 5f;

                targetAltitude = command.Altitude;
                isChangingAltitude = command.Altitude != 0;
                break;

            case DroneCommand.DroneAction.Hover:
                isHovering = true;
                moveSpeed = 0f;
                rb.linearVelocity = Vector3.zero;
                break;

            case DroneCommand.DroneAction.Altitude:
                targetAltitude = command.Altitude;
                isChangingAltitude = true;
                break;

            case DroneCommand.DroneAction.Rotate:
                isRotating = true;
                targetRotation = Quaternion.Euler(command.DirectionVector);
                break;

            case DroneCommand.DroneAction.Return:
                isReturning = true;
                moveSpeed = originalMoveSpeed;
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

        rb.linearVelocity = Vector3.zero;
        moveSpeed = originalMoveSpeed;
    }
}
