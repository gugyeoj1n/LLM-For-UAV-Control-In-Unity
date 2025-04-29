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
        DrawSafeCircle();
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

    void DrawSafeCircle()
    {
        Vector3 center = transform.position;
        Vector3 prevPoint = Vector3.zero;

        for (int i = 0; i <= raysPer360; i++)
        {
            float angle = (360f / raysPer360) * i * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * safeDistance;

            if (i > 0)
            {
                Debug.DrawLine(prevPoint, point, Color.blue); // 안전 거리 원: 빨간 선
            }

            prevPoint = point;
        }
    }
}