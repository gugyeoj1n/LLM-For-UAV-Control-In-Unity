using UnityEngine;
using System.Collections.Generic;

public class DroneMover : MonoBehaviour
{
    public float moveSpeed = 2f;
    private LidarScanner lidar;

    void Start()
    {
        lidar = GetComponent<LidarScanner>();
    }

    void Update()
    {
        if (lidar == null || lidar.scanResults.Count == 0) return;

        Vector3 bestDir = Vector3.zero;
        float bestScore = -1;

        foreach (var entry in lidar.scanResults)
        {
            float dist = entry.Value;
            if (dist < lidar.safeDistance) continue;

            float angle = Vector3.Angle(transform.forward, entry.Key);
            float score = dist - angle * 0.1f;

            if (score > bestScore)
            {
                bestScore = score;
                bestDir = entry.Key;
            }
        }

        if (bestScore > 0)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(bestDir), Time.deltaTime * 2f);
            transform.position += transform.forward * moveSpeed * Time.deltaTime;
        }
        else
        {
            // 막힌 경우 후진
            transform.position -= transform.forward * moveSpeed * Time.deltaTime * 0.5f;
        }
    }

}