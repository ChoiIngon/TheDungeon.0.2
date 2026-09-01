using UnityEngine;

/// <summary>
/// 2.5D 쿼터뷰 카메라.
///
/// 각도를 고정한 채 플레이어만 따라간다. 직교 투영을 쓰기 때문에 화면 어디에 있든 타일 크기가 같고,
/// 원근 왜곡이 없어 "몇 칸 떨어져 있는지"가 눈으로 읽힌다. 그리드 기반 로그라이크에서는 이 점이 중요하다.
///
/// 회전은 Q/E 로 90도씩만 돌린다. 자유 회전을 허용하면 벽 가리기 규칙(DungeonView)이 애매해지고,
/// 방향키와 화면 방향이 어긋나 조작이 헷갈린다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class QuarterViewCamera : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;

    [Tooltip("목표를 따라가는 속도. 0 이면 즉시 붙는다.")]
    public float followSmoothing = 12.0f;

    [Header("Angle")]
    [Range(20.0f, 89.0f)]
    public float pitch = 52.0f;

    [Tooltip("바라보는 방향(도). 45도 배수를 권장한다.")]
    public float yaw = 45.0f;

    [Tooltip("타깃에서 카메라까지의 거리. 직교 투영에서는 클리핑에만 영향을 준다.")]
    public float distance = 80.0f;

    [Header("Zoom")]
    public bool useOrthographic = true;
    public float orthographicSize = 26.0f;
    public float minOrthographicSize = 12.0f;
    public float maxOrthographicSize = 60.0f;
    public float zoomSpeed = 6.0f;

    [Header("Rotation")]
    public bool allowRotation = true;
    public KeyCode rotateLeftKey = KeyCode.Q;
    public KeyCode rotateRightKey = KeyCode.E;

    private Camera cameraComponent;

    /// <summary>카메라가 바라보는 수평 방향. 벽을 가릴지 판단할 때 쓴다.</summary>
    public Vector3 HorizontalForward { get; private set; } = Vector3.forward;

    private void Awake()
    {
        this.cameraComponent = GetComponent<Camera>();
        Apply(true);
    }

    private void LateUpdate()
    {
        HandleInput();
        Apply(false);
    }

    private void HandleInput()
    {
        if (true == this.allowRotation)
        {
            if (true == Input.GetKeyDown(this.rotateLeftKey))
            {
                this.yaw -= 90.0f;
            }

            if (true == Input.GetKeyDown(this.rotateRightKey))
            {
                this.yaw += 90.0f;
            }
        }

        float scroll = Input.mouseScrollDelta.y;
        if (0.0f != scroll)
        {
            this.orthographicSize = Mathf.Clamp(
                this.orthographicSize - scroll * this.zoomSpeed,
                this.minOrthographicSize,
                this.maxOrthographicSize);
        }
    }

    /// <summary>카메라 위치와 각도를 갱신한다. snap 이면 보간 없이 즉시 맞춘다.</summary>
    public void Apply(bool snap)
    {
        if (null == this.cameraComponent)
        {
            this.cameraComponent = GetComponent<Camera>();
        }

        this.cameraComponent.orthographic = this.useOrthographic;
        if (true == this.useOrthographic)
        {
            this.cameraComponent.orthographicSize = this.orthographicSize;

            // 직교 투영은 카메라를 아무리 멀리 두어도 크기가 변하지 않는다.
            // 대신 far plane 안에 던전이 들어와야 하므로 거리에 맞춰 넉넉히 잡는다.
            this.cameraComponent.nearClipPlane = 0.1f;
            this.cameraComponent.farClipPlane = Mathf.Max(this.cameraComponent.farClipPlane, this.distance * 3.0f);
        }

        Quaternion rotation = Quaternion.Euler(this.pitch, this.yaw, 0.0f);
        this.HorizontalForward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;

        transform.rotation = rotation;

        if (null == this.target)
        {
            return;
        }

        Vector3 desired = this.target.position - rotation * Vector3.forward * this.distance;

        if (true == snap || 0.0f >= this.followSmoothing)
        {
            transform.position = desired;
            return;
        }

        transform.position = Vector3.Lerp(transform.position, desired, 1.0f - Mathf.Exp(-this.followSmoothing * Time.deltaTime));
    }

    /// <summary>새 층으로 넘어갔을 때처럼 카메라를 즉시 옮겨야 할 때 부른다.</summary>
    public void SnapToTarget(Transform newTarget)
    {
        this.target = newTarget;
        Apply(true);
    }
}
