using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class Dungeon : MonoBehaviour
{
    private TileMap map = null;

    public GameObject doorStandPrefab;
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject columnPrefab;
    public GameObject ceilPrefab;
    public GameObject stairPrefab;
    public GameObject torchPrefab;
    public GameObject player;
    public GameObject enemyPrefab;

    public const float TileSize = 4.83f;
    public const float TileOffset = TileSize / 2f;
    public int roomCount = 10;
    public int minRoomSize = 5;
    public int maxRoomSize = 7;
    public int randomSeed = (int)System.DateTime.Now.Ticks;

    public Transform Tiles;
    private LevelGenerator levelGenerator = null;

    public void Generate()
    {
        Clear();

        if (0 == randomSeed)
        {
            randomSeed = (int)System.DateTime.Now.Ticks;
        }

        Random.InitState(randomSeed);
        map = new TileMap(roomCount, minRoomSize, maxRoomSize);
        Debug.Log($"Dungeon generated with seed: {randomSeed}");
        levelGenerator = new LevelGenerator(map);
        Debug.Log($"Level generated with seed: {randomSeed}");
        // levelGenerator.Start 타일 저장
        TileMap.Tile startTile = levelGenerator.Start;
        // levelGenerator.End 타일 저장
        TileMap.Tile endTile = levelGenerator.End;

        Debug.Assert(null != startTile);
        Debug.Assert(null != endTile);
        
        Dictionary<TileMap.Room, GameObject> rooms = new Dictionary<TileMap.Room, GameObject>();
        HashSet<Vector3> columnPositions = new HashSet<Vector3>();
        foreach (var room in map.rooms)
        {
            Vector3 positionLB = new Vector3(room.rect.xMin * TileSize + TileOffset, 0, room.rect.yMin * TileSize + TileOffset);
            Vector3 positionLT = new Vector3(room.rect.xMin * TileSize + TileOffset, 0, (room.rect.yMax - 1) * TileSize - TileOffset);
            Vector3 positionRT = new Vector3((room.rect.xMax - 1) * TileSize - TileOffset, 0, (room.rect.yMax - 1) * TileSize - TileOffset);
            Vector3 positionRB = new Vector3((room.rect.xMax - 1) * TileSize - TileOffset, 0, room.rect.yMin * TileSize + TileOffset);

            columnPositions.Add(positionLB);
            columnPositions.Add(positionLT);
            columnPositions.Add(positionRT);
            columnPositions.Add(positionRB);

            GameObject roomObject = new GameObject();
            roomObject.name = $"Room{room.index}";
            roomObject.transform.SetParent(Tiles, false);
            rooms.Add(room, roomObject);

            Rect floorRect = room.GetFloorRect();

            // 50% 확률로 enemyPrefab 생성
            if (Random.value > 0.5f)
            {
                SpawnEnemyInRoom(floorRect);
            }
        }

        for (int i = 0; i < map.width * map.height; i++)
        {
            var tile = map.GetTile(i);
            if (null == tile)
            {
                continue;
            }

            bool hasTopFloor = tile.neighbors[(int)TileMap.Tile.Direction.Top]?.type == TileMap.Tile.Type.Floor;
            bool hasRightFloor = tile.neighbors[(int)TileMap.Tile.Direction.Right]?.type == TileMap.Tile.Type.Floor;
            bool hasBottomFloor = tile.neighbors[(int)TileMap.Tile.Direction.Bottom]?.type == TileMap.Tile.Type.Floor;
            bool hasLeftFloor = tile.neighbors[(int)TileMap.Tile.Direction.Left]?.type == TileMap.Tile.Type.Floor;

            bool hasTopWall = tile.neighbors[(int)TileMap.Tile.Direction.Top]?.type == TileMap.Tile.Type.Wall;
            bool hasRightWall = tile.neighbors[(int)TileMap.Tile.Direction.Right]?.type == TileMap.Tile.Type.Wall;
            bool hasBottomWall = tile.neighbors[(int)TileMap.Tile.Direction.Bottom]?.type == TileMap.Tile.Type.Wall;
            bool hasLeftWall = tile.neighbors[(int)TileMap.Tile.Direction.Left]?.type == TileMap.Tile.Type.Wall;

            if (tile.type == TileMap.Tile.Type.Floor)
            {
                // levelGenerator.End 타일인지 확인
                bool isEndTile = (endTile != null && 
                                 tile.rect.x == endTile.rect.x && 
                                 tile.rect.y == endTile.rect.y);

                Vector3 position = new Vector3(tile.rect.x * TileSize, -0.25f, tile.rect.y * TileSize);

                if (isEndTile)
                {
                    // 계단 생성 (아래로 내려가는 계단이므로 낮은 위치에 생성)
                    Vector3 stairPosition = new Vector3(tile.rect.x * TileSize, -2.8f, tile.rect.y * TileSize);
                    GameObject stair = Instantiate(stairPrefab, stairPosition, Quaternion.identity, transform);
                    stair.name = $"ExitStair_{i}";
                    stair.layer = LayerMask.NameToLayer("DungeonTile");
                    stair.transform.SetParent(Tiles, false);
                    stair.transform.localScale = new Vector3(2f, 1f, 1f);

                    // 계단 주변에 벽 생성 (좌우)
                    // 좌측 벽
                    Vector3 leftWallPosition = new Vector3(tile.rect.x * TileSize - (TileSize / 2), -TileSize, tile.rect.y * TileSize);
                    GameObject leftWall = Instantiate(wallPrefab, leftWallPosition, Quaternion.identity, transform);
                    leftWall.name = $"ExitStairWall_Left_{i}";
                    leftWall.layer = LayerMask.NameToLayer("DungeonTile");
                    leftWall.transform.SetParent(Tiles, false);
                    leftWall.transform.Rotate(0f, 90f, 0f);

                    // 우측 벽
                    Vector3 rightWallPosition = new Vector3(tile.rect.x * TileSize + (TileSize / 2), -TileSize, tile.rect.y * TileSize);
                    GameObject rightWall = Instantiate(wallPrefab, rightWallPosition, Quaternion.identity, transform);
                    rightWall.name = $"ExitStairWall_Right_{i}";
                    rightWall.layer = LayerMask.NameToLayer("DungeonTile");
                    rightWall.transform.SetParent(Tiles, false);
                    rightWall.transform.Rotate(0f, 90f, 0f);
                }
                else
                {
                    // 일반 floor 생성
                    GameObject floor = Instantiate(floorPrefab, position, Quaternion.identity, transform);
                    floor.name = $"Floor_{i}";
                    floor.transform.SetParent(Tiles, false);
                }

                // Floor 타일에서 바깥쪽 코너 감지
                DetectOuterCornerColumns(tile, hasTopWall, hasRightWall, hasBottomWall, hasLeftWall, columnPositions);

                GameObject ceil = Instantiate(ceilPrefab, position + Vector3.up * (4.85f + 0.25f), Quaternion.identity, transform);
                
                ceil.name = $"Ceil_{i}";
                ceil.layer = LayerMask.NameToLayer("DungeonTile");
                ceil.transform.SetParent(Tiles, false);
                ceil.transform.Rotate(180f, 0f, 0f);
            }
            
            if (tile.type == TileMap.Tile.Type.Wall)
            {
                if (hasTopFloor)
                {
                    // levelGenerator.Start 타일이고 tile.room이 null이 아니고, Top 방향이 tile.room을 가리키는지 확인
                    bool isStartTileTopRoom = (startTile != null && 
                                              tile.rect.x == startTile.rect.x && 
                                              tile.rect.y == startTile.rect.y &&
                                              tile.room != null &&
                                              tile.neighbors[(int)TileMap.Tile.Direction.Top]?.room == tile.room);

                    if (isStartTileTopRoom)
                    {
                        // 계단 생성 (Z축이 room 쪽을 봄, 뒤로 2.5 밀어줌)
                        Vector3 stairPosition = new Vector3(tile.rect.x * TileSize, 0f, tile.rect.y * TileSize + (TileSize / 2) - 2.5f);
                        GameObject stair = Instantiate(stairPrefab, stairPosition, Quaternion.identity, transform);
                        stair.name = $"StartStair_Top_{i}";
                        stair.layer = LayerMask.NameToLayer("DungeonTile");
                        stair.transform.SetParent(Tiles, false);
                        stair.transform.Rotate(0f, 0f, 0f);

                        // doorStandPrefab을 벽의 원래 위치에 생성
                        Vector3 doorStandPosition = new Vector3(tile.rect.x * TileSize, 0f, tile.rect.y * TileSize + (TileSize / 2));
                        GameObject doorStand = Instantiate(doorStandPrefab, doorStandPosition, Quaternion.identity, transform);
                        doorStand.name = $"DoorStand_Top_{i}";
                        doorStand.layer = LayerMask.NameToLayer("DungeonTile");
                        doorStand.transform.SetParent(Tiles, false);
                        doorStand.transform.Rotate(0f, 0f, 0f);
                    }
                    else
                    {
                        Vector3 position = new Vector3(tile.rect.x * TileSize, 0f, tile.rect.y * TileSize + (TileSize / 2));
                        GameObject wall = Instantiate(wallPrefab, position, Quaternion.identity, transform);
                        wall.name = $"Wall_{i}";
                        wall.layer = LayerMask.NameToLayer("DungeonTile"); 
                        wall.transform.SetParent(Tiles, false);
                        wall.transform.Rotate(0f, 0f, 0f);
                        AttachTorchToWall(wall);
                    }
                }
                if (hasRightFloor)
                {
                    // levelGenerator.Start 타일이고 tile.room이 null이 아니고, Right 방향이 tile.room을 가리키는지 확인
                    bool isStartTileRightRoom = (startTile != null && 
                                                tile.rect.x == startTile.rect.x && 
                                                tile.rect.y == startTile.rect.y &&
                                                tile.room != null &&
                                                tile.neighbors[(int)TileMap.Tile.Direction.Right]?.room == tile.room);

                    if (isStartTileRightRoom)
                    {
                        // 계단 생성 (Z축이 room 쪽을 봄, 뒤로 2.5 밀어줌)
                        Vector3 stairPosition = new Vector3(tile.rect.x * TileSize + (TileSize / 2) - 2.5f, 0f, tile.rect.y * TileSize);
                        GameObject stair = Instantiate(stairPrefab, stairPosition, Quaternion.identity, transform);
                        stair.name = $"StartStair_Right_{i}";
                        stair.transform.SetParent(Tiles, false);
                        stair.transform.Rotate(0f, 90f, 0f);

                        // doorStandPrefab을 벽의 원래 위치에 생성
                        Vector3 doorStandPosition = new Vector3(tile.rect.x * TileSize + (TileSize / 2), 0f, tile.rect.y * TileSize);
                        GameObject doorStand = Instantiate(doorStandPrefab, doorStandPosition, Quaternion.identity, transform);
                        doorStand.name = $"DoorStand_Right_{i}";
                        doorStand.layer = LayerMask.NameToLayer("DungeonTile");
                        doorStand.transform.SetParent(Tiles, false);
                        doorStand.transform.Rotate(0f, 90f, 0f);
                    }
                    else
                    {
                        Vector3 position = new Vector3(tile.rect.x * TileSize + (TileSize / 2), 0f, tile.rect.y * TileSize);
                        GameObject wall = Instantiate(wallPrefab, position, Quaternion.identity, transform);
                        wall.name = $"Wall_{i}";
                        wall.layer = LayerMask.NameToLayer("DungeonTile");
                        wall.transform.SetParent(Tiles, false);
                        wall.transform.Rotate(0f, 90f, 0f);
                        AttachTorchToWall(wall);
                    }
                }
                if (hasBottomFloor)
                {
                    // levelGenerator.Start 타일이고 tile.room이 null이 아니고, Bottom 방향이 tile.room을 가리키는지 확인
                    bool isStartTileBottomRoom = (startTile != null && 
                                                 tile.rect.x == startTile.rect.x && 
                                                 tile.rect.y == startTile.rect.y &&
                                                 tile.room != null &&
                                                 tile.neighbors[(int)TileMap.Tile.Direction.Bottom]?.room == tile.room);

                    if (isStartTileBottomRoom)
                    {
                        // 계단 생성 (Z축이 room 쪽을 봄, 뒤로 2.5 밀어줌)
                        Vector3 stairPosition = new Vector3(tile.rect.x * TileSize, 0f, tile.rect.y * TileSize - (TileSize / 2) + 2.5f);
                        GameObject stair = Instantiate(stairPrefab, stairPosition, Quaternion.identity, transform);
                        stair.name = $"StartStair_Bottom_{i}";
                        stair.layer = LayerMask.NameToLayer("DungeonTile");
                        stair.transform.SetParent(Tiles, false);
                        stair.transform.Rotate(0f, 180f, 0f);

                        // doorStandPrefab을 벽의 원래 위치에 생성
                        Vector3 doorStandPosition = new Vector3(tile.rect.x * TileSize, 0f, tile.rect.y * TileSize - (TileSize / 2));
                        GameObject doorStand = Instantiate(doorStandPrefab, doorStandPosition, Quaternion.identity, transform);
                        doorStand.name = $"DoorStand_Bottom_{i}";
                        doorStand.layer = LayerMask.NameToLayer("DungeonTile");
                        doorStand.transform.SetParent(Tiles, false);
                        doorStand.transform.Rotate(0f, 180f, 0f);
                    }
                    else
                    {
                        Vector3 position = new Vector3(tile.rect.x * TileSize, 0f, tile.rect.y * TileSize - (TileSize / 2));
                        GameObject wall = Instantiate(wallPrefab, position, Quaternion.identity, transform);
                        wall.name = $"Wall_{i}";
                        wall.layer = LayerMask.NameToLayer("DungeonTile");
                        wall.transform.SetParent(Tiles, false);
                        wall.transform.Rotate(0f, 180f, 0f);
                        AttachTorchToWall(wall);
                    }
                }
                if (hasLeftFloor)
                {
                    // levelGenerator.Start 타일이고 tile.room이 null이 아니고, Left 방향이 tile.room을 가리키는지 확인
                    bool isStartTileLeftRoom = (startTile != null && 
                                               tile.rect.x == startTile.rect.x && 
                                               tile.rect.y == startTile.rect.y &&
                                               tile.room != null &&
                                               tile.neighbors[(int)TileMap.Tile.Direction.Left]?.room == tile.room);

                    if (isStartTileLeftRoom)
                    {
                        // 계단 생성 (Z축이 room 쪽을 봄, 뒤로 2.5 밀어줌)
                        Vector3 stairPosition = new Vector3(tile.rect.x * TileSize - (TileSize / 2) + 2.5f, 0f, tile.rect.y * TileSize);
                        GameObject stair = Instantiate(stairPrefab, stairPosition, Quaternion.identity, transform);
                        stair.name = $"StartStair_Left_{i}";
                        stair.layer = LayerMask.NameToLayer("DungeonTile");
                        stair.transform.SetParent(Tiles, false);
                        stair.transform.Rotate(0f, 270f, 0f);

                        // doorStandPrefab을 벽의 원래 위치에 생성
                        Vector3 doorStandPosition = new Vector3(tile.rect.x * TileSize - (TileSize / 2), 0f, tile.rect.y * TileSize);
                        GameObject doorStand = Instantiate(doorStandPrefab, doorStandPosition, Quaternion.identity, transform);
                        doorStand.name = $"DoorStand_Left_{i}";
                        doorStand.layer = LayerMask.NameToLayer("DungeonTile");
                        doorStand.transform.SetParent(Tiles, false);
                        doorStand.transform.Rotate(0f, 270f, 0f);
                    }
                    else
                    {
                        Vector3 position = new Vector3(tile.rect.x * TileSize - (TileSize / 2), 0f, tile.rect.y * TileSize);
                        GameObject wall = Instantiate(wallPrefab, position, Quaternion.identity, transform);
                        wall.name = $"Wall_{i}";
                        wall.layer = LayerMask.NameToLayer("DungeonTile");
                        wall.transform.SetParent(Tiles, false);
                        wall.transform.Rotate(0f, 270f, 0f);
                        AttachTorchToWall(wall);
                    }
                }

                // 벽의 곡선 코너에 column 감지 및 추가 (안쪽 코너)
                DetectInnerCornerColumns(tile, hasTopFloor, hasRightFloor, hasBottomFloor, hasLeftFloor, columnPositions);
            }
        }

        // 감지된 모든 코너 위치에 column 생성
        foreach (var columnPos in columnPositions)
        {
            GameObject column = Instantiate(columnPrefab, columnPos, Quaternion.identity, transform);
            column.name = $"Column_{columnPos}";
            column.layer = LayerMask.NameToLayer("DungeonColumn");
            column.transform.SetParent(Tiles, false);
        }

        SetInitialPlayerPosition();
    }

    private void DetectInnerCornerColumns(TileMap.Tile tile, bool hasTopFloor, bool hasRightFloor, bool hasBottomFloor, bool hasLeftFloor, HashSet<Vector3> columnPositions)
    {
        float x = tile.rect.x * TileSize;
        float z = tile.rect.y * TileSize;
        float offset = TileSize / 2;

        // 우상단 코너 (Top-Right)
        if (hasTopFloor && hasRightFloor)
        {
            Vector3 position = new Vector3(x + offset, 0f, z + offset);
            columnPositions.Add(position);
        }

        // 좌상단 코너 (Top-Left)
        if (hasTopFloor && hasLeftFloor)
        {
            Vector3 position = new Vector3(x - offset, 0f, z + offset);
            columnPositions.Add(position);
        }

        // 좌하단 코너 (Bottom-Left)
        if (hasBottomFloor && hasLeftFloor)
        {
            Vector3 position = new Vector3(x - offset, 0f, z - offset);
            columnPositions.Add(position);
        }

        // 우하단 코너 (Bottom-Right)
        if (hasBottomFloor && hasRightFloor)
        {
            Vector3 position = new Vector3(x + offset, 0f, z - offset);
            columnPositions.Add(position);
        }
    }

    private void DetectOuterCornerColumns(TileMap.Tile tile, bool hasTopWall, bool hasRightWall, bool hasBottomWall, bool hasLeftWall, HashSet<Vector3> columnPositions)
    {
        float x = tile.rect.x * TileSize;
        float z = tile.rect.y * TileSize;
        float offset = TileSize / 2;

        // 우상단 코너 (Top-Right) - 위와 오른쪽이 모두 Wall인 경우
        if (hasTopWall && hasRightWall)
        {
            Vector3 position = new Vector3(x + offset, 0f, z + offset);
            columnPositions.Add(position);
        }

        // 좌상단 코너 (Top-Left) - 위와 왼쪽이 모두 Wall인 경우
        if (hasTopWall && hasLeftWall)
        {
            Vector3 position = new Vector3(x - offset, 0f, z + offset);
            columnPositions.Add(position);
        }

        // 좌하단 코너 (Bottom-Left) - 아래와 왼쪽이 모두 Wall인 경우
        if (hasBottomWall && hasLeftWall)
        {
            Vector3 position = new Vector3(x - offset, 0f, z - offset);
            columnPositions.Add(position);
        }

        // 우하단 코너 (Bottom-Right) - 아래와 오른쪽이 모두 Wall인 경우
        if (hasBottomWall && hasRightWall)
        {
            Vector3 position = new Vector3(x + offset, 0f, z - offset);
            columnPositions.Add(position);
        }
    }

    private void SetInitialPlayerPosition()
    {
        if (player == null || levelGenerator == null)
        {
            Debug.LogError("Player or LevelGenerator is null. Cannot set player position.");
            return;
        }

        TileMap.Tile startTile = levelGenerator.Start;
        if (startTile == null)
        {
            Debug.LogError("Start tile is null. Cannot set player position.");
            return;
        }

        Debug.Log($"start tile index:{startTile.index}");
        // start 타일과 인접한 floor 타일 찾기
        TileMap.Tile adjacentFloorTile = null;
        int stairDirection = -1;

        // 인접한 타일들을 확인 (상, 우, 하, 좌)
        int[] directions = new int[] 
        { 
            (int)TileMap.Tile.Direction.Top,
            (int)TileMap.Tile.Direction.Right,
            (int)TileMap.Tile.Direction.Bottom,
            (int)TileMap.Tile.Direction.Left
        };

        foreach (int direction in directions)
        {
            TileMap.Tile neighbor = startTile.neighbors[direction];
            if (neighbor != null && neighbor.type == TileMap.Tile.Type.Floor)
            {
                adjacentFloorTile = neighbor;
                stairDirection = direction;
                break;
            }
        }

        if (adjacentFloorTile == null)
        {
            Debug.LogError("No adjacent floor tile found from start tile.");
            return;
        }

        Debug.Log($"tile_index:{adjacentFloorTile.index}, tile_position:({adjacentFloorTile.rect.x}, {adjacentFloorTile.rect.y}), stair_direction:{stairDirection}");
        // 인접한 floor 타일의 center 위치를 플레이어의 시작 지점으로 설정
        Vector3 playerPosition = new Vector3(adjacentFloorTile.rect.x * TileSize, 0.0f, adjacentFloorTile.rect.y * TileSize);
        player.transform.position = playerPosition;

        // 계단을 바라보도록 플레이어 회전 설정
        Vector3 stairDirection3D = Vector3.zero;
        switch (stairDirection)
        {
            case (int)TileMap.Tile.Direction.Top:
                stairDirection3D = Vector3.forward;  // +Z 방향 (위)
                break;
            case (int)TileMap.Tile.Direction.Right:
                stairDirection3D = Vector3.right;    // +X 방향 (오른쪽)
                break;
            case (int)TileMap.Tile.Direction.Bottom:
                stairDirection3D = Vector3.back;     // -Z 방향 (아래)
                break;
            case (int)TileMap.Tile.Direction.Left:
                stairDirection3D = Vector3.left;     // -X 방향 (왼쪽)
                break;
        }

        // 플레이어 회전을 계단 방향으로 설정
        player.transform.rotation = Quaternion.LookRotation(stairDirection3D);

        Debug.Log($"Player positioned at start floor tile center: {playerPosition}, Looking at stair direction: {stairDirection}");
    }

    // TileMap에 접근하기 위한 getter 추가
    public TileMap GetTileMap()
    {
        return map;
    }

    // LevelGenerator에 접근하기 위한 getter 추가
    public LevelGenerator GetLevelGenerator()
    {
        return levelGenerator;
    }

    private void Clear()
    {
        if (Tiles == null)
        {
            Debug.LogWarning("Tiles transform is not assigned.");
            return;
        }

        // Tiles의 모든 자식 오브젝트 제거
        int childCount = Tiles.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = Tiles.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }

        Debug.Log($"Cleared {childCount} objects from Tiles.");
    }

    /// <summary>
    /// Exit 계단 생성
    /// </summary>
    private void CreateExitStairs(TileMap.Tile tile, int tileIndex)
    {
        Vector3 position = new Vector3(tile.rect.x * TileSize, -2.8f, tile.rect.y * TileSize);
        GameObject stair = Instantiate(stairPrefab, position, Quaternion.identity, transform);
        stair.name = $"ExitStair_{tileIndex}";
        stair.layer = LayerMask.NameToLayer("DungeonTile");
        stair.transform.SetParent(Tiles, false);
        stair.transform.localScale = new Vector3(2f, 1f, 1f);

        // 좌측 벽
        Vector3 leftWallPos = new Vector3(tile.rect.x * TileSize - (TileSize / 2), -TileSize, tile.rect.y * TileSize);
        GameObject leftWall = Instantiate(wallPrefab, leftWallPos, Quaternion.identity, transform);
        leftWall.name = $"ExitStairWall_Left_{tileIndex}";
        leftWall.layer = LayerMask.NameToLayer("DungeonTile");
        leftWall.transform.SetParent(Tiles, false);
        leftWall.transform.Rotate(0f, 90f, 0f);

        // 우측 벽
        Vector3 rightWallPos = new Vector3(tile.rect.x * TileSize + (TileSize / 2), -TileSize, tile.rect.y * TileSize);
        GameObject rightWall = Instantiate(wallPrefab, rightWallPos, Quaternion.identity, transform);
        rightWall.name = $"ExitStairWall_Right_{tileIndex}";
        rightWall.layer = LayerMask.NameToLayer("DungeonTile");
        rightWall.transform.SetParent(Tiles, false);
        rightWall.transform.Rotate(0f, 90f, 0f);
    }

    /// <summary>
    /// Wall에 torch를 자식으로 붙입니다
    /// </summary>
    private void AttachTorchToWall(GameObject wall)
    {
        if (torchPrefab == null)
        {
            Debug.LogWarning("Torch prefab is not assigned.");
            return;
        }

        // 90% 확률로 torch 생성
        if (Random.Range(0, 10) + 1 > 8)
        {
            GameObject torch = Instantiate(torchPrefab, wall.transform);
            torch.name = "Torch";

            // torch를 wall의 로컬 좌표 (0, 0, 0)에 배치
            torch.transform.localPosition = new Vector3(0, 3, 0);
            torch.transform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Room 내에 enemy를 생성합니다
    /// </summary>
    private void SpawnEnemyInRoom(Rect floorRect)
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("Enemy prefab is not assigned.");
            return;
        }

        // floorRect 내에서 랜덤한 위치 선택
        int randomX = (int)Random.Range(floorRect.xMin, floorRect.xMax);
        int randomY = (int)Random.Range(floorRect.yMin, floorRect.yMax);

        // 월드 좌표로 변환
        Vector3 enemyPosition = new Vector3(randomX * TileSize, 1.0f, randomY * TileSize);

        // enemy 생성
        GameObject enemy = Instantiate(enemyPrefab, enemyPosition, Quaternion.identity, transform);
        enemy.name = $"Enemy_{randomX}_{randomY}";
        enemy.transform.SetParent(Tiles, false);

        Debug.Log($"Enemy spawned at position: {enemyPosition}");
    }
}
