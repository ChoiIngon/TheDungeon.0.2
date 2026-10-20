"""거대 거미(giant_spider) 2x2 타일(64x64) 몬스터 생성기.

거미를 3D 기본 도형(타원체·구 스윕)으로 세우고, 고정 카메라(남쪽에서 40도 내려다봄)로
정투영해 픽셀 단위로 렌더한다. 방향마다 같은 모델을 돌려서 그리므로 접지선·몸 중심이
네 방향에서 같고, 광원(좌상단)은 화면 기준이라 right도 광원이 뒤집히지 않는다.
툰 음영 → 외곽선(바깥 + 깊이 차이 안쪽 선) 순으로 칠해 레이어 스트립 PNG를 만든다.
giant_spider_build.lua 가 스트립을 읽어 giant_spider_2x2t.aseprite 를 조립한다.

  python Aseprite/src/character/giant_spider/giant_spider_gen.py [out_dir] [--preview sheet.png]

기본 공격(attack) = 독 이빨 물기, skill_1 = 거미줄 뿜기(대상 발밑에 거미줄을 깔아 묶는다).
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image

W = H = 64
OX, OY = 32, 44  # 월드 원점(거미 발 중심, 지면)의 화면 위치
SCALE = 1.2  # 월드 1 = 1.2px
PITCH = math.radians(40)
CP, SP = math.cos(PITCH), math.sin(PITCH)
EX = np.array([1.0, 0.0, 0.0])
EU = np.array([0.0, CP, -SP])   # 화면 위쪽
EC = np.array([0.0, SP, CP])    # 카메라 쪽(깊이, 클수록 가깝다)
LIGHT = np.array([-0.55, 0.62, 0.56])
LIGHT /= np.linalg.norm(LIGHT)

PAL = {
    "o": "#1c1418",  # 외곽선
    "a": "#a6a4b0",  # 갑각 하이라이트
    "b": "#76747f",  # 갑각 밝음
    "c": "#4e4c58",  # 갑각 중간
    "d": "#302e38",  # 갑각 어두움
    "r": "#d0646a",  # 배 줄무늬 붉음 밝음 / 입
    "s": "#9a3640",  # 붉음 중간
    "t": "#5e1f29",  # 붉음 어두움
    "k": "#3d3644",  # 배 검은 줄 밝음
    "l": "#26212b",  # 배 검은 줄 어두움
    "f": "#e2d6c0",  # 송곳니 밝음
    "g": "#988a76",  # 송곳니 어두움
    "e": "#ffcf7a",  # 눈 하이라이트
    "E": "#e8742c",  # 눈
    "x": "#7a2a1c",  # 죽은 눈
    "p": "#b6e85a",  # 독 밝음
    "q": "#5e9e2a",  # 독 어두움
    "w": "#f0eef4",  # 거미줄 밝음 / 피격 플래시
    "v": "#aeacc2",  # 거미줄 어두움
}
LAYERS = ["body", "fx"]
MAT_CARA, MAT_ABD, MAT_MOUTH, MAT_FANG = 1, 2, 3, 4


# ---------------------------------------------------------------- 수학 도우미
def v3(x, y, z):
    return np.array([x, y, z], dtype=float)


def rot_x(a):
    """옆축(x) 회전. 양수 = 앞(+z)이 위로 들림."""
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, s], [0, -s, c]], dtype=float)


def rot_yaw(deg):
    """로컬(lx, ly, lz=앞) -> 월드. X = cos*lx + sin*lz, Z = -sin*lx + cos*lz"""
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]], dtype=float)


YAW = {"down": 0, "up": 180, "left": -90, "right": 90}


def cam(p):
    return np.array([p @ EX, p @ EU, p @ EC])


def to_px(p):
    c = cam(p)
    return OX + c[0] * SCALE, OY - c[1] * SCALE, c[2]


# ---------------------------------------------------------------- 래스터라이저
class Buf:
    def __init__(self):
        self.depth = np.full((H, W), -np.inf)
        self.mat = np.zeros((H, W), dtype=int)
        self.grp = np.zeros((H, W), dtype=int)
        self.nrm = np.zeros((3, H, W))
        self.hit = np.zeros((3, H, W))


PU = ((np.arange(W) + 0.5) - OX) / SCALE  # 화면 x -> 카메라 u
PV = (OY - (np.arange(H) + 0.5)) / SCALE  # 화면 y -> 카메라 v
UU, VV = np.meshgrid(PU, PV)


def _write(buf, sl, d, n, mat, grp):
    cur = buf.depth[sl]
    m = d > cur
    if not m.any():
        return
    cur[m] = d[m]
    buf.mat[sl][m] = mat
    buf.grp[sl][m] = grp
    for i in range(3):
        buf.nrm[i][sl][m] = n[i][m]
    u, v = UU[sl][m], VV[sl][m]
    world = np.outer(EX, u) + np.outer(EU, v) + np.outer(EC, d[m])
    for i in range(3):
        buf.hit[i][sl][m] = world[i]


def _slice(cu, cv, ex, ey):
    x0 = max(0, int(math.floor(OX + (cu - ex) * SCALE)) - 1)
    x1 = min(W, int(math.ceil(OX + (cu + ex) * SCALE)) + 2)
    y0 = max(0, int(math.floor(OY - (cv + ey) * SCALE)) - 1)
    y1 = min(H, int(math.ceil(OY - (cv - ey) * SCALE)) + 2)
    if x0 >= x1 or y0 >= y1:
        return None
    return (slice(y0, y1), slice(x0, x1))


def draw_sphere(buf, c, r, mat, grp):
    cu, cv, cd = cam(c)
    sl = _slice(cu, cv, r, r)
    if sl is None:
        return
    du, dv = UU[sl] - cu, VV[sl] - cv
    d2 = du * du + dv * dv
    inside = d2 < r * r
    h = np.sqrt(np.where(inside, r * r - d2, 0))
    d = np.where(inside, cd + h, -np.inf)
    _write(buf, sl, d, (du / r, dv / r, h / r), mat, grp)


def draw_ellipsoid(buf, c, axes, mat, grp):
    """axes: 반지름을 곱한 축 벡터 3개(월드)."""
    M = np.column_stack([cam(a) for a in axes])
    Mi = np.linalg.inv(M)
    cu, cv, cd = cam(c)
    ex = math.sqrt((M[0] ** 2).sum())
    ey = math.sqrt((M[1] ** 2).sum())
    sl = _slice(cu, cv, ex, ey)
    if sl is None:
        return
    du, dv = UU[sl] - cu, VV[sl] - cv
    A = [Mi[i, 0] * du + Mi[i, 1] * dv + Mi[i, 2] * (-cd) for i in range(3)]
    B = Mi[:, 2]
    bb = (B * B).sum()
    ab = A[0] * B[0] + A[1] * B[1] + A[2] * B[2]
    aa = A[0] ** 2 + A[1] ** 2 + A[2] ** 2
    disc = ab * ab - bb * (aa - 1)
    ok = disc > 0
    t = np.where(ok, (-ab + np.sqrt(np.where(ok, disc, 0))) / bb, -np.inf)
    s = [A[i] + np.where(ok, t, 0) * B[i] for i in range(3)]
    n = [Mi[0, i] * s[0] + Mi[1, i] * s[1] + Mi[2, i] * s[2] for i in range(3)]
    ln = np.sqrt(n[0] ** 2 + n[1] ** 2 + n[2] ** 2) + 1e-9
    _write(buf, sl, t, (n[0] / ln, n[1] / ln, n[2] / ln), mat, grp)


def draw_sweep(buf, pts, radii, mat, grp, step=0.35):
    """구 스윕으로 굵기가 변하는 다리 마디를 그린다."""
    for (p0, r0), (p1, r1) in zip(zip(pts, radii), zip(pts[1:], radii[1:])):
        n = max(2, int(np.linalg.norm(p1 - p0) / step))
        for i in range(n + 1):
            t = i / n
            draw_sphere(buf, p0 + (p1 - p0) * t, r0 + (r1 - r0) * t, mat, grp)


# ---------------------------------------------------------------- 거미 모델
BODY_PIVOT = v3(0, 8, 0)
ABD_BASE = v3(0, 9, -4.0)
ABD_ELEV = 62  # 배가 뒤쪽 위로 솟은 각도
LEG_ROOT = [(4.0, 3.2), (4.8, 1.4), (4.8, -0.6), (4.2, -2.4)]  # (|lx|, lz)
LEG_ANG = [30, 68, 110, 148]
LEG_R = [19.0, 18.5, 18.0, 18.5]


def walk_group(i, s):
    """교대 4족 보행 그룹 A/B."""
    return (i % 2 == 0) == (s > 0)


def build_model(pose, yaw):
    """pose -> (그리기 목록, 앵커). 모든 좌표는 월드."""
    Ry = rot_yaw(YAW[yaw])
    bdx, bdy, bdz = pose.get("body", (0, 0, 0))
    Rb = rot_x(math.radians(pose.get("pitch", 0)))
    boff = v3(bdx, bdy, bdz)

    def B(p):  # 몸 공간 -> 월드
        return Ry @ (Rb @ (p - BODY_PIVOT) + BODY_PIVOT + boff)

    def BV(vec):
        return Ry @ (Rb @ vec)

    def G(p):  # 지면 로컬 -> 월드 (몸 움직임과 무관)
        return Ry @ p

    prims = []
    # 배 (눈물방울: 큰 타원체 + 작은 타원체)
    e = math.radians(ABD_ELEV + pose.get("abd", 0))
    ax = v3(0, math.sin(e), -math.cos(e))
    lat = v3(1, 0, 0)
    perp = np.cross(lat, ax)
    sc = pose.get("abd_s", 1.0)
    base = ABD_BASE
    for along, rl, ra, rp in ((7.0, 9.0, 9.5, 8.3), (15.0, 6.0, 7.0, 5.6)):
        prims.append(("ell", B(base + ax * along * sc),
                      [BV(lat) * rl * sc, BV(ax) * ra * sc, BV(perp) * rp * sc], MAT_ABD, 2))
    tip = B(base + ax * 22.0 * sc)
    # 두흉부·머리
    prims.append(("ell", B(v3(0, 8, 1.0)), [BV(v3(1, 0, 0)) * 6.2, BV(v3(0, 1, 0)) * 4.2, BV(v3(0, 0, 1)) * 7.0],
                  MAT_CARA, 3))
    prims.append(("ell", B(v3(0, 8.8, 7.2)), [BV(v3(1, 0, 0)) * 4.6, BV(v3(0, 1, 0)) * 4.0, BV(v3(0, 0, 1)) * 4.0],
                  MAT_CARA, 3))
    # 협각(입)과 송곳니
    mo = pose.get("mouth", 0.0)
    spread = 1.7 + 1.3 * max(mo, 0) - 0.5 * max(-mo, 0)
    for s in (-1, 1):
        cx = s * spread
        prims.append(("ell", B(v3(cx, 6.4, 10.9)),
                      [BV(v3(1, 0, 0)) * 1.5, BV(v3(0, 1, 0.25)) * 2.4, BV(v3(0, 0, 1)) * 1.5], MAT_MOUTH, 4))
        out = 1.2 * max(mo, 0)
        f0 = v3(cx, 4.6, 11.8)
        f1 = v3(cx + s * out, 3.0 + 0.8 * max(mo, 0), 12.6)
        f2 = v3(cx - s * (0.35 - out * 0.4), 1.6 + 1.4 * max(mo, 0), 11.4 + 0.6 * max(mo, 0))
        prims.append(("sweep", [B(f0), B(f1), B(f2)], [0.85, 0.7, 0.45], MAT_FANG, 5))
    # 다리
    curl = pose.get("curl", 0.0)
    feet = pose.get("feet", {})
    splay = pose.get("splay", 0.0)
    for i, (rx, rz) in enumerate(LEG_ROOT):
        for s in (-1, 1):
            root = B(v3(s * rx, 7.6, rz))
            a = math.radians(LEG_ANG[i])
            R = LEG_R[i] + splay
            foot_l = v3(s * R * math.sin(a), 0, R * math.cos(a))
            off = feet.get((i, s), feet.get(i, (0, 0, 0)))
            foot_l = foot_l + v3(*off)
            if curl:
                tuck = Ry.T @ root + v3(s * (4.0 + 1.1 * i), 8.5 - 1.9 * i, (1.5 - i) * 2.8)
                foot_l = foot_l + (tuck - foot_l) * curl
            foot = G(foot_l)
            rl = Ry.T @ root
            hz = foot_l - rl
            hz[1] = 0
            side = v3(s, 0, 0)
            k_l = rl + hz * 0.36 + side * (5.0 * curl)
            k_l[1] = max(rl[1], foot_l[1]) + 7.0 - 3.0 * curl + pose.get("knee", 0)
            a_l = rl + hz * 0.80 + side * ((7.0 - 1.3 * i) * curl)
            a_l[1] = foot_l[1] + 5.2 + (1.5 - 1.2 * i) * curl
            knee, ankle = G(k_l), G(a_l)
            grp = 10 + i * 2 + (s > 0)
            prims.append(("sweep", [root, knee], [1.55, 1.3], MAT_CARA, grp))
            prims.append(("sph", knee, 1.55, MAT_CARA, grp))
            prims.append(("sweep", [knee, ankle], [1.25, 0.95], MAT_CARA, grp))
            prims.append(("sweep", [ankle, foot], [0.95, 0.5], MAT_CARA, grp))
    eyes = []
    for (x, y, z, big) in ((1.5, 9.8, 10.8, True), (3.4, 10.2, 9.6, True), (0.7, 11.6, 10.3, False)):
        for s in (-1, 1):
            eyes.append((B(v3(s * x, y, z)), big))
    near = -1 if yaw == "right" else 1  # 옆모습에서 카메라 쪽 옆구리 (lx 부호)
    face = dict(eye=B(v3(near * 2.6, 10.0, 10.2)), mouth=B(v3(near * 1.0, 6.0, 11.6)),
                fang=B(v3(near * 1.0, 3.2 + 1.6 * max(mo, 0), 12.4)))
    anchors = dict(face=face, side=yaw in ("left", "right"), tip=tip, mouth=B(v3(0, 3.0, 12.0)), fwd=BV(v3(0, 0, 1)), Ry=Ry, eyes=eyes,
                   target=G(v3(0, 0, {"down": 17.0, "up": 27.0}.get(yaw, 19.0))))
    return prims, anchors


def render(pose, yaw):
    prims, anc = build_model(pose, yaw)
    buf = Buf()
    for p in prims:
        if p[0] == "ell":
            draw_ellipsoid(buf, p[1], p[2], p[3], p[4])
        elif p[0] == "sph":
            draw_sphere(buf, p[1], p[2], p[3], p[4])
        else:
            draw_sweep(buf, p[1], p[2], p[3], p[4])
    return buf, anc


# ---------------------------------------------------------------- 음영·외곽선
def shade(buf, anc, pose):
    cv = [["."] * W for _ in range(H)]
    inten = LIGHT[0] * buf.nrm[0] + LIGHT[1] * buf.nrm[1] + LIGHT[2] * buf.nrm[2]
    # 배 줄무늬 좌표: 배 축 방향 거리 + 지그재그
    Ry = anc["Ry"]
    e = math.radians(ABD_ELEV + pose.get("abd", 0))
    Rb = rot_x(math.radians(pose.get("pitch", 0)))
    ax_w = Ry @ (Rb @ v3(0, math.sin(e), -math.cos(e)))
    lat_w = Ry @ (Rb @ v3(1, 0, 0))
    base_w = Ry @ (Rb @ (ABD_BASE - BODY_PIVOT) + BODY_PIVOT + v3(*pose.get("body", (0, 0, 0))))
    sc = pose.get("abd_s", 1.0)
    for y in range(H):
        for x in range(W):
            m = buf.mat[y, x]
            if not m:
                continue
            I = inten[y, x]
            if m == MAT_CARA:
                c = "a" if I > 0.86 else "b" if I > 0.52 else "c" if I > 0.0 else "d"
            elif m == MAT_ABD:
                h = buf.hit[:, y, x] - base_w
                t = (h @ ax_w) / sc
                ang = math.atan2(h @ lat_w, h @ np.cross(lat_w, ax_w))
                zig = 2.4 * abs(ang) / math.pi + 0.5 * abs(((ang * 4 / math.pi) % 2) - 1)
                band = int(math.floor((t + zig) / 3.3))
                red = band % 2 == 1 and 2 <= t <= 21
                if red:
                    c = "r" if I > 0.62 else "s" if I > 0.12 else "t"
                else:
                    c = "k" if I > 0.40 else "l"
            elif m == MAT_MOUTH:
                c = "r" if I > 0.45 else "s" if I > 0.0 else "t"
            else:
                c = "f" if I > 0.25 else "g"
            cv[y][x] = c
    # 눈 (보이는 쪽만, 깊이 검사)
    dead = pose.get("dead_eyes", False)
    for p, big in anc["eyes"]:
        px, py, d = to_px(p)
        ix, iy = int(px), int(py)
        if not (0 <= ix < W and 0 <= iy < H) or buf.mat[iy, ix] != MAT_CARA:
            continue
        if d < buf.depth[iy, ix] - 1.2:
            continue
        if big:
            for (dx, dy) in ((0, 0), (1, 0), (0, 1), (1, 1)):
                if 0 <= ix + dx < W and 0 <= iy + dy < H and buf.mat[iy + dy, ix + dx] == MAT_CARA:
                    cv[iy + dy][ix + dx] = "x" if dead else ("e" if (dx, dy) == (0, 0) else "E")
        else:
            cv[iy][ix] = "x" if dead else "E"
    # 옆모습 얼굴 덧그리기: 가까운 앞다리가 얼굴을 가려도 눈·입·송곳니가 읽히게 한다
    if anc["side"]:
        fc = anc["face"]
        ex, ey, _ = to_px(fc["eye"])
        ex, ey = int(ex), int(ey)
        dead = pose.get("dead_eyes", False)
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            cv[ey + dy][ex + dx] = "x" if dead else ("e" if (dx, dy) == (0, 0) else "E")
        mx, my, _ = to_px(fc["mouth"])
        fx_, fy_, _ = to_px(fc["fang"])
        mx, my, fx_, fy_ = int(mx), int(my), int(fx_), int(fy_)
        for yy in range(my, fy_):
            cv[yy][mx] = "r" if yy == my else "s"
            buf.mat[yy, mx] = MAT_MOUTH
        for yy in (fy_, fy_ + 1):
            cv[yy][fx_] = "f" if yy == fy_ else "g"
            buf.mat[yy, fx_] = MAT_FANG
            buf.grp[yy, fx_] = 5
            buf.depth[yy, fx_] = 1e9
        for yy in range(my, fy_):
            buf.grp[yy, mx] = 3
            buf.depth[yy, mx] = 1e9
    # 안쪽 선: 이웃이 훨씬 가까운(앞에 있는) 다른 파츠면 뒤쪽 픽셀을 외곽선으로
    out = [r[:] for r in cv]
    for y in range(H):
        for x in range(W):
            if not buf.mat[y, x]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < W and 0 <= ny < H and buf.mat[ny, nx] and buf.grp[ny, nx] != buf.grp[y, x] \
                        and buf.depth[ny, nx] - buf.depth[y, x] > 2.2:
                    out[y][x] = "o"
                    break
    # 바깥 외곽선
    for y in range(H):
        for x in range(W):
            if cv[y][x] != ".":
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < W and 0 <= ny < H and cv[ny][nx] != ".":
                    out[y][x] = "o"
                    break
    # 1x에서 점처럼 떨어진 작은 조각(가려진 다리 끝 등)은 지운다
    seen = set()
    for y in range(H):
        for x in range(W):
            if out[y][x] == "." or (x, y) in seen:
                continue
            comp, st = [], [(x, y)]
            seen.add((x, y))
            while st:
                cx, cy = st.pop()
                comp.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < W and 0 <= ny < H and out[ny][nx] != "." and (nx, ny) not in seen:
                            seen.add((nx, ny))
                            st.append((nx, ny))
            if len(comp) < 10:
                for cx, cy in comp:
                    out[cy][cx] = "."
    if pose.get("flash"):
        out = [[("." if c == "." else "o" if c == "o" else "w" if inten[y, x] > 0.15 else "v")
                for x, c in enumerate(r)] for y, r in enumerate(out)]
    return out


# ---------------------------------------------------------------- 이펙트
def blank():
    return [["."] * W for _ in range(H)]


def fx_put(fx, buf, p, c, bias=0.6):
    px, py, d = to_px(p)
    ix, iy = int(math.floor(px)), int(math.floor(py))
    if 0 <= ix < W and 0 <= iy < H and d >= buf.depth[iy, ix] - bias:
        fx[iy][ix] = c


def fx_line(fx, buf, p0, p1, c, dash=0, sag=0.0):
    n = max(2, int(np.linalg.norm(p1 - p0) * 2.2))
    for i in range(n + 1):
        t = i / n
        if dash and (i // dash) % 2:
            continue
        p = p0 + (p1 - p0) * t
        p = p + v3(0, -sag * 4 * t * (1 - t), 0)
        fx_put(fx, buf, p, c)


def fx_venom(fx, buf, anc, stage):
    m, f = anc["mouth"], anc["fwd"]
    if stage == 1:  # 물기 순간: 독이 앞으로 튄다
        for k, (a, r, c) in enumerate(((-0.9, 2.5, "p"), (-0.3, 3.4, "p"), (0.3, 3.1, "q"), (0.9, 2.4, "p"),
                                       (0.0, 4.4, "q"), (-0.6, 4.0, "q"), (0.6, 4.2, "p"), (-1.2, 3.6, "q"),
                                       (1.2, 3.8, "q"), (-0.2, 5.6, "p"), (0.4, 5.2, "q"), (0.0, 2.2, "p"),
                                       (-0.5, 2.0, "p"), (0.5, 2.0, "p"))):
            side = anc["Ry"] @ v3(1, 0, 0)
            fx_put(fx, buf, m + f * (r * math.cos(a)) + side * (r * math.sin(a)) + v3(0, 0.6 * (k % 2), 0), c, 3)
    else:  # 독이 송곳니에서 떨어진다
        side = anc["Ry"] @ v3(1, 0, 0)
        for sx in (-1.2, 1.2):
            for dy, c in ((-0.6, "p"), (-1.4, "p"), (-2.4, "q"), (-3.4, "q")):
                fx_put(fx, buf, m + side * sx + f * 0.8 + v3(0, dy, 0), c, 3)
        for p, c in ((v3(0, -2.6, 2.5), "p"), (v3(0.8, -2.8, 3.4), "q"), (v3(-1.6, -2.8, 3.0), "q")):
            fx_put(fx, buf, m + f * p[2] + side * p[0] + v3(0, p[1], 0), c, 3)


def fx_web(fx, buf, anc, strand, net, faded=False):
    tip, tgt = anc["tip"], anc["target"] + v3(0, 0.3, 0)
    if strand > 0:
        end = tip + (tgt - tip) * strand
        side = anc["Ry"] @ v3(1, 0, 0)
        if faded:
            fx_line(fx, buf, tip, end, "v", dash=2, sag=1.0)
        else:
            fx_line(fx, buf, tip, end, "w", sag=1.0)
            fx_line(fx, buf, tip + v3(0, 0.8, 0), end + v3(0, 0.8, 0), "w", sag=1.0)
            fx_line(fx, buf, tip + side * 0.7, end + side * 1.8, "v", sag=1.6)
            fx_line(fx, buf, tip - side * 0.7, end - side * 1.8, "v", sag=1.6)
    if net > 0:
        Ry = anc["Ry"]
        R = 5.6 * net
        spokes = [tgt + Ry @ v3(R * math.cos(a), 0, R * math.sin(a))
                  for a in [i * math.pi / 4 + 0.2 for i in range(8)]]
        for sp in spokes:
            fx_line(fx, buf, tgt, sp, "v" if faded else "w", dash=2 if faded else 0)
        if net >= 0.5:
            for ring in (0.5, 1.0):
                pts = [tgt + (sp - tgt) * ring for sp in spokes]
                for a, b in zip(pts, pts[1:] + pts[:1]):
                    fx_line(fx, buf, a, b, "v", dash=2 if faded else 0)


# ---------------------------------------------------------------- 애니메이션 정의
def walk_feet(k):
    seq = [(2.2, 0), (0, 0), (-2.2, 0), (-2.2, 2.5), (0, 3.2), (2.2, 2.5)]
    feet = {}
    for i in range(4):
        for s in (-1, 1):
            z, y = seq[(k + (0 if walk_group(i, s) else 3)) % 6]
            feet[(i, s)] = (0, y, z)
    return feet


def front_feet(off, inward=0.0):
    """앞다리 한 쌍의 발 오프셋. inward: 몸 가운데 쪽으로 모으는 양"""
    return {(0, -1): (off[0] + inward, off[1], off[2]), (0, 1): (off[0] - inward, off[1], off[2])}


def anim_specs():
    walk_bob = [0, 0.8, 0, 0, 0.8, 0]
    return [
        ("idle", [240, 200, 240, 200], [
            dict(),
            dict(mouth=0.35),
            dict(body=(0, -0.9, 0), mouth=0.35),
            dict(body=(0, -0.9, 0)),
        ]),
        ("move", [120, 100, 120, 120, 100, 120],
         [dict(body=(0, walk_bob[k], 0), feet=walk_feet(k), pitch=0.0) for k in range(6)]),
        ("attack", [180, 150, 80, 90, 120, 140], [
            dict(body=(0, 1.5, -1.0), pitch=16, mouth=1.0, feet=front_feet((0, 11, 1), 4.0), knee=2),
            dict(body=(0, 2.2, -1.6), pitch=22, mouth=1.0, feet=front_feet((0, 14, 2), 5.5), knee=3),
            dict(body=(0, -1.0, 4.0), pitch=-12, mouth=-1.0, feet=front_feet((0, 0, 5)), fx=("venom", 1)),
            dict(body=(0, -1.0, 4.0), pitch=-10, mouth=-1.0, feet=front_feet((0, 0, 5)), fx=("venom", 2)),
            dict(body=(0, 0, 1.5), pitch=-3, mouth=0.4, feet=front_feet((0, 1.5, 2))),
            dict(body=(0, 0, 0.5), mouth=0.1),
        ]),
        ("damage", [80, 100, 110], [
            dict(body=(0, 0.5, -1.5), pitch=6, flash=True, mouth=0.6),
            dict(body=(0, -0.5, -2.2), pitch=8, mouth=0.6),
            dict(body=(0, 0, -0.6), pitch=2),
        ]),
        ("skill_1", [220, 180, 90, 90, 100, 120, 140, 160], [
            dict(body=(0, -0.3, 0), pitch=-1, abd=7),
            dict(body=(0, -1.0, -1.0), pitch=-8, abd=85, feet=front_feet((0, 0, 1.5))),
            dict(body=(0, -1.0, -1.0), pitch=-8, abd=95, feet=front_feet((0, 0, 1.5)), fx=("web", 0.5, 0, False)),
            dict(body=(0, -1.0, -1.0), pitch=-9, abd=99, abd_s=1.04, feet=front_feet((0, 0, 1.5)), fx=("web", 1.0, 0.5, False)),
            dict(body=(0, -1.0, -1.0), pitch=-7, abd=90, feet=front_feet((0, 0, 1.5)), fx=("web", 1.0, 1.0, False)),
            dict(body=(0, -1.0, -0.5), pitch=-5, abd=75, fx=("web", 1.0, 1.0, True)),
            dict(body=(0, -0.5, 0), pitch=-3, abd=50, fx=("web", 0, 1.0, True)),
            dict(body=(0, -0.3, 0), pitch=-1, abd=20),
        ]),
    ]


DIE = ([90, 120, 150, 150, 180, 140, 600], [
    dict(body=(0, 1.0, -0.5), pitch=10, mouth=1.0, splay=1.5, flash=True),
    dict(body=(0, -1.0, 0), pitch=4, mouth=0.8, curl=0.2, abd=-20),
    dict(body=(0, -2.5, 0), mouth=0.5, curl=0.35, abd=-45),
    dict(body=(0, -3.5, 0), mouth=0.3, curl=0.5, abd=-62),
    dict(body=(0, -4.0, 0), pitch=-3, curl=0.6, abd=-72, dead_eyes=True),
    dict(body=(0, -4.0, 0), pitch=-3, curl=0.66, abd=-76, dead_eyes=True, feet={(0, -1): (0, 3, 3), (1, 1): (0, 2, 1)}),
    dict(body=(0, -4.2, 0), pitch=-4, curl=0.68, abd=-78, dead_eyes=True),
])


def make_frame(pose, d):
    buf, anc = render(pose, d)
    body = shade(buf, anc, pose)
    fx = blank()
    spec = pose.get("fx")
    if spec and spec[0] == "venom":
        fx_venom(fx, buf, anc, spec[1])
    elif spec and spec[0] == "web":
        fx_web(fx, buf, anc, spec[1], spec[2], spec[3])
    return {"body": body, "fx": fx}


def build():
    frames, durs, tags = [], [], []
    specs = anim_specs()
    for name, dur, poses in specs:
        for d in ("down", "up", "left", "right"):
            start = len(frames)
            for p in poses:
                frames.append(make_frame(p, d))
            durs += dur
            tags.append((f"{name}_{d}", start, len(frames) - 1))
    start = len(frames)
    for p in DIE[1]:
        frames.append(make_frame(p, "down"))
    durs += DIE[0]
    tags.append(("die", start, len(frames) - 1))
    return frames, durs, tags


# ---------------------------------------------------------------- 출력
def rgb(c):
    h = PAL[c].lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def cv_img(cv):
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = im.load()
    for y in range(H):
        for x in range(W):
            if cv[y][x] != ".":
                px[x, y] = rgb(cv[y][x])
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
    edge = []
    for i, L in enumerate(frames):
        for k in LAYERS:
            cv = L[k]
            if any(cv[0][x] != "." or cv[H - 1][x] != "." for x in range(W)) or \
                    any(cv[y][0] != "." or cv[y][W - 1] != "." for y in range(H)):
                edge.append((i + 1, k))
    for k in LAYERS:
        strip = Image.new("RGBA", (W * n, H), (0, 0, 0, 0))
        for i, L in enumerate(frames):
            strip.paste(cv_img(L[k]), (i * W, 0))
        strip.save(os.path.join(out, f"strip_{k}.png"))
    with open(os.path.join(out, "meta.json"), "w") as fp:
        json.dump(dict(frames=n, durations=durs, layers=LAYERS,
                       tags=[dict(name=t, start=a + 1, end=b + 1) for t, a, b in tags]), fp, indent=1)
    with open(os.path.join(out, "meta.lua"), "w") as fp:
        fp.write("return {\n size=%d,\n frames=%d,\n durations={%s},\n layers={%s},\n palette={%s},\n tags={\n" % (
            W, n, ",".join(map(str, durs)), ",".join('"%s"' % k for k in LAYERS),
            ",".join('"%s"' % v for v in PAL.values())))
        for t, a, b in tags:
            fp.write('  {name="%s", from=%d, to=%d},\n' % (t, a + 1, b + 1))
        fp.write(" }\n}\n")
    if "--preview" in sys.argv:
        path = sys.argv[sys.argv.index("--preview") + 1]
        cols, s = 8, 3
        sheet = Image.new("RGBA", (cols * (W + 2) * s, len(tags) * (H + 2) * s), (110, 118, 110, 255))
        for r, (t, a, b) in enumerate(tags):
            for c, i in enumerate(range(a, b + 1)):
                im = flatten(frames[i]).resize((W * s, H * s), Image.NEAREST)
                sheet.alpha_composite(im, (c * (W + 2) * s, r * (H + 2) * s))
        sheet.save(path)
    print(n, "frames,", len(tags), "tags:", [t[0] for t in tags])
    print("edge-touching frames:", edge if edge else "none")


if __name__ == "__main__":
    main()
