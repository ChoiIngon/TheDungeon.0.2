using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 절차적 던전의 타일 맵.
///
/// 생성 순서
///   1. CreateRooms   : 후보 방을 넉넉히 만들고 겹치지 않게 밀어낸다.
///   2. SelectRooms   : 델로네 → MST 로 만든 트리를 훑어 실제로 쓸 방을 고른다.
///   3. CreateTiles   : 선택된 방을 감싸는 타일 배열을 만들고 방 바닥/외곽을 기록한다.
///   4. ConnectRooms  : 델로네 → MST(+여분 간선) 를 따라 A* 로 복도를 판다.
///   5. BuildWall     : 방 외곽과 복도 주변에 벽을 세우고 문을 판정한다.
///   6. Finalize      : 이동 비용을 정규화하고 이웃 캐시를 만든다.
///   7. Validate      : 결과가 실제로 플레이 가능한지 검증한다.
///
/// 생성은 실패할 수 있다(복도를 못 파는 배치가 나올 수 있다). 그래서 생성자를 직접 열지 않고
/// TryBuild() 로만 만들게 해서, 실패를 호출자가 다른 시드로 재시도할 수 있게 한다.
/// </summary>
public class TileMap
{
    public class Tile
    {
        public enum Type
        {
            None,
            Floor,
            Wall
        }

        /// <summary>
        /// 이웃 8방향. neighbors 배열의 인덱스와 DirectionOffsets 의 인덱스가 이 값과 일치한다.
        ///
        /// LeftTop,    Top,    RightTop,
        /// Left,               Right,
        /// LeftBottom, Bottom, RightBottom
        /// </summary>
        public enum Direction
        {
            LeftTop = 0,
            Top = 1,
            RightTop = 2,
            Left = 3,
            Right = 4,
            LeftBottom = 5,
            Bottom = 6,
            RightBottom = 7,
        }

        public const int DirectionCount = 8;

        /// <summary>
        /// A* 의 타일 진입 비용.
        /// 복도를 팔 때 "이미 뚫린 복도를 재사용하고, 방 안쪽은 되도록 가로지르지 않게" 유도하는 값이다.
        /// 생성이 끝나면 Finalize 단계에서 전부 MinCost 로 정규화된다.
        /// </summary>
        public static class PathCost
        {
            public const int MinCost = 1;
            public const int Corridor = 64;
            public const int Floor = 128;
            public const int Wall = 255;
            public const int MaxCost = 255;
        }

        public readonly int index;
        public Rect rect;
        public Type type;
        public int cost;
        public Room room;
        public Tile[] neighbors;

        public static readonly Vector2Int[] DirectionOffsets = new Vector2Int[] {
            new Vector2Int(-1, +1),
            new Vector2Int( 0, +1),
            new Vector2Int(+1, +1),
            new Vector2Int(-1,  0),
            new Vector2Int(+1,  0),
            new Vector2Int(-1, -1),
            new Vector2Int( 0, -1),
            new Vector2Int(+1, -1),
        };

        public Tile(int index)
        {
            this.index = index;
        }

        /// <summary>
        /// 해당 방향의 이웃 타일. 맵 밖이거나 아직 이웃 캐시가 없으면 null 이다.
        /// (맵 밖 타일은 Finalize 단계에서 null 로 치환되므로 호출자는 항상 null 을 고려해야 한다.)
        /// </summary>
        public Tile GetNeighbor(Direction direction)
        {
            if (null == neighbors)
            {
                return null;
            }

            return neighbors[(int)direction];
        }

        public bool IsFloor => Type.Floor == this.type;
    }

    public class Room
    {
        public readonly int index;
        public Rect rect;
        public List<Room> neighbors;
        public List<Tile> doors;

        /*
         * rect의 x, y는 방의 왼쪽 아래 모서리의 좌표입니다.
         * rect의 width, height는 방의 너비와 높이입니다.
         * 방의 바닥은 rect로 표현된 영역에서 가장자리 1칸을 제외한 영역입니다.
         * eg)
            * (x:10, y:44, width:12, height:10) 인 방이 있다고 가정하면,
            * 방의 왼쪽 아래 모서리는 (10, 44)입니다.
            * 방의 오른쪽 위 모서리는 (21, 53)입니다.
         */

        public Room(int index, float x, float y, float width, float height)
        {
            this.index = index;
            this.rect = new Rect(x, y, width, height);
            this.neighbors = new List<Room>();
            this.doors = new List<Tile>();
        }

        public Vector3 position
        {
            get => new Vector3(this.rect.x, this.rect.y, 0.0f);
        }

        public float x
        {
            set => this.rect.x = value;
            get => this.rect.x;
        }

        public float y
        {
            set => this.rect.y = value;
            get => this.rect.y;
        }

        public Vector2 center
        {
            get => rect.center;
        }

        public Rect GetFloorRect()
        {
            Rect floorRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            floorRect.xMin += 1;
            floorRect.xMax -= 1;
            floorRect.yMin += 1;
            floorRect.yMax -= 1;
            return floorRect;
        }
    }

    public class Corridor
    {
        public int index;
        public List<Tile> tiles = new List<Tile>();
    }

    /// <summary>생성 파라미터.</summary>
    public struct Config
    {
        public int roomCount;
        public int minRoomSize;
        public int maxRoomSize;

        public Config(int roomCount, int minRoomSize, int maxRoomSize)
        {
            this.roomCount = roomCount;
            this.minRoomSize = minRoomSize;
            this.maxRoomSize = maxRoomSize;
        }
    }

    private const int MinRoomSize = 5;

