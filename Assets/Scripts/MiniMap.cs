using UnityEngine;

public class MiniMap : MonoBehaviour
{
    [Header("MiniMap Settings")]
    public Transform targetTransform;  // 따라다닐 대상 (플레이어)
    public float orthographicSize = 15f;  // 카메라의 orthographic size
    public float height = 10f;  // 카메라의 높이
    
    [Header("Viewport Settings")]
    public float viewportWidth = 0.2f;  // 화면 너비 대비 미니맵 너비 (20%)
    public float viewportHeight = 0.2f;  // 화면 높이 대비 미니맵 높이 (20%)
    public float cornerOffsetX = 0.01f;  // 우측 상단 모서리로부터의 X 오프셋
    public float cornerOffsetY = 0.01f;  // 우측 상단 모서리로부터의 Y 오프셋
    
    private Camera miniMapCam;

    void Start()
    {
        // 미니맵 카메라 설정
        SetupMiniMapCamera();
    }

    void Update()
    {
        // 미니맵 카메라가 플레이어를 따라다니도록 업데이트
        if (targetTransform != null)
        {
            UpdateMiniMapPosition();
        }
    }

    /// <summary>
    /// 미니맵 카메라 초기 설정
    /// </summary>
    private void SetupMiniMapCamera()
    {
        miniMapCam = GetComponent<Camera>();
        
        if (miniMapCam == null)
        {
            Debug.LogError("MiniMapCamera 게임 오브젝트에 Camera 컴포넌트가 없습니다!");
            return;
        }

        // 카메라 설정
        miniMapCam.orthographic = true;  // Orthographic 모드
        miniMapCam.orthographicSize = orthographicSize;
        miniMapCam.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);  // 어두운 배경

        // Viewport 설정 (화면 우측 상단)
        // X: 우측 상단이므로 1 - width, Y: 상단이므로 1 - height
        Rect viewport = new Rect(
            1f - viewportWidth - cornerOffsetX,
            1f - viewportHeight - cornerOffsetY,
            viewportWidth,
            viewportHeight
        );
        miniMapCam.rect = viewport;

        // 게임 오브젝트 위치 설정
        gameObject.name = "MiniMapCamera";
    }

    /// <summary>
    /// 미니맵 카메라 위치 업데이트 (플레이어를 따라다님)
    /// </summary>
    private void UpdateMiniMapPosition()
    {
        // 플레이어 위치를 따라가면서 높이는 고정
        Vector3 newPosition = targetTransform.position;
        newPosition.y = height;
        
        transform.position = newPosition;
        
        // 카메라가 아래쪽을 바라보도록 설정
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    /// <summary>
    /// 미니맵 카메라 크기 설정
    /// </summary>
    public void SetOrthographicSize(float newSize)
    {
        orthographicSize = newSize;
        if (miniMapCam != null)
        {
            miniMapCam.orthographicSize = orthographicSize;
        }
    }

    /// <summary>
    /// 미니맵 뷰포트 크기 설정
    /// </summary>
    public void SetViewportSize(float width, float height)
    {
        viewportWidth = Mathf.Clamp01(width);
        viewportHeight = Mathf.Clamp01(height);
        
        if (miniMapCam != null)
        {
            Rect viewport = new Rect(
                1f - viewportWidth - cornerOffsetX,
                1f - viewportHeight - cornerOffsetY,
                viewportWidth,
                viewportHeight
            );
            miniMapCam.rect = viewport;
        }
    }
}
