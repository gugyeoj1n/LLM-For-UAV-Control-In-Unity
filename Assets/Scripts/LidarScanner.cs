using UnityEngine;
using System.Collections.Generic;

public class LidarScanner : MonoBehaviour
{
    public int raysPer360 = 36;
    public float scanDistance = 10f;
    public float safeDistance = 2f;

    public Dictionary<Vector3, float> scanResults = new Dictionary<Vector3, float>();

    void Update()
    {
        ScanSurroundings();
    }

    void ScanSurroundings()
    {
        scanResults.Clear();
        for (int i = 0; i < raysPer360; i++)
        {
            float angle = (360f / raysPer360) * i;
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, scanDistance))
            {
                scanResults[dir] = hit.distance;
            }
            else
            {
                scanResults[dir] = scanDistance;
            }

            Debug.DrawRay(transform.position, dir * scanResults[dir], Color.green);
        }
    }
}