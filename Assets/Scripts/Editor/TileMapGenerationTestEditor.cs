using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// <see cref="TileMapGenerationTest"/> 인스펙터.
/// 'Generate Map' 을 누를 때마다 새 무작위 시드로 맵을 만들어 그린다(에디트/플레이 모드 모두).
/// </summary>
[CustomEditor(typeof(TileMapGenerationTest))]
public class TileMapGenerationTestEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var test = (TileMapGenerationTest)target;

        EditorGUILayout.Space();

        if (true == GUILayout.Button("Generate Map", GUILayout.Height(28)))
        {
            // 인스펙터의 seed 값과 무관하게 매번 새 시드로 만든다.
            test.GenerateRandomSingle();
            InternalEditorUtility.RepaintAllViews();
        }

        if (0 == test.LastSeed)
        {
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Last Seed", test.LastSeed.ToString());

            // 마음에 드는(또는 문제가 있는) 맵을 다시 보고 싶을 때 seed 필드로 옮겨 둔다.
            if (true == GUILayout.Button("Use as Seed", GUILayout.Width(100)))
            {
                Undo.RecordObject(test, "Use Last Seed");
                test.seed = test.LastSeed;
                EditorUtility.SetDirty(test);
            }
        }
    }
}
