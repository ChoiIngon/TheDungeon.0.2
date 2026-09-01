using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEngine;
using static TileMap;

public class Dungeon : MonoBehaviour
{
    public const float TileSize = 4.5f;
    public const float TileOffset = TileSize / 2f;
    public const float WallHeight = 5;
    public const float FloorHeightOffset = -0.25f;
    public const string DungeonTileLayerName = "DungeonTile";

    /// <summary>
    /// 생성 재시도 횟수.
    /// 방 배치 운에 따라 복도를 못 파는 경우가 있고, 그 결과는 클리어 불가능한 던전이다.
    /// TileMap.TryBuild() 가 그런 결과를 걸러내므로 시드를 바꿔 다시 시도한다.
    /// </summary>
    private const int MaxGenerationAttempts = 16;

    [Header("Dungeon Object Prefabs")]
    public GameObject doorStandPrefab;
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject torchPrefab;
    public GameObject upStairPrefab;
    public GameObject downStairPrefab;
    public GameObject ceilPrefab;

    [Header("Dungeon Generation Settings")]
    public int randomSeed = 0;

    [Min(2)]
    [Tooltip("생성할 방의 개수. 여정 길이가 맵 크기에 비례하므로 이 값이 곧 분량 조절 값이다.")]
    public int roomCount = 10;

    [Min(TileMap.MinRoomSize)]
    [Tooltip("방 한 변의 최소 길이. 가장자리 한 줄이 벽이라 " + nameof(TileMap) + ".MinRoomSize 미만은 방 구실을 못 한다.")]
    public int minRoomSize = TileMap.MinRoomSize;

    [Min(TileMap.MinRoomSize)]
    public int maxRoomSize = 7;

    [Header("Unit Object Settings")]
    public GameObject player;
    public GameObject enemyPrefab;

    [Header("Build Options")]
    [Tooltip("천장을 만든다. 위에서 내려다보는 쿼터뷰에서는 꺼야 안이 보인다.")]
    public bool buildCeiling = true;

    [Tooltip("NavMesh 를 굽는다. 그리드 턴제(Crawler)에서는 쓰지 않으므로 꺼도 된다.")]
    public bool buildNavMesh = true;

    [Tooltip("플레이어를 배치하고 적을 소환한다. 끄면 지형만 만들고 배치는 호출자가 맡는다.")]
    public bool spawnActors = true;

    /// <summary>
    /// 한 타일에 딸린 표현 오브젝트.
    /// 안개(fog of war)처럼 "타일 단위로 보이고 안 보이고"를 다루려면 타일과 오브젝트의 대응이 필요하다.
    /// 이름을 파싱하는 대신 만들 때 바로 기록해 둔다.
    /// </summary>
    public class TileVisual
    {
        /// <summary>이 타일에 속한 모든 오브젝트(바닥, 천장, 벽, 문, 계단).</summary>
        public readonly List<GameObject> objects = new List<GameObject>();

        /// <summary>그중 벽만 따로 모은 것. 카메라 방향에 따라 가려야 하기 때문에 구분해 둔다.</summary>
        public readonly List<GameObject> walls = new List<GameObject>();
    }

    TileMap tileMap = null;
    LevelGenerator levelGenerator = null;
    DungeonRandom random = null;

    private readonly Dictionary<int, TileVisual> tileVisuals = new Dictionary<int, TileVisual>();

    public GameObject Start { get; private set; } = null;
    public TileMap.Tile End { get; private set; } = null;

    /// <summary>마지막으로 생성한 타일 맵. 생성에 실패했으면 null 이다.</summary>
    public TileMap Map => this.tileMap;

    /// <summary>마지막으로 생성한 레벨 배치(시작/출구/잠긴 방). 생성에 실패했으면 null 이다.</summary>
    public LevelGenerator Level => this.levelGenerator;

    /// <summary>생성에 사용한 난수. 같은 시드로 이어서 굴리고 싶을 때 쓴다.</summary>
    public DungeonRandom Random => this.random;

