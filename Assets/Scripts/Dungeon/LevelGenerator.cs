using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 완성된 TileMap 위에 "레벨"을 배치한다.
/// 시작 계단 / 출구 계단 위치를 정하고, 잠글 방을 고른다.
/// </summary>
public class LevelGenerator
{
    /// <summary>
    /// 맵이 낼 수 있는 최장 경로 중 실제 여정으로 사용할 비율.
    ///
    /// 예전에는 여기가 50타일 고정 상한이었다. 그 결과 방을 10개에서 40개로 늘려도
    /// 실제 이동 거리는 53 → 55타일로 거의 변하지 않았고, roomCount 는 난이도나 분량이 아니라
    /// "지나칠 방의 개수"를 늘리는 값이 되어 버렸다.
    /// 맵 크기에 비례하게 바꿔서 roomCount 가 실제 분량 조절 값이 되도록 한다.
    ///
    /// 1.0 이면 가장 먼 두 방을 그대로 시작/출구로 쓴다.
    /// 0.75 는 기존 기본 설정(roomCount=10)에서의 실사용 비율과 비슷하도록 맞춘 값이다.
    /// </summary>
    private const float JourneyLengthRatio = 0.75f;

    /// <summary>
    /// 여정 길이 하한(타일). 맵이 작아서 최장 경로가 이보다 짧으면 잘라내지 않는다.
    /// </summary>
    private const int MinJourneyTileCount = 30;

    private readonly TileMap tileMap;
    private readonly DungeonRandom random;

    /// <summary>
    /// 방 쌍 사이의 경로를 캐시하기 위한 키.
    ///
    /// IEquatable 을 구현하지 않으면 Dictionary 가 ValueType.Equals 로 떨어져
    /// 리플렉션 기반 필드 비교 + 박싱이 매 조회마다 일어난다. 항목이 O(방 개수²)라 그대로 두면 비싸다.
    /// </summary>
    public struct PathKey : IEquatable<PathKey>
    {
        public PathKey(TileMap.Room start, TileMap.Room end)
        {
            this.start = start;
            this.end = end;
        }

        public TileMap.Room start;
        public TileMap.Room end;

        public bool Equals(PathKey other)
        {
            return ReferenceEquals(this.start, other.start) && ReferenceEquals(this.end, other.end);
        }

        public override bool Equals(object obj)
        {
            return obj is PathKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            int startHash = (null != start) ? start.index : 0;
            int endHash = (null != end) ? end.index : 0;
            return startHash * 397 ^ endHash;
        }
    }

    /// <summary>
    /// 방 쌍의 최단 경로 캐시. 무향 그래프이므로 (a,b) 한 방향만 저장하고
    /// 반대 방향은 GetPath() 가 뒤집어서 돌려준다.
    /// </summary>
    private readonly Dictionary<PathKey, List<TileMap.Room>> paths = new Dictionary<PathKey, List<TileMap.Room>>();

    public TileMap.Room StartRoom { get; private set; }
    public TileMap.Room EndRoom { get; private set; }

    /// <summary>잠겨야 하는 방. 적당한 방이 없으면 null 이다.</summary>
    public TileMap.Room LockedRoom { get; private set; }

    public TileMap.Tile Start { get; private set; }
    public TileMap.Tile End { get; private set; }

    /// <summary>시작/출구 지점을 정상적으로 잡았는지 여부.</summary>
    public bool IsValid => null != Start && null != End && Start != End;

    public LevelGenerator(TileMap tileMap, DungeonRandom random)
    {
        this.tileMap = tileMap;
        this.random = random;

        InitializeRoomPaths();
        InitializeGates();
        SelectLockedRoom();
    }

    private void InitializeRoomPaths()
    {
        for (int i = 0; i < tileMap.rooms.Count; i++)
        {
            for (int j = i + 1; j < tileMap.rooms.Count; j++)
            {
                TileMap.Room start = tileMap.rooms[i];
                TileMap.Room end = tileMap.rooms[j];
                paths[new PathKey(start, end)] = tileMap.FindPath(start, end);
            }
        }
    }

    /// <summary>from → to 최단 경로. 도달할 수 없으면 빈 리스트.</summary>
    public List<TileMap.Room> GetPath(TileMap.Room from, TileMap.Room to)
    {
        if (true == paths.TryGetValue(new PathKey(from, to), out List<TileMap.Room> forward))
        {
            return forward;
        }

        if (true == paths.TryGetValue(new PathKey(to, from), out List<TileMap.Room> backward))
        {
            var reversed = new List<TileMap.Room>(backward);
            reversed.Reverse();
            return reversed;
        }

        return new List<TileMap.Room>();
    }

