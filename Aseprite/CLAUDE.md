# PixelArt 프로젝트

게임용 픽셀아트(32px 타일 기반 캐릭터·몬스터·아이템·건물)를 Aseprite로 제작한다.

결과물은 Unity, Unreal, Godot, 자바스크립트(Canvas·Phaser 등) 등 **플랫폼과 관계없이** 쓰인다. 그래서 지침과 스킬은 특정 엔진의 설정값(PPU, 정규화 피벗, 정렬 레이어, 엔진 API 이름)이 아니라 **PNG + JSON 메타데이터, 픽셀 단위 앵커, 좌표 규약(왼쪽 위 원점·y-down), 그리는 순서**로 규칙을 적는다. 특정 엔진용 설정은 사용자가 그 엔진을 지정했을 때만 덧붙인다.

## 크기 규칙 (기본)
- 크기 지정이 없으면 **32×32px** (1타일)로 만든다
- 기본 단위는 **32×32 타일**이다. 크기를 키울 때는 **타일 수의 배수**로만 늘린다
  - 예: "가로 2, 세로 2 타일 몬스터" → 64×64, "가로 3 × 세로 2 타일" → 96×64
  - 32의 배수가 아닌 크기(예: 48×48)는 사용자가 픽셀 크기를 명시했을 때만 쓴다
- 파일명 크기 접미사는 타일 수로 쓴다: `<name>_<가로>x<세로>t.aseprite` (예: `src/character/golem/golem_2x2t.aseprite`, 1타일은 `src/character/bat/bat_1x1t.aseprite`)

## 캐릭터 기본 애니메이션
캐릭터·몬스터를 그리면 요청이 없어도 다음 애니메이션을 **모두** 만든다 (`sprite-animation` 스킬):
- `idle`, `move`, `attack`, `damage` × **상하좌우 4방향** = 16태그
- `die`는 **down 방향 1개**만 만든다
- 총 **17태그**. 이름은 `<동작>_<방향>` (`idle_down`, `move_left`, `attack_up`, `damage_right` …)이고, die만 `die`
- 요청에 **마법이나 스킬에 대한 별도 언급**이 있으면 스킬마다 `skill_{N}` 애니메이션을 추가한다. 언급 순서대로 `skill_1`, `skill_2` …, 4방향(`skill_1_down` …)으로 만들고 `die` 앞에 둔다. 스킬 K개면 `17 + 4K`태그. 직업 이름만 있고("마법사") 스킬 언급이 없으면 만들지 않는다
- 정지 이미지만 원한다고 명시한 경우, 또는 캐릭터가 아닌 대상(아이템, 소품, 건물)은 제외한다

## 던전 타일
`dungeon-tile` 스킬로 작업한다. 오브젝트·면 크기, 면 방향, 타일링 규약, 도구, 검증 절차는 전부 스킬(`SKILL.md`와 `references/object-spec.md`)에 있다. 여기에는 항상 지킬 원칙만 둔다.
- 던전 타일은 **3D 오브젝트**다: 벽 1×1×0.25, 바닥 1×0.25×1, 문 = 벽 크기 안의 문틀 2개(0.0625) + 문짝(0.875 × 1 × 0.21875). 카메라는 고정이고 남쪽에서 아래로 비스듬히 내려다본다
- 텍스처는 **1유닛 = 32px**, 면마다 따로 그린다 (front·back·top·side). 면 크기는 정수 px여야 한다
- 같은 오브젝트의 변형(벽·바닥 각 4개)은 랜덤으로 붙으므로 어떤 조합으로도 이어져야 한다
- 그림자·외곽선은 텍스처에 굽지 않는다 (3D 조명이 만든다). 문 열림은 문짝 회전이라 열림 그림을 따로 그리지 않는다
- 바닥과 벽이 맞닿는 곳의 이끼·먼지는 바닥 위에 얹는 **디더링 데칼**(`floor_edge`, 투명 32×32)로 만든다. 벽이 닿는 변마다 한 장 (4면이 벽이면 4장). 가장자리 깊이는 **5px 이하**
- 카펫처럼 두께 없는 평면 데코는 데칼과 같은 높이이므로 **데칼 layer 번호**로 순서를 정한다 (높은 layer가 덮는다. 1 = 가장자리 데칼, 2 = 카펫)
- 원본 `src/tileset/<theme>/<theme>.aseprite` → `dungeon_atlas.py build` → `export/tileset/<theme>/<theme>.png` + `.json`. 결과물은 직접 고치지 않는다
- 테마 파일명은 **테마 이름 그대로** 쓴다 (`crypt`, `temple`, `mine`). `dungeon_crypt`처럼 `dungeon_` 접두사를 붙이지 않는다
- 예전 2D 3/4 시점 스프라이트 파이프라인은 `archive/dungeon-tile-2d/`에 있다 (사용자가 요청할 때만 쓴다)
- 게임 프로젝트 폴더에는 사용자가 요청할 때만 쓴다

