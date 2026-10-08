using System;
using UnityEngine;

namespace PixelArt.Dungeon
{
    /// <summary>
    /// dungeon_atlas.py가 만드는 export/dungeon3d/&lt;theme&gt;.json 구조 (명세: dungeon-tile 스킬 references/object-spec.md).
    /// 면 사각형은 px, 왼쪽 위 원점, y-down (패딩 제외한 실제 면 영역).
    /// JsonUtility는 없는 객체를 null이 아니라 0으로 채우므로, 면이 있는지는 <see cref="Has"/>로 확인한다.
    /// </summary>
    [Serializable]
    public class DungeonTilesetData
    {
        public string image;
        public Size size;
        public int pixelsPerUnit;
        public int padding;
        public Objects objects;

        [Serializable] public class Size { public int w; public int h; }
        [Serializable] public class Vec3 { public float x; public float y; public float z; public Vector3 ToVector3() => new Vector3(x, y, z); }
        [Serializable] public class PixelRect { public int x; public int y; public int w; public int h; }
        [Serializable] public class Faces { public PixelRect front; public PixelRect back; public PixelRect top; public PixelRect side; }
        [Serializable] public class Variant { public string name; public Faces faces; }
        [Serializable] public class ObjectDef { public Vec3 size; public Variant[] variants; }
        [Serializable] public class FramePlacement { public int count; public float[] x; }
        [Serializable] public class LeafPlacement { public float x; public float z; }
        [Serializable] public class FrameDef : ObjectDef { public FramePlacement placement; }
        [Serializable] public class LeafDef : ObjectDef { public LeafPlacement placement; }
        [Serializable] public class EdgeRotation { public float north; public float east = 90; public float south = 180; public float west = 270; }
        [Serializable] public class EdgePlacement { public EdgeRotation rotationBySide; }
        /// <summary>바닥 가장자리 디더링 데칼. 텍스처 위쪽 변 = 벽 쪽, 벽이 닿는 변마다 한 장</summary>
        [Serializable] public class EdgeDef : ObjectDef { public bool decal; public int layer = 1; public EdgePlacement placement; }

        [Serializable]
        public class Objects
        {
            public ObjectDef wall;
            public ObjectDef floor;
            public FrameDef door_frame;
            public LeafDef door_leaf;
            public EdgeDef floor_edge;   // 없으면(예전 JSON) 데칼 프리팹을 만들지 않는다
        }

        public bool HasFloorEdge => objects.floor_edge != null && objects.floor_edge.variants != null && objects.floor_edge.variants.Length > 0
                                    && Has(objects.floor_edge.variants[0].faces?.top);

        public static bool Has(PixelRect r) => r != null && r.w > 0 && r.h > 0;

        public static DungeonTilesetData FromJson(string json)
        {
            var d = JsonUtility.FromJson<DungeonTilesetData>(json);
            if (d?.size == null || d.size.w <= 0 || d.objects == null)
                throw new ArgumentException("던전 타일셋 JSON에 size / objects가 없습니다 (export/dungeon3d/<theme>.json인지 확인).");
            foreach (var (name, o) in new (string, ObjectDef)[]
                     { ("wall", d.objects.wall), ("floor", d.objects.floor), ("door_frame", d.objects.door_frame), ("door_leaf", d.objects.door_leaf) })
            {
                if (o?.size == null || o.variants == null || o.variants.Length == 0)
                    throw new ArgumentException($"objects.{name}에 size / variants가 없습니다.");
            }
            return d;
        }

        /// <summary>px 사각형(왼쪽 위 원점) → Unity UV 사각형(왼쪽 아래 원점).</summary>
        public Rect ToUV(PixelRect r)
        {
            float W = size.w, H = size.h;
            return new Rect(r.x / W, 1f - (r.y + r.h) / H, r.w / W, r.h / H);
        }
    }
}
