using UnityEngine.Rendering;

namespace PixelArt.Dungeon
{
    /// <summary>
    /// 평면 데칼(바닥 가장자리 이끼·먼지, 카펫 등 두께 없는 텍스처)의 그리는 순서.
    /// 데칼은 모두 바닥 윗면과 같은 평면에 놓이므로 깊이로는 순서가 안 정해진다. 그래서 layer 번호로 정한다:
    /// 높은 layer가 낮은 layer를 덮는다 (JSON decalLayers: 1 = floor_edge, 2 = 카펫·러그).
    /// 같은 layer끼리 겹치는 곳(모서리 칸의 북·서 가장자리 데칼)은 어느 쪽이 위여도 같아 보이게 그려져 있다.
    /// </summary>
    public static class DungeonDecalLayer
    {
        /// <summary>layer마다 바닥 윗면에서 띄우는 높이. 순서는 큐가 정하고, 이 값은 바닥과의 z-fighting 방지용</summary>
        public const float Lift = 0.002f;

        /// <summary>렌더 큐: 불투명(벽·바닥) 다음, layer 오름차순</summary>
        public static int RenderQueue(int layer) => (int)UnityEngine.Rendering.RenderQueue.AlphaTest + layer;

        public static float Height(float floorTopY, int layer) => floorTopY + Lift * layer;
    }
}
