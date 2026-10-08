---
name: dungeon-tile
description: 던전 3D 타일 오브젝트(벽 1×1×0.25, 바닥 1×0.25×1, 문 = 문틀 2개 + 문짝)와 바닥 가장자리 디더링 데칼(이끼·먼지)에 입힐 픽셀아트 텍스처를 만들 때 사용. 오브젝트·면 크기(32px = 1유닛), 면 방향 규약, 타일링 규약, Aseprite 원본 시트 → 아틀라스 PNG + 면 사각형 JSON 빌드, 고정 카메라 시점 미리보기 검증을 포함. "던전 타일", "벽 텍스처", "바닥 텍스처", "문 텍스처", "던전 테마", "3D 타일", "바닥 이끼", "벽 밑 먼지" 같은 요청에 사용.
---

# 던전 3D 타일 텍스처

던전은 바닥 타일과 벽 타일의 조합이고, 타일은 **3D 오브젝트**(벽, 바닥, 문)다. 카메라는 고정이고 **남쪽에서 아래로 비스듬히** 내려다본다.
이 스킬은 그 오브젝트에 입힐 **면별 텍스처**를 그리고 아틀라스로 굽는다. 결과는 플랫폼과 관계없이 PNG + JSON이다.

> 예전 2D 3/4 시점 스프라이트 파이프라인(파츠 합성, 64장, `tileset.json`)은 `archive/dungeon-tile-2d/`로 옮겼다. 결과물 `Aseprite/src/dungeon/`, `Aseprite/export/dungeon*/`는 그대로 남아 있다. 이전 3D 원본 `Aseprite/src/dungeon3d/temple.aseprite`도 옮기지 않았다.

> **폴더·이름 규약**: **테마마다 폴더를 따로 둔다**: 원본은 `Aseprite/src/tileset/<theme>/`, 미리보기는 `Aseprite/preview/tileset/<theme>/`, 결과물은 `Aseprite/export/tileset/<theme>/` (build가 `Assets/Texture/tileset/<theme>/`에도 복사한다). 생성용 `.lua`도 원본 폴더에 둔다. 테마 파일명은 **테마 이름 그대로**(`crypt`, `temple`) 쓰고 `dungeon_` 접두사를 붙이지 않는다.

## 0. 먼저 읽을 것
| 문서 | 내용 |
|---|---|
| [references/object-spec.md](references/object-spec.md) | **정본.** 오브젝트 크기, 면과 면 크기, 면 방향 규약, 타일링 규약, JSON 형식, 원래 사양과 다른 점, 아직 정하지 않은 배치 |

## 1. 오브젝트와 텍스처 (요약)
| 오브젝트 | 크기 (x × y × z) | 변형 | 면 (px) |
|---|---|---|---|
| `wall` | 1 × 1 × 0.25 | 4 | front 32×32, back 32×32, top 32×8, side 8×32 |
| `floor` | 1 × 0.25 × 1 | 4 | top 32×32, front 32×8 |
| `door_frame` | 0.0625 × 1 × 0.25 | 1 | front 2×32, back 2×32, top 2×8 |
| `door_leaf` | 0.875 × 1 × 0.21875 | 1 | front 28×32, back 28×32, top 28×7, side 7×32 |
| `floor_edge` (데칼) | 1 × 0 × 1 | 4 | top 32×32 (투명 배경) |

