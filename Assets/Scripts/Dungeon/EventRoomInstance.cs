using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EventRoomInstance : MonoBehaviour
{
    public EventRoomAsset eventRoomAsset;

    [Min(5)]
    public int width = 9;

    [Min(5)]
    public int height = 9;

    public GameObject wallPrefab;
    public GameObject floorPrefab;

    public List<EventRoomAsset.DoorDefinition> doors = new List<EventRoomAsset.DoorDefinition>();
    public List<EventRoomAsset.PrefabPlacement> prefabPlacements = new List<EventRoomAsset.PrefabPlacement>();
    public List<EventRoomAsset.MonsterSpawnPoint> monsterSpawnPoints = new List<EventRoomAsset.MonsterSpawnPoint>();

    public void LoadFromAsset(EventRoomAsset asset)
    {
        if (asset == null)
        {
            return;
        }

        eventRoomAsset = asset;
        width = asset.width;
        height = asset.height;
        wallPrefab = asset.wallPrefab;
        floorPrefab = asset.floorPrefab;

        doors.Clear();
        foreach (EventRoomAsset.DoorDefinition source in asset.doors)
        {
            if (source == null)
            {
                continue;
            }

            doors.Add(new EventRoomAsset.DoorDefinition
            {
                side = source.side,
                offset = source.offset,
                prefab = source.prefab
            });
        }

        prefabPlacements.Clear();
        foreach (EventRoomAsset.PrefabPlacement source in asset.prefabPlacements)
        {
            if (source == null)
            {
                continue;
            }

            prefabPlacements.Add(new EventRoomAsset.PrefabPlacement
            {
                id = source.id,
                prefab = source.prefab,
                tilePosition = source.tilePosition,
                localOffset = source.localOffset,
                localEulerAngles = source.localEulerAngles
            });
        }

        monsterSpawnPoints.Clear();
        foreach (EventRoomAsset.MonsterSpawnPoint source in asset.monsterSpawnPoints)
        {
            if (source == null)
            {
                continue;
            }

            monsterSpawnPoints.Add(new EventRoomAsset.MonsterSpawnPoint
            {
                id = source.id,
                tilePosition = source.tilePosition,
                localOffset = source.localOffset,
                localEulerAngles = source.localEulerAngles
            });
        }
    }

    public void CopyToAsset(EventRoomAsset asset)
    {
        if (asset == null)
        {
            return;
        }

        asset.width = Mathf.Max(5, width);
        asset.height = Mathf.Max(5, height);
        asset.wallPrefab = wallPrefab;
        asset.floorPrefab = floorPrefab;

        asset.doors.Clear();
        foreach (EventRoomAsset.DoorDefinition source in doors)
        {
            if (source == null)
            {
                continue;
            }

            asset.doors.Add(new EventRoomAsset.DoorDefinition
            {
                side = source.side,
                offset = source.offset,
                prefab = source.prefab
            });
        }

        asset.prefabPlacements.Clear();
        foreach (EventRoomAsset.PrefabPlacement source in prefabPlacements)
        {
            if (source == null)
            {
                continue;
            }

            asset.prefabPlacements.Add(new EventRoomAsset.PrefabPlacement
            {
                id = source.id,
                prefab = source.prefab,
                tilePosition = source.tilePosition,
                localOffset = source.localOffset,
                localEulerAngles = source.localEulerAngles
            });
        }

        asset.monsterSpawnPoints.Clear();
        foreach (EventRoomAsset.MonsterSpawnPoint source in monsterSpawnPoints)
        {
            if (source == null)
            {
                continue;
            }

            asset.monsterSpawnPoints.Add(new EventRoomAsset.MonsterSpawnPoint
            {
                id = source.id,
                tilePosition = source.tilePosition,
                localOffset = source.localOffset,
                localEulerAngles = source.localEulerAngles
            });
        }
    }
}