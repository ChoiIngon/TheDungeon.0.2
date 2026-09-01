using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// 턴제 던전 크롤러의 게임 루프.
///
/// 흐름은 단순하다.
///   1. <see cref="Dungeon"/> 이 지형을 만든다(기존 절차적 생성 그대로).
///   2. 그 위에 <see cref="CrawlerWorld"/> 를 얹고 플레이어와 몬스터를 세운다.
///   3. 매 프레임 <see cref="TurnScheduler"/> 에게 다음 행동할 액터를 물어보고, 그 액터의 두뇌가
///      행동을 내놓으면 수행한다. 플레이어 차례에 입력이 없으면 거기서 멈춘다.
///
/// "플레이어가 키를 누르기 전까지 세상이 멈춘다"가 3번 한 줄로 구현된다. 실시간 요소가 없으므로
/// 프레임률이 게임 규칙에 영향을 주지 않는다.
///
/// 씬 설정
///   - 이 컴포넌트를 빈 GameObject 에 붙이고 dungeon 을 연결한다.
///   - 기존 실시간 컴포넌트(GameManager, Player, Enemy, CameraOcclusion)는 Awake 에서 끈다.
/// </summary>
public class CrawlerGame : MonoBehaviour
{
    /// <summary>
    /// 한 프레임에 처리할 행동 수의 상한.
    /// 행동이 턴을 소비하지 않는 상황이 생기면 무한 루프가 되므로 안전장치를 둔다.
    /// </summary>
    private const int MaxActionsPerFrame = 512;

    /// <summary>한 행동이 다른 행동으로 대체되는 횟수의 상한(이동 → 공격 → ...).</summary>
    private const int MaxAlternateDepth = 8;

    [Header("References")]
    public Dungeon dungeon;
    public DungeonView dungeonView;
    public QuarterViewCamera viewCamera;

    [Header("Actors")]
    [Tooltip("비워 두면 Dungeon.player 를 쓴다.")]
    public GameObject playerObject;

    [Tooltip("비워 두면 Dungeon.enemyPrefab 을 쓴다.")]
    public GameObject monsterPrefab;

    public float playerGroundOffset = 0.0f;
    public float monsterGroundOffset = 1.0f;

    [Tooltip("방 하나에 몬스터를 몇 마리까지 놓을지.")]
    public int monstersPerRoom = 1;

    [Header("Input")]
    [Tooltip("방향키를 화면 기준으로 해석한다. 카메라를 돌려도 '위'가 화면 위가 된다.")]
    public bool cameraRelativeInput = true;

    [Header("Setup")]
    [Tooltip("실시간 조작용 컴포넌트(GameManager, Player, Enemy, CameraOcclusion)를 끈다.")]
    public bool disableLegacyComponents = true;

    [Tooltip("간단한 HUD(메시지 로그, HP, 턴 수)를 만든다.")]
    public bool createHud = true;

    private readonly TurnScheduler scheduler = new TurnScheduler();
    private readonly PlayerBrain playerBrain = new PlayerBrain();
    private readonly List<string> messages = new List<string>();
    private readonly Dictionary<CrawlerActor, ActorView> actorViews = new Dictionary<CrawlerActor, ActorView>();

    private CrawlerWorld world;
    private CrawlerActor playerActor;
    private bool isGameOver;

    private Text messageText;
    private Text statusText;

    public CrawlerWorld World => this.world;

    private void Awake()
    {
        if (true == this.disableLegacyComponents)
        {
            DisableLegacyComponents();
        }
    }

    private void Start()
    {
        if (null == this.dungeon)
        {
            Debug.LogError("CrawlerGame: dungeon 이 연결되어 있지 않다.");
            enabled = false;
            return;
        }

        EnsureViewComponents();

        if (true == this.createHud)
        {
            CreateHud();
        }

        NewLevel();
    }

    // ------------------------------------------------------------------ 초기화

