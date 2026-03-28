using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EventRoomAsset))]
public class EventRoomAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EventRoomAsset asset = (EventRoomAsset)target;

        EditorGUILayout.HelpBox("Project asset is data-only. Create a hierarchy instance to edit room data and save changes.", MessageType.Info);

        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        if (GUILayout.Button("Create Instance In Hierarchy"))
        {
            EventRoomInstanceEditor.CreateSceneInstanceFromAsset(asset);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(8.0f);
        EditorGUILayout.LabelField("Asset Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Width", asset.RoomWidth.ToString());
        EditorGUILayout.LabelField("Height", asset.RoomHeight.ToString());
        EditorGUILayout.LabelField("Doors", asset.doors.Count.ToString());
        EditorGUILayout.LabelField("Prefabs", asset.prefabPlacements.Count.ToString());
        EditorGUILayout.LabelField("Spawn Points", asset.monsterSpawnPoints.Count.ToString());
    }
}