    /// <summary>
    /// 맵 가장자리에 남겨 두는 빈 타일 폭.
    ///
    /// 맵을 방들의 바운딩 박스에 딱 맞추면, 가장자리를 지나는 복도의 바깥쪽에 벽을 세울 자리가 없어
    /// 벽이 뚫린 채로 남는다(BuildWallOnTile 이 맵 밖이라 그냥 반환한다).
    /// </summary>
    private const int MapPadding = 2;

    /// <summary>
    /// 방 밀어내기 반복 상한. 중심이 완전히 겹친 방끼리는 서로를 반대로 밀며 진동할 수 있어
    /// 상한이 없으면 에디터가 응답 없이 멈춘다.
    /// </summary>
    private const int MaxRepositionIterations = 4096;

    private readonly Config config;
    private readonly DungeonRandom random;
    private readonly int minRoomSize;
    private readonly int maxRoomSize;

    public Tile[] tiles { get; private set; }
    public List<Room> rooms { get; private set; }
    public List<Corridor> corridors { get; private set; }

    public int width { get; private set; }
    public int height { get; private set; }

    /// <summary>맵 전체를 덮는 사각형. A* 탐색 범위 기본값으로 쓴다.</summary>
    public Rect Bounds => new Rect(0, 0, width, height);

    private WeightRandom<int> RandomDepthCount = null;

    /// <summary>
    /// 복도 후보 경로를 미리 깔아 볼 때 쓰는 되돌리기 기록.
    /// 중간에 벽을 만나 실패하면 건드린 타일을 전부 원래 값으로 되돌린다.
    /// </summary>
    private class Journal
    {
        public class Original
        {
            public Original(Tile tile)
            {
                this.tile = tile;
                this.type = tile.type;
                this.cost = tile.cost;
            }

            public Tile tile;
            public Tile.Type type;
            public int cost;
        }

        public Stack<Original> originals = new Stack<Original>();

        public void Rollback()
        {
            while (0 < originals.Count)
            {
                var original = originals.Pop();
                original.tile.type = original.type;
                original.tile.cost = original.cost;
            }
        }

        public void Push(Tile tile)
        {
            originals.Push(new Original(tile));
        }
    }

    /// <summary>
    /// 던전을 만든다. 실패하면 false 를 돌려주고 failureReason 에 이유를 담는다.
    /// 실패는 정상적인 결과다(배치 운이 나쁘면 복도를 못 판다). 호출자가 다른 시드로 재시도하면 된다.
    /// </summary>
    public static bool TryBuild(Config config, DungeonRandom random, out TileMap tileMap, out string failureReason)
    {
        tileMap = null;
        failureReason = null;

        if (null == random)
        {
            failureReason = "DungeonRandom is null";
            return false;
        }

        if (2 > config.roomCount)
        {
            failureReason = $"roomCount({config.roomCount}) must be 2 or more";
            return false;
        }

        var candidate = new TileMap(config, random);
        if (false == candidate.Build(out failureReason))
        {
            return false;
        }

        tileMap = candidate;
        return true;
    }

    private TileMap(Config config, DungeonRandom random)
    {
        this.config = config;
        this.random = random;
        this.minRoomSize = Mathf.Max(config.minRoomSize, TileMap.MinRoomSize);
        this.maxRoomSize = Mathf.Max(config.maxRoomSize, this.minRoomSize);

        RandomDepthCount = new WeightRandom<int>(random);
        RandomDepthCount.Add(1, 4);
        RandomDepthCount.Add(45, 3);
        RandomDepthCount.Add(50, 2);
        RandomDepthCount.Add(4, 1);
    }

    private bool Build(out string failureReason)
    {
        List<Room> candidateRooms = CreateRooms();
        List<Room> selectedRooms = SelectRooms(candidateRooms);

        if (2 > selectedRooms.Count)
        {
            failureReason = $"selected room count({selectedRooms.Count}) is too small";
            return false;
        }

        CreateTiles(selectedRooms);

        if (false == ConnectRooms(selectedRooms, out failureReason))
        {
            return false;
        }

        BuildWall();
        FinalizeTiles();

        return Validate(out failureReason);
    }

    /// <summary>
    /// 생성 후처리.
    /// - 아무것도 놓이지 않은(None) 타일은 배열에서 제거해 "맵 밖"으로 취급한다.
    /// - 남은 타일의 이동 비용을 균일하게 되돌린다(생성용 비용 가중치는 여기서 역할이 끝난다).
    /// - 8방향 이웃을 캐시한다.
    /// - 방 안쪽을 지나가는 복도 타일은 복도 목록에서 뺀다(방이 이미 그 바닥을 그린다).
    /// </summary>
    private void FinalizeTiles()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            Tile tile = tiles[i];
            if (null == tile || Tile.Type.None == tile.type)
            {
                tiles[i] = null;
                continue;
            }

