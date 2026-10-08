"""던전 3D 타일 텍스처 아틀라스 빌더 (벽·바닥·문틀·문짝).

원본 .aseprite의 슬라이스 `<오브젝트>_<변형>.<면>`(예: wall_2.front, door_leaf.side)을 잘라
가장자리 확장(extrude) 여백을 붙여 한 장의 아틀라스 PNG로 묶고, 면 사각형 JSON을 쓴다.
좌표는 모두 px, 왼쪽 위 원점, y-down. 명세는 references/object-spec.md.

사용:
  python dungeon_atlas.py init  Aseprite/src/tileset/<theme>/<theme>.aseprite   # 빈 원본 시트 (슬라이스만)
  python dungeon_atlas.py build Aseprite/src/tileset/<theme>/<theme>.aseprite [--out <dir>] [--pad 2]

결과 (--out 생략 시 src/tileset/<theme>에 대응하는 export/tileset/<theme>):
  Aseprite/export/tileset/<theme>/<theme>.png, <theme>.json  (+ Assets/Texture/tileset/<theme>/에 복사: .claude/hooks/sync_texture.py)
  Aseprite/preview/tileset/<theme>/<theme>.atlas.preview.png    아틀라스 8배
  Aseprite/preview/tileset/<theme>/<theme>.objects.preview.png  오브젝트별 카메라 시점 (변형마다)
  Aseprite/preview/tileset/<theme>/<theme>.scene.preview.png    바닥·벽·문을 조립한 장면 (문 닫힘 / 열림)
"""
import argparse
import json
import os
import re
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

ASEPRITE = os.environ.get("ASEPRITE_PATH") or "aseprite"
ROOT = Path(os.environ.get("CLAUDE_PROJECT_DIR") or Path(__file__).resolve().parents[4])
PPU = 32            # 1유닛 = 32px
TILT = 0.5          # 미리보기 카메라: 윗면(수평면)은 깊이 1유닛이 16px로 줄어 보인다
PREVIEW_SCALE = 2
PREVIEW_PX = 2   # 미리보기 내부 배율: 윗면을 반으로 줄일 때 행이 빠지지 않게 2배로 그린다 (디더 무늬가 줄무늬로 보이는 착시 방지)

# 오브젝트 정의: 크기(유닛, x 가로 · y 높이 · z 깊이), 변형 수, 그리는 면과 면 크기(px)
#   front/back = x×y, top = x×z, side(오른쪽·왼쪽 끝면, 같은 그림) = z×y
OBJECTS = {
    "wall": dict(size=(1, 1, 0.25), variants=4, faces=("front", "back", "top", "side")),
    "floor": dict(size=(1, 0.25, 1), variants=4, faces=("top", "front")),
    "door_frame": dict(size=(0.0625, 1, 0.25), variants=1, faces=("front", "back", "top")),
    "door_leaf": dict(size=(0.875, 1, 0.21875), variants=1, faces=("front", "back", "top", "side")),
    # 바닥 가장자리 디더링 데칼: 바닥 윗면에 얹는 투명 32×32 (높이 없음). 벽이 닿는 변마다 한 장.
    # 텍스처 위쪽 변 = 벽 쪽(북쪽 기준으로 그린다). 동·남·서는 돌려서 쓴다 (EDGE_ROTATION)
    # max_depth: 벽 쪽 변에서부터 그림이 들어올 수 있는 최대 깊이(px). 그 아래는 투명이어야 한다
    "floor_edge": dict(size=(1, 0, 1), variants=4, faces=("top",), decal=True, max_depth=5, layer=1),
}
# 평면 데칼(두께 없는 텍스처)의 그리는 순서. 높은 layer가 낮은 layer를 덮는다. 0 = 바닥 윗면 자체
# 같은 높이에 놓이므로 깊이(높이)가 아니라 이 번호로 순서를 정한다. 같은 layer끼리는 순서 무관하게 그린다
DECAL_LAYERS = {1: "floor_edge (wall-side moss/dust)", 2: "rugs, carpets (reserved)"}
DECAL_LIFT = 0.002   # 3D 엔진용 보조 높이: 바닥 윗면 + DECAL_LIFT × layer
# 바닥 가장자리 데칼 회전 (위에서 내려다봤을 때 시계 방향, 도). 벽이 있는 쪽 = 텍스처 위쪽 변
EDGE_ROTATION = {"north": 0, "east": 90, "south": 180, "west": 270}


