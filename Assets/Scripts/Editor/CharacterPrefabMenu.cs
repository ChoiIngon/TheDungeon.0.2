using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

namespace Pixel.Dungeon.Editor
{
    /// <summary>
    /// Assets/Texture/character/ 아래 .aseprite 임포트 설정.
    /// - 캐릭터용 설정은 한 번만 넣고 .meta의 userData에 표식을 남긴다. 이후에는 덮어쓰지 않으므로 인스펙터에서 바꾼 값이 유지된다.
    ///   (importSettingsMissing을 쓰지 않는 이유: 패키지 설치 전에 들어온 파일은 DefaultImporter용 .meta가 먼저 생겨 false가 된다)
    ///   텍스처 압축은 임포터 기본값이 이미 Uncompressed다.
    /// - 한 번만 재생하는 동작(attack_*, damage_*, die) 클립의 루프를 끈다. 나머지(idle_*, move_*, skill_*)는
    ///   임포터 기본값(Aseprite 태그 Repeat이 무한이면 루프)을 따른다.
    /// </summary>
    public class CharacterAsepritePostprocessor : AssetPostprocessor
    {
        public const string CharacterRoot = "Assets/Texture/character/";
        static readonly string[] OneShotPrefixes = { "attack_", "damage_", "die" };

        void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith(CharacterRoot) || !(assetImporter is AsepriteImporter importer)) return;

            // 임포트 중에는 설정을 저장할 수 없으므로 임포트가 끝난 뒤 적용하고 다시 임포트한다
            if (!HasDefaults(importer))
            {
                var path = assetPath;
                EditorApplication.delayCall += () =>
                {
                    if (AssetImporter.GetAtPath(path) is AsepriteImporter i) EnsureDefaults(i);
                };
            }

