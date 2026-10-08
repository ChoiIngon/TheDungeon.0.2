"""도적(rogue) 1x1 타일 캐릭터 생성기.

파츠별 문자 그리드를 프레임마다 오프셋으로 조합해 레이어 스트립 PNG를 만든다.
rogue_build.lua 가 스트립을 읽어 rogue_1x1t.aseprite 를 조립한다.

  python Aseprite/src/character/rogue/rogue_gen.py [out_dir] [--preview preview.png]
"""
import json
import math
import os
import sys

from PIL import Image

W = H = 32

PAL = {
    "o": "#221a26",  # 외곽선
    "1": "#62806c",  # 두건 밝음
    "2": "#3f544a",  # 두건 중간
    "3": "#283632",  # 두건 어두움
    "k": "#c48a63",  # 피부
    "K": "#6e4536",  # 피부 그늘(두건 속)
    "W": "#efe4cf",  # 눈 흰자 / 플래시
    "4": "#a0714a",  # 가죽 밝음
    "5": "#714c33",  # 가죽 중간
    "6": "#4a3123",  # 가죽 어두움
    "7": "#4b3e55",  # 바지
    "8": "#30283a",  # 바지 어두움
    "B": "#dfe6ec",  # 칼날 밝음
    "b": "#8c97a6",  # 칼날 어두움
    "G": "#d0a64b",  # 금장식
}
LAYERS = ["legs", "weapon_back", "head_back", "body", "head", "eyes", "weapon_front", "fx"]
RAMPS = [("1", "2", "3"), ("4", "5", "6")]


def g(*rows):
    w = max(len(r) for r in rows)
    return [r.ljust(w, ".") for r in rows]


def mirror(grid):
    return [r[::-1] for r in grid]


def vflip(grid):
    return list(reversed(grid))


# ---------------------------------------------------------------- 정면(down)
HEAD_D = g(
    "....oooooo....",
    "..oo122223oo..",
    ".o1122222233o.",
    "o112222222233o",
    "o1222oooo2233o",
    "o122oKKKKo233o",
    "o12oKKKKKKo33o",
    "o12o111122o33o",
    ".o2o222222o3o.",
    "..oo222223oo..",
    "....oooooo....",
)
EYES_D = {  # (x, y) 왼쪽 위 = 머리 기준
    "L": ["oW..oW"],
    "R": ["Wo..Wo"],
    "X": ["oo..oo"],
}
TORSO_D = g(
    "..oooooooooooooo..",
    ".o11122222222233o.",
    "o1122222GG2222333o",
    "o1222o544445o2333o",
    "o2223o544445o3333o",
    ".oooo64444446o55o.",
    "....o666GG666oKko.",
    "....o54444445ooo..",
    "....oooooooooo....",
)
LEG_D = g(  # 화면 왼쪽 다리 (위 2줄은 치마 뒤에 가려짐)
    "o77o.",
    "o77o.",
    "o77o.",
    "o78o.",
    "o66o.",
    "o566o",
    "ooooo",
)
W_IDLE_D = g(
    "...o55o",
    "...oKko",
    "..obGo.",
    ".obBo..",
    "obBo...",
    "oBo....",
    "oo.....",
)
W_RAISE_D = g(
    "oo.....",
    "oBo....",
    "obBo...",
    ".obBo..",
    "..oGGo.",
    "..oKko.",
    "..o55o.",
    "..o55o.",
)
W_STAB_D = g(
    "o55o....",
    "o555o...",
    ".oKko...",
    "..oGGo..",
    "...obBo.",
    "....obBo",
    ".....oBo",
    "......oo",
)
W_DROP = g(  # 바닥에 떨어진 단검
    "oo....",
    "oGoooo",
    "okGBBBo",
    "oo.ooo.",
)

# ---------------------------------------------------------------- 뒷면(up)
HEAD_U = g(
    "....oooooo....",
    "..oo122223oo..",
    ".o1122222233o.",
    "o112222222233o",
    "o122222222233o",
    "o122222222333o",
    "o122222223333o",
    "o122222233333o",
    ".o2222233333o.",
    "..oo222333oo..",
    "....oooooo....",
)
TORSO_U = g(  # 굽은 등: 솟은 등이 두건 아랫부분을 가린다 (머리는 head_back 레이어)
    "......oooooo......",
    "....oo111223oo....",
    "...o1112222233o...",
    "..o112222222333o..",
    ".o11222222222333o.",
    "o1122222222223333o",
    "o1222222222222333o",
    "o2222222222223333o",
    "o2222322223223333o",
    ".o55o3oo3oo3oooo..",
    ".okKoo666666o.....",
    "..oooo5555555o....",
    "....oooooooooo....",
)