    /// <summary>타일 인덱스 → 그 타일의 표현 오브젝트.</summary>
    public IReadOnlyDictionary<int, TileVisual> TileVisuals => this.tileVisuals;

    private GameObject tiles;

    /// <summary>LayerMask.NameToLayer() 는 문자열 조회다. 타일마다 부르지 않도록 한 번만 캐시한다.</summary>
    private int dungeonTileLayer = -1;

    public void Generate()
    {
#if UNITY_ANDROID || UNITY_WEBGL
        string csvPath = Application.streamingAssetsPath + "/MetaData/DungeonLevel.csv";
#else
        string csvPath = Path.Combine(Application.streamingAssetsPath, "MetaData", "DungeonLevel.csv");
#endif
        var reader = new MetaData.Reader<DungeonLevelMetaData>();
        reader.Read(csvPath);
        foreach (var data in reader.All) 
        {
            Debug.Log($"{data.ToString()}");
        }
        Clear();

        this.dungeonTileLayer = LayerMask.NameToLayer(DungeonTileLayerName);

        if (false == GenerateLayout())
        {
            Debug.LogError($"Dungeon: {MaxGenerationAttempts}회 시도했지만 유효한 던전을 만들지 못했다. 생성 설정을 확인하라.");
            return;
        }

        tiles = new GameObject();
        tiles.name = "Tiles";
        tiles.transform.SetParent(transform, false);
        tiles.transform.localPosition = Vector3.zero;
        NavMeshSurface navMeshSurface = tiles.AddComponent<NavMeshSurface>();
        navMeshSurface.layerMask = LayerMask.GetMask(DungeonTileLayerName);

        Build();

        if (false == spawnActors)
        {
            return;
        }

        InitializePlayerPosition();
        InitializeEnemy();
    }

    /// <summary>
    /// 타일 인덱스에 표현 오브젝트를 등록한다.
    /// 안개나 벽 가리기처럼 타일 단위로 오브젝트를 껐다 켜는 기능이 이 대응표를 쓴다.
    /// </summary>
    private void RegisterTileObject(int tileIndex, GameObject target, bool isWall = false)
    {
        if (null == target)
        {
            return;
        }

        if (false == tileVisuals.TryGetValue(tileIndex, out TileVisual visual))
        {
            visual = new TileVisual();
            tileVisuals[tileIndex] = visual;
        }

        visual.objects.Add(target);

        if (true == isWall)
        {
            visual.walls.Add(target);
        }
    }

    /// <summary>
    /// 유효한 타일 맵과 레벨 배치를 얻을 때까지 시드를 바꿔 가며 시도한다.
    /// 실패한 시도의 결과물은 버려지므로, 이 단계에서는 GameObject 를 만들지 않는다.
    /// </summary>
    private bool GenerateLayout()
    {
        int baseSeed = (0 != randomSeed) ? randomSeed : (int)System.DateTime.Now.Ticks;

        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            int seed = baseSeed + attempt;
            var attemptRandom = new DungeonRandom(seed);
            var config = new TileMap.Config(roomCount, minRoomSize, maxRoomSize);

            if (false == TileMap.TryBuild(config, attemptRandom, out TileMap builtMap, out string failureReason))
            {
                Debug.Log($"Dungeon: seed {seed} 생성 실패 - {failureReason}");
                continue;
            }

            var builtLevel = new LevelGenerator(builtMap, attemptRandom);
            if (false == builtLevel.IsValid)
            {
                Debug.Log($"Dungeon: seed {seed} 레벨 배치 실패 - 시작/출구 지점을 잡지 못했다.");
                continue;
            }

            this.random = attemptRandom;
            this.tileMap = builtMap;
            this.levelGenerator = builtLevel;
            this.End = builtLevel.End;

            Debug.Log($"Dungeon: Applied Random Seed {seed} (attempt {attempt + 1}/{MaxGenerationAttempts})");
            return true;
        }