- **문 타일** = 벽 타일 크기 안에 문틀 2개(양쪽 끝, 벽과 같은 테마) + 문짝(가운데). 열림은 문짝을 왼쪽 경첩 축으로 방 쪽으로 90° 돌린 것이라 **열림·닫힘 그림을 따로 그리지 않는다**
- **면 방향**: front = 카메라 쪽에서 본 그대로, back = 뒤에서 본 그대로(뒤집지 않음), top = 앞쪽 모서리가 텍스처 아래, side = 바깥에서 본 끝면
- **타일링**: 같은 오브젝트의 변형은 랜덤으로 붙으므로 좌우(바닥은 네 변)가 어떤 변형끼리도 이어져야 한다. 단 높이·띠·굽·줄눈 위치는 변형끼리 같게, 다른 것은 톤·장식·이음줄 자리뿐
- **굽지 않는 것**: 그림자, 모서리 어두운 테, 벽 밑 띠, 외곽선. 3D 조명과 지오메트리가 만든다
- 알파는 0/255만. 벽·바닥 면은 꽉 채운다 (투명 픽셀 없음)
- **바닥 가장자리 데칼** `floor_edge`: 바닥과 벽이 맞닿는 곳의 이끼·먼지. 바닥 윗면에 얹는 투명 32×32이고, **벽이 닿는 변마다 한 장**(4면이 벽이면 4장, 모서리에서는 두 장이 겹친다). **텍스처 위쪽 변 = 벽 쪽**으로 북쪽 기준 하나만 그리고 동 90° · 남 180° · 서 270°(위에서 봐서 시계 방향)로 돌려 쓴다. 자세한 규칙은 object-spec.md "바닥 가장자리 데칼"
- **데칼 layer**: 평면 텍스처는 같은 높이라 깊이가 아니라 layer 번호로 순서를 정한다. 높은 layer가 덮는다 (1 = floor_edge, 2 = 카펫·러그 예약). 새 평면 데코는 `OBJECTS`에 `decal=True, layer=N`. 규칙은 object-spec.md "데칼 layer"

## 2. 파일
| 파일 | 역할 | 손으로 고치나 |
|---|---|---|
| `Aseprite/src/tileset/<theme>/<theme>.aseprite` | 원본. 슬라이스 `<오브젝트>_<변형>.<면>` 35개 (`wall_2.front`, `door_leaf.side`, `floor_edge_3.top` …) | **여기에 그린다** (aseprite MCP) |
| `Aseprite/export/tileset/<theme>/<theme>.png` + `.json` | 아틀라스 + 면 사각형 (플랫폼 무관) | 안 고친다 (build 결과) |
| `scripts/dungeon_atlas.py` `OBJECTS` | 오브젝트 크기·변형 수·면 목록 | object-spec.md와 같이 바꿀 때만 |

테마: `temple` (고대 지하 사원: 사암 벽돌 + 금·비취 상감 띠 + 사암 캡, 판석 바닥, 적갈색 판자문 + 철물, 가장자리 데칼은 그을음·모래 디더 + 이끼·모래 더미·자갈)

## 3. 도구
```
S=.claude/skills/dungeon-tile/scripts
python $S/dungeon_atlas.py init  Aseprite/src/tileset/<theme>/<theme>.aseprite     # 새 테마: 빈 원본 시트 (슬라이스만)
python $S/dungeon_atlas.py build Aseprite/src/tileset/<theme>/<theme>.aseprite     # 아틀라스 + JSON + 미리보기 3장
```
- 그리기는 aseprite MCP로 원본 시트에 한다. 면이 많고 규칙적이므로 `run_lua_script` 한 번으로 그리고, 슬라이스 사각형(`spr.slices`)을 읽어 그 안에만 칠한다
- build는 슬라이스 누락·면 크기 불일치를 멈춤으로, 반투명·벽/바닥 면의 투명 픽셀을 경고로 알린다 (문·데칼은 투명 픽셀이 정상). 데칼이 벽 쪽 변에서 5px(`OBJECTS` `max_depth`)보다 깊이 들어오면 멈춘다
- 이전 테마 시트에 데칼 슬라이스가 없으면 캔버스를 아래로 늘려(`spr:crop`) `floor_edge_1..4.top` 슬라이스를 `init`과 같은 자리(y 191, x 0/40/80/120)에 추가한다
- **엔진 적용**: Unity는 `engine/unity/PixelDungeon/` (JSON 우클릭 → `Create Tile Prefabs`로 Wall_N·Floor_N·Door·FloorEdge_N 프리팹 + TileSet 에셋 생성, 데칼은 `TileSet.AddFloorEdges(바닥, 북, 동, 남, 서)`로 붙인다. 사용법은 그 폴더 README). 다른 엔진은 요청이 있을 때 같은 JSON 규약으로 만든다