# ---------------------------------------------------------------- 옆면(left)
HEAD_L = g(
    "....ooooo....",
    "..oo12222oo..",
    ".o11222222oo.",
    "o112222222233o",
    "ooooo22222233o",
    "oKKKo2222233o.",
    "oKKKKo222333o.",
    "o111oo22233o..",
    ".o1122223333o.",
    "..oo223333oo..",
    "....ooooooo...",
)
EYES_L = {"L": ["oW"], "R": ["Wo"], "X": ["oo"]}
TORSO_L = g(
    "........oooo...",
    "......oo1222o..",
    "....oo11222223o",
    "...o1122222233o",
    "..o12222222233o",
    "..o122222222333o",
    "..o22222222333o",
    "..o55o5442333o.",
    "..oKko64445oo..",
    "...ooo66G66o...",
    "....o544445o...",
    "....oooooooo...",
)
LEG_L_NEAR = g(
    ".o77o",
    ".o77o",
    ".o77o",
    ".o78o",
    ".o66o",
    "o5566o",
    "oooooo",
)
LEG_L_FAR = g(
    ".o88o",
    ".o88o",
    ".o88o",
    ".o88o",
    ".o66o",
    "o6666o",
    "oooooo",
)
W_IDLE_L = g(
    "......o..",
    ".ooooooGoo",
    "oBBBbboGKko",
    ".ooooooGoko",
    "......o.oo",
)
W_RAISE_L = g(
    "oo...",
    "oBo..",
    "obBo.",
    ".obBo",
    "..oGGo",
    "..oKko",
    "..o55o",
    "..o55o",
)
W_STAB_L = g(
    "........o....",
    ".oooooooGoooo",
    "oBBBBbbbGKk55o",
    ".oooooooGoooo",
    "........o....",
)


def arc_points(cx, cy, r, a0, a1, step=2.0):
    pts = []
    a = a0
    while a <= a1:
        x = round(cx + r * math.cos(math.radians(a)))
        y = round(cy + r * math.sin(math.radians(a)))
        if (x, y) not in pts:
            pts.append((x, y))
        a += step
    return pts


def fx_arc(cx, cy, r, a0, a1, fade=False):
    """휘두름 궤적. 바깥 W, 안쪽 B. fade면 바깥 한 줄만."""
    px = {}
    for x, y in arc_points(cx, cy, r - 1, a0 + 15, a1):
        if not fade:
            px[(x, y)] = "B"
    for x, y in arc_points(cx, cy, r, a0, a1):
        px[(x, y)] = "W" if not fade else "B"
    return px


def fx_thrust(fade=False):
    """옆면 찌르기 잔상 선 (left 기준)."""
    px = {}
    rows = [(19, 4, 8), (23, 5, 8)] if not fade else [(19, 6, 8)]
    for y, x0, x1 in rows:
        for x in range(x0, x1 + 1):
            px[(x, y)] = "W" if (x - x0) > 1 else "B"
    return px


def fx_dust():
    return {(8, 29): "W", (9, 28): "B", (23, 29): "W", (22, 28): "B",
            (6, 28): "B", (25, 28): "B"}


# ---------------------------------------------------------------- 캔버스
def blank():
    return [["."] * W for _ in range(H)]


def put(cv, grid, x, y):
    for j, row in enumerate(grid):
        for i, c in enumerate(row):
            if c == ".":
                continue
            X, Y = x + i, y + j
            if 0 <= X < W and 0 <= Y < H:
                cv[Y][X] = c


def put_px(cv, px):
    for (x, y), c in px.items():
        if 0 <= x < W and 0 <= y < H:
            cv[y][x] = c


def flip_cv(cv):
    return [list(reversed(r)) for r in cv]


def relight_flipped(cv):
    """좌우 반전한 옆면: 반전으로 광원이 우상단으로 간 것을 되돌린다.
    옆면 음영은 앞(밝음)·뒤(어두움)로 나뉘므로 램프의 밝음과 어두움을 맞바꾼다."""
    swap = {}
    for lt, md, dk in RAMPS:
        swap[lt], swap[dk] = dk, lt
    return [[swap.get(c, c) for c in r] for r in cv]