        return false;
    }

    private void Build()
    {
        HashSet<Vector3> floorPositions = new HashSet<Vector3>();

        CreateEnterStairObject(levelGenerator.Start);
        CreateExitStairObject(levelGenerator.End, floorPositions);

        foreach (var room in tileMap.rooms)
        {
            CreateRoomObject(room, floorPositions);
        }

        foreach (var corridor in tileMap.corridors)
        {
            CreateCorridorObject(corridor, floorPositions);
        }

        if (false == buildNavMesh)
        {
            return;
        }

        NavMeshSurface navMeshSurface = tiles.GetComponent<NavMeshSurface>();
        navMeshSurface.BuildNavMesh();
    }

    /// <summary>
    /// 벽 목록에서 무작위로 골라 횃불을 단다.
    /// 횃불이 붙은 벽과 그 양옆은 후보에서 빼서 한 곳에 몰리지 않게 한다.
    /// </summary>
    private void CreateTorchObject(List<GameObject> walls)
    {
        if (0 == walls.Count)
        {
            return;
        }

        int torchCount = walls.Count / 3 + 1;
        for (int i = 0; i < torchCount && 0 < walls.Count; i++)
        {
            int index = random.Range(0, walls.Count);
            GameObject wallObject = walls[index];

            GameObject torchObject = Instantiate(torchPrefab, wallObject.transform);
            torchObject.name = $"Torch";
            torchObject.transform.localPosition = new Vector3(0f, 3.0f, 0.0f);
            torchObject.transform.localRotation = Quaternion.identity;

            // 뒤에서 앞으로 지운다. 앞에서부터 지우면 인덱스가 밀려서 엉뚱한 원소가 빠지고,
            // 정작 횃불을 단 벽은 후보에 남아 같은 자리에 두 번 붙을 수 있다.
            int last = Mathf.Min(index + 1, walls.Count - 1);
            int first = Mathf.Max(index - 1, 0);
            for (int removeIndex = last; removeIndex >= first; removeIndex--)
            {
                walls.RemoveAt(removeIndex);
            }
        }
    }

    private GameObject CreateWallObject(TileMap.Room room, int x, int y, Vector3 offset, float rotationY, Transform parent)
    {
        TileMap.Tile tile = tileMap.GetTile(x, y);
        if(null == tile)
        {
            return null;
        }

        Vector3 position = new Vector3(x * TileSize, 0.0f, y * TileSize) + offset;

        if (true == room.doors.Contains(tile))
        {
            TileMap.Tile.Direction[] directions = new TileMap.Tile.Direction[]
            {
                TileMap.Tile.Direction.Top,
                TileMap.Tile.Direction.Right,
                TileMap.Tile.Direction.Bottom,
                TileMap.Tile.Direction.Left
            };

            // 두 방이 벽을 맞대고 있으면 같은 자리에 문이 두 번 생긴다.
            // index 가 작은 방이 문을 만들기로 정해 중복을 막는다.
            bool createDoor = true;
            for (int i = 0; i < directions.Length; i++)
            {
                // 맵 밖 타일은 null 이다(TileMap 이 None 타일을 배열에서 제거한다).
                TileMap.Tile neighbor = tile.GetNeighbor(directions[i]);
                if (null == neighbor || null == neighbor.room)
                {
                    continue;
                }

                if (neighbor.room != room && neighbor.room.index < room.index)
                {
                    createDoor = false;
                }
            }

            if (true == createDoor)
            {
                GameObject doorObject = Instantiate(doorStandPrefab, position, Quaternion.identity);
                doorObject.name = $"Door_{tile.index}_{rotationY}";
                doorObject.layer = dungeonTileLayer;
                doorObject.transform.SetParent(parent, false);
                doorObject.transform.Rotate(0.0f, rotationY, 0.0f);
                RegisterTileObject(tile.index, doorObject);
            }

            return null;
        }

        GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
        wallObject.name = $"Wall_{tile.index}_{rotationY}";
        wallObject.layer = dungeonTileLayer;
        wallObject.transform.SetParent(parent, false);
        wallObject.transform.Rotate(0.0f, rotationY, 0.0f);
        RegisterTileObject(tile.index, wallObject, isWall: true);

        return wallObject;
    }

    private void CreateRoomObject(TileMap.Room room, HashSet<Vector3> floorPositions)
    {
        GameObject roomObject = new GameObject();
        roomObject.name = $"Room_{room.index}";
        roomObject.transform.SetParent(this.tiles.transform, false);

        for (int y = (int)room.rect.yMin; y < (int)room.rect.yMax; y++)
        {
            for (int x = (int)room.rect.xMin; x < (int)room.rect.xMax; x++)
            {
                TileMap.Tile tile = tileMap.GetTile(x, y);
                Vector3 position = new Vector3(tile.rect.x * TileSize, FloorHeightOffset, tile.rect.y * TileSize);

                // 바닥
                if (false == floorPositions.Contains(position))
                {
                    GameObject floorObject = Instantiate(floorPrefab, position, Quaternion.identity);
                    floorObject.name = $"Floor_{tile.index}";
                    floorObject.transform.SetParent(roomObject.transform, false);
                    floorObject.layer = dungeonTileLayer;
                    floorPositions.Add(position);
                    RegisterTileObject(tile.index, floorObject);
                }

                // 천장
                if (true == buildCeiling && levelGenerator.Start != tile)
                {
                    GameObject ceilObject = Instantiate(ceilPrefab, position + Vector3.up * (WallHeight - FloorHeightOffset), Quaternion.identity);
                    ceilObject.name = $"Ceil_{tile.index}";
                    ceilObject.transform.SetParent(roomObject.transform, false);
                    ceilObject.transform.Rotate(180.0f, 0.0f, 0.0f);
                    RegisterTileObject(tile.index, ceilObject);
                }
            }
        }

        // Top
        {
            List<GameObject> walls = new List<GameObject>();
            for (int x = (int)room.rect.xMin; x < (int)room.rect.xMax; x++)
            {
                GameObject wallObject = CreateWallObject(room, x, (int)room.rect.yMax - 1, new Vector3(0.0f, 0.0f, (TileSize * 1) -TileOffset - 0.2f), 180.0f, roomObject.transform);
                if (null == wallObject)
                {
                    CreateTorchObject(walls);
                    walls.Clear();
                    continue;
                }
                walls.Add(wallObject);
            }

            CreateTorchObject(walls);
        }

        // Bottom
        {
            List<GameObject> walls = new List<GameObject>();
            for (int x = (int)room.rect.xMin; x < (int)room.rect.xMax; x++)
            {
                GameObject wallObject = CreateWallObject(room, x, (int)room.rect.yMin, new Vector3(0.0f, 0.0f, (TileSize * 0) - TileOffset + 0.2f), 0.0f, roomObject.transform);
                if (null == wallObject)
                {
                    CreateTorchObject(walls);
                    walls.Clear();
                    continue;
                }
                walls.Add(wallObject);
            }

            CreateTorchObject(walls);
        }

        // Left
        {
            List<GameObject> walls = new List<GameObject>();
            for (int y = (int)room.rect.yMin; y < (int)room.rect.yMax; y++)
            {
                GameObject wallObject = CreateWallObject(room, (int)room.rect.xMin, y, new Vector3((TileSize * 0) - TileOffset + 0.2f, 0.0f, 0.0f), 90.0f, roomObject.transform);
                if (null == wallObject)
                {
                    CreateTorchObject(walls);
                    walls.Clear();
                    continue;
                }
                walls.Add(wallObject);
            }

            CreateTorchObject(walls);
        }

        // Right
        {
            List<GameObject> walls = new List<GameObject>();
            for (int y = (int)room.rect.yMin; y < (int)room.rect.yMax; y++)
            {
                GameObject wallObject = CreateWallObject(room, (int)room.rect.xMax - 1, y, new Vector3((TileSize * 1) - TileOffset - 0.2f, 0.0f, 0.0f), 270.0f, roomObject.transform);
                if (null == wallObject)
                {
                    CreateTorchObject(walls);
                    walls.Clear();
                    continue;
                }
                walls.Add(wallObject);
            }

            CreateTorchObject(walls);
        }
    }

    private void CreateCorridorObject(TileMap.Corridor corridor, HashSet<Vector3> floorPositions)
    {
        if (2 >= corridor.tiles.Count)
        {
            return;
        }

        GameObject corridorObject = new GameObject();
        corridorObject.name = $"Corridor";
        corridorObject.transform.SetParent(this.tiles.transform, false);

        List<GameObject> topWalls = new List<GameObject>();
        List<GameObject> rightWalls = new List<GameObject>();
        List<GameObject> bottomWalls = new List<GameObject>();
        List<GameObject> leftWalls = new List<GameObject>();

        foreach (TileMap.Tile tile in corridor.tiles)
        {
            {
                Vector3 position = new Vector3(tile.rect.x * TileSize, FloorHeightOffset, tile.rect.y * TileSize);
                if (true == floorPositions.Contains(position))
                {
                    continue;
                }

                // 바닥
                GameObject floorObject = Instantiate(floorPrefab, position, Quaternion.identity);
                floorObject.name = $"Floor_{tile.index}";
                floorObject.transform.SetParent(corridorObject.transform, false);
                floorObject.layer = dungeonTileLayer;
                floorPositions.Add(position);
                RegisterTileObject(tile.index, floorObject);

                // 천장
                if (true == buildCeiling)
                {
                    GameObject ceilObject = Instantiate(ceilPrefab, position + Vector3.up * (WallHeight - FloorHeightOffset), Quaternion.identity);
                    ceilObject.name = $"Ceil_{tile.index}";
                    ceilObject.transform.SetParent(corridorObject.transform, false);
                    ceilObject.transform.Rotate(180.0f, 0.0f, 0.0f);
                    RegisterTileObject(tile.index, ceilObject);
                }
            }

            bool hasTopWall = tile.GetNeighbor(TileMap.Tile.Direction.Top)?.type == TileMap.Tile.Type.Wall;
            bool hasRightWall = tile.GetNeighbor(TileMap.Tile.Direction.Right)?.type == TileMap.Tile.Type.Wall;
            bool hasBottomWall = tile.GetNeighbor(TileMap.Tile.Direction.Bottom)?.type == TileMap.Tile.Type.Wall;
            bool hasLeftWall = tile.GetNeighbor(TileMap.Tile.Direction.Left)?.type == TileMap.Tile.Type.Wall;

            if (true == hasTopWall)
            {
                Vector3 position = new Vector3(tile.rect.x * TileSize, 0.0f, tile.rect.y * TileSize + TileOffset);
                GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                wallObject.name = $"Wall_Top_{tile.index}";
                wallObject.layer = dungeonTileLayer;
                wallObject.transform.SetParent(corridorObject.transform, false);
                wallObject.transform.Rotate(0.0f, 180.0f, 0.0f);
                RegisterTileObject(tile.index, wallObject, isWall: true);
                topWalls.Add(wallObject);
            }

            if (true == hasBottomWall)
            {
                Vector3 position = new Vector3(tile.rect.x * TileSize, 0.0f, tile.rect.y * TileSize - TileOffset);
                GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                wallObject.name = $"Wall_Bottom_{tile.index}";
                wallObject.layer = dungeonTileLayer;
                wallObject.transform.SetParent(corridorObject.transform, false);
                wallObject.transform.Rotate(0.0f, 0.0f, 0.0f);
                RegisterTileObject(tile.index, wallObject, isWall: true);
                bottomWalls.Add(wallObject);
            }

            if (true == hasLeftWall)
            {
                Vector3 position = new Vector3(tile.rect.x * TileSize - TileOffset, 0.0f, tile.rect.y * TileSize);
                GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                wallObject.name = $"Wall_Left_{tile.index}";
                wallObject.layer = dungeonTileLayer;
                wallObject.transform.SetParent(corridorObject.transform, false);
                wallObject.transform.Rotate(0.0f, 90.0f, 0.0f);
                RegisterTileObject(tile.index, wallObject, isWall: true);
                leftWalls.Add(wallObject);
            }

            if (true == hasRightWall)
            {
                Vector3 position = new Vector3(tile.rect.x * TileSize + TileOffset, 0.0f, tile.rect.y * TileSize);
                GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                wallObject.name = $"Wall_Right_{tile.index}";
                wallObject.layer = dungeonTileLayer;
                wallObject.transform.SetParent(corridorObject.transform, false);
                wallObject.transform.Rotate(0.0f, 270.0f, 0.0f);
                RegisterTileObject(tile.index, wallObject, isWall: true);
                rightWalls.Add(wallObject);
            }
        }

        CreateTorchObject(topWalls);
        CreateTorchObject(rightWalls);
        CreateTorchObject(bottomWalls);
        CreateTorchObject(leftWalls);
    }

    public void Clear()
    {
        // 오브젝트가 사라지면 대응표도 함께 버려야 한다. tiles 가 없더라도 남아 있을 수 있으므로 먼저 비운다.
        this.tileVisuals.Clear();

        if (null == this.tiles)
        {
            return;
        }

        // Tiles의 모든 자식 오브젝트 제거
        while (0 < this.tiles.transform.childCount)
        {
            Transform child = this.tiles.transform.GetChild(0);
            child.SetParent(null);
            DestroyObject(child.gameObject);
        }

        DestroyObject(this.tiles);
        this.tiles = null;
    }

    /// <summary>
    /// Destroy() 는 다음 프레임에 처리되므로 에디터 모드(재생 중이 아닐 때)에서는 아무 일도 일어나지 않는다.
    /// 에디터에서 던전을 다시 만들 때도 이전 오브젝트가 확실히 지워지도록 분기한다.
    /// </summary>
    private static void DestroyObject(GameObject target)
    {
        if (true == Application.isPlaying)
        {
            Destroy(target);
            return;
        }

        DestroyImmediate(target);
    }

    private void CreateEnterStairObject(TileMap.Tile tile)
    {
        Debug.Assert(null != tile);

        Vector3 position = new Vector3(tile.rect.x * TileSize, FloorHeightOffset, tile.rect.y * TileSize);
        GameObject stair = Instantiate(upStairPrefab, position, Quaternion.identity);
        stair.name = $"EnterStair_{tile.index}";
        stair.layer = dungeonTileLayer;
        stair.transform.SetParent(this.tiles.transform, false);
        stair.transform.Rotate(0.0f, random.Range(0, 4) * 90.0f, 0.0f);

        for (int i = 0; i < stair.transform.childCount; i++)
        {
            Transform child = stair.transform.GetChild(i);
            child.gameObject.layer = dungeonTileLayer;
        }

        RegisterTileObject(tile.index, stair);

        this.Start = stair;
    }

    private void CreateExitStairObject(TileMap.Tile tile, HashSet<Vector3> floorPositions)
    {
        Vector3 position = new Vector3(tile.rect.x * TileSize, FloorHeightOffset, tile.rect.y * TileSize);
        GameObject stair = Instantiate(downStairPrefab, position, Quaternion.identity);
        stair.name = $"ExitStair_{tile.index}";
        stair.layer = dungeonTileLayer;
        stair.transform.SetParent(this.tiles.transform, false);
        stair.transform.Rotate(0.0f, random.Range(0, 4) * 90.0f, 0.0f);

        for (int i = 0; i < stair.transform.childCount; i++)
        {
            Transform child = stair.transform.GetChild(i);
            child.gameObject.layer = dungeonTileLayer;
        }

        RegisterTileObject(tile.index, stair);

        floorPositions.Add(position); // 내려가는 위치에 바닥 타일이 생성되지 않도록 미리 선점
    }

    private void InitializePlayerPosition()
    {
        if (player == null || levelGenerator == null)
        {
            Debug.LogError("Player or LevelGenerator is null. Cannot set player position.");
            return;
        }

        if (Start == null)
        {
            Debug.LogError("Start GameObject is null. Cannot get stair forward direction.");
            return;
        }

        TileMap.Tile startTile = levelGenerator.Start;
        if (startTile == null)
        {
            Debug.LogError("Start tile is null. Cannot set player position.");
            return;
        }

        // 1. Dungeon.Start 게임 오브젝트로부터 forward 방향을 얻는다
        Vector3 stairForward = Start.transform.forward;
        Debug.Log($"Stair forward direction: {stairForward}");

        // forward 방향을 타일의 Direction 값으로 변환
        int tileDirection = -1;
        
        // forward 방향과 가장 가깝게 맞춰지는 타일 방향을 찾기
        float maxDot = -1f;
        int[] directions = new int[]
        {
            (int)TileMap.Tile.Direction.Top,
            (int)TileMap.Tile.Direction.Right,
            (int)TileMap.Tile.Direction.Bottom,
            (int)TileMap.Tile.Direction.Left
        };

        Vector3[] directionVectors = new Vector3[]
        {
            Vector3.forward,   // Top: +Z
            Vector3.right,     // Right: +X
            Vector3.back,      // Bottom: -Z
            Vector3.left       // Left: -X
        };

        for (int i = 0; i < directions.Length; i++)
        {
            float dot = Vector3.Dot(stairForward, directionVectors[i]);
            if (dot > maxDot)
            {
                maxDot = dot;
                tileDirection = directions[i];
            }
        }

        Debug.Log($"Determined tile direction: {tileDirection}");

        // 2. levelGenerator.Start에서 타일을 얻는다 (이미 startTile로 얻음)
        Debug.Log($"Start tile index: {startTile.index}");

        // 3. Start 타일의 위치에서 1번에서 얻은 방향의 타일을 찾고, 해당 타일을 플레이어의 시작위치로 지정한다
        TileMap.Tile adjacentTile = startTile.neighbors[tileDirection];
        if (adjacentTile == null || adjacentTile.type != TileMap.Tile.Type.Floor)
        {
            Debug.LogError("No valid adjacent floor tile found in the stair forward direction.");
            return;
        }

        Vector3 playerPosition = new Vector3(adjacentTile.rect.x * TileSize, 0.0f, adjacentTile.rect.y * TileSize);
        player.transform.position = playerPosition;
        
        Debug.Log($"Player positioned at tile index: {adjacentTile.index}, position: {playerPosition}");

        // 4. 플레이어의 forward 방향을 Dungeon.Start 게임오브젝트와 같은 방향으로 설정한다
        player.transform.rotation = Quaternion.LookRotation(stairForward);
        
        Debug.Log($"Player forward direction set to match stair: {stairForward}");
    }

    private void InitializeEnemy()
    {
        foreach (TileMap.Room room in tileMap.rooms)
        {
            // 시작 방에는 적을 놓지 않는다. 플레이어가 스폰하자마자 적과 붙어 있게 된다.
            if (room == levelGenerator.StartRoom)
            {
                continue;
            }

            Rect spawnArea = room.GetFloorRect();

            int randomX = (int)random.Range(spawnArea.xMin, spawnArea.xMax);
            int randomY = (int)random.Range(spawnArea.yMin, spawnArea.yMax);

            Vector3 enemyPosition = new Vector3(randomX * TileSize, 1.0f, randomY * TileSize);
            // enemy 생성
            GameObject enemy = Instantiate(enemyPrefab, enemyPosition, Quaternion.identity, transform);
            enemy.name = $"Enemy_{randomX}_{randomY}";
            enemy.transform.SetParent(transform, false);
        }
    }
}