def face_size(obj: str, face: str) -> tuple[int, int]:
    x, y, z = (round(v * PPU) for v in OBJECTS[obj]["size"])
    return {"front": (x, y), "back": (x, y), "top": (x, z), "side": (z, y)}[face]


def slice_names(obj: str):
    o = OBJECTS[obj]
    for v in range(1, o["variants"] + 1):
        key = f"{obj}_{v}" if o["variants"] > 1 else obj
        for f in o["faces"]:
            yield key, v, f, f"{key}.{f}"


# ---------------------------------------------------------------- init
def layout():
    """원본 시트 배치: 오브젝트 변형 하나 = 한 블록 [top 위 / front 아래 | back | side], 블록 사이 4px."""
    gap, y, rects = 4, 0, {}
    width = 0
    for obj in OBJECTS:
        x, row_h = 0, 0
        for v in range(1, OBJECTS[obj]["variants"] + 1):
            key = f"{obj}_{v}" if OBJECTS[obj]["variants"] > 1 else obj
            faces = OBJECTS[obj]["faces"]
            fw, fh = face_size(obj, "front")
            tw, th = face_size(obj, "top")
            cx = x
            if "front" in faces:
                if "top" in faces: rects[f"{key}.top"] = (cx, y, tw, th)
                rects[f"{key}.front"] = (cx, y + th, fw, fh)
                cx += fw + gap
            elif "top" in faces:
                rects[f"{key}.top"] = (cx, y, tw, th); cx += tw + gap
            for f in ("back", "side"):
                if f in faces:
                    w, h = face_size(obj, f)
                    rects[f"{key}.{f}"] = (cx, y + th, w, h)
                    cx += w + gap
            h_used = th + max(face_size(obj, f)[1] for f in faces if f != "top") if any(f != "top" for f in faces) else th
            row_h = max(row_h, h_used)
            x = cx + gap
        width = max(width, x)
        y += row_h + gap * 2
    return rects, width, y


def sibling_dir(src: Path, kind: str) -> Path:
    """폴더 규약 Aseprite/src/<분류>/... 에 대응하는 Aseprite/<kind>/<분류>/... (kind = preview | export)."""
    for parent in src.parents:
        if parent.name == "src":
            return parent.parent / kind / src.parent.relative_to(parent)
    return src.parent / kind


def cmd_init(out: Path) -> None:
    rects, W, H = layout()
    lua = [f"local spr = Sprite({W}, {H}, ColorMode.RGB)", 'spr.layers[1].name = "texture"']
    for name, (x, y, w, h) in rects.items():
        lua.append(f'do local s = spr:newSlice(Rectangle({x}, {y}, {w}, {h})); s.name = "{name}" end')
    out.parent.mkdir(parents=True, exist_ok=True)
    lua.append(f'spr:saveAs("{str(out.resolve()).replace(chr(92), "/")}")')
    with tempfile.NamedTemporaryFile("w", suffix=".lua", delete=False, encoding="utf-8") as f:
        f.write("\n".join(lua)); script = f.name
    r = subprocess.run([ASEPRITE, "-b", "--script", script], capture_output=True, text=True)
    os.unlink(script)
    if r.returncode != 0 or r.stderr.strip():
        sys.exit(f"init failed:\n{r.stdout}\n{r.stderr}")
    print(f"created {out} ({W}x{H}), {len(rects)} face slices")
    for name, (x, y, w, h) in rects.items():
        print(f"  {name:22s} {x:4d} {y:4d} {w:3d}x{h}")


