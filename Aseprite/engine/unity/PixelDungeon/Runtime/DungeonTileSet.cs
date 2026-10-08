using UnityEngine;

namespace PixelArt.Dungeon
{
    /// <summary>바닥 칸의 네 변 (북 = +Z = 카메라에서 먼 쪽)</summary>
    public enum EdgeSide { North, East, South, West }

    /// <summary>
    /// 한 테마의 타일 프리팹 목록. 프리팹 생성 메뉴가 만든다.
    /// 벽·바닥·바닥 가장자리 데칼은 칸(데칼)마다 변형을 랜덤으로 고른다 (어떤 조합으로 붙어도 이어지게 그려져 있다).
    /// </summary>
    [CreateAssetMenu(menuName = "Pixel Dungeon/Tile Set", fileName = "DungeonTileSet")]
    public class DungeonTileSet : ScriptableObject
    {
        public GameObject[] walls;
        public GameObject[] floors;
        public GameObject door;

        [Tooltip("바닥 가장자리 디더링 데칼 (이끼·먼지). 텍스처 위쪽 변 = 벽 쪽")]
        public GameObject[] floorEdges;

        [Tooltip("변별 회전(도, 위에서 봐서 시계 방향). JSON의 floor_edge.placement.rotationBySide")]
        public float[] edgeRotation = { 0, 90, 180, 270 };   // North, East, South, West

        [Tooltip("바닥 윗면 높이 (바닥 프리팹 피벗 기준)")]
        public float floorTopY = 0.25f;

        [Tooltip("가장자리 데칼의 데칼 layer (JSON floor_edge.layer). 높은 layer가 덮는다. 카펫은 2")]
        public int edgeLayer = 1;

        public GameObject RandomWall() => Pick(walls);
        public GameObject RandomFloor() => Pick(floors);
        public GameObject RandomFloorEdge() => Pick(floorEdges);

        public Quaternion EdgeRotation(EdgeSide side) => Quaternion.Euler(0f, edgeRotation[(int)side], 0f);

        /// <summary>
        /// 바닥 칸 하나에 벽이 닿는 변마다 디더링 데칼을 붙인다 (4면이 벽이면 4장).
        /// floor = 바닥 프리팹 인스턴스(피벗 = 바닥 가운데). 데칼은 floor의 자식으로 놓인다.
        /// </summary>
        public void AddFloorEdges(Transform floor, bool wallNorth, bool wallEast, bool wallSouth, bool wallWest)
        {
            if (floorEdges == null || floorEdges.Length == 0) return;
            if (wallNorth) AddEdge(floor, EdgeSide.North);
            if (wallEast) AddEdge(floor, EdgeSide.East);
            if (wallSouth) AddEdge(floor, EdgeSide.South);
            if (wallWest) AddEdge(floor, EdgeSide.West);
        }

        public GameObject AddEdge(Transform floor, EdgeSide side)
        {
            var prefab = RandomFloorEdge();
            if (prefab == null) return null;
            var go = Instantiate(prefab, floor);
            go.name = $"{prefab.name}_{side}";
            go.transform.localPosition = new Vector3(0f, DungeonDecalLayer.Height(floorTopY, edgeLayer), 0f);
            go.transform.localRotation = EdgeRotation(side);
            return go;
        }

        static GameObject Pick(GameObject[] list) =>
            list == null || list.Length == 0 ? null : list[Random.Range(0, list.Length)];
    }
}