## 도구
- Aseprite 작업은 **aseprite MCP 도구**(`mcp__aseprite__*`)로 한다. `.aseprite` 바이너리를 직접 수정하지 않는다.
- 픽셀 단위 대량 작업(패턴, 해시 디더, 절차적 배치)은 `run_lua_script`로 한 번에 처리한다. 도구 호출 수백 번보다 정확하다.
- Aseprite CLI 경로는 환경변수 `ASEPRITE_PATH`로 받는다. 없으면 PATH의 `aseprite`를 쓴다

## 로컬 환경 설정
프로젝트 파일에는 기계마다 다른 절대 경로를 두지 않는다. 기계별 값은 `.claude/settings.local.json`의 `env`에만 둔다 (공유하지 않는 개인 설정. 템플릿: `.claude/settings.local.example.json`).

| 변수 | 쓰는 곳 | 없을 때 |
|---|---|---|
| `ASEPRITE_PATH` | 훅(`auto_preview.py`), 던전 스크립트(`tileset.py` 등) | PATH의 `aseprite` |
| `ASEPRITE_MCP_DIR` | `.mcp.json` (aseprite MCP 서버 코드 위치) | 프로젝트 옆 폴더 `../aseprite-mcp` |

- 프로젝트 밖 의존: aseprite MCP 서버는 자기 폴더의 `.env`에서 `ASEPRITE_PATH`를 따로 읽는다. 새 기계에서는 그 파일도 맞춘다
- PATH에 있어야 하는 프로그램: `python`(+ Pillow), `uv`
- 새 경로나 기계별 값을 추가할 때도 같은 방식으로 환경변수 + 기본값으로 만들고 이 표에 적는다

## 폴더 규약 (새 작업부터 적용)
`Aseprite/` 아래 `src/`·`preview/`·`export/` 안에 요청 종류별 분류 폴더를 둔다.

| 요청 | 원본 | 미리보기 | 결과물 |
|---|---|---|---|
| 캐릭터·몬스터 | `src/character/<name>/<name>_<가로>x<세로>t.aseprite` | `preview/character/<name>/` | `export/character/<name>/` |
| 던전 타일셋 | `src/tileset/<theme>/<theme>.aseprite` | `preview/tileset/<theme>/` | `export/tileset/<theme>/` |

- **캐릭터·몬스터는 캐릭터마다 폴더를 따로 둔다**: `src/character/<name>/`, `preview/character/<name>/`, `export/character/<name>/`. 원본·생성 스크립트·미리보기·검수 이미지·결과물을 `character/` 바로 아래에 늘어놓지 않는다 (Unity 쪽도 `Assets/Texture/character/<name>/`로 복사된다)
- **타일셋도 테마마다 폴더를 따로 둔다**: `src/tileset/<theme>/`, `preview/tileset/<theme>/`, `export/tileset/<theme>/` (Unity 쪽 `Assets/Texture/tileset/<theme>/`). `tileset/` 바로 아래에 파일을 두지 않는다
- `src/` 원본 `.aseprite` (생성용 `.lua`·`.py`도 같이 둔다)
- `export/` 게임용 결과물 (1x png, 시트 png+json, gif)
- `preview/` 검증용 확대본. 버려도 되는 파일
- 던전 테마는 `dungeon_xxx`가 아니라 **테마 이름**으로 짓는다: `src/tileset/crypt/crypt.aseprite` → `export/tileset/crypt/crypt.png`
- 미리보기 훅과 빌드 스크립트는 원본 `src/<분류>/…` 경로에서 `preview/<분류>/…`·`export/<분류>/…`를 정한다. 그래서 원본은 반드시 `src/<분류>/` 안에 둔다
- **Unity 복사**: `export/<분류>/…`의 결과물은 `Assets/Texture/<분류>/…`에 같은 하위 경로로 복사된다 (타일셋 `Assets/Texture/tileset/<theme>/`). 캐릭터는 예외로 원본 `src/character/<name>/*.aseprite`만 `Assets/Texture/character/<name>/`에 복사하고, `export/character/`의 png·시트 png·시트 json은 Unity로 복사하지 않는다 (Unity Aseprite Importer가 .aseprite에서 스프라이트·태그별 클립·AnimatorController를 만든다). aseprite MCP `export_*`·`run_lua_script` 호출 후 훅(`.claude/hooks/sync_texture.py`)이, 타일셋은 `dungeon_atlas.py`가 끝에 실행한다. GIF와 `export/` 바로 아래 파일은 복사하지 않는다. 수동 실행: `python .claude/hooks/sync_texture.py`
- `Assets/Texture/` 쪽은 복사본이다. 고칠 때는 `Aseprite/src/`를 고치고 다시 export한다
- MCP 도구·스크립트에 넘기는 상대 경로는 프로젝트 루트 기준이므로 `Aseprite/`를 붙인다 (예: `Aseprite/src/character/goblin/goblin_1x1t.aseprite`)
- 아이템·소품도 분류 폴더를 둔다 (`src/item/<name>/`, `src/prop/<name>/` 등). `export/<분류>/` 아래에 두어야 Unity로 복사된다
- 기존 파일(루트의 potion, slime, skeleton, townhouse, `src/`·`export/` 바로 아래의 이전 결과물, `src/dungeon3d/`)은 옮기지 않는다

