using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 층의 게임 상태. 생성된 <see cref="TileMap"/> 위에 액터와 문 상태를 얹은 것이다.
///
/// TileMap 은 "지형"만 알고 있고, 여기서 "지금 누가 어디 서 있는지 / 문이 열렸는지 / 어디까지 봤는지"를 다룬다.
/// 규칙(행동, AI, 시야)은 모두 이 클래스를 통해 던전을 읽는다. MonoBehaviour 를 참조하지 않으므로
/// 나중에 층을 통째로 저장하거나 테스트에서 뷰 없이 돌릴 수 있다.
/// </summary>
public class CrawlerWorld
{
    public enum DoorState
    {
        /// <summary>문이 아니다.</summary>
        None = 0,
        Closed = 1,
        Open = 2,
    }

    private readonly TileMap map;
    private readonly LevelGenerator level;
    private readonly DungeonRandom random;
    private readonly FieldOfView fieldOfView;

    private readonly List<CrawlerActor> actors = new List<CrawlerActor>();

    /// <summary>타일 인덱스 → 그 자리에 선 액터. 한 칸에는 하나만 설 수 있다.</summary>
    private readonly Dictionary<int, CrawlerActor> occupancy = new Dictionary<int, CrawlerActor>();

    /// <summary>타일 인덱스 → 문 상태. 문이 아닌 타일은 아예 들어 있지 않다.</summary>
    private readonly Dictionary<int, DoorState> doors = new Dictionary<int, DoorState>();

    public TileMap Map => this.map;
    public LevelGenerator Level => this.level;
    public DungeonRandom Random => this.random;
    public VisibilityMap Visibility { get; }
    public CrawlerActor Player { get; private set; }
    public IReadOnlyList<CrawlerActor> Actors => this.actors;

    /// <summary>화면 하단 메시지 로그로 보낼 문구.</summary>
    public event Action<string> MessageLogged;

    /// <summary>공격자, 피격자, 피해량.</summary>
    public event Action<CrawlerActor, CrawlerActor, int> ActorDamaged;

    public event Action<CrawlerActor> ActorDied;

    /// <summary>문 타일 인덱스, 바뀐 상태. 뷰가 DoorStand 를 여닫는 데 쓴다.</summary>
    public event Action<int, DoorState> DoorChanged;

    public CrawlerWorld(TileMap map, LevelGenerator level, DungeonRandom random)
    {
        this.map = map;
        this.level = level;
        this.random = random;
        this.Visibility = new VisibilityMap(map.width, map.height);
        this.fieldOfView = new FieldOfView(this);

        InitializeDoors();
    }

    /// <summary>생성기가 잡아 둔 문 자리를 모두 닫힌 문으로 등록한다.</summary>
    private void InitializeDoors()
    {
        foreach (TileMap.Room room in this.map.rooms)
        {
            foreach (TileMap.Tile door in room.doors)
            {
                // 두 방이 벽을 맞대면 같은 타일이 양쪽 방의 문 목록에 들어 있다. 덮어써도 결과는 같다.
                this.doors[door.index] = DoorState.Closed;
            }
        }
    }

    // ------------------------------------------------------------------ 지형 조회

    public TileMap.Tile GetTile(int x, int y) => this.map.GetTile(x, y);

    public bool IsInside(int x, int y)
    {
        return 0 <= x && x < this.map.width && 0 <= y && y < this.map.height;
    }

    /// <summary>지나갈 수 있는 바닥인가. 닫힌 문은 막힌 것으로 본다(액터가 서 있는지는 보지 않는다).</summary>
    public bool IsWalkable(int x, int y)
    {
        TileMap.Tile tile = this.map.GetTile(x, y);
        if (null == tile || false == tile.IsFloor)
        {
            return false;
        }

        return DoorState.Closed != GetDoorState(tile.index);
    }

    /// <summary>시야를 막는가. 맵 밖과 벽, 그리고 닫힌 문이 막는다.</summary>
    public bool BlocksSight(int x, int y)
    {
        TileMap.Tile tile = this.map.GetTile(x, y);
        if (null == tile || false == tile.IsFloor)
        {
            return true;
        }

        return DoorState.Closed == GetDoorState(tile.index);
    }

    /// <summary>
    /// (fromX, fromY) 에서 (deltaX, deltaY) 만큼 실제로 한 걸음 옮길 수 있는가.
    ///
    /// NetHack 의 대각선 규칙을 따른다.
    ///  - 벽 모서리를 대각선으로 스쳐 지나갈 수 없다(양옆 중 하나라도 막혀 있으면 안 된다).
    ///  - 문간에서는 대각선으로 드나들 수 없다.
    /// 규칙이 없으면 캐릭터가 벽 모서리를 뚫고 지나가는 것처럼 보인다.
    /// </summary>
    public bool CanStep(int fromX, int fromY, int deltaX, int deltaY)
    {
        int x = fromX + deltaX;
        int y = fromY + deltaY;

        if (false == IsWalkable(x, y))
        {
            return false;
        }

        if (0 == deltaX || 0 == deltaY)
        {
            return true;
        }

        if (true == IsDoorway(fromX, fromY) || true == IsDoorway(x, y))
        {
            return false;
        }

        return IsWalkable(fromX + deltaX, fromY) && IsWalkable(fromX, fromY + deltaY);
    }