    /// <summary>
    /// 시작 방과 출구 방을 정한다.
    /// 가장 먼 두 방을 고른 뒤, 문이 적은 쪽(막다른 방에 가까운 쪽)을 출구로 삼는다.
    /// </summary>
    private void InitializeGates()
    {
        List<TileMap.Room> furthestPath = GetFurthestPath();
        if (0 == furthestPath.Count)
        {
            Debug.LogWarning("LevelGenerator: 방 사이 경로를 찾지 못했다.");
            return;
        }

        this.StartRoom = furthestPath[0];
        this.EndRoom = furthestPath[furthestPath.Count - 1];

        if (this.StartRoom == this.EndRoom)
        {
            Debug.LogWarning("LevelGenerator: 시작 방과 출구 방을 분리할 수 없다.");
            return;
        }

        if (this.StartRoom.doors.Count < this.EndRoom.doors.Count)
        {
            TileMap.Room tmp = StartRoom;
            this.StartRoom = EndRoom;
            this.EndRoom = tmp;
        }

        AdjustStartRoomByJourneyLength();

        this.Start = GetRandomTileInRoom(StartRoom);
        this.End = GetRandomTileInRoom(EndRoom);

        if (null == Start || null == End)
        {
            Debug.LogWarning("LevelGenerator: 시작/출구 타일을 잡지 못했다.");
            return;
        }

        if (Start == End)
        {
            Debug.LogWarning("LevelGenerator: 시작 타일과 출구 타일이 같다.");
            this.Start = null;
            this.End = null;
        }
    }

    /// <summary>
    /// 여정 길이를 맵 크기에 비례하도록 맞춘다.
    /// 출구에서 시작 방까지의 타일 경로를 따라가다가, 예산 거리만큼 떨어진 지점의 방을 시작 방으로 삼는다.
    /// </summary>
    private void AdjustStartRoomByJourneyLength()
    {
        TileMap.Tile startTile = tileMap.GetTile((int)this.StartRoom.rect.center.x, (int)this.StartRoom.rect.center.y);
        TileMap.Tile endTile = tileMap.GetTile((int)this.EndRoom.rect.center.x, (int)this.EndRoom.rect.center.y);

        if (null == startTile || null == endTile)
        {
            return;
        }

        // 경로가 없으면 빈 리스트가 온다. TileMap.Validate() 를 통과했다면 여기서 비어 있을 수 없다.
        List<TileMap.Tile> tilePath = tileMap.FindPath(endTile, startTile);

        int budget = Mathf.Max(MinJourneyTileCount, Mathf.RoundToInt(tilePath.Count * JourneyLengthRatio));
        if (tilePath.Count <= budget)
        {
            return;
        }

        for (int i = budget; i < tilePath.Count; i++)
        {
            TileMap.Room room = tilePath[i].room;
            if (null == room || room == this.EndRoom)
            {
                continue;
            }

            this.StartRoom = room;
            return;
        }
    }

    /// <summary>
    /// 잠겨야 하는 방을 선택합니다.
    /// 1. StartRoom과 EndRoom을 제외한 방 중에서 선택
    /// 2. StartRoom에서 도달 가능한 위치에 있는 방 중에서 선택
    /// </summary>
    private void SelectLockedRoom()
    {
        this.LockedRoom = null;

        if (null == StartRoom || null == EndRoom)
        {
            return;
        }

        List<TileMap.Room> candidateRooms = new List<TileMap.Room>();

        foreach (TileMap.Room room in tileMap.rooms)
        {
            if (room == StartRoom || room == EndRoom)
            {
                continue;
            }

            // StartRoom 에서 도달 가능한 방만 후보로 삼는다.
            if (0 == GetPath(StartRoom, room).Count)
            {
                continue;
            }

            candidateRooms.Add(room);
        }

        if (0 == candidateRooms.Count)
        {
            Debug.LogWarning("No suitable room found for locking.");
            return;
        }

        this.LockedRoom = candidateRooms[random.Range(0, candidateRooms.Count)];
        Debug.Log($"Locked room selected: Room {LockedRoom.index}");
    }

    /// <summary>캐시된 방 경로 중 가장 긴 것(홉 수 기준). 없으면 빈 리스트.</summary>
    private List<TileMap.Room> GetFurthestPath()
    {
        List<TileMap.Room> furthestPath = new List<TileMap.Room>();
        foreach (var pair in paths)
        {
            if (null == pair.Value)
            {
                continue;
            }

            if (furthestPath.Count < pair.Value.Count)
            {
                furthestPath = pair.Value;
            }
        }

        return furthestPath;
    }

    /// <summary>
    /// 방 바닥 안쪽에서 임의의 타일을 고른다.
    /// 계단이 벽에 붙으면 플레이어를 내려놓을 자리가 없으므로 바닥에서 한 칸 더 안쪽으로 좁힌다.
    /// </summary>
    private TileMap.Tile GetRandomTileInRoom(TileMap.Room room)
    {
        if (null == room)
        {
            return null;
        }

        Rect floorRect = room.GetFloorRect();

        int x = (int)random.Range(floorRect.xMin + 1, floorRect.xMax - 1);
        int y = (int)random.Range(floorRect.yMin + 1, floorRect.yMax - 1);

        TileMap.Tile tile = tileMap.GetTile(x, y);
        if (null != tile && true == tile.IsFloor)
        {
            return tile;
        }

        // 방이 최소 크기라 안쪽으로 좁힐 여지가 없으면 중심 타일로 대체한다.
        return tileMap.GetTile((int)room.rect.center.x, (int)room.rect.center.y);
    }
}
