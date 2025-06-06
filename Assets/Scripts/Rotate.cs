using UnityEngine;

public class Rotate : MonoBehaviour
{
    public enum Type
    {
        X,
        Y,
        Z
    };

    public Type type;
    public float speed;

    void Update()
    {
        Vector3 axis = Vector3.zero;

        switch (type)
        {
            case Type.X:
                axis = Vector3.right;
                break;
            case Type.Y:
                axis = Vector3.up;
                break;
            case Type.Z:
                axis = Vector3.forward;
                break;
        }

        transform.Rotate(axis, speed * Time.deltaTime);
    }
}