using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(EventRoomInstance))]
public class EventRoomInstanceEditor : Editor
{
    private const float CellSize = 22.0f;
    private const float WallInsetRatio = 0.2f / 4.5f;
    private const float PreviewWallHeight = 2.0f;
    private const float PreviewWallThickness = 0.12f;
    private const float PreviewMarkerHeight = 0.2f;

    private bool scenePreviewEnabled = true;
    private bool showPlacementMarkers = true;
    private bool showPrefabMeshes = true;
    private bool showWallMeshes = true;
    private bool showFloorMeshes = true;
    private bool useInstanceTransformAsPivot = true;
    private float previewTileSize = Dungeon.TileSize;
    private Vector3 previewPivot = Vector3.zero;
    private int selectedX = 1;
    private int selectedY = 1;

    private GameObject previewRoot;
    private readonly List<GameObject> previewInstances = new List<GameObject>();
    private int previewSignature = int.MinValue;

    private const string PreviewRootName = "__EventRoomPreview";
    private const string PreviewFloorRootName = "Floor";
    private const string PreviewWallRootName = "Wall";
    private const string PreviewPrefabRootName = "Prefabs";
    private const string PreviewSpawnRootName = "SpawnPoints";

    private struct DoorKey
    {
        public EventRoomAsset.DoorSide side;
        public int offset;

        public DoorKey(EventRoomAsset.DoorSide side, int offset)
        {
            this.side = side;
            this.offset = offset;
        }
    }

    [MenuItem("GameObject/Dungeon/Event Room", false, 10)]
    private static void CreateEventRoomInstance(MenuCommand command)
    {
        CreateSceneInstanceFromAsset(null, command.context as GameObject);
    }

    [MenuItem("GameObject/Dungeon/Event Room", true)]
    private static bool ValidateCreateEventRoomInstance()
    {
        return !EditorApplication.isPlaying;
    }

    public static EventRoomInstance CreateSceneInstanceFromAsset(EventRoomAsset asset, GameObject parent = null)
    {
        string objectName = (asset == null || string.IsNullOrEmpty(asset.name)) ? "EventRoomInstance" : asset.name;

        GameObject root = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(root, "Create Event Room Instance");

        EventRoomInstance instance = Undo.AddComponent<EventRoomInstance>(root);
        instance.width = 9;
        instance.height = 9;

        if (asset != null)
        {
            instance.LoadFromAsset(asset);
        }

        GameObjectUtility.SetParentAndAlign(root, parent);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        EditorUtility.SetDirty(instance);
        return instance;
    }