def fill_holes(L, layer):
    """회전한 몸 안에 갇힌 투명 구멍(캔버스 가장자리에서 닿지 않는 곳)을 외곽선색으로 메운다."""
    solid = lambda x, y: any(L[k][y][x] != "." for k in L)
    seen = set()
    stack = [(x, y) for x in range(W) for y in (0, H - 1)] + [(x, y) for y in range(H) for x in (0, W - 1)]
    while stack:
        x, y = stack.pop()
        if not (0 <= x < W and 0 <= y < H) or (x, y) in seen or solid(x, y):
            continue
        seen.add((x, y))
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    for y in range(H):
        for x in range(W):
            if (x, y) not in seen and not solid(x, y):
                L[layer][y][x] = "o"


def rotate_cw(cv):
    return [[cv[H - 1 - x][y] for x in range(W)] for y in range(H)]


def bbox(cvs):
    xs, ys = [], []
    for cv in cvs:
        for y in range(H):
            for x in range(W):
                if cv[y][x] != ".":
                    xs.append(x)
                    ys.append(y)
    return min(xs), min(ys), max(xs), max(ys)


def shift(cv, dx, dy):
    out = blank()
    for y in range(H):
        for x in range(W):
            if cv[y][x] != "." and 0 <= x + dx < W and 0 <= y + dy < H:
                out[y + dy][x + dx] = cv[y][x]
    return out


# ---------------------------------------------------------------- 프레임 조합
BASE = {
    # 상체 기준: torso, head 위치 / 다리 위치
    "down": dict(torso=(7, 16), head=(9, 7), eyes=(13, 13), legs=[(11, 23), (16, 23)]),
    "up": dict(torso=(7, 12), head=(9, 7), legs=[(11, 23), (16, 23)]),
    "left": dict(torso=(9, 14), head=(6, 8), eyes=(7, 14), legs=[(14, 23), (17, 23)]),
}
WEAPON = {
    "down": {"idle": (W_IDLE_D, 4, 21), "raise": (W_RAISE_D, 3, 9), "stab": (W_STAB_D, 9, 19)},
    "up": {"idle": (mirror(W_IDLE_D), 21, 21), "raise": (mirror(W_RAISE_D), 22, 9),
           "stab": (vflip(W_STAB_D), 11, 3)},
    "left": {"idle": (W_IDLE_L, 3, 20), "raise": (W_RAISE_L, 18, 8), "stab": (W_STAB_L, 2, 19)},
}


def compose(d, f):
    """d: down/up/left, f: 프레임 스펙 dict -> {layer: canvas}"""
    L = {k: blank() for k in LAYERS}
    b = BASE[d]
    bdx, bdy = f.get("body", (0, 0))
    hdy = f.get("head_dy", 0)
    # 다리
    lo = f.get("legs", [(0, 0), (0, 0)])
    if d == "left":
        (fx_, fy_), (nx, ny) = b["legs"][1], b["legs"][0]
        put(L["legs"], LEG_L_FAR, fx_ + lo[1][0], fy_ + lo[1][1])
        put(L["legs"], LEG_L_NEAR, nx + lo[0][0], ny + lo[0][1])
    else:
        (ax, ay), (cx, cy) = b["legs"]
        put(L["legs"], LEG_D, ax + lo[0][0], ay + lo[0][1])
        put(L["legs"], mirror(LEG_D), cx + lo[1][0], cy + lo[1][1])
    # 몸통·머리
    torso = {"down": TORSO_D, "up": TORSO_U, "left": TORSO_L}[d]
    head = {"down": HEAD_D, "up": HEAD_U, "left": HEAD_L}[d]
    put(L["body"], torso, b["torso"][0] + bdx, b["torso"][1] + bdy)
    hx, hy = b["head"][0] + bdx, b["head"][1] + bdy + hdy
    head_layer = "head_back" if d == "up" else "head"
    put(L[head_layer], head, hx, hy)
    # 눈 (뒷면은 두건 솔기로 시선 방향을 표시)
    look = f.get("look", "L")
    if d == "down":
        put(L["eyes"], EYES_D[look], b["eyes"][0] + bdx, b["eyes"][1] + bdy + hdy)
    elif d == "left":
        put(L["eyes"], EYES_L[look], b["eyes"][0] + bdx, b["eyes"][1] + bdy + hdy)
    else:
        sx = {"L": 15, "R": 17, "X": 16}[look]
        put(L["head_back"], ["3", "3", "3", "3"], hx + sx - 9, hy + 2)
    # 무기
    wp = f.get("weapon", "idle")
    if wp == "drop":
        put(L["weapon_front"], W_DROP, 3, 26)
    elif wp:
        grid, wx, wy = WEAPON[d][wp]
        wdx, wdy = f.get("wofs", (0, 0))
        # left: 단검 든 오른손이 먼 쪽 팔. up 휘두르기: 단검이 몸 앞(화면 안쪽)이라 머리·등에 가려진다
        behind = d == "left" or (d == "up" and wp == "stab")
        layer = "weapon_back" if behind else "weapon_front"
        put(L[layer], grid, wx + bdx + wdx, wy + bdy + wdy)
    # 이펙트 (up은 궤적도 몸 앞이므로 머리·몸과 겹치는 픽셀을 뺀다)
    fx = f.get("fx")
    if fx:
        if d == "up":
            fx = {(x, y): c for (x, y), c in fx.items()
                  if not (0 <= x < W and 0 <= y < H and (L["head_back"][y][x] != "." or L["body"][y][x] != "."))}
        put_px(L["fx"], fx)
    # 넉백
    kx, ky = f.get("knock", (0, 0))
    if kx or ky:
        for k in LAYERS:
            if k != "fx":
                L[k] = shift(L[k], kx, ky)
    if f.get("flash"):
        for k in LAYERS:
            if k == "fx":
                continue
            L[k] = [[("W" if c not in (".", "o") else c) for c in r] for r in L[k]]
    return L


