using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class Dungeon2 : MonoBehaviour
{
    public GameObject floorPrefab;
    public GameObject wallPrefab;

    public const float TileSize = 4.83f;
    public const float TileOffset = TileSize / 2f;
    public const float WallHeight = 5;

    TileMap tileMap = null;
    void Start()
    {
        Generate();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    Dictionary<TileMap.Room, GameObject> rooms = new Dictionary<TileMap.Room, GameObject>();
    Dictionary<TileMap.Corridor, GameObject> corridors = new Dictionary<TileMap.Corridor, GameObject>();

    public void Generate()
    {
        int randomSeed = (int)System.DateTime.Now.Ticks;
        Random.InitState(randomSeed);

        tileMap = new TileMap(10, 3, 6);

        HashSet<Vector3> wallPositions = new HashSet<Vector3>();

        foreach (var room in tileMap.rooms)
        {
            GameObject roomObject = new GameObject();
            roomObject.name = $"Room_{room.index}";
            roomObject.transform.SetParent(this.transform, false);
            rooms.Add(room, roomObject);

            CreateRoomObject(room, wallPositions);
        }

        foreach(var corridor in tileMap.corridors)
        {
            GameObject corridorObject = new GameObject();
            corridorObject.name = $"Corridor";
            corridorObject.transform.SetParent(this.transform, false);
            corridors.Add(corridor, corridorObject);

            CreateCorridorObject(corridor);
        }

        /*
        foreach (var tile in tileMap.tiles)
        {
            CreateTile(tile);
        }
        */
    }

    private void CreateWallObject(TileMap.Tile tile)
    {
        bool hasTopFloor = tile.neighbors[(int)TileMap.Tile.Direction.Top]?.type == TileMap.Tile.Type.Floor;
        bool hasRightFloor = tile.neighbors[(int)TileMap.Tile.Direction.Right]?.type == TileMap.Tile.Type.Floor;
        bool hasBottomFloor = tile.neighbors[(int)TileMap.Tile.Direction.Bottom]?.type == TileMap.Tile.Type.Floor;
        bool hasLeftFloor = tile.neighbors[(int)TileMap.Tile.Direction.Left]?.type == TileMap.Tile.Type.Floor;

        bool hasTopWall = tile.neighbors[(int)TileMap.Tile.Direction.Top]?.type == TileMap.Tile.Type.Wall;
        bool hasRightWall = tile.neighbors[(int)TileMap.Tile.Direction.Right]?.type == TileMap.Tile.Type.Wall;
        bool hasBottomWall = tile.neighbors[(int)TileMap.Tile.Direction.Bottom]?.type == TileMap.Tile.Type.Wall;
        bool hasLeftWall = tile.neighbors[(int)TileMap.Tile.Direction.Left]?.type == TileMap.Tile.Type.Wall;

        GameObject wallObject = null;
        if (true == hasTopFloor)
        {
            Vector3 position = new Vector3(tile.rect.x * TileSize, 0.0f, (tile.rect.y - 1) * TileSize + TileOffset);
            wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
            wallObject.transform.Rotate(0.0f, 0.0f, 0.0f);
        }

        if (true == hasRightFloor)
        {
            Vector3 position = new Vector3((tile.rect.x - 1) * TileSize + TileOffset, 0f, tile.rect.y * TileSize);
            wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
            wallObject.transform.Rotate(0.0f, 90.0f, 0.0f);
        }

        if (true == hasBottomFloor)
        {
            Vector3 position = new Vector3(tile.rect.x * TileSize, 0f, tile.rect.y * TileSize - (TileSize / 2));
            wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
            wallObject.transform.Rotate(0.0f, 180.0f, 0.0f);
        }

        if (true == hasLeftFloor)
        {
            Vector3 position = new Vector3(tile.rect.x * TileSize - (TileSize / 2), 0f, tile.rect.y * TileSize);
            wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
            wallObject.transform.Rotate(0.0f, 270, 0.0f);
        }

        if (null == wallObject)
        {
            return;
        }

        wallObject.name = $"Wall_{tile.index}";
        if (null != tile.room)
        {
            wallObject.transform.SetParent(rooms[tile.room].transform, false);
        }
        else
        {
            wallObject.transform.SetParent(transform, false);
        }
    }

    private void CreateFloorObject(TileMap.Tile tile, Transform parent)
    {
        float FloorHeightOffset = -0.25f;
        Vector3 position = new Vector3(tile.rect.x * TileSize, FloorHeightOffset, tile.rect.y * TileSize);
        GameObject floorObject = Instantiate(floorPrefab, position, Quaternion.identity);

        floorObject.transform.SetParent(parent, false);
    }

    private void CreateRoomObject(TileMap.Room room, HashSet<Vector3> wallPositions)
    {
        for (int y = (int)room.rect.yMin; y < (int)room.rect.yMax; y++)
        {
            for (int x = (int)room.rect.xMin; x < (int)room.rect.xMax; x++)
            {
                TileMap.Tile floor = tileMap.GetTile(x, y);
                CreateFloorObject(floor, rooms[room].transform);
            }
        }

        for (int x = (int)room.rect.xMin; x < (int)room.rect.xMax; x++)
        {
            TileMap.Tile top = tileMap.GetTile(x, (int)room.rect.yMax - 1);
            if (false == room.doors.Contains(top))
            {
                Vector3 position = new Vector3(top.rect.x * TileSize, 0f, (top.rect.y + 1) * TileSize - TileOffset);

                if (false == wallPositions.Contains(position))
                {
                    GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                    wallObject.transform.Rotate(0.0f, 180.0f, 0.0f);
                    wallObject.transform.SetParent(rooms[top.room].transform, false);
                    wallPositions.Add(position);
                }
            }

            TileMap.Tile bottom = tileMap.GetTile(x, (int)room.rect.yMin);
            if (false == room.doors.Contains(bottom))
            {
                Vector3 position = new Vector3(bottom.rect.x * TileSize, 0f, bottom.rect.y * TileSize - TileOffset);

                if (false == wallPositions.Contains(position))
                {
                    GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                    wallObject.transform.Rotate(0.0f, 0.0f, 0.0f);
                    wallObject.transform.SetParent(rooms[bottom.room].transform, false);

                    wallPositions.Add(position);
                }
            }
        }

        for (int y = (int)room.rect.yMin; y < (int)room.rect.yMax; y++)
        {
            TileMap.Tile left = tileMap.GetTile((int)room.rect.xMin, y);
            if (false == room.doors.Contains(left))
            {
                Vector3 position = new Vector3(left.rect.x * TileSize - (TileSize / 2), 0f, left.rect.y * TileSize);
                if (false == wallPositions.Contains(position))
                {
                    GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                    wallObject.transform.Rotate(0.0f, 90.0f, 0.0f);
                    wallObject.transform.SetParent(rooms[left.room].transform, false);

                    wallPositions.Add(position);
                }
            }

            TileMap.Tile right = tileMap.GetTile((int)room.rect.xMax - 1, y);
            if (false == room.doors.Contains(right))
            {
                Vector3 position = new Vector3(right.rect.x * TileSize + (TileSize / 2), 0f, right.rect.y * TileSize);
                if (false == wallPositions.Contains(position))
                {
                    GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                    wallObject.transform.Rotate(0.0f, 270.0f, 0.0f);
                    wallObject.transform.SetParent(rooms[right.room].transform, false);

                    wallPositions.Add(position);
                }
            }
        }
    }

    private void CreateCorridorObject(TileMap.Corridor corridor)
    {
        foreach (TileMap.Tile tile in corridor.tiles.Skip(1).SkipLast(1))
        {
            CreateFloorObject(tile, corridors[corridor].transform);

            bool hasTopWall = tile.neighbors[(int)TileMap.Tile.Direction.Top]?.type == TileMap.Tile.Type.Wall;
            bool hasRightWall = tile.neighbors[(int)TileMap.Tile.Direction.Right]?.type == TileMap.Tile.Type.Wall;
            bool hasBottomWall = tile.neighbors[(int)TileMap.Tile.Direction.Bottom]?.type == TileMap.Tile.Type.Wall;
            bool hasLeftWall = tile.neighbors[(int)TileMap.Tile.Direction.Left]?.type == TileMap.Tile.Type.Wall;

            if (true == hasTopWall)
            {
                Vector3 position = new Vector3(tile.rect.x * TileSize, 0f, tile.rect.y * TileSize - TileOffset);

                GameObject wallObject = Instantiate(wallPrefab, position, Quaternion.identity);
                wallObject.transform.Rotate(0.0f, 0.0f, 0.0f);
                wallObject.transform.SetParent(corridors[corridor].transform, false);
            }
        }
    }
}