## 4. 작업 순서
1. **결정 보고**: 테마, 팔레트(재질별 램프), 벽 단 높이·띠·굽의 행, 바닥 줄눈 자리를 표로 정한다
2. 새 테마면 `init` → 빈 원본 시트
3. **벽** front → top(front 맨 위와 이음) → side(front 좌우와 이음) → back. 4변형은 같은 단·띠·굽에서 톤·장식만 바꾼다
4. **바닥** top(네 변 이음) → front(top 맨 아래 행의 줄눈과 이음)
5. **문틀**(벽과 같은 테마, 벽의 띠·굽 행을 이어 받는다) → **문짝** front → back(경첩 반대로) → top → side(열렸을 때 카메라 쪽)
6. **바닥 가장자리 데칼** 4변형: 위쪽 변(벽 쪽)에서 아래로 옅어지는 순서 디더(Bayer 4×4, 깊이 **5px 이하**(ly 0..4), 행 위상을 4열마다 밀기) 띠를 **모든 변형에서 같게** 깔고, 그 위에 변형마다 이끼 덩이·먼지 더미·자갈 같은 덩이를 x 3..28, ly 0..4 안에만 얹는다
7. `build` → 미리보기 3장을 Read로 본다 (§5)
8. 완료 전 `pixel-reviewer`에 원본 시트와 미리보기를 넘겨 검수

## 5. 검증 (build마다)
| 미리보기 | 볼 것 |
|---|---|
| `Aseprite/preview/tileset/<theme>/<theme>.scene.preview.png` | **고정 카메라 시점으로 조립한 장면** (뒤 벽 줄 + 가운데 문, 깊이 방향 벽, 바닥 3×2, 문 너머 통로). 왼쪽 닫힘 / 오른쪽 열림. 모서리 이음, 벽·바닥 변형끼리의 이음, 문이 벽과 같은 테마로 읽히는지, 열린 문 너머로 바닥이 이어지는지, 가장자리 데칼이 벽 쪽(뒤 벽 앞 줄의 북쪽, 왼쪽 벽 옆 칸의 서쪽)에 붙고 이웃 데칼과 줄무늬·끊김 없이 이어지는지 |
| `Aseprite/preview/tileset/<theme>/<theme>.objects.preview.png` | 오브젝트·변형별로 윗면 + 앞면 (데칼은 floor_1 위에 얹어 보인다). 변형끼리 차이가 보이는지 |
| `Aseprite/preview/tileset/<theme>/<theme>.atlas.preview.png` | 아틀라스 배치와 extrude 여백 |

장면 미리보기는 텍스처 확인용 조립이다. 게임의 실제 배치 규칙(벽을 칸 어디에 세우는지 등)은 아직 정하지 않았다 (object-spec.md 맨 아래).

## 6. 하지 말 것
- 면 크기를 정수 px가 아닌 값으로 정하기 (1.6px 같은 값은 픽셀이 뭉개진다. 크기를 32px 격자에 맞춘다)
- back을 앞에서 본 그림을 좌우 뒤집어 그리기 (뒤에서 본 그대로 그린다)
- top의 앞뒤를 거꾸로 그리기 (앞쪽 모서리 = 텍스처 아래)
- 변형마다 벽돌 단 높이·띠·굽 행을 다르게 두기 (랜덤으로 붙으면 끊긴다)
- 그림자·외곽선·벽 밑 띠를 벽·바닥 텍스처에 굽기 (벽 밑 이끼·먼지는 `floor_edge` 데칼로 따로 만든다)
- 데칼을 방향(북·동·남·서)마다 따로 그리기, 또는 데칼의 디더 띠 높이·밀도를 변형마다 다르게 두기 (옆 칸과 끊긴다)
- 데칼에 반투명 알파 쓰기 (디더로 옅어지게 한다. 알파는 0/255)
- 열림·닫힘 문 그림을 따로 만들기 (문짝을 돌린다)
- 특정 엔진 설정값(PPU, 정규화 피벗)을 규칙으로 적기. 엔진을 지정받았을 때만 덧붙인다