# ---------------------------------------------------------------- build
def load(src: Path):
    with tempfile.TemporaryDirectory() as tmp:
        png, data = Path(tmp) / "sheet.png", Path(tmp) / "sheet.json"
        subprocess.run([ASEPRITE, "-b", "--frame-range", "0,0", str(src), "--sheet", str(png), "--data", str(data),
                        "--format", "json-array", "--list-slices"], check=True, capture_output=True)
        img = Image.open(png).convert("RGBA")
        meta = json.loads(data.read_text(encoding="utf-8"))["meta"]
    slices = {s["name"]: tuple(s["keys"][0]["bounds"][k] for k in "xywh") for s in meta.get("slices", [])}
    return img, slices


def extrude(face: Image.Image, pad: int) -> Image.Image:
    w, h = face.size
    out = Image.new("RGBA", (w + 2 * pad, h + 2 * pad))
    out.paste(face, (pad, pad))
    for k in range(pad):
        out.paste(face.crop((0, 0, 1, h)), (k, pad))
        out.paste(face.crop((w - 1, 0, w, h)), (pad + w + k, pad))
    top_row, bottom_row = out.crop((0, pad, w + 2 * pad, pad + 1)), out.crop((0, pad + h - 1, w + 2 * pad, pad + h))
    for k in range(pad):
        out.paste(top_row, (0, k)); out.paste(bottom_row, (0, pad + h + k))
    return out


def pack(items, max_w=256):
    """선반 배치: 큰 것부터 왼→오, 줄이 차면 다음 줄"""
    order = sorted(items, key=lambda kv: (-kv[1].height, -kv[1].width))
    x = y = row_h = 0
    pos, W = {}, 0
    for name, im in order:
        if x + im.width > max_w and x > 0:
            x, y, row_h = 0, y + row_h, 0
        pos[name] = (x, y)
        x += im.width; row_h = max(row_h, im.height); W = max(W, x)
    return pos, W, y + row_h


