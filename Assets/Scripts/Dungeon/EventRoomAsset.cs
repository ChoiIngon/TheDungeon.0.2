using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EventRoomAsset", menuName = "Dungeon/Event Room Asset")]
public class EventRoomAsset : ScriptableObject
{
    public enum DoorSide
    {
        Top,
        Right,
        Bottom,
        Left
    }

    [System.Serializable]
    public class DoorDefinition
    {
        public DoorSide side;
        public int offset = 1;
        public GameObject prefab;
    }

    [System.Serializable]
    public class PrefabPlacement
    {
        public string id;
        public GameObject prefab;
        public Vector2Int tilePosition = new Vector2Int(1, 1);
        public Vector3 localOffset = Vector3.zero;
        public Vector3 localEulerAngles = Vector3.zero;
    }

    [System.Serializable]
    public class MonsterSpawnPoint
    {
        public string id;
        public Vector2Int tilePosition = new Vector2Int(1, 1);
        public Vector3 localOffset = Vector3.zero;
        public Vector3 localEulerAngles = Vector3.zero;
    }

    [Min(5)]
    public int width = 9;

    [Min(5)]
    public int height = 9;

    public GameObject wallPrefab;
    public GameObject floorPrefab;

    public List<DoorDefinition> doors = new List<DoorDefinition>();
    public List<PrefabPlacement> prefabPlacements = new List<PrefabPlacement>();
    public List<MonsterSpawnPoint> monsterSpawnPoints = new List<MonsterSpawnPoint>();

    public int RoomWidth => Mathf.Max(width, 5);
    public int RoomHeight => Mathf.Max(height, 5);

    public Vector2Int GetLocalDoorPosition(DoorDefinition door)
    {
        switch (door.side)
        {
            case DoorSide.Top:
                return new Vector2Int(Mathf.Clamp(door.offset, 1, RoomWidth - 2), RoomHeight - 1);
            case DoorSide.Right:
                return new Vector2Int(RoomWidth - 1, Mathf.Clamp(door.offset, 1, RoomHeight - 2));
            case DoorSide.Bottom:
                return new Vector2Int(Mathf.Clamp(door.offset, 1, RoomWidth - 2), 0);
            case DoorSide.Left:
                return new Vector2Int(0, Mathf.Clamp(door.offset, 1, RoomHeight - 2));
        }

        return new Vector2Int(0, 0);
    }

    public List<Vector2Int> GetLocalDoorPositions()
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        foreach (DoorDefinition door in doors)
        {
            positions.Add(GetLocalDoorPosition(door));
        }

        return positions;
    }
}