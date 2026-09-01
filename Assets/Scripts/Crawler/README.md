# Crawler — 턴제 던전 크롤러

`Scripts/Dungeon` 의 절차적 생성 위에 얹은 NetHack 계열 게임 규칙이다.
2.5D 쿼터뷰로 그리고, 시간은 플레이어가 행동할 때만 흐른다.

## 구조

시뮬레이션과 표현을 나눈다. 규칙은 MonoBehaviour 를 모른다.

```
Sim/                      순수 C#. 뷰 없이도 돌아간다.
  CrawlerWorld            한 층의 상태(액터 배치, 문, 시야). TileMap 위에 얹힌다.
  CrawlerActor            좌표 / HP / 속도. 플레이어와 몬스터가 같은 타입이다.
  TurnScheduler           에너지 기반 턴 순서. ActionCost(12) 가 보통 속도.
  CrawlerActions          ICrawlerAction 과 Move / MeleeAttack / OpenDoor / Wait.
  ActorBrains             IActorBrain. PlayerBrain 은 입력을, MonsterBrain 은 AI 를 담는다.
  FieldOfView             재귀 그림자 투사 + "방은 통째로 밝다" 규칙.
  VisibilityMap           타일별 Unknown / Remembered / Visible.

View/                     Unity 표현 계층.
  QuarterViewCamera       고정 각도 직교 카메라. Q/E 로 90도 회전, 휠로 확대.
  DungeonView             안개(fog of war) 렌더링, 카메라 앞을 막는 벽 가리기, 문 여닫기 동기화.
  ActorView               논리 좌표를 부드럽게 따라가는 몸통.

CrawlerGame               위를 엮는 게임 루프. 씬에 붙이는 유일한 컴포넌트.
```

핵심은 `CrawlerGame.RunSimulation()` 이다.
스케줄러에게 다음 액터를 묻고, 그 액터의 두뇌가 행동을 내놓으면 수행한다.
`PlayerBrain` 이 `null` 을 돌려주면(= 아직 키를 안 눌렀으면) 거기서 멈춘다. 이것이 턴제의 전부다.

## 씬 설정

1. 빈 GameObject 에 `CrawlerGame` 을 붙이고 `dungeon` 에 씬의 Dungeon 오브젝트를 연결한다.
2. 나머지는 비워 둬도 된다. 실행 시 자동으로 처리한다.
   - `Camera.main` 에 `QuarterViewCamera` 를 붙인다.
   - 자신에게 `DungeonView` 를 붙인다.
   - `Dungeon.player` / `Dungeon.enemyPrefab` 을 액터 몸통으로 쓴다.
3. `disableLegacyComponents` 가 켜져 있으면 Awake 에서 실시간용 컴포넌트를 끈다.
   `GameManager`, `Player`, `Enemy`, `CameraOcclusion` 이 대상이다.
   (Awake 에서 끄는 이유는 그쪽 `Start()` 가 던전을 한 번 더 생성하거나 캐릭터 모델을 두 번 만들기 때문이다.)

`Dungeon` 은 `buildCeiling` / `buildNavMesh` / `spawnActors` 세 스위치로 제어한다.
`CrawlerGame` 이 셋 다 꺼서 지형만 만들게 하고, 배치는 직접 한다.

## 조작

| 키 | 동작 |
| --- | --- |
| 방향키 / 숫자패드 1-9 / `hjkl yubn` | 8방향 이동 (부딪히면 공격 / 문 열기) |
| `.` / `5` / Space | 한 턴 기다린다 |
| Enter / `>` | 계단 위에서 아래층으로 |
| `Q` / `E` | 카메라 90도 회전 |
| 마우스 휠 | 확대 / 축소 |
| `R` | 죽은 뒤 다시 시작 |

이동 입력은 화면 기준이다. 카메라를 돌리면 "위"도 함께 돈다 (`cameraRelativeInput`).

## 규칙 메모

- **속도**: 한 게임 턴마다 `Speed` 만큼 에너지를 얻고, 12 이상이면 행동한다.
  플레이어 12, 사신 10 이라 플레이어가 조금 빠르다.
- **대각선**: 벽 모서리를 스쳐 지나갈 수 없고 문간에서는 대각선으로 드나들 수 없다(NetHack 규칙).
- **시야**: 방이나 문간에 서 있으면 방 전체가 보인다. 복도에서는 `SightRadius`(기본 7) 안만 보인다.
  닫힌 문은 시야를 막는다.
- **몬스터**: 시야가 통하면 쫓아오고, 놓치면 마지막으로 본 자리까지 가 본 뒤 포기한다.
  매 턴 A* 를 돌리지 않고 8방향 중 가장 가까워지는 칸을 고른다.
- **행동 실패**: 플레이어는 턴을 쓰지 않고 다시 입력할 수 있다.
  몬스터는 헛손질로 한 턴을 쓴다. 그러지 않으면 같은 판단을 반복해 프레임이 멈춘다.

## 다음에 할 일

- 층 영속화. 지금은 `>` 로 내려가면 새 층을 뽑는다. `TileMap` + `CrawlerWorld` 를 층별로 보관하고
  `<` 로 올라갈 수 있게 하려면 직렬화가 필요하다.
- 아이템과 인벤토리. `MetaData` CSV 시스템을 아이템까지 확장하면 된다.
- 함정, 잠긴 문(`DoorStand.isLocked` 가 이미 있다), `LevelGenerator.LockedRoom` 활용.
- `EventRoomAsset` 을 생성 파이프라인에 연결(현재는 에디터 저작만 된다).
- 배고픔, 경험치, 직업.

## 성능 메모

`DungeonView` 는 기억 구역만 `MaterialPropertyBlock` 으로 어둡게 칠한다.
보이는 구역은 블록을 비워 SRP 배처가 묶을 수 있게 둔다.
그래도 타일 하나당 GameObject 를 만드는 구조라 방이 많아지면 드로우 콜이 문제가 된다.
그때는 바닥/벽을 청크 단위 메시로 합치는 편이 낫다.
