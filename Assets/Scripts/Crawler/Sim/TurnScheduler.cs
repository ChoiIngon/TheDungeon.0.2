using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에너지 기반 턴 스케줄러.
///
/// NetHack 과 같은 방식이다. 한 "게임 턴"이 지날 때마다 모든 액터가 자신의 Speed 만큼 에너지를 얻고,
/// 에너지가 ActionCost 이상 쌓인 액터부터 순서대로 행동한다. 행동하면 그만큼 에너지를 잃는다.
///
/// 이 구조 덕분에 속도가 다른 액터가 자연스럽게 섞인다.
///  - Speed 12(기본) : 한 턴에 한 번
///  - Speed 24(빠름) : 한 턴에 두 번
///  - Speed  6(느림) : 두 턴에 한 번
///
/// 실시간이 아니므로 이 클래스는 Time.deltaTime 을 전혀 보지 않는다.
/// 게임 루프가 NextActor() 를 반복해서 부르고, 플레이어 입력이 필요하면 그 자리에서 멈춘다.
/// </summary>
public class TurnScheduler
{
    /// <summary>행동 하나의 기본 비용. NetHack 의 NORMAL_SPEED 와 같은 역할이다.</summary>
    public const int ActionCost = 12;

    /// <summary>
    /// 준비된 액터를 찾느라 게임 턴을 몇 번까지 진행시킬지에 대한 상한.
    ///
    /// 모두가 Speed 0 이거나 행동 비용이 지나치게 큰 경우 시간만 흐르고 아무도 행동하지 못한다.
    /// 그대로 두면 프레임이 멈추므로 상한을 두고 경고를 남긴다.
    /// </summary>
    private const int MaxTurnAdvance = 64;

    private readonly List<CrawlerActor> actors = new List<CrawlerActor>();

    /// <summary>
    /// 이번 게임 턴에서 다음으로 검사할 액터의 위치.
    ///
    /// 매번 목록 앞에서부터 훑으면 앞쪽 액터만 계속 행동하게 된다. 커서를 유지해서
    /// 한 턴 안에서 모든 액터가 한 번씩 기회를 갖도록 한다.
    /// </summary>
    private int cursor = 0;

    /// <summary>지금까지 흐른 게임 턴 수.</summary>
    public int TurnCount { get; private set; }

    public IReadOnlyList<CrawlerActor> Actors => this.actors;

    public void Add(CrawlerActor actor)
    {
        if (null == actor || true == this.actors.Contains(actor))
        {
            return;
        }

        actor.Energy = 0;
        this.actors.Add(actor);
    }

    public void Remove(CrawlerActor actor)
    {
        int index = this.actors.IndexOf(actor);
        if (0 > index)
        {
            return;
        }

        this.actors.RemoveAt(index);

        // 커서보다 앞쪽이 빠지면 커서가 한 칸씩 밀린다. 보정하지 않으면 액터 하나를 건너뛴다.
        if (index < this.cursor)
        {
            this.cursor--;
        }
    }

    public void Clear()
    {
        this.actors.Clear();
        this.cursor = 0;
        this.TurnCount = 0;
    }

    /// <summary>
    /// 다음에 행동할 액터를 돌려준다. 준비된 액터가 없으면 게임 턴을 진행시켜 에너지를 채운다.
    ///
    /// 같은 액터를 여러 번 돌려줄 수 있다. 플레이어가 아직 입력을 하지 않았다면 매 프레임 같은 액터가 나오고,
    /// 속도가 빠른 몬스터는 한 턴에 두 번 나온다. 호출자는 행동을 수행한 뒤 SpendEnergy() 를 불러야 한다.
    /// </summary>
    public CrawlerActor NextActor()
    {
        if (0 == this.actors.Count)
        {
            return null;
        }

        for (int advance = 0; advance < MaxTurnAdvance; advance++)
        {
            while (this.cursor < this.actors.Count)
            {
                CrawlerActor actor = this.actors[this.cursor];

                if (true == actor.IsDead)
                {
                    this.cursor++;
                    continue;
                }

                if (ActionCost <= actor.Energy)
                {
                    return actor;
                }

                this.cursor++;
            }

            if (false == AdvanceTurn())
            {
                return null;
            }
        }

        Debug.LogWarning($"TurnScheduler: {MaxTurnAdvance}턴을 진행했지만 행동 가능한 액터가 없다. Speed 설정을 확인하라.");
        return null;
    }

    /// <summary>
    /// 게임 턴을 하나 진행시켜 모든 액터에게 에너지를 준다.
    /// 아무도 에너지를 얻지 못하면(전원 Speed 0) 시간이 흘러도 상황이 바뀌지 않으므로 false 를 돌려준다.
    /// </summary>
    private bool AdvanceTurn()
    {
        this.cursor = 0;
        this.TurnCount++;

        bool gained = false;
        foreach (CrawlerActor actor in this.actors)
        {
            if (true == actor.IsDead || 0 >= actor.Speed)
            {
                continue;
            }

            actor.Energy += actor.Speed;
            gained = true;
        }

        return gained;
    }
}
