using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator
{
    private const int MaxJourneyTileCount = 50;
    private TileMap tileMap;

    public struct PathKey
    {
        public PathKey(TileMap.Room start, TileMap.Room end)
        {
            this.start = start;
            this.end = end;
        }

        public TileMap.Room start;
        public TileMap.Room end;
    }

    private Dictionary<PathKey, List<TileMap.Room>> paths = new Dictionary<PathKey, List<TileMap.Room>>();

    private TileMap.Room StartRoom;
    private TileMap.Room EndRoom;
    private TileMap.Room LockedRoom;

    public TileMap.Tile Start { get; private set; }
    public TileMap.Tile End { get; private set; }

    public LevelGenerator(TileMap tileMap)
    {
        this.tileMap = tileMap;

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
                paths.Add(new PathKey(start, end), tileMap.FindPath(start, end));
                paths.Add(new PathKey(end, start), tileMap.FindPath(end, start));
            }
        }
    }

    private void InitializeGates()
    {
        List<TileMap.Room> furthestPath = GetFurthestPath();
        this.StartRoom = furthestPath[0];
        this.EndRoom = furthestPath[furthestPath.Count - 1];

        if (this.StartRoom.doors.Count < this.EndRoom.doors.Count)
        {
            TileMap.Room tmp = StartRoom;
            this.StartRoom = EndRoom;
            this.EndRoom = tmp;
        }

        TileMap.Tile startTile = tileMap.GetTile((int)this.StartRoom.rect.center.x, (int)this.StartRoom.rect.center.y);
        TileMap.Tile endTile = tileMap.GetTile((int)this.EndRoom.rect.center.x, (int)this.EndRoom.rect.center.y);
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

    /// <summary>
    /// 잠겨야 하는 방을 선택합니다.
    /// 1. StartRoom과 EndRoom을 제외한 방 중에서 선택
    /// 2. StartRoom에서 도달 가능한 위치에 있는 방 중에서 선택
    /// </summary>
    private void SelectLockedRoom()
    {
        List<TileMap.Room> candidateRooms = new List<TileMap.Room>();

        // StartRoom에서 도달 가능한 모든 방을 수집
        foreach (var pair in paths)
        {
            // StartRoom에서 출발하는 경로만 확인
            if (pair.Key.start != StartRoom)
            {
                continue;
            }

            TileMap.Room room = pair.Key.end;

            // StartRoom과 EndRoom을 제외
            if (room == StartRoom || room == EndRoom)
            {
                continue;
            }

            // 도달 가능한 경로가 존재하면 후보에 추가
            if (pair.Value != null && pair.Value.Count > 0)
            {
                candidateRooms.Add(room);
            }
        }

        // 후보 방들 중에서 무작위로 선택
        if (candidateRooms.Count > 0)
        {
            this.LockedRoom = candidateRooms[Random.Range(0, candidateRooms.Count)];
            Debug.Log($"Locked room selected: Room {LockedRoom.index}");
        }
        else
        {
            Debug.LogWarning("No suitable room found for locking.");
            this.LockedRoom = null;
        }
    }

    private List<TileMap.Room> GetFurthestPath()
    {
        List<TileMap.Room> furthestPath = new List<TileMap.Room>();
        foreach (var pair in paths)
        {
            if (furthestPath.Count < pair.Value.Count)
            {
                furthestPath = pair.Value;
            }
        }

        return furthestPath;
    }

    private TileMap.Tile GetRandomTileInRoom(TileMap.Room room, int offset)
    {
        Rect floorRect = room.GetFloorRect();

        int x = (int)Random.Range(floorRect.xMin - offset, floorRect.xMax + offset);
        int y = (int)Random.Range(floorRect.yMin - offset, floorRect.yMax + offset);

        return tileMap.GetTile(x, y);
    }
}
