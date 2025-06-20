using UnityEngine;
using TMPro;

public class WorldToUIFollow : MonoBehaviour
{
    public Camera mainCamera;          // Viewport가 W=0.5인 카메라
    public Transform targetWorld;      // 따라다닐 3D 오브젝트
    public RectTransform uiTextRect;   // TMP_Text의 RectTransform
    public GameObject uiContainer;     // TMP_Text가 포함된 GameObject
    public Vector3 worldOffset = new Vector3(0, 2f, 0);  // 오브젝트 위로 띄우는 오프셋

    void Update()
    {
        if (targetWorld == null || mainCamera == null || uiTextRect == null)
            return;

        Vector3 worldPos = targetWorld.position + worldOffset;

        // 오브젝트가 카메라 앞에 있는지 확인
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
        bool isVisible = screenPos.z > 0 &&
                         screenPos.x >= 0 && screenPos.x <= mainCamera.pixelWidth &&
                         screenPos.y >= 0 && screenPos.y <= mainCamera.pixelHeight;

        if (!isVisible)
        {
            if (uiContainer.activeSelf)
                uiContainer.SetActive(false);
            return;
        }

        if (!uiContainer.activeSelf)
            uiContainer.SetActive(true);

        // 오브젝트의 스크린 위치를 UI 위치로 직접 할당
        uiTextRect.position = screenPos;
    }
}