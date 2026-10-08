---
name: game-export
description: Aseprite 결과물을 게임용(Unity, Unreal, Godot, 자바스크립트 등 플랫폼 무관)으로 내보낼 때 사용. 스프라이트 시트 PNG+JSON(태그 포함), 태그별 GIF, 레이어·슬라이스 export, 팔레트 스왑 색 변형(포션 7색 같은) 일괄 생성. "시트로 뽑아줘", "export", "색 바리에이션", "유니티/언리얼/고도트/웹용" 요청에 사용.
---

# 게임용 Export

## 1. 파일 이름 규약 (기존 결과물 기준)

경로는 `Aseprite/` 기준. `<분류>` = `character`, `tileset`, `item` 등. `export/<분류>/` 아래에 쓴 결과물은 훅(`.claude/hooks/sync_texture.py`)이 `Assets/Texture/<분류>/`에 같은 하위 경로로 자동 복사한다 (GIF 제외). 캐릭터는 예외로 원본 `src/character/<name>/*.aseprite`만 `Assets/Texture/character/<name>/`에 복사하고, `export/character/`의 png·시트 png·시트 json은 Unity로 복사하지 않는다 (Unity Aseprite Importer가 .aseprite에서 스프라이트·태그별 클립·AnimatorController를 만든다). 이 export와 복사는 스프라이트·텍스처 작업의 **필수 마지막 단계**다. 끝나면 `Assets/Texture/` 쪽에 파일이 생겼는지 확인한다.

캐릭터·몬스터는 캐릭터 폴더 `<분류>/<char>/`를 쓴다 (`<char>` = 캐릭터 이름, `<name>` = `<char>_<W>x<H>t`).

| 결과물 | 경로 | 도구 |
|---|---|---|
| 대표 1프레임 | `export/<분류>/<char>/<name>.png` | `export_frame` 또는 `export_sprite` |
| 전체 시트 | `export/<분류>/<char>/<name>_sheet.png` + `_sheet.json` | `export_spritesheet` |
| 태그별 GIF | `export/<분류>/<char>/<name>_gif/<tag>.gif` | `export_tag`. 17개를 한 번에 하려면 `run_lua_script`에서 `app.command.SaveFileCopyAs{filename=…, tag=…}` |
| 미리보기 | `preview/<분류>/<char>/<name>_preview.png` (scale 8) | `export_spritesheet(scale=8)` |

`<name>`에는 타일 크기 접미사를 붙인다 (`golem_2x2t`, `bat_1x1t`). 태그별 GIF는 17개(`idle_down.gif` … `damage_right.gif`, `die.gif`)이고, 스킬이 있으면 `skill_1_down.gif` 등 스킬당 4개가 더해진다.

## 2. 스프라이트 시트 기본값
```
export_spritesheet(
  filename="Aseprite/src/character/<name>/<name>.aseprite",
  output_filename="Aseprite/export/character/<name>/<name>_sheet.png",
  data_filename="Aseprite/export/character/<name>/<name>_sheet.json",
  sheet_type="horizontal",   # 프레임이 많으면(>64) "rows"
  data_format="json-array",
  list_tags=true,            # 반드시 true: 게임 쪽에서 태그로 애니메이션 분리
  scale=1, padding=0         # 텍스처 블리딩 문제가 있으면 padding=1
)
```
export 직후 훅이 시트 JSON 맨 앞에 `"type": "character"`, `"name": "<char>"`를 넣는다 (Unity 우클릭 메뉴가 type으로 캐릭터·타일셋을 구분한다). 타일셋 JSON은 `dungeon_atlas.py`가 `"type": "tileset"`을 넣는다.

export 후 JSON을 읽어 **type·name, 태그 목록, 프레임 수, 시트 크기**를 보고한다 (`frames` 개수 = 스프라이트 프레임 수인지 확인).

## 3. 팔레트 스왑 색 변형
potion 7색(red/blue/green/cyan/magenta/orange/yellow) 방식:
1. 원본의 **바뀌어야 하는 색만** 식별한다 (`get_palette`, `get_color_stats`). 외곽선, 유리, 하이라이트는 유지한다
2. 변형마다 `copy_sprite(src/<분류>/<name>.aseprite, preview/<분류>/_tmp_<variant>.aseprite)`로 복사
3. `replace_color`를 램프 단계마다 적용한다 (또는 `remap_colors_in_cel_range`, 대상 레이어만 `adjust_hsl_native`)
4. `export/<분류>/<name>_<variant>.png`로 export하고, 전체를 비교하는 `export/<분류>/<name>_sheet.png`도 만든다
5. 변형이 많으면 `run_lua_script` 한 번으로 복제, 치환, 저장을 반복한다

색 변형은 HSL을 일괄로 돌리기보다 **램프를 새로 지정**해야 명도 관계가 유지된다 (`generate_color_ramp`).

## 4. 플랫폼 독립 원칙
결과물은 Unity, Unreal, Godot, 자바스크립트 등 어디서든 쓰인다.
- 내보내는 것은 **PNG + JSON**뿐이다. 특정 엔진 전용 파일(`.meta`, `.tres`, `.uasset`, 엔진 임포트 설정)은 사용자가 그 엔진을 지정했을 때만 만든다
- Aseprite 시트 JSON(`frames`의 `frame` 사각형, `duration` ms, `meta.frameTags`)은 엔진 중립 형식이다. 이 형식을 그대로 둔다
- 좌표는 **왼쪽 위 원점, y-down px**로 적는다. 피벗·앵커가 필요하면 정규화 값이 아니라 **스프라이트 안의 px 좌표**로 적는다 (예: 32×32 캐릭터 발밑 앵커 `(16, 30)`). y-up 엔진의 정규화 피벗은 `(ax / w, 1 − ay / h)`로 환산된다고 함께 적는다
- 결과를 쓸 때 할 일은 엔진 이름 없이 적는다: 최근접(nearest/point) 필터, 색이 섞이는 압축·밉맵 끄기, 타일 32px = 칸 1개

## 5. 최종 점검
- [ ] 1x export에 반투명 픽셀이 없는지 확인 (게임용은 보통 알파 0/255만)
- [ ] 시트 JSON에 `frameTags`가 있는지 확인
- [ ] `Assets/Texture/<분류>/…`에 PNG·JSON(캐릭터는 `.aseprite`만)이 복사됐는지 확인 (안 됐으면 `python .claude/hooks/sync_texture.py`)
- [ ] 결과 파일 목록과 크기를 사용자에게 보고
