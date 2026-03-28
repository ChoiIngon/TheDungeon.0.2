using UnityEngine;

public class EventRoomPreviewBinding : MonoBehaviour
{
    public enum NodeType
    {
        PrefabPlacement,
        MonsterSpawnPoint
    }

    public NodeType nodeType;
    public int index;
}
