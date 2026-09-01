using UnityEngine;

/// <summary>
/// 액터가 한 차례에 수행하는 행동.
///
/// 행동을 값으로 만들어 두면 입력과 AI 가 같은 통로를 쓴다. 플레이어의 키 입력도, 몬스터의 판단도
/// 결국 ICrawlerAction 하나를 만들어 게임 루프에 넘기는 것으로 끝난다.
/// </summary>
public interface ICrawlerAction
{
    ActionResult Perform(CrawlerWorld world, CrawlerActor actor);
}

/// <summary>
/// 행동의 결과.
///
/// 로그라이크에서는 한 입력이 다른 행동으로 바뀌는 일이 잦다. 몬스터 쪽으로 걸어가면 공격이 되고,
/// 닫힌 문 쪽으로 걸어가면 문 열기가 된다. 그래서 "대신 이 행동을 수행하라"를 결과에 담는다.
/// </summary>
public readonly struct ActionResult
{
    /// <summary>턴(에너지)을 소비했는가.</summary>
    public readonly bool consumedTurn;

    /// <summary>소비한 에너지. consumedTurn 이 false 면 의미가 없다.</summary>
    public readonly int cost;

    /// <summary>이 행동 대신 수행해야 할 행동. null 이 아니면 게임 루프가 이어서 수행한다.</summary>
    public readonly ICrawlerAction alternate;

    private ActionResult(bool consumedTurn, int cost, ICrawlerAction alternate)
    {
        this.consumedTurn = consumedTurn;
        this.cost = cost;
        this.alternate = alternate;
    }

    public static ActionResult Done()
    {
        return new ActionResult(true, TurnScheduler.ActionCost, null);
    }

    public static ActionResult Done(int cost)
    {
        return new ActionResult(true, cost, null);
    }

    /// <summary>행동이 성립하지 않았다. 턴을 쓰지 않으므로 플레이어는 다시 입력할 수 있다.</summary>
    public static ActionResult Failed()
    {
        return new ActionResult(false, 0, null);
    }

    public static ActionResult Alternate(ICrawlerAction action)
    {
        return new ActionResult(false, 0, action);
    }
}

/// <summary>제자리에서 한 턴을 보낸다.</summary>
public class WaitAction : ICrawlerAction
{
    public ActionResult Perform(CrawlerWorld world, CrawlerActor actor)
    {
        return ActionResult.Done();
    }
}

/// <summary>
/// 한 칸 이동한다.
/// 목적지에 액터가 있으면 공격으로, 닫힌 문이 있으면 문 열기로 바뀐다(부딪히면 상호작용).
/// </summary>
public class MoveAction : ICrawlerAction
{
    private readonly int deltaX;
    private readonly int deltaY;

    public MoveAction(int deltaX, int deltaY)
    {
        this.deltaX = deltaX;
        this.deltaY = deltaY;
    }

    public ActionResult Perform(CrawlerWorld world, CrawlerActor actor)
    {
        if (0 == this.deltaX && 0 == this.deltaY)
        {
            return ActionResult.Alternate(new WaitAction());
        }

        int x = actor.X + this.deltaX;
        int y = actor.Y + this.deltaY;

        CrawlerActor other = world.GetActor(x, y);
        if (null != other && other != actor && false == other.IsDead)
        {
            return ActionResult.Alternate(new MeleeAttackAction(other));
        }

        if (true == world.IsClosedDoor(x, y))
        {
            return ActionResult.Alternate(new OpenDoorAction(x, y));
        }

        if (false == world.CanStep(actor.X, actor.Y, this.deltaX, this.deltaY))
        {
            if (true == actor.IsPlayer)
            {
                world.Log("그쪽으로는 갈 수 없다.");
            }

            return ActionResult.Failed();
        }

        world.MoveActor(actor, x, y);
        return ActionResult.Done();
    }
}

/// <summary>인접한 대상을 때린다.</summary>
public class MeleeAttackAction : ICrawlerAction
{
    private readonly CrawlerActor target;

    public MeleeAttackAction(CrawlerActor target)
    {
        this.target = target;
    }

    public ActionResult Perform(CrawlerWorld world, CrawlerActor actor)
    {
        if (null == this.target || true == this.target.IsDead)
        {
            return ActionResult.Failed();
        }

        if (1 < CrawlerActor.Distance(actor, this.target))
        {
            return ActionResult.Failed();
        }

        int damage = world.Random.Range(1, Mathf.Max(2, actor.AttackPower + 1));
        world.Log($"{actor.Name}이(가) {this.target.Name}을(를) 공격했다. ({damage})");
        world.ApplyDamage(actor, this.target, damage);

        return ActionResult.Done();
    }
}

/// <summary>닫힌 문을 연다. 한 턴을 쓴다.</summary>
public class OpenDoorAction : ICrawlerAction
{
    private readonly int x;
    private readonly int y;

    public OpenDoorAction(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public ActionResult Perform(CrawlerWorld world, CrawlerActor actor)
    {
        if (false == world.IsClosedDoor(this.x, this.y))
        {
            return ActionResult.Failed();
        }

        world.SetDoorState(this.x, this.y, CrawlerWorld.DoorState.Open);

        if (true == actor.IsPlayer)
        {
            world.Log("문을 열었다.");
        }

        return ActionResult.Done();
    }
}
