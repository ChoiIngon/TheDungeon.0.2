using UnityEngine;

/// <summary>
/// 액터가 자기 차례에 무엇을 할지 정한다.
/// </summary>
public interface IActorBrain
{
    /// <summary>
    /// 이번 차례에 수행할 행동.
    /// null 을 돌려주면 "아직 정해지지 않았다"는 뜻이며, 게임 루프는 그 자리에서 멈춘다.
    /// 플레이어가 키를 누를 때까지 시간이 멈추는 것이 이 규약으로 구현된다.
    /// </summary>
    ICrawlerAction NextAction(CrawlerWorld world, CrawlerActor actor);
}

/// <summary>
/// 플레이어의 두뇌. 스스로 판단하지 않고 입력이 넣어 준 행동을 하나씩 꺼내 준다.
/// </summary>
public class PlayerBrain : IActorBrain
{
    private ICrawlerAction pending;

    public bool HasPendingAction => null != this.pending;

    /// <summary>입력 처리기가 부른다. 아직 소비되지 않은 행동이 있으면 덮어쓴다(가장 최근 입력 우선).</summary>
    public void Queue(ICrawlerAction action)
    {
        this.pending = action;
    }

    public void Cancel()
    {
        this.pending = null;
    }

    public ICrawlerAction NextAction(CrawlerWorld world, CrawlerActor actor)
    {
        ICrawlerAction action = this.pending;
        this.pending = null;
        return action;
    }
}

/// <summary>
/// 몬스터의 두뇌.
///
/// 플레이어를 보면 쫓아가고, 시야에서 놓치면 마지막으로 본 자리까지 가 본 뒤 포기한다.
/// 마지막 목격 지점을 기억하기 때문에 벽 뒤로 숨어도 곧장 잊어버리지 않는다.
/// </summary>
public class MonsterBrain : IActorBrain
{
    /// <summary>이 거리(체스판 거리) 밖의 플레이어는 보이더라도 알아채지 못한다.</summary>
    public int DetectionRange = 12;

    /// <summary>플레이어를 놓쳤을 때 아무 데나 한 걸음 옮길 확률(%).</summary>
    public float WanderChance = 20.0f;

    private bool isAware;
    private int lastKnownX;
    private int lastKnownY;

    public ICrawlerAction NextAction(CrawlerWorld world, CrawlerActor actor)
    {
        CrawlerActor player = world.Player;
        if (null == player || true == player.IsDead)
        {
            return new WaitAction();
        }

        if (true == CanSee(world, actor, player))
        {
            this.isAware = true;
            this.lastKnownX = player.X;
            this.lastKnownY = player.Y;

            if (1 >= CrawlerActor.Distance(actor, player))
            {
                return new MeleeAttackAction(player);
            }

            return StepToward(world, actor, player.X, player.Y);
        }

        if (true == this.isAware)
        {
            // 마지막으로 본 자리에 도착했는데 아무도 없다. 여기서 추격을 포기한다.
            if (actor.X == this.lastKnownX && actor.Y == this.lastKnownY)
            {
                this.isAware = false;
                return new WaitAction();
            }

            return StepToward(world, actor, this.lastKnownX, this.lastKnownY);
        }

        if (true == world.Random.Chance(this.WanderChance))
        {
            Vector2Int direction = CrawlerActor.Directions[world.Random.Range(0, CrawlerActor.Directions.Length)];
            if (true == world.CanStep(actor.X, actor.Y, direction.x, direction.y))
            {
                return new MoveAction(direction.x, direction.y);
            }
        }

        return new WaitAction();
    }

    private bool CanSee(CrawlerWorld world, CrawlerActor actor, CrawlerActor target)
    {
        if (this.DetectionRange < CrawlerActor.Distance(actor, target))
        {
            return false;
        }

        return world.HasLineOfSight(actor, target);
    }

    /// <summary>
    /// 목표에 가장 가까워지는 한 걸음을 고른다.
    ///
    /// A* 를 매 턴 돌리지 않는 대신, 갈 수 있는 8방향 중 목표와의 거리가 가장 짧아지는 칸을 택한다.
    /// 오목한 벽에서는 제자리를 맴돌 수 있지만, 방과 복도로 이루어진 이 던전에서는 대부분 통한다.
    /// </summary>
    private ICrawlerAction StepToward(CrawlerWorld world, CrawlerActor actor, int targetX, int targetY)
    {
        int bestScore = int.MaxValue;
        Vector2Int bestStep = Vector2Int.zero;

        foreach (Vector2Int direction in CrawlerActor.Directions)
        {
            int x = actor.X + direction.x;
            int y = actor.Y + direction.y;

            // 닫힌 문도 후보다. MoveAction 이 문 열기로 바꿔 준다.
            bool closedDoor = world.IsClosedDoor(x, y);
            if (false == closedDoor && false == world.CanStep(actor.X, actor.Y, direction.x, direction.y))
            {
                continue;
            }

            // 다른 액터가 서 있는 칸은 비켜 가지 않는다. 몬스터끼리 서로 때리는 것을 막는다.
            if (null != world.GetActor(x, y))
            {
                continue;
            }

            int score = CrawlerActor.Distance(x, y, targetX, targetY);
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestStep = direction;
        }

        if (Vector2Int.zero == bestStep)
        {
            return new WaitAction();
        }

        return new MoveAction(bestStep.x, bestStep.y);
    }
}