    /// <summary>
    /// 실시간 액션용으로 만들어진 컴포넌트를 끈다.
    ///
    /// Awake 에서 하는 이유가 있다. Unity 는 모든 Awake 를 끝낸 뒤에 Start 를 부르므로,
    /// 여기서 꺼야 Player.Start() 나 GameManager.Start() 가 아예 실행되지 않는다.
    /// Start 에서 끄면 던전이 두 번 생성되거나 캐릭터 모델이 두 번 만들어질 수 있다.
    /// </summary>
    private void DisableLegacyComponents()
    {
        foreach (GameManager gameManager in FindObjectsByType<GameManager>(FindObjectsSortMode.None))
        {
            gameManager.enabled = false;
            Debug.Log("CrawlerGame: GameManager 를 껐다. 던전 생성과 진행은 CrawlerGame 이 맡는다.");
        }

        foreach (Player player in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            player.enabled = false;
        }

        foreach (Enemy enemy in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            enemy.enabled = false;
        }

        foreach (CameraOcclusion occlusion in FindObjectsByType<CameraOcclusion>(FindObjectsSortMode.None))
        {
            occlusion.enabled = false;
        }
    }

    private void EnsureViewComponents()
    {
        if (null == this.viewCamera)
        {
            Camera mainCamera = Camera.main;
            if (null == mainCamera)
            {
                Debug.LogError("CrawlerGame: 메인 카메라를 찾지 못했다.");
                return;
            }

            this.viewCamera = mainCamera.GetComponent<QuarterViewCamera>();
            if (null == this.viewCamera)
            {
                this.viewCamera = mainCamera.gameObject.AddComponent<QuarterViewCamera>();
            }
        }

        if (null == this.dungeonView)
        {
            this.dungeonView = GetComponent<DungeonView>();
            if (null == this.dungeonView)
            {
                this.dungeonView = gameObject.AddComponent<DungeonView>();
            }
        }
    }

    /// <summary>새 층을 만들고 액터를 배치한다.</summary>
    public void NewLevel()
    {
        ClearActors();

        // 쿼터뷰에서는 천장이 시야를 막고, 그리드 이동에는 NavMesh 가 필요 없다.
        this.dungeon.buildCeiling = false;
        this.dungeon.buildNavMesh = false;
        this.dungeon.spawnActors = false;
        this.dungeon.Generate();

        if (null == this.dungeon.Map || null == this.dungeon.Level)
        {
            Debug.LogError("CrawlerGame: 던전 생성에 실패했다.");
            enabled = false;
            return;
        }

        this.world = new CrawlerWorld(this.dungeon.Map, this.dungeon.Level, this.dungeon.Random);
        this.world.MessageLogged += OnMessageLogged;
        this.world.ActorDied += OnActorDied;

        this.isGameOver = false;
        this.scheduler.Clear();
        this.playerBrain.Cancel();

        SpawnPlayer();
        SpawnMonsters();

        this.world.RecomputePlayerFieldOfView();
        this.dungeonView.Bind(this.dungeon, this.world, this.viewCamera);

        if (null != this.viewCamera && null != this.playerObject)
        {
            this.viewCamera.SnapToTarget(this.playerObject.transform);
        }

        this.world.Log("던전에 들어섰다. 아래로 내려가는 계단을 찾아라.");
    }

    private void SpawnPlayer()
    {
        if (null == this.playerObject)
        {
            this.playerObject = this.dungeon.player;
        }

        if (null == this.playerObject)
        {
            Debug.LogError("CrawlerGame: 플레이어 오브젝트가 없다. Dungeon.player 를 연결하라.");
            return;
        }

        // 층을 내려가도 같은 액터를 이어 쓴다. HP 와 성장이 층마다 초기화되면 크롤러가 되지 않는다.
        if (null == this.playerActor || true == this.playerActor.IsDead)
        {
            this.playerActor = new CrawlerActor
            {
                Name = "당신",
                IsPlayer = true,
                Brain = this.playerBrain,
                MaxHp = 30,
                Hp = 30,
                AttackPower = 5,
                SightRadius = 7,
                Speed = TurnScheduler.ActionCost,
            };
        }

        TileMap.Tile start = this.dungeon.Level.Start;
        this.world.AddActor(this.playerActor, (int)start.rect.x, (int)start.rect.y);
        this.scheduler.Add(this.playerActor);

        PreparePresentation(this.playerObject);
        BindActorView(this.playerActor, this.playerObject, this.playerGroundOffset);
    }

