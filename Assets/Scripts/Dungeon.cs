using System.Collections.Generic;
using UnityEngine;

public class Dungeon : MonoBehaviour
{
    public const float TileSize = 4.5f;
    public const float TileOffset = TileSize / 2f;
    public const float WallHeight = 5;
    public const float FloorHeightOffset = -0.25f;

    [Header("Dungeon Object Prefabs")]
    public GameObject doorStandPrefab;
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject columnPrefab;
    public GameObject TorchPrefab;
    public GameObject upStairPrefab;
    public GameObject downStairPrefab;
    public GameObject ceilPrefab;
    
    [Header("Dungeon Generation Settings")]
    public int randomSeed = 0;
    public int roomCount = 10;
    public int minRoomSize = 3;
    public int maxRoomSize = 7;

    [Header("Player")]
    public GameObject player;

    TileMap tileMap = null;
    LevelGenerator levelGenerator = null;

    public GameObject Start { get; private set; } = null;
    public TileMap.Tile End { get; private set; } = null;

    private Transform tiles;

    public void Generate()
    {
        Clear();

        GameObject tilesObject = new GameObject();
        tilesObject.name = "Tiles";
        tilesObject.transform.SetParent(transform, false);
        tilesObject.transform.localPosition = Vector3.zero;
        this.tiles = tilesObject.transform;

        InitializeRandomSeed();

        tileMap = new TileMap(roomCount, minRoomSize, maxRoomSize);
        levelGenerator = new LevelGenerator(tileMap);

        this.End = levelGenerator.End;

        HashSet<Vector3> floorPositions = new HashSet<Vector3>();
        HashSet<Vector3> wallPositions = new HashSet<Vector3>();

        CreateEnterStairObject(levelGenerator.Start);
        CreateExitStairObject(levelGenerator.End, floorPositions);

        foreach (var room in tileMap.rooms)
        {
            CreateRoomObject(room, floorPositions, wallPositions);
        }

        foreach(var corridor in tileMap.corridors)
        {
            CreateCorridorObject(corridor, floorPositions, wallPositions);
        }

        InitializePlayerPosition();
    }

