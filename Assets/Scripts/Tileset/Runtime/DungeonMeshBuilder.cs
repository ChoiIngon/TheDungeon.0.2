using System.Collections.Generic;
using UnityEngine;

namespace Pixel.Dungeon
{
    /// <summary>
    /// 던전 타일 오브젝트(벽·바닥·문틀·문짝)의 직육면체 메시를 JSON의 면 사각형으로 만든다.
    /// 좌표: Unity 기준 +X 오른쪽, +Y 위, +Z 카메라에서 멀어지는 쪽. front 면은 -Z 쪽(카메라를 향함).
    /// 면 방향 규약 (object-spec.md):
    ///   front: 앞에서 본 그대로 / back: 뒤에서 본 그대로 / top: 앞쪽 모서리가 텍스처 아래 /
    ///   side: 바깥에서 본 끝면 (오른쪽 끝은 텍스처 왼쪽 = 앞쪽 모서리, 왼쪽 끝은 텍스처 왼쪽 = 뒤쪽 모서리).
    /// 그림이 없는 면은 이렇게 채운다:
    ///   back 없음 → front를 쓴다 (바닥) / side 없음 → front의 가장자리 1px 열을 늘린다 (문틀) /
    ///   아랫면 → front의 맨 아래 1px 행을 늘린다 (늘 바닥이나 땅에 닿아 보이지 않는다).
    /// </summary>
    public static class DungeonMeshBuilder
    {
        /// <param name="origin">박스의 (x 최소, y 최소, z 최소) 모서리. 피벗(0,0,0)에서의 위치</param>
        public static Mesh Build(DungeonTilesetData data, DungeonTilesetData.Faces f, Vector3 size, Vector3 origin, string name)
        {
            float x0 = origin.x, x1 = origin.x + size.x;
            float y0 = origin.y, y1 = origin.y + size.y;
            float z0 = origin.z, z1 = origin.z + size.z;

            var verts = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var tris = new List<int>(36);

            // bl/tl/tr/br = 텍스처의 왼쪽 아래/왼쪽 위/오른쪽 위/오른쪽 아래에 붙는 꼭짓점 (면 바깥에서 봐서 시계 방향)
            void Quad(Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Vector3 n, DungeonTilesetData.PixelRect px)
            {
                var uv = data.ToUV(px);
                int i = verts.Count;
                verts.Add(bl); verts.Add(tl); verts.Add(tr); verts.Add(br);
                for (int k = 0; k < 4; k++) normals.Add(n);
                uvs.Add(new Vector2(uv.xMin, uv.yMin));
                uvs.Add(new Vector2(uv.xMin, uv.yMax));
                uvs.Add(new Vector2(uv.xMax, uv.yMax));
                uvs.Add(new Vector2(uv.xMax, uv.yMin));
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            var front = f.front;
            var back = DungeonTilesetData.Has(f.back) ? f.back : front;
            var top = DungeonTilesetData.Has(f.top) ? f.top : front;
            bool hasSide = DungeonTilesetData.Has(f.side);
            var leftCol = new DungeonTilesetData.PixelRect { x = front.x, y = front.y, w = 1, h = front.h };
            var rightCol = new DungeonTilesetData.PixelRect { x = front.x + front.w - 1, y = front.y, w = 1, h = front.h };
            var bottomRow = new DungeonTilesetData.PixelRect { x = front.x, y = front.y + front.h - 1, w = front.w, h = 1 };
            // 바닥처럼 옆면 크기(z×y)가 front(x×y)와 같고 side가 없으면 front를 그대로 쓴다
            bool sideLikeFront = !hasSide && Mathf.Approximately(size.z, size.x);
            var sideLeft = hasSide ? f.side : (sideLikeFront ? front : leftCol);
            var sideRight = hasSide ? f.side : (sideLikeFront ? front : rightCol);

            // front (-Z)
            Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), Vector3.back, front);
            // back (+Z): 뒤에서 보면 텍스처 왼쪽 = +X
            Quad(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.forward, back);
            // top (+Y): 텍스처 아래 = 앞쪽 모서리(z0)
            Quad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), Vector3.up, top);
            // 왼쪽 끝 (-X): 바깥에서 보면 텍스처 왼쪽 = 뒤쪽 모서리(z1)
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), Vector3.left, sideLeft);
            // 오른쪽 끝 (+X): 바깥에서 보면 텍스처 왼쪽 = 앞쪽 모서리(z0)
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, sideRight);
            // 아랫면 (-Y)
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), Vector3.down, bottomRow);

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>
        /// 데칼용 수평 사각형 한 장 (y = 0, 위를 본다). 피벗 = 가운데.
        /// 텍스처 아래 = -Z(앞, 카메라 쪽), 텍스처 위 = +Z(뒤, 북쪽). 바닥 가장자리 데칼은 텍스처 위쪽 변이 벽 쪽이다.
        /// </summary>
        public static Mesh BuildDecal(DungeonTilesetData data, DungeonTilesetData.PixelRect top, Vector2 sizeXZ, string name)
        {
            float x0 = -sizeXZ.x / 2, x1 = sizeXZ.x / 2, z0 = -sizeXZ.y / 2, z1 = sizeXZ.y / 2;
            var uv = data.ToUV(top);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new List<Vector3> { new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0) });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>피벗 = 바닥 가운데 (x·z 가운데, y 0). 벽·바닥·문틀용</summary>
        public static Vector3 BottomCenter(Vector3 size) => new Vector3(-size.x / 2, 0, -size.z / 2);

        /// <summary>피벗 = 왼쪽 끝 바닥 가운데 (문짝 경첩 축). 문짝이 +X 쪽으로 뻗는다</summary>
        public static Vector3 HingeLeft(Vector3 size) => new Vector3(0, 0, -size.z / 2);
    }
}