    private void SpawnMonsters()
    {
        GameObject prefab = (null != this.monsterPrefab) ? this.monsterPrefab : this.dungeon.enemyPrefab;
        if (null == prefab)
        {
            Debug.LogWarning("CrawlerGame: 몬스터 프리팹이 없다. 빈 던전으로 진행한다.");
            return;
        }

        DungeonRandom random = this.world.Random;

        foreach (TileMap.Room room in this.world.Map.rooms)
        {
            // 시작 방에는 놓지 않는다. 내려오자마자 얻어맞으면 아무것도 할 수 없다.
            if (room == this.world.Level.StartRoom)
            {
                continue;
            }

            for (int i = 0; i < this.monstersPerRoom; i++)
            {
                if (false == TryFindSpawnTile(room, random, out int x, out int y))
                {
                    continue;
                }

                SpawnMonster(prefab, x, y);
            }
        }
    }

    /// <summary>방 안에서 비어 있는 바닥 칸을 찾는다. 몇 번 굴려 보고 실패하면 포기한다.</summary>
    private bool TryFindSpawnTile(TileMap.Room room, DungeonRandom random, out int x, out int y)
    {
        Rect floorRect = room.GetFloorRect();

        for (int attempt = 0; attempt < 16; attempt++)
        {
            x = (int)random.Range(floorRect.xMin, floorRect.xMax);
            y = (int)random.Range(floorRect.yMin, floorRect.yMax);

            if (false == this.world.IsWalkable(x, y))
            {
                continue;
            }

            if (null != this.world.GetActor(x, y))
            {
                continue;
            }

            return true;
        }

        x = 0;
        y = 0;
        return false;
    }

    private void SpawnMonster(GameObject prefab, int x, int y)
    {
        var monster = new CrawlerActor
        {
            Name = "사신",
            Brain = new MonsterBrain(),
            MaxHp = 8,
            Hp = 8,
            AttackPower = 3,
            SightRadius = 7,

            // 플레이어보다 조금 느리게 둔다. 도망칠 여지가 있어야 턴제가 재미있다.
            Speed = 10,
        };

        this.world.AddActor(monster, x, y);
        this.scheduler.Add(monster);

        GameObject instance = Instantiate(prefab, monster.GetWorldPosition(this.monsterGroundOffset), Quaternion.identity, transform);
        instance.name = $"Monster_{x}_{y}";

        PreparePresentation(instance);
        BindActorView(monster, instance, this.monsterGroundOffset);
    }

    /// <summary>
    /// 실시간용 컴포넌트를 끄고 캐릭터 모델을 직접 만든다.
    ///
    /// Player / Enemy 가 Start 에서 하던 일(모델 조립, 무기 부착)을 여기서 대신한다.
    /// 그 컴포넌트들은 꺼져 있으므로 스스로 하지 않는다.
    /// </summary>
    private void PreparePresentation(GameObject target)
    {
        Player playerComponent = target.GetComponent<Player>();
        if (null != playerComponent)
        {
            playerComponent.enabled = false;
        }

        Enemy enemyComponent = target.GetComponent<Enemy>();
        if (null != enemyComponent)
        {
            enemyComponent.enabled = false;
        }

        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (null != agent)
        {
            agent.enabled = false;
        }

        // 물리로 밀리면 그리드 좌표와 화면 위치가 어긋난다. 위치는 ActorView 가 직접 정한다.
        Rigidbody body = target.GetComponent<Rigidbody>();
        if (null != body)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        BuildActorModel(target);
    }

    private static void BuildActorModel(GameObject target)
    {
        PlayerModel playerModel = target.GetComponent<PlayerModel>();
        if (null != playerModel)
        {
            // 이미 조립되어 있으면 다시 만들지 않는다(몸이 두 겹으로 겹친다).
            if (null == playerModel.RightHand)
            {
                playerModel.Build();

                var sword = new GameObject("Sword").AddComponent<Sword>();
                sword.Build(playerModel.RightHand);
            }

            return;
        }

        GrimReaper grimReaper = target.GetComponent<GrimReaper>();
        if (null != grimReaper && null == grimReaper.RightHand)
        {
            grimReaper.Build();

            var scythe = new GameObject("Scythe").AddComponent<Scythe>();
            scythe.Build(grimReaper.RightHand);
        }
    }

