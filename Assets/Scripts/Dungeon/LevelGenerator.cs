using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static TileMap;

public class LevelGenerator
{
    private const int MaxJourneyTileCount = 50;
    private TileMap tileMap;

    public struct PathKey
    {
        public PathKey(Room start, Room end)
        {
            this.start = start;
            this.end = end;
        }

        public Room start;
        public Room end;
    }

    private Dictionary<PathKey, List<Room>> paths = new Dictionary<PathKey, List<Room>>();

    private TileMap.Room StartRoom;
    private TileMap.Room EndRoom;

    public TileMap.Tile Start { get; private set; }
    public TileMap.Tile End { get; private set; }

    public LevelGenerator(TileMap tileMap)
    {
        this.tileMap = tileMap;

        InitializeRoomPaths();
        InitializeGates();
    }

    private void InitializeRoomPaths()
    {
        for (int i = 0; i < tileMap.rooms.Count; i++)
        {
            for (int j = i + 1; j < tileMap.rooms.Count; j++)
            {
                Room start = tileMap.rooms[i];
                Room end = tileMap.rooms[j];
                paths.Add(new PathKey(start, end), tileMap.FindPath(start, end));
                paths.Add(new PathKey(end, start), tileMap.FindPath(end, start));
            }
        }
    }

    private void InitializeGates()
    {
        List<Room> furthestPath = GetFurthestPath();
        this.StartRoom = furthestPath[0];
        this.EndRoom = furthestPath[furthestPath.Count - 1];

        if (this.StartRoom.doors.Count < this.EndRoom.doors.Count)
        {
            Room tmp = StartRoom;
            this.StartRoom = EndRoom;
            this.EndRoom = tmp;
        }

        Tile startTile = tileMap.GetTile((int)this.StartRoom.rect.center.x, (int)this.StartRoom.rect.center.y);
        Tile endTile = tileMap.GetTile((int)this.EndRoom.rect.center.x, (int)this.EndRoom.rect.center.y);
        var tilePath = tileMap.FindPath(endTile, startTile);

        if (tilePath.Count > MaxJourneyTileCount)
        {
            for (int i = MaxJourneyTileCount; i < tilePath.Count; i++)
            {
                var tile = tilePath[i];
                if (null != tile.room)
                {
                    this.StartRoom = tile.room;
                    break;
                }
            }
        }

        this.Start = GetRandomTileInRoom(StartRoom, -1);
        this.End = GetRandomTileInRoom(EndRoom, -1);
    }

    private List<TileMap.Room> GetFurthestPath()
    {
        List<Room> furthestPath = new List<Room>();
        foreach (var pair in paths)
        {
            if (furthestPath.Count < pair.Value.Count)
            {
                furthestPath = pair.Value;
            }
        }

        return furthestPath;
    }

    private Tile GetRandomTileInRoom(Room room, int offset)
    {
        Rect floorRect = room.GetFloorRect();

        int x = (int)Random.Range(floorRect.xMin - offset, floorRect.xMax + offset);
        int y = (int)Random.Range(floorRect.yMin - offset, floorRect.yMax + offset);

        return tileMap.GetTile(x, y);
    }
}
