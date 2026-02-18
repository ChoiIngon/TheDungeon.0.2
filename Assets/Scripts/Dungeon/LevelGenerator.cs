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

        this.Start = CreateStaircaseTile(StartRoom);
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

    public Tile CreateStaircaseTile(Room room)
    {
        // 1. 방의 벽 타일들을 상, 하, 좌, 우 4개의 그룹으로 분리합니다.
        List<Tile> topWall = new List<Tile>();
        List<Tile> bottomWall = new List<Tile>();
        List<Tile> leftWall = new List<Tile>();
        List<Tile> rightWall = new List<Tile>();

        // 상, 하 벽면 추가 (모서리 제외: x 범위를 1씩 줄임)
        int yTop = (int)room.rect.yMax - 1;
        int yBottom = (int)room.rect.yMin;
        for (int x = (int)room.rect.xMin + 1; x < (int)room.rect.xMax - 1; x++)
        {
            topWall.Add(tileMap.GetTile(x, yTop));
            bottomWall.Add(tileMap.GetTile(x, yBottom));
        }

        // 좌, 우 벽면 추가 (모서리 중복 방지를 위해 y 범위를 1씩 줄임, x는 모서리 제외)
        int xLeft = (int)room.rect.xMin;
        int xRight = (int)room.rect.xMax - 1;
        for (int y = (int)room.rect.yMin + 1; y < (int)room.rect.yMax - 1; y++)
        {
            leftWall.Add(tileMap.GetTile(xLeft, y));
            rightWall.Add(tileMap.GetTile(xRight, y));
        }

        List<List<Tile>> allWallFaces = new List<List<Tile>> { topWall, bottomWall, leftWall, rightWall };

        // ========== 조건 1: 아무런 문이 없는 벽면이 있는지 확인 ==========
        List<List<Tile>> emptyWallFaces = new List<List<Tile>>();
        foreach (var wallFace in allWallFaces)
        {
            // 현재 벽면에 기존 문(room.doors)이 하나도 없는지 확인
            if (!wallFace.Any(wallTile => room.doors.Contains(wallTile)))
            {
                emptyWallFaces.Add(wallFace);
            }
        }

        if (emptyWallFaces.Count > 0)
        {
            // 조건 1 충족: 문이 없는 벽면이 하나 이상 존재함
            Debug.Log($"[다음 층 문 생성] 조건 1 충족: 문이 없는 벽면 {emptyWallFaces.Count}개 발견");

            // 문 없는 벽면 중 하나를 랜덤으로 선택
            List<Tile> chosenWall = emptyWallFaces[Random.Range(0, emptyWallFaces.Count)];

            // 해당 벽면에서 랜덤한 타일 하나를 다음 층 문으로 선택
            Tile staircaseTile = chosenWall[Random.Range(0, chosenWall.Count)];
            Debug.Log($"[다음 층 문 생성] 벽면에서 타일 선택: {staircaseTile.rect}");
            return staircaseTile;
        }

        // ========== 조건 2: 모든 벽면에 문이 존재할 경우 ==========
        Debug.Log("[다음 층 문 생성] 조건 2 충족: 모든 벽면에 기존 문이 존재. 인접하지 않은 위치 탐색");

        // 4개의 벽면 중 하나를 랜덤으로 선택
        List<Tile> randomWallFace = allWallFaces[Random.Range(0, allWallFaces.Count)];

        // 후보 타일 조건:
        // (A) 스스로가 기존 문이 아니고,
        // (B) 이웃 타일 중에 기존 문이 없는 타일
        List<Tile> candidates = randomWallFace.Where(tile =>
            !room.doors.Contains(tile) &&
            (tile.neighbors == null || !tile.neighbors.Any(n => n != null && room.doors.Contains(n)))
        ).ToList();

        if (candidates.Count > 0)
        {
            // 유효한 후보 타일이 있으면, 그중에서 랜덤하게 선택
            Tile staircaseTile = candidates[Random.Range(0, candidates.Count)];
            Debug.Log($"[다음 층 문 생성] 조건 2 - 인접하지 않은 위치에서 선택: {staircaseTile.rect}");
            return staircaseTile;
        }
        else
        {
            // 예외 처리: 만약 유효한 후보 타일이 없다면 (예: 벽이 문으로 꽉 찬 경우)
            // 규칙을 완화하여 '문이 아닌 아무 벽 타일' 중 하나를 선택
            List<Tile> fallbackCandidates = randomWallFace.Where(tile => !room.doors.Contains(tile)).ToList();
            if (fallbackCandidates.Count > 0)
            {
                Tile staircaseTile = fallbackCandidates[Random.Range(0, fallbackCandidates.Count)];
                Debug.LogWarning($"[다음 층 문 생성] 인접 규칙 완화: 문이 아닌 아무 벽 타일 선택: {staircaseTile.rect}");
                return staircaseTile;
            }
        }

        // 모든 시도가 실패한 경우 (이런 경우는 거의 없음)
        Debug.LogError("[다음 층 문 생성] 실패: 유효한 위치를 찾지 못했습니다.");
        return null;
    }
}
