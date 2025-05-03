using UnityEngine;

public class DroneBlade : MonoBehaviour
{
    void Update( )
    {
        transform.Rotate( 0f, 0f,  800f * Time.deltaTime );
    }
}
