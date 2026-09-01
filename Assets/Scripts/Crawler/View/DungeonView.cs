using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 생성된 던전 오브젝트에 시야 상태를 입히는 표현 계층.
///
/// 하는 일은 두 가지다.
///  1. 안개(fog of war) : 본 적 없는 타일은 감추고, 기억만 하는 타일은 어둡게 하고, 보이는 타일은 그대로 둔다.
///  2. 벽 가리기        : 쿼터뷰에서 카메라와 플레이어 사이를 막는 벽을 감춘다.
///
/// 시뮬레이션은 이 클래스를 모른다. <see cref="CrawlerWorld"/> 가 시야를 다시 계산하면
/// 바뀐 타일 목록(<see cref="VisibilityMap.Dirty"/>)만 받아 그 타일의 렌더러만 건드린다.
/// </summary>
public class DungeonView : MonoBehaviour
{
    /// <summary>기억만 하는 타일에 입히는 색. 본 적은 있지만 지금 보이지는 않는다는 표시다.</summary>
    [Header("Fog of War")]
    public Color rememberedTint = new Color(0.30f, 0.32f, 0.42f, 1.0f);

    [Tooltip("보이지 않는 타일의 횃불(Light)을 끈다. 기억 구역이 실제로 어두워 보이게 한다.")]
    public bool disableLightsOutsideView = true;

    [Header("Wall Culling")]
    [Tooltip("카메라와 플레이어 사이를 막는 벽을 감춘다.")]
    public bool cullNearWalls = true;

    /// <summary>한 타일에 딸린 표현 오브젝트 묶음. 바인딩할 때 한 번만 모아 둔다.</summary>
    private class TileView
    {
        public Renderer[] renderers;
        public Color[] baseColors;
        public Light[] lights;

        /// <summary>벽 오브젝트. 벽에 붙은 횃불도 자식으로 함께 딸려 온다.</summary>
        public Transform[] walls;
        public Renderer[][] wallRenderers;
        public Color[][] wallBaseColors;
        public Light[][] wallLights;

        /// <summary>마지막으로 적용한 시야 상태. 같은 상태면 렌더러를 다시 건드리지 않는다.</summary>
        public TileVisibility applied = (TileVisibility)(-1);
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>벽이 카메라 쪽을 향한다고 볼 최소 내적. 0 이면 옆에서 스치는 벽까지 지워져 구멍이 뚫린다.</summary>
    private const float WallCullThreshold = 0.1f;

    /// <summary>카메라 방향이 이만큼 바뀌면 벽 가리기를 다시 계산한다.</summary>
    private const float CameraForwardEpsilon = 0.01f;

    private readonly Dictionary<int, TileView> tileViews = new Dictionary<int, TileView>();
    private readonly Dictionary<int, DoorStand> doorStands = new Dictionary<int, DoorStand>();

    private MaterialPropertyBlock propertyBlock;
    private CrawlerWorld world;
    private QuarterViewCamera viewCamera;
    private Vector3 lastCameraForward = Vector3.zero;

    /// <summary>
    /// 던전이 만들어 놓은 타일 오브젝트를 훑어 표현 정보를 캐시하고, 전체를 한 번 갱신한다.
    /// 던전을 새로 생성할 때마다 다시 불러야 한다.
    /// </summary>
    public void Bind(Dungeon dungeon, CrawlerWorld world, QuarterViewCamera viewCamera)
    {
        // 층을 새로 만들면 이전 월드의 이벤트가 남는다. 먼저 떼어 낸다.
        if (null != this.world)
        {
            this.world.DoorChanged -= OnDoorChanged;
        }

        this.world = world;
        this.viewCamera = viewCamera;
        this.propertyBlock = new MaterialPropertyBlock();

        this.tileViews.Clear();
        this.doorStands.Clear();

        foreach (KeyValuePair<int, Dungeon.TileVisual> pair in dungeon.TileVisuals)
        {
            this.tileViews[pair.Key] = CreateTileView(pair.Value);
            CollectDoorStand(pair.Key, pair.Value);
        }

        this.world.DoorChanged += OnDoorChanged;
        SyncAllDoors();

        this.lastCameraForward = Vector3.zero;
        RefreshAll();
    }