    // ------------------------------------------------------------------ 문

    public DoorState GetDoorState(int tileIndex)
    {
        return this.doors.TryGetValue(tileIndex, out DoorState state) ? state : DoorState.None;
    }

    public DoorState GetDoorState(int x, int y)
    {
        TileMap.Tile tile = this.map.GetTile(x, y);
        return (null == tile) ? DoorState.None : GetDoorState(tile.index);
    }

    public bool IsDoorway(int x, int y) => DoorState.None != GetDoorState(x, y);

    public bool IsClosedDoor(int x, int y) => DoorState.Closed == GetDoorState(x, y);

    public void SetDoorState(int x, int y, DoorState state)
    {
        TileMap.Tile tile = this.map.GetTile(x, y);
        if (null == tile || false == this.doors.ContainsKey(tile.index))
        {
            return;
        }

        if (this.doors[tile.index] == state)
        {
            return;
        }

        this.doors[tile.index] = state;
        this.DoorChanged?.Invoke(tile.index, state);
    }

    /// <summary>등록된 모든 문. 뷰가 초기 상태를 맞출 때 쓴다.</summary>
    public IReadOnlyDictionary<int, DoorState> Doors => this.doors;

    // ------------------------------------------------------------------ 액터

    public void AddActor(CrawlerActor actor, int x, int y)
    {
        if (null == actor || true == this.actors.Contains(actor))
        {
            return;
        }

        actor.X = x;
        actor.Y = y;

        this.actors.Add(actor);
        this.occupancy[ToIndex(x, y)] = actor;

        if (true == actor.IsPlayer)
        {
            this.Player = actor;
        }
    }

    public void RemoveActor(CrawlerActor actor)
    {
        if (null == actor)
        {
            return;
        }

        this.actors.Remove(actor);

        int index = ToIndex(actor.X, actor.Y);
        if (true == this.occupancy.TryGetValue(index, out CrawlerActor occupant) && occupant == actor)
        {
            this.occupancy.Remove(index);
        }

        if (this.Player == actor)
        {
            this.Player = null;
        }
    }

    public CrawlerActor GetActor(int x, int y)
    {
        if (false == IsInside(x, y))
        {
            return null;
        }

        return this.occupancy.TryGetValue(ToIndex(x, y), out CrawlerActor actor) ? actor : null;
    }

    /// <summary>액터를 옮긴다. 이동 가능 여부는 호출자(행동)가 이미 확인했다고 본다.</summary>
    public void MoveActor(CrawlerActor actor, int x, int y)
    {
        int from = ToIndex(actor.X, actor.Y);
        if (true == this.occupancy.TryGetValue(from, out CrawlerActor occupant) && occupant == actor)
        {
            this.occupancy.Remove(from);
        }

        actor.X = x;
        actor.Y = y;
        this.occupancy[ToIndex(x, y)] = actor;
    }

    public void ApplyDamage(CrawlerActor attacker, CrawlerActor target, int damage)
    {
        if (null == target || true == target.IsDead)
        {
            return;
        }

        target.Hp -= damage;
        this.ActorDamaged?.Invoke(attacker, target, damage);

        if (false == target.IsDead)
        {
            return;
        }

        target.Hp = 0;
        Log($"{target.Name}이(가) 쓰러졌다.");
        this.ActorDied?.Invoke(target);

        // 시체는 칸을 막지 않는다. 액터 목록에서는 스케줄러가 정리한다.
        int index = ToIndex(target.X, target.Y);
        if (true == this.occupancy.TryGetValue(index, out CrawlerActor occupant) && occupant == target)
        {
            this.occupancy.Remove(index);
        }
    }

    // ------------------------------------------------------------------ 시야

    /// <summary>플레이어 기준으로 시야를 다시 계산한다. 플레이어가 움직이거나 문이 여닫힐 때 부른다.</summary>
    public void RecomputePlayerFieldOfView()
    {
        if (null == this.Player)
        {
            return;
        }

        this.fieldOfView.Compute(this.Player.X, this.Player.Y, this.Player.SightRadius, this.Visibility);
    }

    public bool HasLineOfSight(CrawlerActor from, CrawlerActor to)
    {
        return this.fieldOfView.HasLineOfSight(from.X, from.Y, to.X, to.Y);
    }

    public bool IsVisibleToPlayer(int x, int y) => this.Visibility.IsVisible(x, y);

    // ------------------------------------------------------------------ 기타

    public void Log(string message)
    {
        this.MessageLogged?.Invoke(message);
    }

    private int ToIndex(int x, int y) => y * this.map.width + x;
}