    private void BindActorView(CrawlerActor actor, GameObject target, float groundOffset)
    {
        ActorView view = target.GetComponent<ActorView>();
        if (null == view)
        {
            view = target.AddComponent<ActorView>();
        }

        view.groundOffset = groundOffset;
        view.Bind(actor, this.world);

        this.actorViews[actor] = view;
    }

    private void ClearActors()
    {
        if (null != this.world)
        {
            this.world.MessageLogged -= OnMessageLogged;
            this.world.ActorDied -= OnActorDied;
        }

        foreach (KeyValuePair<CrawlerActor, ActorView> pair in this.actorViews)
        {
            if (null == pair.Value)
            {
                continue;
            }

            // 플레이어의 몸은 씬에 원래 있던 오브젝트라 다음 층에서도 다시 쓴다.
            if (true == pair.Key.IsPlayer)
            {
                continue;
            }

            Destroy(pair.Value.gameObject);
        }

        this.actorViews.Clear();
        this.scheduler.Clear();
    }

    // ------------------------------------------------------------------ 게임 루프

    private void Update()
    {
        if (null == this.world)
        {
            return;
        }

        HandleInput();
        RunSimulation();
        UpdateStatusText();
    }

    /// <summary>
    /// 행동할 수 있는 액터를 차례로 처리한다.
    /// 플레이어 차례인데 입력이 없으면 그 자리에서 멈춘다. 이것이 "턴제"의 전부다.
    /// </summary>
    private void RunSimulation()
    {
        bool acted = false;

        for (int guard = 0; guard < MaxActionsPerFrame; guard++)
        {
            if (true == this.isGameOver)
            {
                break;
            }

            CrawlerActor actor = this.scheduler.NextActor();
            if (null == actor || null == actor.Brain)
            {
                break;
            }

            ICrawlerAction action = actor.Brain.NextAction(this.world, actor);
            if (null == action)
            {
                break; // 플레이어 입력 대기
            }

            actor.Energy -= ResolveAction(actor, action);
            acted = true;
        }

        if (false == acted)
        {
            return;
        }

        // 문이 열리거나 플레이어가 움직였을 수 있다. 한 묶음이 끝난 뒤 한 번만 다시 계산한다.
        this.world.RecomputePlayerFieldOfView();
        this.dungeonView.RefreshDirty();
    }

    /// <summary>행동을 수행하고 소비한 에너지를 돌려준다. 대체 행동(이동 → 공격 등)을 따라간다.</summary>
    private int ResolveAction(CrawlerActor actor, ICrawlerAction action)
    {
        for (int depth = 0; depth < MaxAlternateDepth; depth++)
        {
            ActionResult result = action.Perform(this.world, actor);

            if (true == result.consumedTurn)
            {
                return result.cost;
            }

            if (null != result.alternate)
            {
                action = result.alternate;
                continue;
            }

            // 행동이 성립하지 않았다.
            // 플레이어는 턴을 쓰지 않고 다시 입력할 수 있다. 하지만 몬스터에게 같은 처리를 하면
            // 두뇌가 같은 판단을 반복해 프레임이 멈춘다. 헛손질로 한 턴을 쓰게 한다.
            return (true == actor.IsPlayer) ? 0 : TurnScheduler.ActionCost;
        }

        Debug.LogWarning($"CrawlerGame: {actor.Name} 의 행동이 {MaxAlternateDepth}번 넘게 대체되었다.");
        return TurnScheduler.ActionCost;
    }

    private void OnActorDied(CrawlerActor actor)
    {
        this.scheduler.Remove(actor);

        if (true == actor.IsPlayer)
        {
            this.isGameOver = true;
            this.world.Log("당신은 죽었다. R 을 눌러 다시 시작한다.");
            return;
        }

        this.world.RemoveActor(actor);

        if (true == this.actorViews.TryGetValue(actor, out ActorView view) && null != view)
        {
            // 사망 애니메이션을 볼 시간을 준 뒤 치운다.
            Destroy(view.gameObject, 3.0f);
        }

        this.actorViews.Remove(actor);
    }

    // ------------------------------------------------------------------ 입력