    public void Unbind()
    {
        if (null != this.world)
        {
            this.world.DoorChanged -= OnDoorChanged;
        }

        this.world = null;
        this.tileViews.Clear();
        this.doorStands.Clear();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private static TileView CreateTileView(Dungeon.TileVisual visual)
    {
        var tileView = new TileView();

        var renderers = new List<Renderer>();
        var lights = new List<Light>();

        foreach (GameObject target in visual.objects)
        {
            if (null == target || true == visual.walls.Contains(target))
            {
                continue;
            }

            renderers.AddRange(target.GetComponentsInChildren<Renderer>(true));
            lights.AddRange(target.GetComponentsInChildren<Light>(true));
        }

        tileView.renderers = renderers.ToArray();
        tileView.baseColors = ReadBaseColors(tileView.renderers);
        tileView.lights = lights.ToArray();

        int wallCount = visual.walls.Count;
        tileView.walls = new Transform[wallCount];
        tileView.wallRenderers = new Renderer[wallCount][];
        tileView.wallBaseColors = new Color[wallCount][];
        tileView.wallLights = new Light[wallCount][];

        for (int i = 0; i < wallCount; i++)
        {
            GameObject wall = visual.walls[i];
            if (null == wall)
            {
                tileView.wallRenderers[i] = new Renderer[0];
                tileView.wallBaseColors[i] = new Color[0];
                tileView.wallLights[i] = new Light[0];
                continue;
            }

            tileView.walls[i] = wall.transform;
            tileView.wallRenderers[i] = wall.GetComponentsInChildren<Renderer>(true);
            tileView.wallBaseColors[i] = ReadBaseColors(tileView.wallRenderers[i]);
            tileView.wallLights[i] = wall.GetComponentsInChildren<Light>(true);
        }

        return tileView;
    }

    /// <summary>렌더러의 원래 색을 읽어 둔다. 기억 상태에서 어둡게 칠했다가 되돌릴 때 필요하다.</summary>
    private static Color[] ReadBaseColors(Renderer[] renderers)
    {
        var colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;
            if (null == material)
            {
                colors[i] = Color.white;
                continue;
            }

            if (true == material.HasProperty(BaseColorId))
            {
                colors[i] = material.GetColor(BaseColorId);
                continue;
            }

            colors[i] = (true == material.HasProperty(ColorId)) ? material.GetColor(ColorId) : Color.white;
        }

        return colors;
    }

    private void CollectDoorStand(int tileIndex, Dungeon.TileVisual visual)
    {
        foreach (GameObject target in visual.objects)
        {
            if (null == target)
            {
                continue;
            }

            DoorStand doorStand = target.GetComponentInChildren<DoorStand>(true);
            if (null == doorStand)
            {
                continue;
            }

            this.doorStands[tileIndex] = doorStand;
            return;
        }
    }

    private void LateUpdate()
    {
        if (null == this.world)
        {
            return;
        }

        // 카메라를 돌리면 가려야 할 벽이 통째로 바뀐다. 이때만 전체를 다시 훑는다.
        if (true == this.cullNearWalls && null != this.viewCamera)
        {
            if (CameraForwardEpsilon < Vector3.Distance(this.lastCameraForward, this.viewCamera.HorizontalForward))
            {
                RefreshAll();
            }
        }
    }

