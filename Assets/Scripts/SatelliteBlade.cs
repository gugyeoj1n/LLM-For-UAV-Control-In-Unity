using UnityEngine;

public class SatelliteBlade : MonoBehaviour
{
    void Update( )
    {
        transform.Rotate( 0f, 0f,  800f * Time.deltaTime );
    }
}