    public override void OnInspectorGUI()
    {
        EventRoomInstance instance = (EventRoomInstance)target;
        SyncPlacementDataFromChildren(instance, GetPreviewPivot(instance));
        EditorGUI.BeginChangeCheck();

        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eventRoomAsset"));
        EditorGUILayout.Space(6.0f);
        DrawRoomGridLayout(instance);

        EditorGUILayout.LabelField("Room Data", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("width"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("height"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("wallPrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("floorPrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("doors"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("prefabPlacements"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("monsterSpawnPoints"), true);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8.0f);
        
        DrawQuickPlacementButtons(instance);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load From Linked Asset"))
            {
                if (instance.eventRoomAsset == null)
                {
                    EditorUtility.DisplayDialog("Event Room", "Linked asset is not assigned.", "OK");
                }
                else
                {
                    Undo.RecordObject(instance, "Load Event Room From Asset");
                    instance.LoadFromAsset(instance.eventRoomAsset);
                    EditorUtility.SetDirty(instance);
                }
            }

            if (GUILayout.Button("Save"))
            {
                SaveInstanceToAsset(instance);
            }
        }

        EditorGUILayout.Space(8.0f);
        DrawScenePreviewOptions(instance);

        EditorGUILayout.HelpBox("Edit room data on this instance, then click Save to create/update a ScriptableObject in Assets/EventRooms using this instance name.", MessageType.Info);

        if (EditorGUI.EndChangeCheck())
        {
            previewSignature = int.MinValue;
            SceneView.RepaintAll();
        }
    }

    private void OnDisable()
    {
        previewInstances.Clear();
        previewRoot = null;
    }

    private void DrawScenePreviewOptions(EventRoomInstance instance)
    {
        EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);
        scenePreviewEnabled = EditorGUILayout.Toggle("Enable Scene Preview", scenePreviewEnabled);
        showPlacementMarkers = EditorGUILayout.Toggle("Show Placement Markers", showPlacementMarkers);
        showPrefabMeshes = EditorGUILayout.Toggle("Show Prefab Meshes", showPrefabMeshes);
        showFloorMeshes = EditorGUILayout.Toggle("Show Floor Meshes", showFloorMeshes);
        showWallMeshes = EditorGUILayout.Toggle("Show Wall Meshes", showWallMeshes);
        useInstanceTransformAsPivot = EditorGUILayout.Toggle("Use Instance Transform As Pivot", useInstanceTransformAsPivot);
        previewTileSize = EditorGUILayout.Slider("Preview Tile Size", previewTileSize, 0.25f, 16.0f);

        if (GUILayout.Button("Use Dungeon.TileSize"))
        {
            previewTileSize = Dungeon.TileSize;
            previewSignature = int.MinValue;
            SceneView.RepaintAll();
        }

        if (!useInstanceTransformAsPivot)
        {
            previewPivot = EditorGUILayout.Vector3Field("Preview Pivot", previewPivot);
        }

        if (GUILayout.Button("Frame Preview"))
        {
            FrameScenePreview(instance);
        }
    }

    private void DrawRoomGridLayout(EventRoomInstance instance)
    {
        int width = Mathf.Max(5, instance.width);
        int height = Mathf.Max(5, instance.height);

        selectedX = Mathf.Clamp(selectedX, 0, width - 1);
        selectedY = Mathf.Clamp(selectedY, 0, height - 1);

        EditorGUILayout.LabelField("Grid Layout", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Perimeter cells (except corners) are wall segments. Click perimeter to toggle door; click any cell to select.", MessageType.None);

        HashSet<string> doorSet = BuildDoorSet(instance);

        GUILayout.BeginVertical("box");
        for (int y = height - 1; y >= 0; y--)
        {
            GUILayout.BeginHorizontal();
            for (int x = 0; x < width; x++)
            {
                bool isCorner = IsCorner(x, y, width, height);
                bool isPerimeter = IsPerimeter(x, y, width, height);
                bool isSelected = x == selectedX && y == selectedY;

                DoorKey key;
                bool hasDoor = TryGetDoorKey(width, height, x, y, out key) && doorSet.Contains(GetDoorKeyString(key));

                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = GetCellColor(isCorner, isPerimeter, isSelected, hasDoor);

                if (GUILayout.Button(string.Empty, GUILayout.Width(CellSize), GUILayout.Height(CellSize)))
                {
                    selectedX = x;
                    selectedY = y;

                    if (isPerimeter && !isCorner)
                    {
                        ToggleDoor(instance, key, hasDoor);
                        doorSet = BuildDoorSet(instance);
                    }
                }

                GUI.backgroundColor = previousColor;
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        EditorGUILayout.LabelField("Selected Tile", "(" + selectedX + ", " + selectedY + ")");
    }

    private void DrawQuickPlacementButtons(EventRoomInstance instance)
    {
        EditorGUILayout.Space(6.0f);
        EditorGUILayout.LabelField("Quick Placement", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Prefab At Selected Tile"))
            {
                Undo.RecordObject(instance, "Add Prefab Placement");
                instance.prefabPlacements.Add(new EventRoomAsset.PrefabPlacement
                {
                    tilePosition = new Vector2Int(selectedX, selectedY)
                });
                EditorUtility.SetDirty(instance);
                previewSignature = int.MinValue;
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Add Spawn Point At Selected Tile"))
            {
                Undo.RecordObject(instance, "Add Spawn Point");
                instance.monsterSpawnPoints.Add(new EventRoomAsset.MonsterSpawnPoint
                {
                    tilePosition = new Vector2Int(selectedX, selectedY)
                });
                EditorUtility.SetDirty(instance);
                previewSignature = int.MinValue;
                SceneView.RepaintAll();
            }
        }
    }

    private void ToggleDoor(EventRoomInstance instance, DoorKey key, bool hasDoor)
    {
        Undo.RecordObject(instance, "Toggle Event Room Door");

        if (hasDoor)
        {
            for (int i = instance.doors.Count - 1; i >= 0; i--)
            {
                EventRoomAsset.DoorDefinition door = instance.doors[i];
                if (door == null)
                {
                    continue;
                }

                if (door.side == key.side && door.offset == key.offset)
                {
                    instance.doors.RemoveAt(i);
                }
            }
        }
        else
        {
            instance.doors.Add(new EventRoomAsset.DoorDefinition
            {
                side = key.side,
                offset = key.offset
            });
        }

        EditorUtility.SetDirty(instance);
        previewSignature = int.MinValue;
        SceneView.RepaintAll();
    }

    private static Color GetCellColor(bool isCorner, bool isPerimeter, bool isSelected, bool hasDoor)
    {
        if (isSelected)
        {
            return new Color(0.35f, 0.7f, 1.0f, 1.0f);
        }

        if (hasDoor)
        {
            return new Color(0.25f, 0.8f, 0.45f, 1.0f);
        }

        if (isCorner)
        {
            return new Color(0.25f, 0.25f, 0.25f, 1.0f);
        }

        if (isPerimeter)
        {
            return new Color(0.95f, 0.75f, 0.25f, 1.0f);
        }

        return new Color(0.55f, 0.55f, 0.55f, 1.0f);
    }

    private void OnSceneGUI()
    {
        EventRoomInstance instance = (EventRoomInstance)target;
        if (instance == null || !scenePreviewEnabled)
        {
            ClearPreviewInstances();
            return;
        }

        Vector3 pivot = GetPreviewPivot(instance);

        if (showPrefabMeshes || showWallMeshes || showFloorMeshes)
        {
            EnsurePreviewInstances(instance, pivot);
        }
        else
        {
            ClearPreviewInstances();
        }

        DrawRoomPreview(instance, pivot);
        DrawDoorPreview(instance, pivot);

        if (showPlacementMarkers)
        {
            DrawPlacementPreview(instance, pivot);
        }

        if (SyncPlacementDataFromChildren(instance, pivot))
        {
            previewSignature = BuildPreviewSignature(instance, pivot);
            EditorUtility.SetDirty(instance);
            Repaint();
        }
    }

    private void EnsurePreviewInstances(EventRoomInstance instance, Vector3 pivot)
    {
        if (instance == null || !scenePreviewEnabled || (!showPrefabMeshes && !showWallMeshes && !showFloorMeshes))
        {
            ClearPreviewInstances();
            return;
        }

        TryAttachExistingPreviewRoot(instance);

        int currentSignature = BuildPreviewSignature(instance, pivot);
        if (previewRoot != null && previewSignature == currentSignature)
        {
            return;
        }

        RebuildPreviewInstances(instance, pivot);
        previewSignature = currentSignature;
    }

    private int BuildPreviewSignature(EventRoomInstance instance, Vector3 pivot)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 23) + instance.GetInstanceID();
            hash = (hash * 23) + Mathf.Max(5, instance.width);
            hash = (hash * 23) + Mathf.Max(5, instance.height);
            hash = (hash * 23) + previewTileSize.GetHashCode();
            hash = (hash * 23) + pivot.GetHashCode();
            hash = (hash * 23) + (showPrefabMeshes ? 1 : 0);
            hash = (hash * 23) + (showFloorMeshes ? 1 : 0);
            hash = (hash * 23) + (showWallMeshes ? 1 : 0);
            int wallPrefabId = instance.wallPrefab == null ? 0 : instance.wallPrefab.GetInstanceID();
            hash = (hash * 23) + wallPrefabId;
            int floorPrefabId = instance.floorPrefab == null ? 0 : instance.floorPrefab.GetInstanceID();
            hash = (hash * 23) + floorPrefabId;
            hash = (hash * 23) + instance.prefabPlacements.Count;
            hash = (hash * 23) + instance.doors.Count;

            for (int i = 0; i < instance.prefabPlacements.Count; i++)
            {
                EventRoomAsset.PrefabPlacement placement = instance.prefabPlacements[i];
                if (placement == null)
                {
                    continue;
                }

                int prefabId = placement.prefab == null ? 0 : placement.prefab.GetInstanceID();
                hash = (hash * 23) + prefabId;
                hash = (hash * 23) + placement.tilePosition.GetHashCode();
                hash = (hash * 23) + placement.localOffset.GetHashCode();
                hash = (hash * 23) + placement.localEulerAngles.GetHashCode();
            }

            for (int i = 0; i < instance.doors.Count; i++)
            {
                EventRoomAsset.DoorDefinition door = instance.doors[i];
                if (door == null)
                {
                    continue;
                }

                hash = (hash * 23) + ((int)door.side);
                hash = (hash * 23) + door.offset;
                hash = (hash * 23) + (door.prefab == null ? 0 : door.prefab.GetInstanceID());
            }

            return hash;
        }
    }

    private void RebuildPreviewInstances(EventRoomInstance instance, Vector3 pivot)
    {
        ClearPreviewInstances();

        if (instance == null)
        {
            return;
        }

        Transform existingRoot = instance.transform.Find(PreviewRootName);
        if (existingRoot != null)
        {
            previewRoot = existingRoot.gameObject;
            ClearTransformChildren(previewRoot.transform);
        }
        else
        {
            previewRoot = new GameObject(PreviewRootName);
            previewRoot.transform.SetParent(instance.transform, false);
            previewRoot.hideFlags = HideFlags.None;
        }

        int width = Mathf.Max(5, instance.width);
        int height = Mathf.Max(5, instance.height);

        if (showFloorMeshes && instance.floorPrefab != null)
        {
            CreateFloorPreviewMeshes(instance, pivot, width, height);
        }

        if (showWallMeshes && instance.wallPrefab != null)
        {
            CreateWallPreviewMeshes(instance, pivot, width, height);
        }

        if (!showPrefabMeshes)
        {
            return;
        }

        for (int i = 0; i < instance.prefabPlacements.Count; i++)
        {
            EventRoomAsset.PrefabPlacement placement = instance.prefabPlacements[i];
            if (placement == null || placement.prefab == null)
            {
                continue;
            }

            GameObject previewObject = PrefabUtility.InstantiatePrefab(placement.prefab) as GameObject;
            if (previewObject == null)
            {
                previewObject = Instantiate(placement.prefab);
            }

            if (previewObject == null)
            {
                continue;
            }

            previewObject.transform.SetParent(previewRoot.transform, true);
            previewObject.transform.position = LocalTileCenter(
                pivot,
                Mathf.Clamp(placement.tilePosition.x, 0, width - 1),
                Mathf.Clamp(placement.tilePosition.y, 0, height - 1)) + placement.localOffset;
            previewObject.transform.rotation = Quaternion.Euler(placement.localEulerAngles);

            EventRoomPreviewBinding binding = previewObject.GetComponent<EventRoomPreviewBinding>();
            if (binding == null)
            {
                binding = previewObject.AddComponent<EventRoomPreviewBinding>();
            }
            binding.nodeType = EventRoomPreviewBinding.NodeType.PrefabPlacement;
            binding.index = i;

            SetActiveRecursively(previewObject, true);
            ApplyHideFlagsRecursively(previewObject);
            EnsureRendererEnabled(previewObject);
            previewInstances.Add(previewObject);
        }

        for (int i = 0; i < instance.monsterSpawnPoints.Count; i++)
        {
            EventRoomAsset.MonsterSpawnPoint spawnPoint = instance.monsterSpawnPoints[i];
            if (spawnPoint == null)
            {
                continue;
            }

            Vector3 position = LocalTileCenter(
                pivot,
                Mathf.Clamp(spawnPoint.tilePosition.x, 0, width - 1),
                Mathf.Clamp(spawnPoint.tilePosition.y, 0, height - 1));
            position += spawnPoint.localOffset;

            GameObject spawnNode = new GameObject(string.IsNullOrEmpty(spawnPoint.id) ? "SpawnPoint_" + i.ToString() : spawnPoint.id);
            spawnNode.transform.SetParent(previewRoot.transform, true);
            spawnNode.transform.position = position;
            spawnNode.transform.rotation = Quaternion.Euler(spawnPoint.localEulerAngles);

            EventRoomPreviewBinding binding = spawnNode.AddComponent<EventRoomPreviewBinding>();
            binding.nodeType = EventRoomPreviewBinding.NodeType.MonsterSpawnPoint;
            binding.index = i;

            previewInstances.Add(spawnNode);
        }
    }

    private void CreateFloorPreviewMeshes(EventRoomInstance instance, Vector3 pivot, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject floorObject = PrefabUtility.InstantiatePrefab(instance.floorPrefab) as GameObject;
                if (floorObject == null)
                {
                    floorObject = Instantiate(instance.floorPrefab);
                }

                if (floorObject == null)
                {
                    continue;
                }

                Vector3 position = LocalTileCenter(pivot, x, y);
                position.y = Dungeon.FloorHeightOffset;

                floorObject.transform.SetParent(previewRoot.transform, true);
                floorObject.transform.position = position;
                floorObject.transform.rotation = Quaternion.identity;

                SetActiveRecursively(floorObject, true);
                ApplyHideFlagsRecursively(floorObject);
                EnsureRendererEnabled(floorObject);
                previewInstances.Add(floorObject);
            }
        }
    }

    private void CreateWallPreviewMeshes(EventRoomInstance instance, Vector3 pivot, int width, int height)
    {
        for (int x = 0; x < width; x++)
        {
            CreateWallMeshIfNeeded(instance, pivot, width, height, x, height - 1, new Vector3(0.0f, 0.0f, previewTileSize * (0.5f - WallInsetRatio)), 180.0f);
            CreateWallMeshIfNeeded(instance, pivot, width, height, x, 0, new Vector3(0.0f, 0.0f, -previewTileSize * (0.5f - WallInsetRatio)), 0.0f);
        }

        for (int y = 0; y < height; y++)
        {
            CreateWallMeshIfNeeded(instance, pivot, width, height, 0, y, new Vector3(-previewTileSize * (0.5f - WallInsetRatio), 0.0f, 0.0f), 90.0f);
            CreateWallMeshIfNeeded(instance, pivot, width, height, width - 1, y, new Vector3(previewTileSize * (0.5f - WallInsetRatio), 0.0f, 0.0f), 270.0f);
        }
    }

    private void CreateWallMeshIfNeeded(EventRoomInstance instance, Vector3 pivot, int width, int height, int x, int y, Vector3 offset, float rotationY)
    {
        EventRoomAsset.DoorDefinition doorDefinition;
        bool hasDoor = TryGetDoorDefinition(width, height, x, y, instance.doors, out doorDefinition);

        if (hasDoor && doorDefinition != null && doorDefinition.prefab != null)
        {
            GameObject doorObject = PrefabUtility.InstantiatePrefab(doorDefinition.prefab) as GameObject;
            if (doorObject == null)
            {
                doorObject = Instantiate(doorDefinition.prefab);
            }

            if (doorObject == null)
            {
                return;
            }

            doorObject.transform.SetParent(previewRoot.transform, true);
            doorObject.transform.position = LocalTileCenter(pivot, x, y) + offset;
            doorObject.transform.rotation = Quaternion.Euler(0.0f, rotationY, 0.0f);

            SetActiveRecursively(doorObject, true);
            ApplyHideFlagsRecursively(doorObject);
            EnsureRendererEnabled(doorObject);
            previewInstances.Add(doorObject);
            return;
        }

        if (hasDoor)
        {
            return;
        }

        GameObject wallObject = PrefabUtility.InstantiatePrefab(instance.wallPrefab) as GameObject;
        if (wallObject == null)
        {
            wallObject = Instantiate(instance.wallPrefab);
        }

        if (wallObject == null)
        {
            return;
        }

        wallObject.transform.SetParent(previewRoot.transform, true);
        wallObject.transform.position = LocalTileCenter(pivot, x, y) + offset;
        wallObject.transform.rotation = Quaternion.Euler(0.0f, rotationY, 0.0f);

        SetActiveRecursively(wallObject, true);
        ApplyHideFlagsRecursively(wallObject);
        EnsureRendererEnabled(wallObject);
        previewInstances.Add(wallObject);
    }

    private bool IsDoorTile(int width, int height, int x, int y, HashSet<string> doorSet)
    {
        DoorKey key;
        if (!TryGetDoorKey(width, height, x, y, out key))
        {
            return false;
        }

        return doorSet.Contains(GetDoorKeyString(key));
    }

    private bool TryGetDoorDefinition(int width, int height, int x, int y, List<EventRoomAsset.DoorDefinition> doors, out EventRoomAsset.DoorDefinition found)
    {
        found = null;

        DoorKey key;
        if (!TryGetDoorKey(width, height, x, y, out key))
        {
            return false;
        }

        for (int i = 0; i < doors.Count; i++)
        {
            EventRoomAsset.DoorDefinition door = doors[i];
            if (door == null)
            {
                continue;
            }

            if (door.side == key.side && door.offset == key.offset)
            {
                found = door;
                return true;
            }
        }

        return false;
    }

    private static void SetActiveRecursively(GameObject root, bool active)
    {
        root.SetActive(active);
        for (int i = 0; i < root.transform.childCount; i++)
        {
            SetActiveRecursively(root.transform.GetChild(i).gameObject, active);
        }
    }

    private static void ApplyHideFlagsRecursively(GameObject root)
    {
        root.hideFlags = HideFlags.DontSaveInEditor;
        for (int i = 0; i < root.transform.childCount; i++)
        {
            ApplyHideFlagsRecursively(root.transform.GetChild(i).gameObject);
        }
    }

    private static void EnsureRendererEnabled(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
        }
    }