            tile.cost = Tile.PathCost.MinCost;
            tile.neighbors = new Tile[Tile.DirectionCount];
        }

        for (int i = 0; i < tiles.Length; i++)
        {
            Tile tile = tiles[i];
            if (null == tile)
            {
                continue;
            }

            for (int direction = 0; direction < Tile.DirectionCount; direction++)
            {
                Vector2Int offset = Tile.DirectionOffsets[direction];
                tile.neighbors[direction] = GetTile((int)tile.rect.x + offset.x, (int)tile.rect.y + offset.y);
            }
        }

        foreach (Corridor corridor in this.corridors)
        {
            corridor.tiles.RemoveAll(tile =>
            {
                if (null == tile.room)
                {
                    return false;
                }

                return tile.room.GetFloorRect().Contains(new Vector2(tile.rect.x, tile.rect.y));
            });
        }
    }

    /// <summary>
    /// 결과가 실제로 플레이 가능한지 검증한다.
    ///
    /// 방 그래프(neighbors)가 이어져 있어도 타일이 이어져 있다는 보장은 없다.
    /// 복도 굴착이 실패하면 논리적으로만 연결된 던전이 나오고, 출구까지 갈 수 없게 된다.
    /// 그래서 타일 기준으로 직접 확인한다.
    /// </summary>
    private bool Validate(out string failureReason)
    {
        failureReason = null;

        // 1. 모든 방에 최소 하나의 문이 있어야 한다.
        foreach (Room room in this.rooms)
        {
            if (0 == room.doors.Count)
            {
                failureReason = $"room {room.index} has no door";
                return false;
            }
        }

        // 2. 모든 바닥 타일이 하나의 4-연결 성분이어야 한다.
        int floorCount = 0;
        Tile seed = null;
        foreach (Tile tile in tiles)
        {
            if (null == tile || false == tile.IsFloor)
            {
                continue;
            }

            floorCount++;
            if (null == seed)
            {
                seed = tile;
            }
        }

        if (null == seed)
        {
            failureReason = "no floor tile";
            return false;
        }

        int reached = CountReachableFloorTiles(seed);
        if (reached != floorCount)
        {
            failureReason = $"dungeon is split: reachable floor {reached} / total {floorCount}";
            return false;
        }

        return true;
    }

    private int CountReachableFloorTiles(Tile seed)
    {
        var visited = new HashSet<int>();
        var queue = new Queue<Tile>();

        visited.Add(seed.index);
        queue.Enqueue(seed);

        Tile.Direction[] directions =
        {
            Tile.Direction.Top,
            Tile.Direction.Right,
            Tile.Direction.Bottom,
            Tile.Direction.Left
        };

        while (0 < queue.Count)
        {
            Tile tile = queue.Dequeue();

            foreach (Tile.Direction direction in directions)
            {
                Tile neighbor = tile.GetNeighbor(direction);
                if (null == neighbor || false == neighbor.IsFloor)
                {
                    continue;
                }

                if (false == visited.Add(neighbor.index))
                {
                    continue;
                }

                queue.Enqueue(neighbor);
            }
        }

        return visited.Count;
    }

    public Tile GetTile(int index)
    {
        if (0 > index || index >= tiles.Length)
        {
            return null;
        }

        return tiles[index];
    }

    public Tile GetTile(int x, int y)
    {
        if (0 > x || x >= width)
        {
            return null;
        }

        if (0 > y || y >= height)
        {
            return null;
        }

        return tiles[y * width + x];
    }

    /// <summary>타일 단위 최단 경로. 경로가 없으면 빈 리스트를 돌려준다(null 이 아니다).</summary>
    public List<Tile> FindPath(Tile from, Tile to)
    {
        AStarPathFinder pathFinder = new AStarPathFinder(this, Bounds, random);
        return pathFinder.FindPath(from, to);
    }

    /// <summary>방 단위 최단 경로(BFS, 홉 수 기준). 경로가 없으면 빈 리스트를 돌려준다(null 이 아니다).</summary>
    public List<Room> FindPath(Room from, Room to)
    {
        List<Room> path = new List<Room>();

        if (null == from || null == to)
        {
            return path;
        }

        Dictionary<Room, Room> parents = new Dictionary<Room, Room>(); // 부모 노드 저장
        Queue<Room> queue = new Queue<Room>();
        queue.Enqueue(from);
        parents[from] = null;  // 시작점의 부모는 없음

        while (queue.Count > 0)
        {
            Room room = queue.Dequeue();
            if (room == to) // 목표 노드 도착
            {
                break;
            }

            foreach (Room neighbor in room.neighbors)
            {
                if (false == parents.ContainsKey(neighbor)) // 방문하지 않은 노드
                {
                    parents[neighbor] = room; // 부모 노드 기록
                    queue.Enqueue(neighbor);
                }
            }
        }

        // 목표 노드까지 경로 추적
        if (false == parents.ContainsKey(to))
        {
            return path; // 도달 불가
        }

        path.Add(to);
        Room parent = parents[to];
        while (null != parent)
        {
            path.Add(parent);
            parent = parents[parent];
        }

        path.Reverse(); // 시작점부터 출력하도록 뒤집기

        return path;
    }

    private List<Room> CreateRooms()
    {
        List<Room> candidateRooms = new List<Room>();
        int index = 1;

        // 삼각분할을 시작하려면 최소 세 점이 필요하므로 씨앗 방 3개를 서로 떨어뜨려 놓는다.
        candidateRooms.Add(CreateRoom(index++, 0, 0));
        candidateRooms.Add(CreateRoom(index++, this.maxRoomSize * 2, 0));
        candidateRooms.Add(CreateRoom(index++, this.maxRoomSize / 2, this.maxRoomSize * 2));

        // 실제로 쓸 개수보다 넉넉히 만들어 두고 SelectRooms 에서 고른다.
        for (int i = 0; i < this.config.roomCount * 2; i++)
        {
            Room room = CreateRoom(index++, candidateRooms);
            candidateRooms.Add(room);
            RepositionRoom(room.center, candidateRooms);
        }

        return candidateRooms;
    }

    private Room CreateRoom(int index, int x, int y)
    {
        int width = GetRandomRoomSize();
        int height = GetRandomRoomSize();
        return new Room(index, x, y, width, height);
    }

    /// <summary>
    /// 기존 방들 사이에서 가장 넓게 비어 있는 자리에 새 방을 놓는다.
    /// 델로네 삼각형의 내접원이 클수록 그 삼각형 안쪽이 비어 있다는 뜻이므로,
    /// 내접원이 가장 큰 삼각형의 중심을 새 방의 자리로 쓴다.
    /// </summary>
    private Room CreateRoom(int index, List<Room> existRooms)
    {
        int width = GetRandomRoomSize();
        int height = GetRandomRoomSize();

        var triangulation = new DelaunayTriangulation(existRooms);

        DelaunayTriangulation.Circle biggestCircle = null;
        foreach (var triangle in triangulation.triangles)
        {
            if (null == triangle.innerCircle)
            {
                continue;
            }

            if (null == biggestCircle || biggestCircle.radius <= triangle.innerCircle.radius)
            {
                biggestCircle = triangle.innerCircle;
            }
        }

        // 삼각형을 하나도 못 만든 경우(전부 축퇴)에도 생성을 중단하지 않는다.
        // 기존 방들의 바깥에 붙여 두면 RepositionRoom 이 알아서 겹침을 풀어 준다.
        if (null == biggestCircle)
        {
            Rect boundary = GetBoundaryRect(existRooms);
            return new Room(index, (int)boundary.xMax, (int)boundary.center.y, width, height);
        }

        int x = (int)biggestCircle.center.x - width / 2;
        int y = (int)biggestCircle.center.y - height / 2;

        return new Room(index, x, y, width, height);
    }

    private int GetRandomRoomSize()
    {
        return random.Range(this.minRoomSize, this.maxRoomSize + 1);
    }

    /// <summary>
    /// 겹친 방들을 한 칸씩 밀어 겹침을 없앤다.
    /// center 는 새로 추가된 방의 중심이며, 그 지점에서 바깥쪽으로 밀어내는 기준이 된다.
    /// </summary>
    private void RepositionRoom(Vector3 center, List<Room> rooms)
    {
        for (int iteration = 0; iteration < MaxRepositionIterations; iteration++)
        {
            // 경계 사각형은 이 반복 안에서는 바뀌지 않으므로 쌍마다 다시 구하지 않는다.
            Rect boundary = GetBoundaryRect(rooms);

            bool overlap = false;
            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    if (true == rooms[i].rect.Overlaps(rooms[j].rect))
                    {
                        ResolveOverlap(center, boundary, rooms[i], rooms[j]);
                        overlap = true;
                    }
                }
            }

            if (false == overlap)
            {
                return;
            }
        }

        // 상한에 걸렸다면 겹친 방이 남아 있을 수 있다. Validate() 가 최종적으로 걸러낸다.
        Debug.LogWarning($"RepositionRoom: {MaxRepositionIterations}회 안에 겹침을 해소하지 못했다.");
    }

    /// <summary>
    /// 겹친 두 방을 한 칸 밀어낸다.
    /// 전체 배치가 세로로 길면 가로로, 가로로 길면 세로로 밀어서 배치가 한쪽으로 길어지는 것을 막는다.
    /// 미는 방향은 center 를 기준으로 바깥쪽이다.
    /// </summary>
    private void ResolveOverlap(Vector3 center, Rect boundary, Room room1, Room room2)
    {
        if (boundary.width < boundary.height) // 전체 배치가 세로로 길다. 그러므로 가로로 밀어낸다.
        {
            if (room1.center.x < room2.center.x) // 두 방 중 room2 가 오른쪽에 있는 경우
            {
                if (center.x < room2.center.x)
                {
                    room2.x += 1; // room2 가 기준점의 오른쪽이면 room2 를 오른쪽으로 한 칸 이동
                }
                else
                {
                    room1.x -= 1; // room2 가 기준점의 왼쪽이면 room1 을 왼쪽으로 한 칸 이동
                }
            }
            else // 두 방 중 room1 이 오른쪽에 있는 경우
            {
                if (center.x < room1.center.x)
                {
                    room1.x += 1; // room1 이 기준점의 오른쪽이면 room1 을 오른쪽으로 한 칸 이동
                }
                else
                {
                    room2.x -= 1; // room1 이 기준점의 왼쪽이면 room2 를 왼쪽으로 한 칸 이동
                }
            }
        }
        else // 전체 배치가 가로로 길다. 그러므로 세로로 밀어낸다.
        {
            if (room1.center.y < room2.center.y) // 두 방 중 room2 가 위에 있는 경우
            {
                if (center.y < room2.center.y)
                {
                    room2.y += 1; // room2 가 기준점의 위쪽이면 room2 를 위로 한 칸 이동
                }
                else
                {
                    room1.y -= 1; // room2 가 기준점의 아래쪽이면 room1 을 아래로 한 칸 이동
                }
            }
            else // 두 방 중 room1 이 위에 있는 경우
            {
                if (center.y < room1.center.y)
                {
                    room1.y += 1; // room1 이 기준점의 위쪽이면 room1 을 위로 한 칸 이동
                }
                else
                {
                    room2.y -= 1;  // room1 이 기준점의 아래쪽이면 room2 를 아래로 한 칸 이동
                }
            }
        }
    }

    private Rect GetBoundaryRect(List<Room> rooms)
    {
        Rect boundary = new Rect();
        boundary.xMin = float.MaxValue;
        boundary.yMin = float.MaxValue;
        boundary.xMax = float.MinValue;
        boundary.yMax = float.MinValue;
        foreach (Room room in rooms)
        {
            boundary.xMin = Mathf.Min(boundary.xMin, room.rect.xMin);
            boundary.yMin = Mathf.Min(boundary.yMin, room.rect.yMin);
            boundary.xMax = Mathf.Max(boundary.xMax, room.rect.xMax);
            boundary.yMax = Mathf.Max(boundary.yMax, room.rect.yMax);
        }
        return boundary;
    }

    /// <summary>두 방만 감싸는 사각형. 리스트를 새로 만들지 않기 위한 오버로드.</summary>
    private static Rect GetBoundaryRect(Room a, Room b)
    {
        Rect boundary = new Rect();
        boundary.xMin = Mathf.Min(a.rect.xMin, b.rect.xMin);
        boundary.yMin = Mathf.Min(a.rect.yMin, b.rect.yMin);
        boundary.xMax = Mathf.Max(a.rect.xMax, b.rect.xMax);
        boundary.yMax = Mathf.Max(a.rect.yMax, b.rect.yMax);
        return boundary;
    }

    /// <summary>
    /// 후보 방 중 실제로 쓸 방을 고른다.
    /// 델로네 → MST 로 만든 트리를 무작위로 훑으면서 일정 간격(RandomDepthCount)마다 방을 채택한다.
    /// 서로 인접한 방만 뽑히면 던전이 뭉치므로, 간격을 두어 적당히 흩어지게 만든다.
    /// </summary>
    private List<Room> SelectRooms(List<Room> candidateRooms)
    {
        this.rooms = new List<Room>();

        var triangulation = new DelaunayTriangulation(candidateRooms);
        var mst = new MinimumSpanningTree(candidateRooms);
        foreach (var triangle in triangulation.triangles)
        {
            foreach (var edge in triangle.edges)
            {
                mst.AddEdge(new MinimumSpanningTree.Edge(edge.v0.room, edge.v1.room, Vector3.Distance(edge.v0.room.rect.center, edge.v1.room.rect.center)));
            }
        }

        mst.BuildTree();

        foreach (var connection in mst.connections)
        {
            connection.room1.neighbors.Add(connection.room2);
            connection.room2.neighbors.Add(connection.room1);
        }

        Room startRoom = candidateRooms[random.Range(0, candidateRooms.Count)];

        SelectRoom(startRoom, 1);

        // 트리 순회만으로 목표 개수를 못 채웠다면 남은 후보로 채운다.
        while (this.config.roomCount > this.rooms.Count && 0 < candidateRooms.Count)
        {
            Room room = candidateRooms[0];
            if (false == this.rooms.Contains(room))
            {
                this.rooms.Add(room);
            }
            candidateRooms.RemoveAt(0);
        }

        // 여기서 만든 인접 관계는 "고르기" 위한 임시 그래프다.
        // 실제 연결은 ConnectRooms 가 선택된 방만으로 다시 계산한다.
        foreach (Room room in this.rooms)
        {
            room.neighbors.Clear();
        }

        return this.rooms;
    }

    private void SelectRoom(Room room, int depth)
    {
        depth--;

        if (0 == depth)
        {
            depth = RandomDepthCount.Random();
            this.rooms.Add(room);
        }

        // MST 는 트리(사이클 없음)이므로, 지나온 간선을 지우면 같은 방을 두 번 방문하지 않는다.
        while (0 < room.neighbors.Count && this.config.roomCount > this.rooms.Count)
        {
            int index = random.Range(0, room.neighbors.Count);
            Room neighbor = room.neighbors[index];

            neighbor.neighbors.Remove(room);
            room.neighbors.RemoveAt(index);

            SelectRoom(neighbor, depth);
        }
    }

    /// <summary>
    /// 선택된 방들을 담을 타일 배열을 만들고, 방의 바닥과 외곽을 기록한다.
    /// 방 좌표는 여기서 (0,0) 기준으로 옮겨지며, 가장자리에는 MapPadding 만큼 여백을 남긴다.
    /// </summary>
    private void CreateTiles(List<Room> selectedRooms)
    {
        Rect roomsBoundary = GetBoundaryRect(selectedRooms);

        this.width = (int)roomsBoundary.width + MapPadding * 2;
        this.height = (int)roomsBoundary.height + MapPadding * 2;
        this.tiles = new Tile[width * height];

        for (int i = 0; i < tiles.Length; i++)
        {
            Tile tile = new Tile(i);
            tile.rect = new Rect(i % width, i / width, 1, 1);
            tile.type = Tile.Type.None;
            tile.cost = Tile.PathCost.MaxCost;
            this.tiles[i] = tile;
        }

        float offsetX = roomsBoundary.xMin - MapPadding;
        float offsetY = roomsBoundary.yMin - MapPadding;

        foreach (Room room in this.rooms)
        {
            room.rect.x -= offsetX;
            room.rect.y -= offsetY;

            // 외곽 한 줄은 일단 Floor 로 두고 비용만 최대로 올려 둔다.
            // 복도가 이 줄을 지나가면(=문이 되면) 비용이 내려가고, 그렇지 않으면 BuildWall 이 벽으로 바꾼다.
            for (int x = (int)room.rect.xMin; x < (int)room.rect.xMax; x++)
            {
                Tile top = GetTile(x, (int)room.rect.yMax - 1);
                top.type = Tile.Type.Floor;
                top.cost = Tile.PathCost.MaxCost;

                Tile bottom = GetTile(x, (int)room.rect.yMin);
                bottom.type = Tile.Type.Floor;
                bottom.cost = Tile.PathCost.MaxCost;
            }

            for (int y = (int)room.rect.yMin; y < (int)room.rect.yMax; y++)
            {
                Tile left = GetTile((int)room.rect.xMin, y);
                left.type = Tile.Type.Floor;
                left.cost = Tile.PathCost.MaxCost;

                Tile right = GetTile((int)room.rect.xMax - 1, y);
                right.type = Tile.Type.Floor;
                right.cost = Tile.PathCost.MaxCost;
            }

            // 네 모서리는 문이 될 수 없으므로 곧바로 벽으로 못박는다.
            {
                GetTile((int)room.rect.xMin, (int)room.rect.yMax - 1).type = Tile.Type.Wall;
                GetTile((int)room.rect.xMax - 1, (int)room.rect.yMax - 1).type = Tile.Type.Wall;
                GetTile((int)room.rect.xMin, (int)room.rect.yMin).type = Tile.Type.Wall;
                GetTile((int)room.rect.xMax - 1, (int)room.rect.yMin).type = Tile.Type.Wall;
            }

            // 방 안쪽 바닥을 Floor 로 채운다.
            // 비용을 높게 주어 복도가 방을 가로지르는 것을 억제한다(막지는 않는다).
            Rect floorRect = room.GetFloorRect();
            for (int y = (int)floorRect.yMin; y < (int)floorRect.yMax; y++)
            {
                for (int x = (int)floorRect.xMin; x < (int)floorRect.xMax; x++)
                {
                    Tile floor = GetTile(x, y);
                    floor.type = Tile.Type.Floor;
                    floor.cost = Tile.PathCost.Floor;
                }
            }

            Rect roomRect = room.rect;
            for (int y = (int)roomRect.yMin; y < (int)roomRect.yMax; y++)
            {
                for (int x = (int)roomRect.xMin; x < (int)roomRect.xMax; x++)
                {
                    GetTile(x, y).room = room;
                }
            }
        }
    }

    /// <summary>
    /// 방들을 복도로 잇는다.
    /// MST 로 최소 연결을 만든 뒤, 일부 간선을 더 추가해서 순환로(돌아가는 길)를 만든다.
    /// 굴착에 실패한 간선은 인접 관계로 기록하지 않는다.
    /// 그래야 "논리적으로는 이웃인데 실제로는 못 가는" 상태가 생기지 않는다.
    /// </summary>
    private bool ConnectRooms(List<Room> selectedRooms, out string failureReason)
    {
        failureReason = null;
        this.corridors = new List<Corridor>();

        int requiredConnections = selectedRooms.Count - 1;

        var mst = BuildRoomGraph(selectedRooms, useCompleteGraph: false);
        if (mst.connections.Count != requiredConnections)
        {
            // 방이 3개 미만이거나 중심이 한 줄로 늘어서면 삼각형이 만들어지지 않아 간선이 부족하다.
            // 이럴 때는 모든 방 쌍을 후보로 넣고 크루스칼이 고르게 한다.
            mst = BuildRoomGraph(selectedRooms, useCompleteGraph: true);
        }

        if (mst.connections.Count != requiredConnections)
        {
            failureReason = $"spanning tree covers {mst.connections.Count + 1} of {selectedRooms.Count} rooms";
            return false;
        }

        foreach (var edge in mst.edges)
        {
            if (false == random.Chance(12.5f)) // 12.5% 확률로 여분 간선 추가
            {
                continue;
            }

            if (true == mst.connections.Contains(edge))
            {
                continue;
            }

            mst.connections.Add(edge);
        }

        foreach (var connection in mst.connections)
        {
            if (false == ConnectRoom(connection.room1, connection.room2))
            {
                continue;
            }

            connection.room1.neighbors.Add(connection.room2);
            connection.room2.neighbors.Add(connection.room1);
        }

        return true;
    }

    /// <summary>
    /// 방 사이의 연결 후보 간선을 모아 최소 신장 트리를 만든다.
    /// 기본은 델로네 삼각분할이 만든 간선만 쓰고, 그것으로 전부를 잇지 못할 때만 완전 그래프로 물러선다.
    /// </summary>
    private MinimumSpanningTree BuildRoomGraph(List<Room> selectedRooms, bool useCompleteGraph)
    {
        var mst = new MinimumSpanningTree(selectedRooms);

        if (true == useCompleteGraph)
        {
            for (int i = 0; i < selectedRooms.Count; i++)
            {
                for (int j = i + 1; j < selectedRooms.Count; j++)
                {
                    Room a = selectedRooms[i];
                    Room b = selectedRooms[j];
                    mst.AddEdge(new MinimumSpanningTree.Edge(a, b, Vector3.Distance(a.rect.center, b.rect.center)));
                }
            }
        }
        else
        {
            var triangulation = new DelaunayTriangulation(selectedRooms);
            foreach (var triangle in triangulation.triangles)
            {
                foreach (var edge in triangle.edges)
                {
                    mst.AddEdge(new MinimumSpanningTree.Edge(edge.v0.room, edge.v1.room, Vector3.Distance(edge.v0.room.rect.center, edge.v1.room.rect.center)));
                }
            }
        }

        mst.BuildTree();
        return mst;
    }

    /// <summary>두 방을 잇는 복도를 판다. 성공하면 true.</summary>
    private bool ConnectRoom(Room a, Room b)
    {
        float xMin = Mathf.Max(a.rect.xMin, b.rect.xMin);
        float xMax = Mathf.Min(a.rect.xMax, b.rect.xMax);
        float yMin = Mathf.Max(a.rect.yMin, b.rect.yMin);
        float yMax = Mathf.Min(a.rect.yMax, b.rect.yMax);

        List<Vector3> positions = null;
        if (3 <= xMax - xMin)       // x축이 겹친다. 세로 복도를 만든다.
        {
            positions = ConnectVerticalRoom(a, b);
        }
        else if (3 <= yMax - yMin)  // y축이 겹친다. 가로 복도를 만든다.
        {
            positions = ConnectHorizontalRoom(a, b);
        }
        else                        // 겹치는 축이 없다. 한 번 꺾이는 복도를 만든다.
        {
            positions = ConnectDiagonalRoom(a, b);
        }

        // 미리 깔기에 실패했으면 두 방의 중심을 잇는 것으로 대체한다.
        // 이때도 A* 가 벽을 뚫지는 못하므로, 길이 없으면 아래에서 실패로 처리된다.
        Vector3 startPosition = a.center;
        Vector3 endPosition = b.center;

        if (null != positions && 2 <= positions.Count)
        {
            startPosition = positions[0];
            endPosition = positions[positions.Count - 1];
        }

        var start = GetTile((int)startPosition.x, (int)startPosition.y);
        var end = GetTile((int)endPosition.x, (int)endPosition.y);
        if (null == start || null == end)
        {
            return false;
        }

        Rect searchBoundary = GetBoundaryRect(a, b);
        AStarPathFinder pathFinder = new AStarPathFinder(this, searchBoundary, random);
        var path = pathFinder.FindPath(start, end);

        if (0 == path.Count)
        {
            return false;
        }

        Corridor corridor = new Corridor();
        corridor.index = a.index;
        corridor.tiles = path;
        corridors.Add(corridor);

        foreach (var tile in corridor.tiles)
        {
            tile.type = Tile.Type.Floor;
            tile.cost = Tile.PathCost.MinCost;
        }

        return true;
    }

    private List<Vector3> ConnectVerticalRoom(Room a, Room b)
    {
        Room upperRoom = null;
        Room bottomRoom = null;

        if (a.center.y > b.center.y)
        {
            upperRoom = a;
            bottomRoom = b;
        }
        else
        {
            upperRoom = b;
            bottomRoom = a;
        }

        int xMin = (int)Mathf.Max(a.rect.xMin, b.rect.xMin);
        int xMax = (int)Mathf.Min(a.rect.xMax, b.rect.xMax);
        int x = random.Range(xMin + 1, xMax - 1);

        int upperY = (int)upperRoom.rect.center.y;
        int bottomY = (int)bottomRoom.rect.center.y;

        Vector3 start = new Vector3(x, upperY);
        Vector3 end = new Vector3(x, bottomY);
        List<Vector3> positions = new List<Vector3>() { start, end };
        if (false == AdjustTileCostOnCorridor(positions))
        {
            return null;
        }
        return positions;
    }

    private List<Vector3> ConnectHorizontalRoom(Room a, Room b)
    {
        Room leftRoom = null;
        Room rightRoom = null;

        if (a.center.x > b.center.x)
        {
            rightRoom = a;
            leftRoom = b;
        }
        else
        {
            rightRoom = b;
            leftRoom = a;
        }

        int yMin = (int)Mathf.Max(a.rect.yMin, b.rect.yMin);
        int yMax = (int)Mathf.Min(a.rect.yMax, b.rect.yMax);
        int y = random.Range(yMin + 1, yMax - 1);

        int leftX = (int)leftRoom.rect.center.x;
        int rightX = (int)rightRoom.rect.center.x;

        Vector3 start = new Vector3(leftX, y);
        Vector3 end = new Vector3(rightX, y);
        List<Vector3> positions = new List<Vector3>() { start, end };
        if (false == AdjustTileCostOnCorridor(positions))
        {
            return null;
        }
        return positions;
    }

    /// <summary>
    /// 겹치는 축이 없는 두 방을 한 번 꺾인 복도로 잇는다.
    /// 꺾이는 방향은 두 가지(먼저 세로로 / 먼저 가로로)이며, 무작위로 하나를 고르고 실패하면 나머지를 쓴다.
    /// </summary>
    private List<Vector3> ConnectDiagonalRoom(Room a, Room b)
    {
        int xMin = (int)Mathf.Max(a.rect.xMin, b.rect.xMin);
        int xMax = (int)Mathf.Min(a.rect.xMax, b.rect.xMax);
        int xOverlap = Mathf.Max(0, xMax - xMin);

        int yMin = (int)Mathf.Max(a.rect.yMin, b.rect.yMin);
        int yMax = (int)Mathf.Min(a.rect.yMax, b.rect.yMax);
        int yOverlap = Mathf.Max(0, yMax - yMin);

        Room upperRoom = null;
        Room bottomRoom = null;
        if (a.center.y > b.center.y)
        {
            upperRoom = a;
            bottomRoom = b;
        }
        else
        {
            upperRoom = b;
            bottomRoom = a;
        }

        Room leftRoom = null;
        Room rightRoom = null;
        if (a.center.x > b.center.x)
        {
            rightRoom = a;
            leftRoom = b;
        }
        else
        {
            rightRoom = b;
            leftRoom = a;
        }

        int upperRoomY = (int)random.Range(upperRoom.rect.yMin + 1 + yOverlap, upperRoom.rect.yMax - 2);
        int bottomRoomY = (int)random.Range(bottomRoom.rect.yMin + 1, bottomRoom.rect.yMax - 2 - yOverlap);
        int leftRoomX = (int)random.Range(leftRoom.rect.xMin + 1, leftRoom.rect.xMax - 2 - xOverlap);
        int rightRoomX = (int)random.Range(rightRoom.rect.xMin + 1 + xOverlap, rightRoom.rect.xMax - 2);

        if (upperRoom == leftRoom)
        {
            var corridorBL = new List<Vector3>() {
                new Vector3(leftRoomX,               leftRoom.rect.center.y),
                new Vector3(leftRoomX,               bottomRoomY),
                new Vector3(rightRoom.rect.center.x, bottomRoomY)
            };

            var corridorRT = new List<Vector3>()
            {
                new Vector3(leftRoom.rect.center.x,  upperRoomY),
                new Vector3(rightRoomX,              upperRoomY),
                new Vector3(rightRoomX,              rightRoom.rect.center.y)
            };

            return SelectCorridor(corridorBL, corridorRT);
        }

        if (bottomRoom == leftRoom)
        {
            var corridorTL = new List<Vector3>()
            {
                new Vector3(leftRoomX,               bottomRoom.rect.center.y),
                new Vector3(leftRoomX,               upperRoomY),
                new Vector3(rightRoom.rect.center.x, upperRoomY)
            };

            var corridorRB = new List<Vector3>()
            {
                new Vector3(leftRoom.rect.center.x,  bottomRoomY),
                new Vector3(rightRoomX,              bottomRoomY),
                new Vector3(rightRoomX,              upperRoom.rect.center.y)
            };

            return SelectCorridor(corridorTL, corridorRB);
        }

        return null;
    }

    /// <summary>두 후보 경로 중 하나를 무작위로 고르고, 실패하면 나머지를 시도한다.</summary>
    private List<Vector3> SelectCorridor(List<Vector3> first, List<Vector3> second)
    {
        List<Vector3> positions = (0 == random.Range(0, 2)) ? first : second;
        if (true == AdjustTileCostOnCorridor(positions))
        {
            return positions;
        }

        positions = (first == positions) ? second : first;
        if (true == AdjustTileCostOnCorridor(positions))
        {
            return positions;
        }

        return null;
    }

    /// <summary>
    /// 복도가 지날 자리의 이동 비용을 미리 낮춰서 A* 가 그 경로를 따라오게 유도한다.
    /// positions 는 꺾임점 목록이며, 이웃한 두 점은 항상 같은 x 또는 같은 y 를 공유한다(직선 구간).
    /// 도중에 벽을 만나면 지금까지 바꾼 값을 전부 되돌리고 실패를 알린다.
    /// </summary>
    private bool AdjustTileCostOnCorridor(List<Vector3> positions)
    {
        if (null == positions || 2 > positions.Count)
        {
            return false;
        }

        Journal journal = new Journal();
        for (int start = 0; start < positions.Count - 1; start++)
        {
            Vector3 startPosition = positions[start];
            Vector3 endPosition = positions[start + 1];

            int xMin = (int)Mathf.Min(startPosition.x, endPosition.x);
            int xMax = (int)Mathf.Max(startPosition.x, endPosition.x);
            int yMin = (int)Mathf.Min(startPosition.y, endPosition.y);
            int yMax = (int)Mathf.Max(startPosition.y, endPosition.y);

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    var tile = this.GetTile(x, y);
                    if (null == tile)
                    {
                        journal.Rollback();
                        return false;
                    }

                    if (Tile.Type.Wall == tile.type)
                    {
                        journal.Rollback();
                        return false;
                    }

                    journal.Push(tile);
                    tile.cost = Mathf.Min(tile.cost, Tile.PathCost.Corridor);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 벽을 세운다.
    /// 방 외곽 중 복도가 지나간 자리(비용이 MinCost 로 내려간 타일)는 문이 되고, 나머지는 벽이 된다.
    /// 복도 주변의 빈 타일에는 바깥벽을 세운다.
    /// </summary>
    private void BuildWall()
    {
        foreach (var room in this.rooms)
        {
            for (int x = (int)room.rect.xMin; x < (int)room.rect.xMax; x++)
            {
                BuildWallOrDoor(room, GetTile(x, (int)room.rect.yMax - 1));
                BuildWallOrDoor(room, GetTile(x, (int)room.rect.yMin));
            }

            for (int y = (int)room.rect.yMin; y < (int)room.rect.yMax; y++)
            {
                BuildWallOrDoor(room, GetTile((int)room.rect.xMin, y));
                BuildWallOrDoor(room, GetTile((int)room.rect.xMax - 1, y));
            }
        }

        foreach (var corridor in corridors)
        {
            foreach (Tile tile in corridor.tiles)
            {
                int x = (int)tile.rect.x;
                int y = (int)tile.rect.y;

                foreach (var offset in Tile.DirectionOffsets)
                {
                    BuildWallOnTile(x + offset.x, y + offset.y);
                }
            }
        }
    }

    private void BuildWallOrDoor(Room room, Tile tile)
    {
        if (null == tile)
        {
            return;
        }

        if (Tile.PathCost.MinCost < tile.cost)
        {
            tile.type = Tile.Type.Wall;
            return;
        }

        // 같은 타일이 위/아래 순회와 좌/우 순회에서 두 번 걸릴 수 있다(모서리는 이미 Wall 이라 제외되지만
        // 방어적으로 중복 등록을 막는다).
        if (false == room.doors.Contains(tile))
        {
            room.doors.Add(tile);
        }
    }

    private void BuildWallOnTile(int x, int y)
    {
        Tile tile = GetTile(x, y);
        if (null == tile)
        {
            return;
        }

        if (Tile.Type.None != tile.type)
        {
            return;
        }

        tile.type = Tile.Type.Wall;
        tile.cost = Tile.PathCost.Wall;
    }
}