def to_right(L):
    out = {}
    for k in LAYERS:
        cv = flip_cv(L[k])
        if k in ("body", "head", "head_back", "legs"):
            cv = relight_flipped(cv)
        out[k] = cv
    # 오른쪽을 볼 때 단검을 든 오른손이 앞쪽 팔이 된다
    out["weapon_front"], out["weapon_back"] = out["weapon_back"], blank()
    return out


# ---------------------------------------------------------------- 애니메이션 정의
def anim_specs(d):
    back = {"down": (0, -1), "up": (0, 1), "left": (1, 0)}[d]
    if d == "down":
        slash, slash2 = fx_arc(16, 18, 11, 95, 215), fx_arc(16, 18, 11, 95, 140, fade=True)
    elif d == "up":
        slash, slash2 = fx_arc(16, 14, 11, 200, 340), fx_arc(16, 14, 11, 200, 260, fade=True)
    else:
        slash, slash2 = fx_thrust(), fx_thrust(fade=True)
    lunge = (-1, 0) if d == "left" else (0, 1) if d == "down" else (0, -1)
    if d == "left":
        walk = [
            dict(legs=[(-2, 0), (2, 0)], body=(0, 0), wofs=(0, 0)),
            dict(legs=[(0, -1), (0, 0)], body=(0, -1), wofs=(1, 0)),
            dict(legs=[(2, 0), (-2, 0)], body=(0, 0), wofs=(0, 0)),
            dict(legs=[(0, 0), (0, -1)], body=(0, -1), wofs=(-1, 0)),
        ]
    else:
        walk = [
            dict(legs=[(0, -1), (0, 0)], body=(0, 0), wofs=(0, 1)),
            dict(legs=[(0, 0), (0, 0)], body=(0, -1), wofs=(0, 0)),
            dict(legs=[(0, 0), (0, -1)], body=(0, 0), wofs=(0, -1)),
            dict(legs=[(0, 0), (0, 0)], body=(0, -1), wofs=(0, -2)),
        ]
    return [
        ("idle", [220, 180, 220, 180], [
            dict(body=(0, 0), look="L"),
            dict(body=(0, 1), look="L"),
            dict(body=(0, 1), look="R"),
            dict(body=(0, 0), look="R"),
        ]),
        ("move", [130, 110, 130, 110], [dict(w, look="L") for w in walk]),
        ("attack", [180, 140, 80, 100, 130], [
            dict(body=(0, 1), weapon="raise", look="L"),
            dict(body=(0, 1), weapon="raise", wofs=(1 if d == "left" else 0, -1), look="L"),
            dict(body=lunge, weapon="stab", fx=slash, look="L"),
            dict(body=lunge, weapon="stab", fx=slash2, look="L"),
            dict(body=(0, 0), weapon="idle", wofs=(0, 1) if d != "left" else (1, 0), look="L"),
        ]),
        ("damage", [80, 100, 110], [
            dict(knock=back, flash=True, look="X"),
            dict(knock=back, body=(0, 1), look="X"),
            dict(body=(0, 0), look="L"),
        ]),
    ]


