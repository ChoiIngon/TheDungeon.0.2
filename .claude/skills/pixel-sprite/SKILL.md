---
name: pixel-sprite
description: Aseprite로 게임 스프라이트(32px 타일 기반 캐릭터, 몬스터, 아이템, 아이콘, 소품)를 새로 그리거나 다듬을 때 사용. 캔버스·팔레트 결정, 실루엣, 음영, 외곽선, 색 수 검증, 미리보기 확인 절차. "몬스터 그려줘", "포션 아이콘", "32px 스프라이트" 같은 요청에 사용.
---

# 픽셀 스프라이트 제작

## 0. 결정 먼저 (픽셀 찍기 전에 사용자에게 한 줄로 보고)
| 항목 | 기본값 |
|---|---|
| 캔버스 | **32×32** (1타일). 여러 타일이면 32의 배수 (2×2타일 → 64×64). CLAUDE.md 크기 규칙 |
| 광원 | 좌상단 |
| 색 수 | 32px 1타일은 외곽선 포함 **12~16색 이하**, 여러 타일은 24색 이하 |
| 외곽선 | 순검정 금지. 본체 색 계열의 어두운 웜톤 (예: `#2b1a10`) |
| 파일 | 캐릭터·몬스터: `Aseprite/src/character/<name>/<name>_<가로>x<세로>t.aseprite` (예: `Aseprite/src/character/bat/bat_1x1t.aseprite`). 미리보기 `preview/character/<name>/`, 결과물 `export/character/<name>/` (캐릭터마다 폴더를 따로 둔다). 그 밖(아이템·소품)은 `Aseprite/src/` |

캐릭터·몬스터라면 정지 이미지를 완성한 뒤 `sprite-animation` 스킬로 기본 애니메이션 17태그를 만든다. 마법·스킬 언급이 있으면 `skill_{N}` 태그도 추가한다.

## 1. 레이어 구조 (아래 → 위)
```
base      실루엣 + 바디 색
shade     그림자·하이라이트
detail    눈, 무늬, 장식
outline   외곽선 (마지막)
```
단순한 아이템·소품은 `base`/`outline` 두 장으로 합쳐도 된다. 애니메이션할 파츠가 있으면 파츠별로 나눈다 (`sprite-animation` 스킬 참고).

## 2. 절차
1. `create_canvas` → `add_layer`로 레이어 구성
2. **팔레트**: 재질마다 `generate_color_ramp(base, steps=4~5)`로 휴 시프트 램프를 만든다. 명도만 바꾼 램프는 탁해진다. 필요하면 `set_palette`로 등록
3. **실루엣**: `base`에 바디 색 한 가지로만 형태를 잡는다 (`draw_polygon`, `fill_area_at`, `draw_pixels_at`)
   → **preview 확인**: 1색 실루엣만으로 무엇인지 읽혀야 한다. 안 읽히면 여기서 고친다
4. **음영**: 광원 반대쪽(우하단)에 한 단계 어두운 색, 좌상단 가장자리에 하이라이트 1px. 2~3단계면 충분
5. **디테일**: 눈은 32px에서 2~3px. 흰 하이라이트 1px로 생기를 준다
6. **외곽선**: `outline_native(place="outside", matrix="square")`는 모서리가 각지고, `"circle"`은 부드럽다. 외곽선 레이어를 따로 두고 **안쪽 외곽선(sel-out)**은 인접 면의 어두운 색으로 바꾼다
7. **검증** (아래 체크리스트)
8. export: `export_sprite(src/<분류>/..., export/<분류>/<name>.png)` (캐릭터는 `Aseprite/export/character/<name>/`). 훅이 `Assets/Texture/<분류>/<name>/`에 복사하므로 복사됐는지 확인한다 (캐릭터는 `.aseprite` 원본만 복사된다). 캐릭터·몬스터는 애니메이션까지 만든 뒤 `sprite-animation` §6 순서로 시트를 export한다

## 3. 검증 체크리스트
- [ ] `preview/<분류>/<name>.preview.png`를 Read로 봤다
- [ ] `get_color_stats` → 거의 같은 색이 여러 개 없음 (있으면 `quantize_to_palette` 또는 `replace_color`)
- [ ] 고아 픽셀(주변과 이어지지 않은 1px) 없음
- [ ] 외곽선이 2px 두께로 뭉친 곳(더블 라인, 계단 뭉침) 없음
- [ ] 광원 방향이 모든 파츠에서 같음
- [ ] 1x 크기로도 읽힘 (8배로만 보면 속는다)

## 4. 하지 말 것
- `apply_gradient_rect`(매끄러운 그라데이션)을 스프라이트 바디에 쓰기 → 색 수 폭증, 흐릿함. 섞어야 하면 `apply_dither_pattern`을 소량만 쓴다
- `apply_convolution`(blur 등) → 픽셀아트를 망가뜨린다. 쓰지 않는다
- 필로우 셰이딩(외곽에서 안쪽으로 동심원처럼 밝아지는 음영) → 광원 방향을 따른다
- 순검정 `#000000` 외곽선

## 5. Lua 스크립트를 쓸 때
`run_lua_script` 전에 [references/lua-pitfalls.md](references/lua-pitfalls.md)를 읽는다 (`math.pow` 제거, 저장 누락, cel 좌표계 등).