    private void ClearPreviewInstances()
    {
        for (int i = 0; i < previewInstances.Count; i++)
        {
            if (previewInstances[i] != null)
            {
                DestroyImmediate(previewInstances[i]);
            }
        }
        previewInstances.Clear();

        if (previewRoot != null)
        {
            ClearTransformChildren(previewRoot.transform);
        }
    }

    private bool SyncPlacementDataFromChildren(EventRoomInstance instance, Vector3 pivot)
    {
        if (instance == null)
        {
            return false;
        }

        TryAttachExistingPreviewRoot(instance);
        if (previewRoot == null)
        {
            return false;
        }

        bool changed = false;
        int width = Mathf.Max(5, instance.width);
        int height = Mathf.Max(5, instance.height);

        EventRoomPreviewBinding[] bindings = previewRoot.GetComponentsInChildren<EventRoomPreviewBinding>(true);
        for (int i = 0; i < bindings.Length; i++)
        {
            EventRoomPreviewBinding binding = bindings[i];
            if (binding == null)
            {
                continue;
            }

            if (binding.nodeType == EventRoomPreviewBinding.NodeType.PrefabPlacement)
            {
                if (binding.index < 0 || binding.index >= instance.prefabPlacements.Count)
                {
                    continue;
                }

                EventRoomAsset.PrefabPlacement placement = instance.prefabPlacements[binding.index];
                if (placement == null)
                {
                    continue;
                }

                Vector3 basePosition = LocalTileCenter(
                    pivot,
                    Mathf.Clamp(placement.tilePosition.x, 0, width - 1),
                    Mathf.Clamp(placement.tilePosition.y, 0, height - 1));
                Vector3 nextOffset = binding.transform.position - basePosition;
                Vector3 nextEuler = binding.transform.eulerAngles;

                if (placement.localOffset != nextOffset)
                {
                    placement.localOffset = nextOffset;
                    changed = true;
                }

                if (placement.localEulerAngles != nextEuler)
                {
                    placement.localEulerAngles = nextEuler;
                    changed = true;
                }
            }
            else if (binding.nodeType == EventRoomPreviewBinding.NodeType.MonsterSpawnPoint)
            {
                if (binding.index < 0 || binding.index >= instance.monsterSpawnPoints.Count)
                {
                    continue;
                }

                EventRoomAsset.MonsterSpawnPoint spawnPoint = instance.monsterSpawnPoints[binding.index];
                if (spawnPoint == null)
                {
                    continue;
                }

                Vector3 basePosition = LocalTileCenter(
                    pivot,
                    Mathf.Clamp(spawnPoint.tilePosition.x, 0, width - 1),
                    Mathf.Clamp(spawnPoint.tilePosition.y, 0, height - 1));
                Vector3 nextOffset = binding.transform.position - basePosition;
                Vector3 nextEuler = binding.transform.eulerAngles;

                if (spawnPoint.localOffset != nextOffset)
                {
                    spawnPoint.localOffset = nextOffset;
                    changed = true;
                }

                if (spawnPoint.localEulerAngles != nextEuler)
                {
                    spawnPoint.localEulerAngles = nextEuler;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorUtility.SetDirty(instance);
        }

        return changed;
    }

    private bool IsPreviewObjectSelected()
    {
        if (previewRoot == null || Selection.activeGameObject == null)
        {
            return false;
        }

        return Selection.activeGameObject.transform.IsChildOf(previewRoot.transform) ||
               Selection.activeGameObject == previewRoot;
    }

    private void TryAttachExistingPreviewRoot(EventRoomInstance instance)
    {
        if (previewRoot != null || instance == null)
        {
            return;
        }

        Transform existingRoot = instance.transform.Find(PreviewRootName);
        if (existingRoot == null)
        {
            return;
        }

        previewRoot = existingRoot.gameObject;
    }

    private static void ClearTransformChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private void DrawRoomPreview(EventRoomInstance instance, Vector3 pivot)
    {
        int width = Mathf.Max(5, instance.width);
        int height = Mathf.Max(5, instance.height);

        Color fillColor = new Color(0.18f, 0.18f, 0.2f, 0.2f);
        Color gridColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 center = LocalTileCenter(pivot, x, y);
                float half = previewTileSize * 0.5f;
                Vector3[] quad = new Vector3[]
                {
                    center + new Vector3(-half, 0.0f, -half),
                    center + new Vector3(-half, 0.0f, +half),
                    center + new Vector3(+half, 0.0f, +half),
                    center + new Vector3(+half, 0.0f, -half),
                };

                Handles.DrawSolidRectangleWithOutline(quad, fillColor, gridColor);
            }
        }
    }