def die_frames():
    base = [
        dict(knock=(0, -1), look="X"),
        dict(body=(0, 2), legs=[(0, 1), (0, 1)], wofs=(0, 1), look="X"),
        dict(body=(0, 4), head_dy=1, legs=[(0, 3), (0, 3)], weapon="drop", look="X"),
    ]
    frames = [compose("down", f) for f in base]
    kneel = frames[2]
    # 쓰러짐: 무릎 꿇은 자세를 시계 방향 90도 회전 (무기 레이어는 바닥에 그대로)
    rot = {k: rotate_cw(kneel[k]) for k in LAYERS}
    x0, y0, x1, y1 = bbox([rot[k] for k in ("legs", "body", "head")])
    cx = (W - (x1 - x0 + 1)) // 2 - x0
    for up, dust in ((3, False), (0, True), (0, False)):
        F = {}
        for k in LAYERS:
            if k in ("weapon_front", "fx"):
                F[k] = blank()
            else:
                F[k] = shift(rot[k], cx, 29 - y1 - up)
        F["weapon_front"] = kneel["weapon_front"]
        fill_holes(F, "legs")
        if dust:
            put_px(F["fx"], fx_dust())
        frames.append(F)
    return frames, [120, 150, 180, 120, 160, 600]


def build():
    frames, durs, tags = [], [], []
    for dname in ("idle", "move", "attack", "damage"):
        for d in ("down", "up", "left", "right"):
            src = "left" if d == "right" else d
            name, dur, specs = next(a for a in anim_specs(src) if a[0] == dname)
            start = len(frames)
            for f in specs:
                L = compose(src, f)
                frames.append(to_right(L) if d == "right" else L)
            durs += dur
            tags.append((f"{dname}_{d}", start, len(frames) - 1))
    df, dd = die_frames()
    start = len(frames)
    frames += df
    durs += dd
    tags.append(("die", start, len(frames) - 1))
    return frames, durs, tags


def rgb(c):
    h = PAL[c].lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def cv_img(cv):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for y in range(H):
        for x in range(W):
            if cv[y][x] != ".":
                im.putpixel((x, y), rgb(cv[y][x]))
    return im


def flatten(L):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for k in LAYERS:
        im.alpha_composite(cv_img(L[k]))
    return im


def main():
    out = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "."
    frames, durs, tags = build()
    os.makedirs(out, exist_ok=True)
    n = len(frames)
    for k in LAYERS:
        strip = Image.new("RGBA", (W * n, H), (0, 0, 0, 0))
        for i, L in enumerate(frames):
            strip.paste(cv_img(L[k]), (i * W, 0))
        strip.save(os.path.join(out, f"strip_{k}.png"))
    meta = dict(frames=n, durations=durs, layers=LAYERS,
                tags=[dict(name=t, from_=a, to=b) for t, a, b in tags])
    with open(os.path.join(out, "meta.json"), "w") as fp:
        json.dump(meta, fp, indent=1)
    with open(os.path.join(out, "meta.lua"), "w") as fp:
        fp.write("return {\n frames=%d,\n durations={%s},\n layers={%s},\n palette={%s},\n tags={\n" % (
            n, ",".join(map(str, durs)), ",".join('"%s"' % k for k in LAYERS),
            ",".join('"%s"' % v for v in PAL.values())))
        for t, a, b in tags:
            fp.write('  {name="%s", from=%d, to=%d},\n' % (t, a + 1, b + 1))
        fp.write(" }\n}\n")
    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        cols, s = 8, 5
        rows = []
        for t, a, b in tags:
            rows.append((t, list(range(a, b + 1))))
        sheet = Image.new("RGBA", (cols * (W + 2) * s, len(rows) * (H + 2) * s), (120, 128, 120, 255))
        for r, (t, idx) in enumerate(rows):
            for c, i in enumerate(idx):
                im = flatten(frames[i]).resize((W * s, H * s), Image.NEAREST)
                sheet.alpha_composite(im, (c * (W + 2) * s, r * (H + 2) * s))
        sheet.save(path)
    print(n, "frames", [t[0] for t in tags])


if __name__ == "__main__":
    main()