            // 임포트 이벤트는 임포터 인스턴스마다 붙인다 (임포트할 때마다 새 인스턴스)
            importer.OnPostAsepriteImport += OnPostImport;
        }

        const string DefaultsMarker = "character-defaults";

        static bool HasDefaults(AsepriteImporter importer) =>
            importer.userData != null && importer.userData.Contains(DefaultsMarker);

        /// <summary>캐릭터용 임포트 설정을 아직 넣지 않았으면 넣고 다시 임포트한다. 넣었으면 true.</summary>
        public static bool EnsureDefaults(AsepriteImporter importer)
        {
            if (HasDefaults(importer)) return false;
            importer.importMode = FileImportModes.AnimatedSprite;
            importer.spritePixelsPerUnit = 32;                       // 32px = 1유닛
            importer.pivotAlignment = SpriteAlignment.BottomCenter;  // 발밑 = 타일 가운데
            importer.filterMode = FilterMode.Point;
            importer.generateAnimationClips = true;
            importer.generateModelPrefab = true;
            importer.userData = DefaultsMarker;
            importer.SaveAndReimport();
            Debug.Log($"캐릭터 임포트 설정 적용: {importer.assetPath} (PPU 32, 피벗 발밑, Point)");
            return true;
        }

        static void OnPostImport(AsepriteImporter.ImportEventArgs args)
        {
            var objects = new List<Object>();
            args.context.GetObjects(objects);
            foreach (var clip in objects.OfType<AnimationClip>())
            {
                if (!OneShotPrefixes.Any(p => clip.name.StartsWith(p))) continue;
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (!settings.loopTime) continue;
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
        }
    }

    /// <summary>
    /// Project 창에서 캐릭터 .aseprite 우클릭 → Create Character Prefab.
    /// Assets/Prefabs/Character/&lt;이름&gt;.prefab을 만든다:
    ///   &lt;이름&gt; (CharacterView)
    ///   └ Sprite (.aseprite 임포터가 만든 모델 프리팹 인스턴스: SpriteRenderer + Animator + AnimatorController)
    /// 다시 실행하면 기존 프리팹을 열어 참조만 갱신하므로, 루트에 붙인 컴포넌트(콜라이더, Actor 등)는 유지된다.
    /// 스프라이트·클립은 중첩 프리팹으로 연결되어 있어 .aseprite를 고치면 자동으로 반영된다.
    /// </summary>
    public static class CharacterPrefabMenu
    {
        const string MenuPath = "Assets/Create Character Prefab";
        const string OutDir = "Assets/Prefabs/Character";
        const string SpriteChild = "Sprite";

        static bool IsAseprite(Object o, out string path)
        {
            path = AssetDatabase.GetAssetPath(o);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".aseprite")
                && AssetImporter.GetAtPath(path) is AsepriteImporter;
        }

        [MenuItem(MenuPath, true)]
        static bool Validate() => Selection.objects.Any(o => IsAseprite(o, out _));

        [MenuItem(MenuPath)]
        static void Create()
        {
            GameObject last = null;
            foreach (var o in Selection.objects)
                if (IsAseprite(o, out var path))
                    last = CreateFor(path) ?? last;
            AssetDatabase.SaveAssets();
            if (last != null) EditorGUIUtility.PingObject(last);
        }

        /// <summary>코드에서 호출용 (배치 처리 등)</summary>
        public static GameObject CreateFor(string asepritePath)
        {
            if (asepritePath.StartsWith(CharacterAsepritePostprocessor.CharacterRoot)
                && AssetImporter.GetAtPath(asepritePath) is AsepriteImporter importer)
                CharacterAsepritePostprocessor.EnsureDefaults(importer);

            var model = AssetDatabase.LoadMainAssetAtPath(asepritePath) as GameObject;
            if (model == null)
            {
                Debug.LogError($"{asepritePath}: 모델 프리팹이 없습니다. 임포트 설정에서 Import Mode = Animated Sprite, Generate Model Prefab을 켠다.");
                return null;
            }
            if (model.GetComponentInChildren<Animator>() == null)
            {
                Debug.LogError($"{asepritePath}: Animator가 없습니다. 임포트 설정에서 Generate Animation Clips를 켠다.");
                return null;
            }

            var charName = CharacterName(asepritePath);
            EnsureFolder(OutDir);
            var prefabPath = $"{OutDir}/{charName}.prefab";

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject(charName);
            try
            {
                // 이전 스프라이트 자식이 다른 .aseprite를 가리키면 교체한다
                var sprite = root.transform.Find(SpriteChild);
                if (sprite != null && PrefabUtility.GetCorrespondingObjectFromSource(sprite.gameObject) != model)
                {
                    Object.DestroyImmediate(sprite.gameObject);
                    sprite = null;
                }
                if (sprite == null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                    inst.name = SpriteChild;
                    sprite = inst.transform;
                    sprite.localPosition = Vector3.zero;
                    sprite.localRotation = Quaternion.Euler(90f, 0f, 0f); // 카메라가 없을 때(에디터) 기본: 바닥에서 위를 보게
                    sprite.localScale = Vector3.one;
                }

                var sr = sprite.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.spriteSortPoint = SpriteSortPoint.Pivot; // 발밑 기준으로 앞뒤 정렬

                var view = root.GetComponent<CharacterView>();
                if (view == null) view = root.AddComponent<CharacterView>(); // 에디터의 가짜 null 때문에 ??를 쓰지 않는다
                var so = new SerializedObject(view);
                so.FindProperty("animator").objectReferenceValue = sprite.GetComponentInChildren<Animator>();
                so.FindProperty("spriteRenderer").objectReferenceValue = sr;
                so.ApplyModifiedPropertiesWithoutUndo();

                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"{(exists ? "갱신" : "생성")}: {prefabPath} ← {asepritePath}", saved);
                return saved;
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        /// <summary>Assets/Texture/character/&lt;name&gt;/&lt;name&gt;_1x1t.aseprite → &lt;name&gt;. 폴더가 없으면 파일명에서 _&lt;W&gt;x&lt;H&gt;t를 뗀다.</summary>
        static string CharacterName(string path)
        {
            var dir = Path.GetDirectoryName(path).Replace('\\', '/');
            if (dir.StartsWith(CharacterAsepritePostprocessor.CharacterRoot.TrimEnd('/') + "/"))
                return Path.GetFileName(dir);
            return System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(path), @"_\d+x\d+t$", "");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