## 검증 루프 (필수)
Claude는 `.aseprite`를 볼 수 없다. **큰 단계(실루엣 / 음영 / 외곽선 / 애니메이션 키포즈)가 끝날 때마다**:
1. `preview/<분류>/<name>.preview.png`(캐릭터는 `preview/character/<name>/…`)를 확인한다 (그리기 도구 호출 후 훅이 자동 갱신함. 없으면 직접 export)
2. **Read로 이미지를 직접 보고** 문제를 말로 적은 뒤 다음 단계로 간다
3. 숫자 검증을 병행: `get_color_stats`(색 수), `compare_frames`, `audit_animation`
"그렸다"가 아니라 "봤더니 이렇다"로 보고한다.

## 스타일
- 특정 게임·작품 스타일(예: 스타듀밸리풍)은 기본으로 적용하지 않는다. 사용자가 스프라이트 생성 요청에서 **따로 요청할 때만** 적용한다
- 예전에 정리한 스타일 노트는 `archive/`에 있다(스킬로 로드되지 않음). 사용자가 그 스타일을 요청하면 해당 폴더를 읽어 참고한다

## 스킬 선택
- 새 스프라이트/아이템/몬스터 → `pixel-sprite`
- 애니메이션(idle/move/attack/damage 4방향 + die 1개, 스킬 언급 시 skill_{N}) → `sprite-animation`
- 던전 3D 타일(벽·바닥·문) 텍스처 → `dungeon-tile`
- 게임용 export(플랫폼 무관 PNG·JSON), 색 변형(팔레트 스왑) → `game-export`

## 완료 전 검수
작업을 "완료"로 보고하기 전에 `pixel-reviewer` 에이전트에 파일 경로와 스타일을 넘겨 독립 검수를 받는다. NEEDS_WORK이면 고친 뒤 다시 검수한다.

## 게임용 export와 Unity 복사 (파이프라인 마지막 단계, 필수)
스프라이트·텍스처 작업은 **검수 PASS → 게임용 export → `Assets/Texture/` 복사 확인**까지 끝내야 완료다. 사용자가 따로 요청하지 않아도 한다 (`game-export` 스킬).

| 종류 | export 결과 (`Aseprite/export/…`) | Unity 복사 위치 |
|---|---|---|
| 캐릭터·몬스터 | `character/<name>/<name>_<W>x<H>t.png`(대표 1프레임), `_sheet.png` + `_sheet.json`(태그 포함), `_gif/<tag>.gif` | `Assets/Texture/character/<name>/`에 원본 `src/character/<name>/*.aseprite`**만** 복사 (png·시트·json·GIF는 복사하지 않음. Unity Aseprite Importer가 태그별 클립·AnimatorController 생성) |
| 아이템·소품 | `<분류>/<name>/<name>.png` (애니메이션이 있으면 시트도) | `Assets/Texture/<분류>/<name>/` |
| 던전 타일셋 | `dungeon_atlas.py build` → `tileset/<theme>/<theme>.png` + `.json` | `Assets/Texture/tileset/<theme>/` |

- 복사는 훅·빌드 스크립트가 자동으로 한다. 끝나면 `Assets/Texture/<분류>/…`에 파일이 생겼는지 확인하고, 결과 파일 목록(시트 크기·프레임 수·태그 수)을 보고한다
- 자동 복사가 안 됐으면 `python .claude/hooks/sync_texture.py`를 실행한다
- 원본을 고치면 export를 다시 해서 `Assets/Texture/` 복사본도 갱신한다