    private void DrawDoorPreview(EventRoomInstance instance, Vector3 pivot)
    {
        int width = Mathf.Max(5, instance.width);
        int height = Mathf.Max(5, instance.height);
        HashSet<string> doorSet = BuildDoorSet(instance);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsPerimeter(x, y, width, height) || IsCorner(x, y, width, height))
                {
                    continue;
                }

                DoorKey key;
                if (!TryGetDoorKey(width, height, x, y, out key))
                {
                    continue;
                }

                bool hasDoor = doorSet.Contains(GetDoorKeyString(key));
                DrawPerimeterSegment(pivot, x, y, key.side, hasDoor);
            }
        }
    }

    private void DrawPerimeterSegment(Vector3 pivot, int x, int y, EventRoomAsset.DoorSide side, bool hasDoor)
    {
        Vector3 center = LocalTileCenter(pivot, x, y);
        float half = previewTileSize * 0.5f;

        Vector3 wallCenter = center;
        Vector3 wallSize = Vector3.one;

        switch (side)
        {
            case EventRoomAsset.DoorSide.Top:
                wallCenter += new Vector3(0.0f, PreviewWallHeight * 0.5f, half);
                wallSize = new Vector3(previewTileSize, PreviewWallHeight, PreviewWallThickness);
                break;
            case EventRoomAsset.DoorSide.Right:
                wallCenter += new Vector3(half, PreviewWallHeight * 0.5f, 0.0f);
                wallSize = new Vector3(PreviewWallThickness, PreviewWallHeight, previewTileSize);
                break;
            case EventRoomAsset.DoorSide.Bottom:
                wallCenter += new Vector3(0.0f, PreviewWallHeight * 0.5f, -half);
                wallSize = new Vector3(previewTileSize, PreviewWallHeight, PreviewWallThickness);
                break;
            case EventRoomAsset.DoorSide.Left:
                wallCenter += new Vector3(-half, PreviewWallHeight * 0.5f, 0.0f);
                wallSize = new Vector3(PreviewWallThickness, PreviewWallHeight, previewTileSize);
                break;
        }

        Color color = hasDoor ? new Color(0.15f, 0.85f, 0.35f, 0.8f) : new Color(0.9f, 0.25f, 0.25f, 0.65f);
        Handles.color = color;

        Matrix4x4 previousMatrix = Handles.matrix;
        Handles.matrix = Matrix4x4.TRS(wallCenter, Quaternion.identity, wallSize);
        Handles.DrawWireCube(Vector3.zero, Vector3.one);
        Handles.matrix = previousMatrix;
    }

    private void DrawPlacementPreview(EventRoomInstance instance, Vector3 pivot)
    {
        int width = Mathf.Max(5, instance.width);
        int height = Mathf.Max(5, instance.height);

        Handles.color = new Color(0.0f, 0.85f, 1.0f, 0.9f);
        foreach (EventRoomAsset.PrefabPlacement placement in instance.prefabPlacements)
        {
            if (placement == null)
            {
                continue;
            }

            Vector3 position = LocalTileCenter(
                pivot,
                Mathf.Clamp(placement.tilePosition.x, 0, width - 1),
                Mathf.Clamp(placement.tilePosition.y, 0, height - 1));
            position += placement.localOffset;
            position.y += PreviewMarkerHeight;

            Handles.DrawWireDisc(position, Vector3.up, previewTileSize * 0.2f);
            if (!string.IsNullOrEmpty(placement.id))
            {
                Handles.Label(position + Vector3.up * 0.3f, "P: " + placement.id);
            }
        }

        Handles.color = new Color(1.0f, 0.6f, 0.15f, 0.95f);
        foreach (EventRoomAsset.MonsterSpawnPoint spawnPoint in instance.monsterSpawnPoints)
        {
            if (spawnPoint == null)
            {
                continue;
            }

            Vector3 position = LocalTileCenter(
                pivot,
                Mathf.Clamp(spawnPoint.tilePosition.x, 0, width - 1),
                Mathf.Clamp(spawnPoint.tilePosition.y, 0, height - 1));
            position += spawnPoint.localOffset;
            position.y += PreviewMarkerHeight;

            Handles.ConeHandleCap(0, position, Quaternion.Euler(90.0f, 0.0f, 0.0f), previewTileSize * 0.26f, EventType.Repaint);
            if (!string.IsNullOrEmpty(spawnPoint.id))
            {
                Handles.Label(position + Vector3.up * 0.3f, "S: " + spawnPoint.id);
            }
        }
    }

    private Vector3 LocalTileCenter(Vector3 pivot, int x, int y)
    {
        float px = (x + 0.5f) * previewTileSize;
        float pz = (y + 0.5f) * previewTileSize;
        return pivot + new Vector3(px, 0.0f, pz);
    }

    private Vector3 GetPreviewPivot(EventRoomInstance instance)
    {
        if (useInstanceTransformAsPivot && instance != null)
        {
            return instance.transform.position;
        }

        return previewPivot;
    }

    private void FrameScenePreview(EventRoomInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        Vector3 pivot = GetPreviewPivot(instance);
        int width = Mathf.Max(5, instance.width);
        int height = Mathf.Max(5, instance.height);

        Vector3 roomCenter = pivot + new Vector3(width * previewTileSize * 0.5f, 0.0f, height * previewTileSize * 0.5f);
        float radius = Mathf.Max(width, height) * previewTileSize;

        SceneView lastActive = SceneView.lastActiveSceneView;
        if (lastActive == null)
        {
            return;
        }

        lastActive.pivot = roomCenter;
        lastActive.size = radius;
        lastActive.Repaint();
    }

    private static HashSet<string> BuildDoorSet(EventRoomInstance instance)
    {
        HashSet<string> set = new HashSet<string>();
        if (instance == null)
        {
            return set;
        }

        foreach (EventRoomAsset.DoorDefinition door in instance.doors)
        {
            if (door == null)
            {
                continue;
            }

            set.Add(GetDoorKeyString(new DoorKey(door.side, door.offset)));
        }

        return set;
    }

    private static bool IsPerimeter(int x, int y, int width, int height)
    {
        return x == 0 || x == width - 1 || y == 0 || y == height - 1;
    }

    private static bool IsCorner(int x, int y, int width, int height)
    {
        bool onLeftOrRight = x == 0 || x == width - 1;
        bool onTopOrBottom = y == 0 || y == height - 1;
        return onLeftOrRight && onTopOrBottom;
    }

    private static bool TryGetDoorKey(int width, int height, int x, int y, out DoorKey key)
    {
        key = default;

        if (!IsPerimeter(x, y, width, height) || IsCorner(x, y, width, height))
        {
            return false;
        }

        if (y == height - 1)
        {
            key = new DoorKey(EventRoomAsset.DoorSide.Top, x);
            return true;
        }

        if (x == width - 1)
        {
            key = new DoorKey(EventRoomAsset.DoorSide.Right, y);
            return true;
        }

        if (y == 0)
        {
            key = new DoorKey(EventRoomAsset.DoorSide.Bottom, x);
            return true;
        }

        if (x == 0)
        {
            key = new DoorKey(EventRoomAsset.DoorSide.Left, y);
            return true;
        }

        return false;
    }

    private static string GetDoorKeyString(DoorKey key)
    {
        return ((int)key.side).ToString() + "_" + key.offset.ToString();
    }

    private static void SaveInstanceToAsset(EventRoomInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        const string assetFolder = "Assets/EventRooms";
        EnsureFolder(assetFolder);

        string safeName = SanitizeFileName(instance.name);
        if (string.IsNullOrEmpty(safeName))
        {
            safeName = "EventRoom";
        }

        string targetPath = AssetDatabase.GenerateUniqueAssetPath(assetFolder + "/" + safeName + ".asset");
        EventRoomAsset asset = ScriptableObject.CreateInstance<EventRoomAsset>();
        instance.CopyToAsset(asset);

        AssetDatabase.CreateAsset(asset, targetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Undo.RecordObject(instance, "Link Event Room Asset");
        instance.eventRoomAsset = asset;
        EditorUtility.SetDirty(instance);

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);

        EditorUtility.DisplayDialog("Event Room", "Saved asset: " + targetPath, "OK");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] split = folderPath.Split('/');
        string current = split[0];
        for (int i = 1; i < split.Length; i++)
        {
            string next = current + "/" + split[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, split[i]);
            }
            current = next;
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        string result = name;
        for (int i = 0; i < invalid.Length; i++)
        {
            result = result.Replace(invalid[i].ToString(), string.Empty);
        }
        return result.Trim();
    }
}