using UnityEngine;

/// <summary>
/// <see cref="CrawlerActor"/> 의 논리 좌표를 따라다니는 몸통.
///
/// 시뮬레이션은 턴이 끝나는 즉시 좌표를 옮기지만, 그대로 그리면 캐릭터가 순간이동한다.
/// 여기서는 목표 좌표를 향해 매 프레임 조금씩 따라가게 해서 그리드 이동을 부드럽게 보이게 한다.
/// 시뮬레이션은 이 보간을 기다리지 않는다. 규칙과 연출을 분리해 두어야 입력이 밀리지 않는다.
/// </summary>
public class ActorView : MonoBehaviour
{
    [Tooltip("한 칸을 이동하는 데 걸리는 시간(초).")]
    public float moveDuration = 0.12f;

    [Tooltip("바라보는 방향이 도는 속도(초당 도).")]
    public float turnSpeed = 900.0f;

    [Tooltip("바닥에서 띄울 높이. 캐릭터 모델의 원점 위치에 맞춘다.")]
    public float groundOffset = 0.0f;

    /// <summary>플레이어에게 보이지 않는 몬스터는 감춘다. 플레이어 본인에게는 적용하지 않는다.</summary>
    public bool hideWhenNotVisible = true;

    private CrawlerActor actor;
    private CrawlerWorld world;
    private ActorModel actorModel;
    private Renderer[] renderers;

    private Vector3 targetPosition;
    private Quaternion facing = Quaternion.identity;
    private bool wasVisible = true;

    public CrawlerActor Actor => this.actor;

    public void Bind(CrawlerActor actor, CrawlerWorld world)
    {
        this.actor = actor;
        this.world = world;
        this.actorModel = GetComponent<ActorModel>();
        this.renderers = GetComponentsInChildren<Renderer>(true);

        this.targetPosition = actor.GetWorldPosition(this.groundOffset);
        transform.position = this.targetPosition;
        this.facing = transform.rotation;

        // 첫 프레임에 한 번 깜빡이지 않도록 지금 상태를 그대로 반영해 둔다.
        this.wasVisible = true;
        UpdateVisibility();
    }

    private void Update()
    {
        if (null == this.actor)
        {
            return;
        }

        UpdatePosition();
        UpdateVisibility();
    }

    private void UpdatePosition()
    {
        Vector3 desired = this.actor.GetWorldPosition(this.groundOffset);

        if (desired != this.targetPosition)
        {
            this.targetPosition = desired;

            Vector3 direction = desired - transform.position;
            direction.y = 0.0f;
            if (0.01f < direction.sqrMagnitude)
            {
                // 이동 방향을 바라보게 한다. 회전 자체는 아래에서 부드럽게 따라간다.
                this.facing = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        float step = (0.0f < this.moveDuration) ? (Dungeon.TileSize / this.moveDuration) * Time.deltaTime : float.MaxValue;
        transform.position = Vector3.MoveTowards(transform.position, this.targetPosition, step);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, this.facing, this.turnSpeed * Time.deltaTime);

        if (null == this.actorModel)
        {
            return;
        }

        bool arrived = 0.001f > (transform.position - this.targetPosition).sqrMagnitude;
        this.actorModel.PlayAnimation(true == this.actor.IsDead
            ? ActorModel.ActorState.Dead
            : (true == arrived ? ActorModel.ActorState.Idle : ActorModel.ActorState.Walk));
    }

    private void UpdateVisibility()
    {
        if (false == this.hideWhenNotVisible || true == this.actor.IsPlayer || null == this.world)
        {
            return;
        }

        bool visible = this.world.IsVisibleToPlayer(this.actor.X, this.actor.Y);
        if (visible == this.wasVisible)
        {
            return;
        }

        this.wasVisible = visible;
        foreach (Renderer renderer in this.renderers)
        {
            if (null == renderer)
            {
                continue;
            }

            renderer.enabled = visible;
        }
    }
}
