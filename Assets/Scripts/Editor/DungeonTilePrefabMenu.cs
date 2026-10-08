using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pixel.Dungeon.Editor
{
    /// <summary>
    /// 같은 폴더에 같은 이름의 .json이 있는 PNG(dungeon_atlas.py 결과)를 픽셀아트용으로 임포트한다.
    /// </summary>
    class DungeonTexturePostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.EndsWith(".png") || !File.Exists(Path.ChangeExtension(assetPath, ".json"))) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None; // 256x120 같은 NPOT를 늘리지 않게 (UV가 px 기준)
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.sRGBTexture = true;
            ti.alphaIsTransparency = true;
        }
    }

    /// <summary>
    /// Project 창에서 던전 타일셋 JSON(Assets/Texture/tileset/&lt;theme&gt;/&lt;theme&gt;.json) 우클릭 → Create Tile Prefabs.
    /// JSON 최상위 "type"이 "tileset"인 파일에서만 메뉴가 활성화된다 (캐릭터 시트 JSON은 "character").
    /// Assets/Prefabs/Tileset/&lt;theme&gt;/ 에 메시·머티리얼·프리팹(Wall_N, Floor_N, Door)과 DungeonTileSet 에셋을 만든다.
    /// 다시 실행하면 같은 경로의 에셋 내용만 갱신하므로, 씬에 놓인 프리팹 인스턴스의 참조가 유지된다.
    /// </summary>
    public static class DungeonTilePrefabMenu
    {
        const string MenuPath = "Assets/Create Tile Prefabs";
        const string TilesetType = "tileset";
        const string OutRoot = "Assets/Prefabs/Tileset";

        [System.Serializable]
        class JsonHeader { public string type; }

        static bool IsTileset(Object o, out TextAsset json, out string path)
        {
            json = o as TextAsset;
            path = json != null ? AssetDatabase.GetAssetPath(json) : null;
            if (json == null || !path.EndsWith(".json")) return false;
            try { return JsonUtility.FromJson<JsonHeader>(json.text)?.type == TilesetType; }
            catch (System.ArgumentException) { return false; }
        }

        [MenuItem(MenuPath, true)]
        static bool Validate()
        {
            foreach (var o in Selection.objects)
                if (IsTileset(o, out _, out _)) return true;
            return false;
        }

        [MenuItem(MenuPath)]
        static void Create()
        {
            foreach (var o in Selection.objects)
                if (IsTileset(o, out var json, out var path)) CreateFor(json, path);
            AssetDatabase.SaveAssets();
        }

        /// <summary>코드에서 호출용 (배치 처리, CI 등)</summary>
        public static DungeonTileSet CreateFor(TextAsset json, string jsonPath)
        {
            DungeonTilesetData data;
            try { data = DungeonTilesetData.FromJson(json.text); }
            catch (System.ArgumentException e) { Debug.LogError($"{jsonPath}: {e.Message}"); return null; }

            var dir = Path.GetDirectoryName(jsonPath).Replace('\\', '/');
            var theme = Path.GetFileNameWithoutExtension(jsonPath);
            var texPath = $"{dir}/{(string.IsNullOrEmpty(data.image) ? theme + ".png" : data.image)}";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) { Debug.LogError($"{jsonPath}: 텍스처 {texPath}가 없습니다 (PNG를 JSON과 같은 폴더에 둔다)."); return null; }

            var outDir = $"{OutRoot}/{theme}";   // 예: Assets/Prefabs/Tileset/crypt
            EnsureFolder(outDir);
            var meshDir = $"{outDir}/Meshes";
            if (!AssetDatabase.IsValidFolder(meshDir)) AssetDatabase.CreateFolder(outDir, "Meshes");

            var mat = SaveMaterial($"{outDir}/{theme}.mat", tex);
            var objs = data.objects;

            // 벽·바닥: 변형마다 프리팹 하나, 피벗 = 바닥 가운데
            var walls = new List<GameObject>();
            foreach (var v in objs.wall.variants)
                walls.Add(SimplePrefab(data, objs.wall, v, mat, meshDir, outDir, Pascal(v.name)));
            var floors = new List<GameObject>();
            foreach (var v in objs.floor.variants)
                floors.Add(SimplePrefab(data, objs.floor, v, mat, meshDir, outDir, Pascal(v.name)));

            // 문: 벽 타일과 같은 크기 안에 문틀 2개 + 경첩 축에 매달린 문짝
            var door = DoorPrefab(data, mat, meshDir, outDir);

            // 바닥 가장자리 디더링 데칼: 투명 → 알파 컷아웃 머티리얼, 콜라이더·그림자 없음
            var edges = new List<GameObject>();
            if (data.HasFloorEdge)
            {
                var decalMat = SaveMaterial($"{outDir}/{theme}_decal.mat", tex, cutout: true);
                var es = objs.floor_edge.size;
                foreach (var v in objs.floor_edge.variants)
                {
                    var name = Pascal(v.name);
                    var mesh = SaveMesh($"{meshDir}/{name}.asset",
                        DungeonMeshBuilder.BuildDecal(data, v.faces.top, new Vector2(es.x, es.z), name));
                    var go = new GameObject(name);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = decalMat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    edges.Add(PrefabUtility.SaveAsPrefabAsset(go, $"{outDir}/{name}.prefab"));
                    Object.DestroyImmediate(go);
                }
            }

            var setPath = $"{outDir}/{theme}_TileSet.asset";
            var set = AssetDatabase.LoadAssetAtPath<DungeonTileSet>(setPath);
            if (set == null) { set = ScriptableObject.CreateInstance<DungeonTileSet>(); AssetDatabase.CreateAsset(set, setPath); }
            set.walls = walls.ToArray();
            set.floors = floors.ToArray();
            set.door = door;
            set.floorEdges = edges.ToArray();
            set.floorTopY = objs.floor.size.y;
            var rot = objs.floor_edge?.placement?.rotationBySide;
            if (rot != null) set.edgeRotation = new[] { rot.north, rot.east, rot.south, rot.west };
            EditorUtility.SetDirty(set);

            Debug.Log($"던전 타일 프리팹 생성: {outDir} (Wall {walls.Count}, Floor {floors.Count}, Door 1, FloorEdge {edges.Count})");
            return set;
        }

        static GameObject SimplePrefab(DungeonTilesetData data, DungeonTilesetData.ObjectDef def, DungeonTilesetData.Variant v,
                                       Material mat, string meshDir, string outDir, string name)
        {
            var size = def.size.ToVector3();
            var mesh = SaveMesh($"{meshDir}/{name}.asset",
                DungeonMeshBuilder.Build(data, v.faces, size, DungeonMeshBuilder.BottomCenter(size), name));
            var go = new GameObject(name);
            AddRenderer(go, mesh, mat);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{outDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject DoorPrefab(DungeonTilesetData data, Material mat, string meshDir, string outDir)
        {
            var o = data.objects;
            var tile = o.wall.size.ToVector3();            // 문 타일 = 벽 타일과 같은 크기
            var frameSize = o.door_frame.size.ToVector3();
            var leafSize = o.door_leaf.size.ToVector3();

            var frameMesh = SaveMesh($"{meshDir}/DoorFrame.asset",
                DungeonMeshBuilder.Build(data, o.door_frame.variants[0].faces, frameSize, DungeonMeshBuilder.BottomCenter(frameSize), "DoorFrame"));
            var leafMesh = SaveMesh($"{meshDir}/DoorLeaf.asset",
                DungeonMeshBuilder.Build(data, o.door_leaf.variants[0].faces, leafSize, DungeonMeshBuilder.HingeLeft(leafSize), "DoorLeaf"));

            var root = new GameObject("Door");                 // 피벗 = 문 타일 바닥 가운데 (벽 프리팹과 같다)
            var frameX = o.door_frame.placement?.x;
            if (frameX == null || frameX.Length < 2) frameX = new[] { 0f, tile.x - frameSize.x };
            for (int i = 0; i < 2; i++)
            {
                var f = new GameObject(i == 0 ? "FrameLeft" : "FrameRight");
                f.transform.SetParent(root.transform, false);
                // placement.x = 문 타일 왼쪽 끝 기준 문틀 왼쪽 끝 → 피벗(가운데) 기준으로 바꾼다
                f.transform.localPosition = new Vector3(-tile.x / 2 + frameX[i] + frameSize.x / 2, 0, 0);
                AddRenderer(f, frameMesh, mat);
            }

            var leafX = o.door_leaf.placement != null && o.door_leaf.placement.x > 0 ? o.door_leaf.placement.x : frameSize.x;
            var leafZ = o.door_leaf.placement != null ? o.door_leaf.placement.z : (tile.z - leafSize.z) / 2;
            var hinge = new GameObject("Hinge");
            hinge.transform.SetParent(root.transform, false);
            // 경첩 축 = 문짝 왼쪽 끝, 문짝 깊이 가운데
            hinge.transform.localPosition = new Vector3(-tile.x / 2 + leafX, 0, -tile.z / 2 + leafZ + leafSize.z / 2);
            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(hinge.transform, false);
            AddRenderer(leaf, leafMesh, mat);

            var door = root.AddComponent<DungeonDoor>();
            door.hinge = hinge.transform;
            door.openAngle = 90f;   // -Z(카메라 쪽, 방 쪽)으로 열린다

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{outDir}/Door.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddRenderer(GameObject go, Mesh mesh, Material mat)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.AddComponent<BoxCollider>();
            col.center = mesh.bounds.center;
            col.size = mesh.bounds.size;
        }

        static Mesh SaveMesh(string path, Mesh built)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { AssetDatabase.CreateAsset(built, path); return built; }
            var name = mesh.name;
            EditorUtility.CopySerialized(built, mesh);   // 같은 에셋을 갱신해 프리팹·인스턴스 참조를 유지
            mesh.name = name;
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Material SaveMaterial(string path, Texture2D tex, bool cutout = false)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                // URP면 URP/Lit, 아니면 Standard. 매트하게 (광택 0)
                var shader = GraphicsSettings.currentRenderPipeline != null
                    ? Shader.Find("Universal Render Pipeline/Lit")
                    : Shader.Find("Standard");
                mat = new Material(shader);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = tex;
            if (cutout) SetCutout(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>알파 0/255 텍스처용 알파 컷아웃 (URP/Lit, Standard 둘 다)</summary>
        static void SetCutout(Material mat)
        {
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);   // URP
            if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 1f);             // Standard: Cutout
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
        }

        /// <summary>wall_2 → Wall_2, door_frame → DoorFrame</summary>
        static string Pascal(string s)
        {
            var parts = s.Split('_');
            var outName = "";
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.Length == 0) continue;
                bool digits = char.IsDigit(p[0]);
                outName += (digits && i > 0 ? "_" : "") + (digits ? p : char.ToUpperInvariant(p[0]) + p.Substring(1));
            }
            return outName;
        }
    }
}