    private void CreateRoomObject(TileMap.Room room, HashSet<Vector3> floorPositions, HashSet<Vector3> wallPositions)
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
                    floorPositions.Add(position);
                }

                // 천장
                if (levelGenerator.Start != tile)
                {
                    GameObject ceilObject = Instantiate(ceilPrefab, position + Vector3.up * (WallHeight - FloorHeightOffset), Quaternion.identity);
                    ceilObject.name = $"Ceil_{tile.index}";
                    ceilObject.transform.SetParent(roomObject.transform, false);
                    ceilObject.transform.Rotate(180.0f, 0.0f, 0.0f);
                }
            }
        }

        for (int x = (int)room.rect.xMin; x < (int)room.rect.xMax; x++)
        {
            TileMap.Tile top = tileMap.GetTile(x, (int)room.rect.yMax - 1);
            Vector3 position = new Vector3(top.rect.x * TileSize, 0f, (top.rect.y + 1) * TileSize - TileOffset - 0.2f);
            if (true == room.doors.Contains(top))
            {
                /*
                GameObject doorStand = Instantiate(doorStandPrefab, position, Quaternion.identity);
                doorStand.name = $"DoorStand_{top.index}";
                doorStand.transform.SetParent(parent, false);
                doorStand.transform.Rotate(0.0f, 180.0f, 0.0f);
                */
                continue;
            }

            if (false == wallPositions.Contains(position))
            {
                GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                wallObject.name = $"Wall_top_{top.index}";
                wallObject.layer = LayerMask.NameToLayer("DungeonTile");
                wallObject.transform.SetParent(roomObject.transform, false);
                wallObject.transform.Rotate(0.0f, 180.0f, 0.0f);
                wallPositions.Add(position);
            }
        }

        for (int x = (int)room.rect.xMin; x < (int)room.rect.xMax; x++)
        {
            TileMap.Tile bottom = tileMap.GetTile(x, (int)room.rect.yMin);
            Vector3 position = new Vector3(bottom.rect.x * TileSize, 0f, bottom.rect.y * TileSize - TileOffset + 0.2f);
            if (true == room.doors.Contains(bottom))
            {
                /*
                GameObject doorStand = Instantiate(doorStandPrefab, position, Quaternion.identity);
                doorStand.name = $"DoorStand_{bottom.index}";
                doorStand.transform.SetParent(parent, false);
                doorStand.transform.Rotate(0.0f, 0.0f, 0.0f);
                */
                continue;
            }

            if (false == wallPositions.Contains(position))
            {
                GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                wallObject.name = $"Wall_Bottom_{bottom.index}";
                wallObject.layer = LayerMask.NameToLayer("DungeonTile");
                wallObject.transform.SetParent(roomObject.transform, false);
                wallObject.transform.Rotate(0.0f, 0.0f, 0.0f);

                wallPositions.Add(position);
            }
        }

        for (int y = (int)room.rect.yMin; y < (int)room.rect.yMax; y++)
        {
            TileMap.Tile left = tileMap.GetTile((int)room.rect.xMin, y);
            Vector3 position = new Vector3(left.rect.x * TileSize - TileOffset + 0.2f, 0f, left.rect.y * TileSize);
            if (true == room.doors.Contains(left))
            {
                /*
                GameObject doorStand = Instantiate(doorStandPrefab, position, Quaternion.identity);
                doorStand.name = $"DoorStand_{left.index}";
                doorStand.transform.SetParent(parent, false);
                doorStand.transform.Rotate(0.0f, 90.0f, 0.0f);
                */
                continue;
            }
            if (false == wallPositions.Contains(position))
            {
                GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                wallObject.name = $"Wall_Left_{left.index}";
                wallObject.layer = LayerMask.NameToLayer("DungeonTile");
                wallObject.transform.SetParent(roomObject.transform, false);
                wallObject.transform.Rotate(0.0f, 90.0f, 0.0f);

                wallPositions.Add(position);
            }
        }

        for (int y = (int)room.rect.yMin; y < (int)room.rect.yMax; y++)
        {
            TileMap.Tile right = tileMap.GetTile((int)room.rect.xMax - 1, y);
            Vector3 position = new Vector3(right.rect.x * TileSize + TileOffset - 0.2f, 0f, right.rect.y * TileSize);
            if (true == room.doors.Contains(right))
            {
                /*
                GameObject doorStand = Instantiate(doorStandPrefab, position, Quaternion.identity);
                doorStand.name = $"DoorStand_{right.index}";
                doorStand.transform.SetParent(parent, false);
                doorStand.transform.Rotate(0.0f, 270.0f, 0.0f);
                */
                continue;
            }
             
            if (false == wallPositions.Contains(position))
            {
                GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                wallObject.name = $"Wall_Right_{right.index}";
                wallObject.layer = LayerMask.NameToLayer("DungeonTile");
                wallObject.transform.SetParent(roomObject.transform, false);
                wallObject.transform.Rotate(0.0f, 270.0f, 0.0f);

                wallPositions.Add(position);
            }
        }
    }

    private void CreateCorridorObject(TileMap.Corridor corridor, HashSet<Vector3> floorPositions, HashSet<Vector3> wallPositions)
    {
        if (2 >= corridor.tiles.Count)
        {
            return;
        }

        GameObject corridorObject = new GameObject();
        corridorObject.name = $"Corridor";
        corridorObject.transform.SetParent(this.tiles.transform, false);

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
                floorPositions.Add(position);

                // 천장
                GameObject ceilObject = Instantiate(ceilPrefab, position + Vector3.up * (WallHeight - FloorHeightOffset), Quaternion.identity);
                ceilObject.name = $"Ceil_{tile.index}";
                ceilObject.transform.SetParent(corridorObject.transform, false);
                ceilObject.transform.Rotate(180.0f, 0.0f, 0.0f);
            }

            bool hasTopWall = tile.neighbors[(int)TileMap.Tile.Direction.Top]?.type == TileMap.Tile.Type.Wall;
            bool hasRightWall = tile.neighbors[(int)TileMap.Tile.Direction.Right]?.type == TileMap.Tile.Type.Wall;
            bool hasBottomWall = tile.neighbors[(int)TileMap.Tile.Direction.Bottom]?.type == TileMap.Tile.Type.Wall;
            bool hasLeftWall = tile.neighbors[(int)TileMap.Tile.Direction.Left]?.type == TileMap.Tile.Type.Wall;

            if (true == hasTopWall)
            {
                Vector3 position = new Vector3(tile.rect.x * TileSize, 0.0f, tile.rect.y * TileSize + TileOffset);
                if (false == wallPositions.Contains(position))
                {
                    GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                    wallObject.name = $"Wall_Top_{tile.index}";
                    wallObject.layer = LayerMask.NameToLayer("DungeonTile");
                    wallObject.transform.SetParent(corridorObject.transform, false);
                    wallObject.transform.Rotate(0.0f, 180.0f, 0.0f);
                    wallPositions.Add(position);
                }
            }

            if (true == hasBottomWall)
            {
                Vector3 position = new Vector3(tile.rect.x * TileSize, 0.0f, tile.rect.y * TileSize - TileOffset);
                if (false == wallPositions.Contains(position))
                {
                    GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                    wallObject.name = $"Wall_Bottom_{tile.index}";
                    wallObject.layer = LayerMask.NameToLayer("DungeonTile");
                    wallObject.transform.SetParent(corridorObject.transform, false);
                    wallObject.transform.Rotate(0.0f, 0.0f, 0.0f);
                    wallPositions.Add(position);
                }
            }

            if (true == hasLeftWall)
            {
                Vector3 position = new Vector3(tile.rect.x * TileSize - TileOffset, 0.0f, tile.rect.y * TileSize);
                if (false == wallPositions.Contains(position))
                {
                    GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                    wallObject.name = $"Wall_Left_{tile.index}";
                    wallObject.layer = LayerMask.NameToLayer("DungeonTile");
                    wallObject.transform.SetParent(corridorObject.transform, false);
                    wallObject.transform.Rotate(0.0f, 90.0f, 0.0f);
                    wallPositions.Add(position);
                }
            }

            if (true == hasRightWall)
            {
                Vector3 position = new Vector3(tile.rect.x * TileSize + TileOffset, 0.0f, tile.rect.y * TileSize);
                if (false == wallPositions.Contains(position))
                {
                    GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                    wallObject.name = $"Wall_Right_{tile.index}";
                    wallObject.layer = LayerMask.NameToLayer("DungeonTile");
                    wallObject.transform.SetParent(corridorObject.transform, false);
                    wallObject.transform.Rotate(0.0f, 270.0f, 0.0f);
                    wallPositions.Add(position);
                }
            }
        }
    }

    private void CreateColumnObject(Vector3 position, Transform parent)
    {
        GameObject column = Instantiate(columnPrefab, position, Quaternion.identity);
        column.name = $"Column_{position}";
        column.transform.SetParent(parent, false);
    }

    public void Clear()
    {
        if (null == this.tiles)
        {
            return;
        }

        // Tiles의 모든 자식 오브젝트 제거
        int childCount = this.tiles.childCount;
        while (0 < this.tiles.childCount)
        {
            Transform child = this.tiles.GetChild(0);
            child.SetParent(null);
            GameObject.Destroy(child.gameObject);
        }

        GameObject.Destroy(this.tiles.gameObject);
        this.tiles = null;
    }

    private void InitializeRandomSeed()
    {
        if (0 == randomSeed)
        {
            int appliedRandomSeed = (int)System.DateTime.Now.Ticks;
            Random.InitState(appliedRandomSeed);

            Debug.Log($"Applied Random Seed: {appliedRandomSeed}");
            return;
        }

        Random.InitState(randomSeed);
    }

    private void CreateEnterStairObject(TileMap.Tile tile)
    {
        Debug.Assert(null != tile);
        
        Vector3 position = new Vector3(tile.rect.x * TileSize, FloorHeightOffset, tile.rect.y * TileSize);
        GameObject stair = Instantiate(upStairPrefab, position, Quaternion.identity);
        stair.name = $"EnterStair_{tile.index}";
        stair.layer = LayerMask.NameToLayer("DungeonTile");
        stair.transform.SetParent(this.tiles.transform, false);
        stair.transform.Rotate(0.0f, Random.Range(0, 4) * 90.0f, 0.0f);

        for (int i = 0; i < stair.transform.childCount; i++)
        {
            Transform child = stair.transform.GetChild(i);
            child.gameObject.layer = LayerMask.NameToLayer("DungeonTile");
        }

        this.Start = stair;
    }

    private void CreateExitStairObject(TileMap.Tile tile, HashSet<Vector3> floorPositions)
    {
        Vector3 position = new Vector3(tile.rect.x * TileSize, FloorHeightOffset, tile.rect.y * TileSize);
        GameObject stair = Instantiate(downStairPrefab, position, Quaternion.identity);
        stair.name = $"ExitStair_{tile.index}";
        stair.layer = LayerMask.NameToLayer("DungeonTile");
        stair.transform.SetParent(this.tiles.transform, false);
        stair.transform.Rotate(0.0f, Random.Range(0, 4) * 90.0f, 0.0f);

        for (int i = 0; i < stair.transform.childCount; i++)
        {
            Transform child = stair.transform.GetChild(i);
            child.gameObject.layer = LayerMask.NameToLayer("DungeonTile");
        }

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
}