    /// <summary>방향키/숫자패드/vi 키를 화면 기준 8방향 인덱스로 옮긴다. 없으면 -1.</summary>
    private static int ReadDirectionIndex()
    {
        // CrawlerActor.Directions 와 같은 순서(북 → 시계 방향).
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.K)) return 0;
        if (Input.GetKeyDown(KeyCode.Keypad9) || Input.GetKeyDown(KeyCode.U)) return 1;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.L)) return 2;
        if (Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.N)) return 3;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.J)) return 4;
        if (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.B)) return 5;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.H)) return 6;
        if (Input.GetKeyDown(KeyCode.Keypad7) || Input.GetKeyDown(KeyCode.Y)) return 7;

        return -1;
    }

    private void HandleInput()
    {
        if (true == this.isGameOver)
        {
            if (true == Input.GetKeyDown(KeyCode.R))
            {
                RestartAfterDeath();
            }

            return;
        }

        // 계단 내려가기: '>' 또는 Enter. 계단을 밟았다고 자동으로 내려가면 되돌아갈 수 없다.
        if (Input.GetKeyDown(KeyCode.Greater) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            TryDescend();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Space))
        {
            this.playerBrain.Queue(new WaitAction());
            return;
        }

        int index = ReadDirectionIndex();
        if (0 > index)
        {
            return;
        }

        Vector2Int direction = CrawlerActor.Directions[(index + GetCameraDirectionShift()) % CrawlerActor.Directions.Length];
        this.playerBrain.Queue(new MoveAction(direction.x, direction.y));
    }

    /// <summary>
    /// 카메라 방향만큼 입력을 돌린다.
    ///
    /// 쿼터뷰에서는 화면의 '위'가 월드의 +Z 가 아니다(기본 yaw 45도에서는 북동쪽이다).
    /// 카메라 각도를 45도 단위로 반올림해 방향 배열의 인덱스를 그만큼 밀어 준다.
    /// </summary>
    private int GetCameraDirectionShift()
    {
        if (false == this.cameraRelativeInput || null == this.viewCamera)
        {
            return 0;
        }

        int steps = Mathf.RoundToInt(this.viewCamera.yaw / 45.0f);
        return ((steps % 8) + 8) % 8;
    }

    private void TryDescend()
    {
        TileMap.Tile exit = this.world.Level.End;
        if (null == exit)
        {
            return;
        }

        if (this.playerActor.X != (int)exit.rect.x || this.playerActor.Y != (int)exit.rect.y)
        {
            this.world.Log("여기에는 내려가는 계단이 없다.");
            return;
        }

        // 시드를 비워 다음 층이 새로 뽑히게 한다.
        this.dungeon.randomSeed = 0;
        NewLevel();
    }

    private void RestartAfterDeath()
    {
        this.dungeon.randomSeed = 0;
        this.messages.Clear();
        NewLevel();
    }

    // ------------------------------------------------------------------ HUD

    private void OnMessageLogged(string message)
    {
        Debug.Log($"[Crawler] {message}");

        this.messages.Add(message);
        while (5 < this.messages.Count)
        {
            this.messages.RemoveAt(0);
        }

        if (null != this.messageText)
        {
            this.messageText.text = string.Join("\n", this.messages);
        }
    }

    private void UpdateStatusText()
    {
        if (null == this.statusText || null == this.playerActor)
        {
            return;
        }

        this.statusText.text = $"HP {this.playerActor.Hp}/{this.playerActor.MaxHp}    턴 {this.scheduler.TurnCount}";
    }

    private void CreateHud()
    {
        var canvasObject = new GameObject("CrawlerHud");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        this.statusText = CreateText(canvas, "Status", new Vector2(24, -24), new Vector2(600, 48), 32, TextAnchor.UpperLeft, new Vector2(0, 1));
        this.messageText = CreateText(canvas, "Messages", new Vector2(24, 24), new Vector2(1200, 200), 26, TextAnchor.LowerLeft, new Vector2(0, 0));
    }

    private static Text CreateText(Canvas canvas, string name, Vector2 offset, Vector2 size, int fontSize, TextAnchor alignment, Vector2 anchor)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(canvas.transform, false);

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(2, -2);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        return text;
    }
}