def build(src: Path, out_dir: Path, pad: int) -> None:
    theme = src.stem
    img, slices = load(src)
    faces, errors = {}, []
    for obj in OBJECTS:
        for key, v, f, name in slice_names(obj):
            if name not in slices:
                errors.append(f"슬라이스 없음: {name}"); continue
            x, y, w, h = slices[name]
            if (w, h) != face_size(obj, f):
                errors.append(f"{name}: 크기 {w}x{h}, 필요 {face_size(obj, f)}")
            faces[name] = img.crop((x, y, x + w, y + h))
    if errors:
        sys.exit("\n".join(errors))
    for name, im in faces.items():
        a = set(im.getchannel("A").tobytes())
        if a - {0, 255}:
            print(f"경고 {name}: 반투명 픽셀")
        if 0 in a and not name.startswith("door") and not OBJECTS[name.split(".")[0].rsplit("_", 1)[0]].get("decal"):
            print(f"경고 {name}: 투명 픽셀 있음 (벽·바닥 면은 꽉 채운다)")
        key = re.sub(r"_\d+$", "", name.split(".")[0])   # wall_2 → wall, door_frame → door_frame
        depth = OBJECTS[key].get("max_depth")
        if depth and im.getchannel("A").crop((0, depth, im.width, im.height)).getbbox():
            errors.append(f"{name}: 벽 쪽 변에서 {depth}px 아래에 그림이 있음 (가장자리 깊이 {depth}px 이하)")
    if errors:
        sys.exit("\n".join(errors))

    padded = {n: extrude(im, pad) for n, im in faces.items()}
    pos, W, H = pack(padded.items())
    atlas = Image.new("RGBA", (W, H))
    rects = {}
    for n, (x, y) in pos.items():
        atlas.paste(padded[n], (x, y))
        rects[n] = {"x": x + pad, "y": y + pad, "w": faces[n].width, "h": faces[n].height}

    objects = {}
    for obj, o in OBJECTS.items():
        sx, sy, sz = o["size"]
        variants = []
        for v in range(1, o["variants"] + 1):
            key = f"{obj}_{v}" if o["variants"] > 1 else obj
            variants.append({"name": key, "faces": {f: rects[f"{key}.{f}"] for f in o["faces"]}})
        objects[obj] = {"size": {"x": sx, "y": sy, "z": sz}, "variants": variants}
    objects["door_frame"]["placement"] = {"count": 2, "x": [0, 1 - objects["door_frame"]["size"]["x"]],
                                          "note": "문 타일(1×1×0.25) 안 좌우 끝. 오른쪽 문틀도 같은 텍스처"}
    leaf_x = objects["door_frame"]["size"]["x"]
    objects["door_leaf"]["placement"] = {
        "x": leaf_x, "z": round((0.25 - objects["door_leaf"]["size"]["z"]) / 2, 6),
        "hinge": "left edge (x = 0.0625), vertical axis", "open": "rotate 90° around the hinge toward the room (camera side)"}
    objects["floor_edge"]["decal"] = True
    objects["floor_edge"]["layer"] = OBJECTS["floor_edge"]["layer"]
    objects["floor_edge"]["placement"] = {
        "onTopOf": "floor", "y": "floor top (0.25) + decalLayers.lift × layer",
        "when": "one decal per floor side whose neighbor cell is a wall (a floor walled on 4 sides gets 4)",
        "textureTopEdge": "the wall side",
        "rotationBySide": EDGE_ROTATION,
        "rotationNote": "degrees clockwise seen from above, around the floor tile's vertical center axis; north = +z (away from camera)",
        "selection": "random variant per decal"}
    meta = {
        "type": "tileset", "name": theme,  # Unity 우클릭 메뉴 구분용 (type = tileset | character)
        "image": f"{theme}.png", "size": {"w": W, "h": H},
        "coords": "px, origin top-left, y-down", "pixelsPerUnit": PPU, "padding": pad,
        "decalLayers": {
            "rule": "flat decals lie on the same plane, so order them by layer, not by depth. "
                    "A higher layer always covers a lower one; decals of the same layer may draw in any order "
                    "(they are drawn to look the same either way, e.g. two floor_edge decals crossing in a corner)",
            "2d": "draw order: floor → layer 1 → layer 2 → … → walls/doors (back to front as usual)",
            "3d": "render decals after opaque geometry, in ascending layer order, depth test on, depth write off; "
                  "also lift each decal by lift × layer above the floor top",
            "lift": DECAL_LIFT,
            "layers": {str(k): v for k, v in DECAL_LAYERS.items()}},
        "axes": "x right, y up, z away from the camera (front face = z min side, faces the camera)",
        "faceRules": {
            "front": "seen from the front (camera side); texture left = object left (x min), texture top = object top",
            "back": "seen from behind; texture left = object right (x max)",
            "top": "seen from above with the FRONT edge at the texture bottom; texture left = object left",
            "side": "end faces (x min and x max); seen from outside, texture top = object top, texture left = front edge for the right end",
        },
        "selection": "wall_N / floor_N / floor_edge_N: random variant per tile (per decal), independently",
        "objects": objects,
    }
    out_dir.mkdir(parents=True, exist_ok=True)
    atlas.save(out_dir / f"{theme}.png")
    (out_dir / f"{theme}.json").write_text(json.dumps(meta, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    prev = sibling_dir(src, "preview"); prev.mkdir(parents=True, exist_ok=True)
    s = PREVIEW_SCALE
    atlas.resize((W * 8, H * 8), Image.NEAREST).save(prev / f"{theme}.atlas.preview.png")
    objs = objects_preview(faces)
    objs.resize((objs.width * s, objs.height * s), Image.NEAREST).save(prev / f"{theme}.objects.preview.png")
    sc = scene_preview(faces)
    sc.resize((sc.width * s, sc.height * s), Image.NEAREST).save(prev / f"{theme}.scene.preview.png")
    print(f"{theme}: atlas {W}x{H}, {len(faces)} faces -> {out_dir}")
    # Aseprite/export/tileset → Assets/Texture/tileset 복사 (훅과 같은 스크립트)
    subprocess.run([sys.executable, str(ROOT / ".claude" / "hooks" / "sync_texture.py"), "--cli"], stdin=subprocess.DEVNULL)


# ---------------------------------------------------------------- 미리보기 (카메라: 남쪽에서 비스듬히 내려다봄, 옆면은 안 보임)
def squash(top: Image.Image) -> Image.Image:
    """수평면(윗면)을 카메라 기울기만큼 세로로 줄인다 (내부 배율 PREVIEW_PX로 키운 뒤라 행이 빠지지 않는다)"""
    return top.resize((top.width * PREVIEW_PX, max(1, round(top.height * PREVIEW_PX * TILT))), Image.NEAREST)


def grow(tex: Image.Image) -> Image.Image:
    return tex.resize((tex.width * PREVIEW_PX, tex.height * PREVIEW_PX), Image.NEAREST)


class Canvas:
    """화면 좌표: sx = x·PPU, sy = BASE − (y·PPU + z·PPU·TILT). 먼 것(z 큰 것)부터 그린다"""
    def __init__(self, w_units, h_px, base):
        P = PREVIEW_PX
        self.im = Image.new("RGBA", (round(w_units * PPU * P), h_px * P), (24, 22, 30, 255))
        self.base = base * P

    def at(self, x, y, z):
        P = PPU * PREVIEW_PX
        return round(x * P), round(self.base - (y * P + z * P * TILT))

    def front(self, tex, x, y, z):           # z = 앞면이 놓인 깊이, (x, y) = 앞면 왼쪽 아래
        tex = grow(tex)
        sx, sy = self.at(x, y, z)
        self.im.alpha_composite(tex, (sx, sy - tex.height))

    def top(self, tex, x, y, z):             # (x, y, z) = 윗면 앞쪽 왼쪽 모서리
        t = squash(tex)
        sx, sy = self.at(x, y, z)
        self.im.alpha_composite(t, (sx, sy - t.height))


def edge_decal(cv, faces, v, x, z, side):
    """바닥 (x, z) 칸 윗면에 가장자리 데칼. side = 벽이 있는 쪽"""
    # 위에서 본 텍스처를 시계 방향으로 돌린다. 미리보기 top()은 '텍스처 아래 = 앞쪽'이므로
    # 북쪽(먼 쪽) 벽 = 텍스처 위쪽 변 그대로
    tex = faces[f"floor_edge_{v}.top"].rotate(-EDGE_ROTATION[side], expand=True)
    cv.top(tex, x, 0.25, z)


def box(cv, faces, key, obj, x, y, z, rotated=False):
    """오브젝트 하나: 윗면 + 카메라 쪽 면. rotated = 90° 돌려 깊이 방향으로 선 경우 (끝면이 카메라 쪽)"""
    sx, sy, sz = OBJECTS[obj]["size"]
    if not rotated:
        cv.top(faces[f"{key}.top"], x, y + sy, z)
        cv.front(faces[f"{key}.front"], x, y, z)
    else:  # 가로 sz, 깊이 sx: 윗면은 90° 돌린 그림, 카메라 쪽 = 끝면(side)
        top = faces[f"{key}.top"].rotate(90, expand=True)
        cv.top(top, x, y + sy, z)
        if f"{key}.side" in faces:
            cv.front(faces[f"{key}.side"], x, y, z)


def objects_preview(faces):
    tiles = []
    for obj, o in OBJECTS.items():
        for v in range(1, o["variants"] + 1):
            key = f"{obj}_{v}" if o["variants"] > 1 else obj
            sx, sy, sz = o["size"]
            if o.get("decal"):  # 데칼은 바닥 위에 얹은 모습으로 (벽 = 북쪽, 미리보기에서 위쪽)
                fy, fz = OBJECTS["floor"]["size"][1], OBJECTS["floor"]["size"][2]
                cv = Canvas(1.25, round((fy + fz * TILT) * PPU) + 8, round((fy + fz * TILT) * PPU) + 4)
                box(cv, faces, "floor_1", "floor", 0, 0, 0)
                cv.top(faces[f"{key}.top"], 0, fy, 0)
                tiles.append(cv.im)
                continue
            cv = Canvas(sx + 0.25, round((sy + sz * TILT) * PPU) + 8, round((sy + sz * TILT) * PPU) + 4)
            box(cv, faces, key, obj, 0.125 if sx < 0.5 else 0, 0, 0)
            tiles.append(cv.im)
    W = sum(t.width for t in tiles) + 4 * len(tiles)
    out = Image.new("RGBA", (W, max(t.height for t in tiles)), (40, 40, 48, 255))
    x = 0
    for t in tiles:
        out.paste(t, (x, out.height - t.height)); x += t.width + 4
    return out


def scene_preview(faces):
    """방 한 조각: 뒤쪽 벽 줄(가운데 문), 왼쪽에 깊이 방향 벽(끝면이 카메라 쪽), 앞쪽 바닥 3×2. 닫힘 | 열림"""
    shots = []
    for open_ in (False, True):
        cv = Canvas(5, 140, 120)
        # 문 너머 통로 바닥 (가장 먼 것부터): 문이 열리면 이 바닥이 보인다
        cv.top(faces["floor_1.top"], 2, 0.25, 2)
        # 바닥 (먼 줄부터): x 1..4, z 0..2 (벽 앞)
        for zi in (1, 0):
            for xi in range(1, 4 + 0):
                v = (xi + zi * 3) % 4 + 1
                cv.top(faces[f"floor_{v}.top"], xi, 0.25, zi)
                cv.front(faces[f"floor_{v}.front"], xi, 0, zi)
                # 벽이 닿는 변마다 디더링 데칼: 뒤 벽 줄 앞 (z=1 줄의 북쪽, 문 칸 x=2 제외), 왼쪽 벽 옆 (x=1 칸의 서쪽)
                if zi == 1 and xi != 2:
                    edge_decal(cv, faces, (xi % 4) + 1, xi, zi, "north")
                if xi == 1:
                    edge_decal(cv, faces, ((zi + 2) % 4) + 1, xi, zi, "west")
        y0 = 0.25
        # 뒤 벽 줄 z = 2..2.25: 벽 / 문 / 벽, 문 = x 2..3
        for xi in (1, 3):
            box(cv, faces, f"wall_{xi % 4 + 1}", "wall", xi, y0, 2)
        box(cv, faces, "door_frame", "door_frame", 2, y0, 2)
        box(cv, faces, "door_frame", "door_frame", 3 - 0.0625, y0, 2)
        leaf = OBJECTS["door_leaf"]["size"]
        if not open_:
            box(cv, faces, "door_leaf", "door_leaf", 2.0625, y0, 2 + (0.25 - leaf[2]) / 2)
        else:  # 경첩(왼쪽 끝)을 축으로 방 쪽(카메라 쪽)으로 90° 열림: 깊이 방향으로 선다
            box(cv, faces, "door_leaf", "door_leaf", 2.0625, y0, 2 - leaf[0] + 0.03, rotated=True)
        # 왼쪽 벽: 깊이 방향으로 선 벽 2칸 (x 0.75..1, z 0..2), 카메라 쪽 끝면이 보인다
        for zi in (1, 0):
            box(cv, faces, f"wall_{zi + 2}", "wall", 0.75, y0, zi, rotated=True)
        shots.append(cv.im)
    out = Image.new("RGBA", (shots[0].width * 2 + 6, shots[0].height), (40, 40, 48, 255))
    out.paste(shots[0], (0, 0)); out.paste(shots[1], (shots[0].width + 6, 0))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("init"); p.add_argument("out", type=Path)
    p = sub.add_parser("build"); p.add_argument("sources", nargs="+", type=Path)
    p.add_argument("--out", type=Path, default=None); p.add_argument("--pad", type=int, default=2)
    a = ap.parse_args()
    if a.cmd == "init":
        cmd_init(a.out if a.out.is_absolute() else ROOT / a.out)
    else:
        for src in a.sources:
            src = src if src.is_absolute() else ROOT / src
            build(src, a.out or sibling_dir(src, "export"), a.pad)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