    /// <summary>시야가 바뀐 타일만 갱신한다. 플레이어가 움직이거나 문이 여닫힌 뒤에 부른다.</summary>
    public void RefreshDirty()
    {
        if (null == this.world)
        {
            return;
        }

        Vector3 cameraForward = GetCameraForward();

        foreach (int tileIndex in this.world.Visibility.Dirty)
        {
            if (false == this.tileViews.TryGetValue(tileIndex, out TileView tileView))
            {
                continue;
            }

            ApplyVisibility(tileView, this.world.Visibility.Get(tileIndex), cameraForward, false);
        }
    }

    /// <summary>모든 타일을 다시 갱신한다. 바인딩 직후와 카메라 회전 시에만 쓴다.</summary>
    public void RefreshAll()
    {
        if (null == this.world)
        {
            return;
        }

        Vector3 cameraForward = GetCameraForward();
        this.lastCameraForward = cameraForward;

        foreach (KeyValuePair<int, TileView> pair in this.tileViews)
        {
            ApplyVisibility(pair.Value, this.world.Visibility.Get(pair.Key), cameraForward, true);
        }
    }

    private Vector3 GetCameraForward()
    {
        if (false == this.cullNearWalls || null == this.viewCamera)
        {
            return Vector3.zero;
        }

        return this.viewCamera.HorizontalForward;
    }

    private void ApplyVisibility(TileView tileView, TileVisibility visibility, Vector3 cameraForward, bool force)
    {
        if (false == force && tileView.applied == visibility)
        {
            return;
        }

        tileView.applied = visibility;

        bool draw = TileVisibility.Unknown != visibility;
        bool lit = TileVisibility.Visible == visibility;

        SetRenderers(tileView.renderers, tileView.baseColors, draw, lit);
        SetLights(tileView.lights, lit);

        for (int i = 0; i < tileView.walls.Length; i++)
        {
            Transform wall = tileView.walls[i];

            // 카메라를 등진 벽(= 카메라와 던전 사이를 막는 벽)은 보이더라도 감춘다.
            bool wallDraw = draw;
            if (true == wallDraw && Vector3.zero != cameraForward && null != wall)
            {
                wallDraw = WallCullThreshold >= Vector3.Dot(wall.forward, cameraForward);
            }

            SetRenderers(tileView.wallRenderers[i], tileView.wallBaseColors[i], wallDraw, lit);
            SetLights(tileView.wallLights[i], lit && wallDraw);
        }
    }

    private void SetRenderers(Renderer[] renderers, Color[] baseColors, bool draw, bool lit)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (null == renderer)
            {
                continue;
            }

            renderer.enabled = draw;

            if (false == draw)
            {
                continue;
            }

            // 보이는 타일은 프로퍼티 블록을 비워 둔다. 블록이 붙어 있으면 SRP 배처가 묶지 못해
            // 드로우 콜이 타일 수만큼 늘어난다. 어둡게 칠할 때만 붙인다.
            if (true == lit)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }

            Color color = baseColors[i] * this.rememberedTint;

            this.propertyBlock.Clear();
            this.propertyBlock.SetColor(BaseColorId, color);
            this.propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(this.propertyBlock);
        }
    }

    private void SetLights(Light[] lights, bool enabled)
    {
        if (false == this.disableLightsOutsideView)
        {
            return;
        }

        foreach (Light light in lights)
        {
            if (null == light)
            {
                continue;
            }

            light.enabled = enabled;
        }
    }

    // ------------------------------------------------------------------ 문

    private void SyncAllDoors()
    {
        foreach (KeyValuePair<int, CrawlerWorld.DoorState> pair in this.world.Doors)
        {
            ApplyDoorState(pair.Key, pair.Value);
        }
    }

    private void OnDoorChanged(int tileIndex, CrawlerWorld.DoorState state)
    {
        ApplyDoorState(tileIndex, state);
    }

    private void ApplyDoorState(int tileIndex, CrawlerWorld.DoorState state)
    {
        if (false == this.doorStands.TryGetValue(tileIndex, out DoorStand doorStand) || null == doorStand)
        {
            return;
        }

        doorStand.Open(CrawlerWorld.DoorState.Open == state);
    }
}
