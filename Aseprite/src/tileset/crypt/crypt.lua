-- crypt 테마: 어두운 회갈색 벽돌 + 황갈색 캡, 정사각 판석 바닥, 적갈색 판자문 + 청회색 철물
local PATH = "E:/TheDungeon.0.2/Aseprite/src/tileset/crypt/crypt.aseprite"
local spr = app.open(PATH)
local img = Image(spr.width, spr.height, ColorMode.RGB)
local pc = app.pixelColor

local function C(hex)
  return pc.rgba(tonumber(hex:sub(2,3),16), tonumber(hex:sub(4,5),16), tonumber(hex:sub(6,7),16), 255)
end

local function hash(...)
  local h = 2166136261
  for _, v in ipairs({...}) do h = ((h ~ (v & 0xffffffff)) * 16777619) & 0xffffffff end
  h = h ~ (h >> 13); h = (h * 0x5bd1e995) & 0xffffffff; h = h ~ (h >> 15)
  return h
end
local function rnd(...) return (hash(...) % 10000) / 10000 end

-- 팔레트
local M0, M1 = C"#231b1a", C"#2e2321"                         -- 줄눈
local BRICK = {                                                -- {hl, base, dk}
  {C"#7a675d", C"#63524b", C"#514240"},
  {C"#6f5d55", C"#5a4a44", C"#4a3d39"},
  {C"#847066", C"#6b5951", C"#574842"},
}
local CAP_HL, CAP_MID, CAP_DK, CAP_SPK = C"#cbb8a2", C"#ad9884", C"#8a7666", C"#9c8774"
local P_HL, P_BASE, P_DK = C"#6b5a52", C"#54453f", C"#453934"  -- 굽(기단)
local F_HL, F_BASE, F_DK, GROUT = C"#5a4a44", C"#4b3d39", C"#403431", C"#2b211f"
local F_ALT = {C"#4b3d39", C"#504140", C"#463935", C"#4b3d39"}
local CRACK = C"#261c1a"
local FF_BASE, FF_DK = C"#3e322f", C"#372c29"
local W_HL, W_BASE, W_DK, W_SEAM = C"#b5543a", C"#93402c", C"#6e2c1f", C"#45190f"
local I_DK, I_MID, I_HL = C"#2a2a33", C"#4c5260", C"#8fb0b4"
local MOSS_DK, MOSS, MOSS_HL = C"#3d4a2a", C"#56653a", C"#6f7f46"
local PEB_HL, PEB, PEB_DK = C"#9c8774", C"#7a675d", C"#54453f"
local BONE_HL, BONE = C"#d8ccb4", C"#a99d88"
local DUST = {C"#2e2219", C"#34271c", C"#3a2c20", C"#433324", C"#4a3928"}   -- 따뜻한 흙색 (바닥과 색조 구분)

local slices = {}
for _, s in ipairs(spr.slices) do slices[s.name] = s.bounds end

local function px(b, x, y, c)
  if x >= 0 and y >= 0 and x < b.width and y < b.height then img:drawPixel(b.x + x, b.y + y, c) end
end

local CAP_JOINT = {9, 21, 14, 26}   -- 변형마다 캡 이음줄 자리 (앞에서 본 x)

-- ---------------------------------------------------------------- 벽
-- 단: ly0-2 캡, ly3 줄눈, ly4-23 벽돌 5단(3+1), ly24-31 굽
local function brickTone(seed, c, k, offset)
  if offset == 4 and k % 4 == 0 then return BRICK[hash(7, c) % 3 + 1] end   -- 타일 경계를 넘는 벽돌은 변형과 무관
  return BRICK[hash(seed, c, k) % 3 + 1]
end

local function wallBody(b, seed, joint)
  for ly = 0, 31 do
    for lx = 0, 31 do
      local col
      local nseed = (lx == 0 or lx == 31) and 0 or seed
      if ly <= 2 then
        col = ({CAP_HL, CAP_MID, CAP_DK})[ly + 1]
        if ly == 1 and rnd(nseed, lx, 91) < 0.12 then col = CAP_SPK end
        if lx == joint then col = (ly == 0) and CAP_DK or M1
        elseif lx == joint + 1 and ly < 2 then col = CAP_HL end
      elseif ly == 3 then
        col = M0
      elseif ly <= 23 then
        local c, r = (ly - 4) // 4, (ly - 4) % 4
        local offset = (c % 2) * 4
        local k, p = (lx + offset) // 8, (lx + offset) % 8
        if r == 3 or p == 7 then col = M1
        else
          local t = brickTone(seed, c, k, offset)
          col = t[r + 1]
          if p == 0 and r < 2 then col = t[1] end
          if p == 6 and r > 0 then col = t[3] end
          if r == 1 and p > 0 and p < 6 and rnd(nseed, lx, ly) < 0.08 then col = t[3] end
        end
      else
        local p = (lx + 4) % 16      -- 굽 이음 lx 11, 27
        if p == 15 then col = M1
        elseif ly == 24 then col = P_HL
        else
          col = P_BASE
          if p == 0 then col = P_HL end
          if rnd(nseed, lx, ly) < 0.07 then col = P_DK end
        end
      end
      px(b, lx, ly, col)
    end
  end
end

local function crack(b, pts, col)
  for _, p in ipairs(pts) do px(b, p[1], p[2], col or M0) end
end

local function wallDecor(b, v)
  if v == 2 then   -- 금
    crack(b, {{13,4},{13,5},{14,6},{14,7},{15,8},{15,9},{14,10},{15,11},{16,12},{16,13},{17,14},{17,15},{18,16},{18,17},{17,18},{18,19}})
  elseif v == 3 then   -- 빠진 벽돌 + 이끼
    -- 빠진 벽돌 홈: 윗변 그늘 → 안쪽 → 아랫변 밝은 턱
    for lx = 20, 26 do px(b, lx, 12, M0); px(b, lx, 13, C"#3a2e2b"); px(b, lx, 14, BRICK[1][1]) end
    px(b, 20, 13, M0); px(b, 26, 13, BRICK[2][3])
    for _, p in ipairs({{5,22},{6,22},{7,21},{8,22},{9,22},{6,23},{7,23},{10,23},{20,23},{21,22},{22,23}}) do
      px(b, p[1], p[2], MOSS) end
    for _, p in ipairs({{7,22},{8,23},{21,23}}) do px(b, p[1], p[2], MOSS_HL) end
    for _, p in ipairs({{5,23},{9,23},{4,23}}) do px(b, p[1], p[2], MOSS_DK) end
  elseif v == 4 then   -- 철 고리
    for lx = 15, 17 do px(b, lx, 9, I_MID); px(b, lx, 10, I_DK) end
    px(b, 16, 9, I_HL)
    local ring = {{15,11},{17,11},{14,12},{18,12},{14,13},{18,13},{14,14},{18,14},{15,15},{17,15},{16,15}}
    for _, p in ipairs(ring) do px(b, p[1], p[2], I_MID) end
    px(b, 15, 11, I_HL); px(b, 14, 12, I_HL); px(b, 18, 14, I_DK); px(b, 17, 15, I_DK)
  end
end

for v = 1, 4 do
  local j = CAP_JOINT[v]
  local front = slices["wall_" .. v .. ".front"]
  wallBody(front, 100 + v, j); wallDecor(front, v)
  local back = slices["wall_" .. v .. ".back"]
  wallBody(back, 200 + v, 31 - j)
  if v == 3 then crack(back, {{8,9},{9,10},{9,11},{10,12},{10,13},{11,14},{11,15}}) end
  -- 윗면: 맨 아래 행 = 앞면 ly0, 맨 위 행 = 뒷면 ly0
  local top = slices["wall_" .. v .. ".top"]
  for ly = 0, 7 do
    for lx = 0, 31 do
      local col = CAP_MID
      if ly == 0 or ly == 7 then col = CAP_HL
      elseif ly == 6 then col = CAP_MID
      elseif rnd((lx == 0 or lx == 31) and 0 or 300 + v, lx, ly) < 0.12 then col = CAP_SPK end
      if lx == j then col = (ly == 0 or ly == 7) and CAP_DK or M1 end
      if lx == j + 1 and ly > 0 and ly < 7 then col = CAP_HL end
      px(top, lx, ly, col)
    end
  end
  -- 끝면 8×32
  local side = slices["wall_" .. v .. ".side"]
  for ly = 0, 31 do
    for lx = 0, 7 do
      local col
      if ly <= 2 then col = ({CAP_HL, CAP_MID, CAP_DK})[ly + 1]
      elseif ly == 3 then col = M0
      elseif ly <= 23 then
        local c, r = (ly - 4) // 4, (ly - 4) % 4
        local jx = (c % 2 == 0) and 7 or 3
        if r == 3 or lx == jx then col = M1
        else
          local t = BRICK[hash(400 + v, c, lx > jx and 1 or 0) % 3 + 1]
          col = t[r + 1]
        end
      else
        col = (ly == 24) and P_HL or P_BASE
        if ly > 24 and rnd(400 + v, lx, ly) < 0.08 then col = P_DK end
      end
      px(side, lx, ly, col)
    end
  end
end

-- ---------------------------------------------------------------- 바닥
-- 판석 16×16, 줄눈 x/y = 7, 23 (32px 경계를 피함). 경계를 넘는 판석은 변형과 무관
for v = 1, 4 do
  local top = slices["floor_" .. v .. ".top"]
  for ly = 0, 31 do
    for lx = 0, 31 do
      local dx, dy = (lx - 8) % 16, (ly - 8) % 16
      local col
      if dx == 15 or dy == 15 then col = GROUT
      else
        local center = (lx >= 8 and lx <= 22 and ly >= 8 and ly <= 22)
        col = center and F_ALT[v] or F_BASE
        if dx == 0 or dy == 0 then col = F_HL end
        if dx == 14 or dy == 14 then col = F_DK end
        if lx >= 2 and lx <= 29 and ly >= 2 and ly <= 29 then
          local r = rnd(500 + v, lx, ly)
          if r < 0.05 then col = F_DK elseif r < 0.075 then col = F_HL end
        end
      end
      px(top, lx, ly, col)
    end
  end
  if v == 2 then
    crack(top, {{10,11},{11,12},{12,12},{13,13},{14,14},{15,14},{16,15},{17,16},{17,17},{18,18},{16,16}}, CRACK)
  elseif v == 3 then
    crack(top, {{25,3},{26,4},{26,5},{27,5},{28,6}}, CRACK)
    crack(top, {{4,26},{5,27},{6,27},{5,28}}, CRACK)
    px(top, 13, 18, PEB); px(top, 14, 18, PEB_HL); px(top, 13, 19, PEB_DK); px(top, 14, 19, PEB)
  elseif v == 4 then
    -- 뼛조각
    for _, p in ipairs({{11,17},{12,16},{13,15},{14,14},{15,13}}) do px(top, p[1], p[2], BONE) end
    px(top, 10, 17, BONE_HL); px(top, 11, 18, BONE_HL); px(top, 15, 12, BONE_HL); px(top, 16, 13, BONE_HL)
    px(top, 13, 14, BONE_HL)
    crack(top, {{19,9},{20,10},{20,11},{21,12}}, CRACK)
  end
  -- 앞면 32×8: 위 행이 윗면 맨 아래 행(판석 안쪽)과 이어진다. 세로 줄눈 7, 23
  local fr = slices["floor_" .. v .. ".front"]
  for ly = 0, 7 do
    for lx = 0, 31 do
      local col = FF_BASE
      if ly == 0 then col = F_BASE end
      if lx == 7 or lx == 23 then col = GROUT
      elseif (lx == 8 or lx == 24) and ly > 0 then col = F_DK
      elseif ly > 0 and rnd((lx < 2 or lx > 29) and 0 or 600 + v, lx, ly) < 0.08 then col = FF_DK end
      px(fr, lx, ly, col)
    end
  end
end

-- ---------------------------------------------------------------- 문틀 (황갈색 석재 기둥, 벽의 단·굽 행을 잇는다)
local function frameFace(b)
  for ly = 0, 31 do
    for lx = 0, 1 do
      local col
      if ly <= 2 then col = ({CAP_HL, CAP_MID, CAP_DK})[ly + 1]
      elseif ly == 3 or (ly <= 23 and (ly - 4) % 4 == 3) then col = CAP_DK
      elseif ly == 24 then col = CAP_HL
      elseif ly < 24 then col = (lx == 0) and CAP_HL or CAP_MID
      else col = (lx == 0) and CAP_MID or CAP_SPK end
      if ly > 3 and ly < 24 and (ly - 4) % 4 == 0 then col = CAP_HL end
      px(b, lx, ly, col)
    end
  end
end
frameFace(slices["door_frame.front"]); frameFace(slices["door_frame.back"])
do
  local t = slices["door_frame.top"]
  for ly = 0, 7 do for lx = 0, 1 do
    px(t, lx, ly, (ly == 0 or ly == 7) and CAP_HL or CAP_MID)
  end end
end

-- ---------------------------------------------------------------- 문짝
local STRAPS = {5, 25}   -- 철띠 행 (2px)
local function planks(b, seed)
  for ly = 0, 31 do
    for lx = 0, 27 do
      local p = lx % 4
      local col
      if p == 3 then col = W_SEAM
      elseif p == 0 then col = W_HL
      else col = W_BASE end
      if p ~= 3 and rnd(seed, lx, ly // 2) < 0.12 then col = W_DK end
      if ly == 0 then col = W_DK end
      if lx == 27 then col = W_DK end
      px(b, lx, ly, col)
    end
  end
end
local function strap(b, y, x0, x1)
  for lx = x0, x1 do px(b, lx, y, I_MID); px(b, lx, y + 1, I_DK) end
  for lx = x0 + 1, x1 - 1, 4 do px(b, lx, y, I_HL) end
end

do
  local f = slices["door_leaf.front"]
  planks(f, 700)
  for _, y in ipairs(STRAPS) do
    strap(f, y, 0, 20)
    px(f, 21, y, I_MID); px(f, 0, y - 1, I_DK); px(f, 0, y + 2, I_DK); px(f, 1, y - 1, I_MID); px(f, 1, y + 2, I_MID)
  end
  -- 고리 손잡이 (경첩 반대쪽)
  px(f, 22, 14, I_MID); px(f, 23, 14, I_HL); px(f, 22, 15, I_DK); px(f, 23, 15, I_DK)
  for _, p in ipairs({{21,16},{24,16},{21,17},{24,17},{22,18},{23,18}}) do px(f, p[1], p[2], I_MID) end
  px(f, 21, 16, I_HL); px(f, 23, 18, I_DK)

  local bk = slices["door_leaf.back"]
  planks(bk, 710)
  -- 가로 띠목 2개 + 사선 버팀목 (Z자)
  local function batten(y)
    for lx = 1, 26 do px(bk, lx, y, W_HL); px(bk, lx, y + 1, W_BASE); px(bk, lx, y + 2, W_DK) end
  end
  batten(4); batten(24)
  for i = 0, 16 do
    local ly = 23 - i
    local lx = 3 + math.floor(i * 21 / 16)
    px(bk, lx, ly, W_HL); px(bk, lx + 1, ly, W_BASE); px(bk, lx + 2, ly, W_DK)
  end
  -- 뒤에서 본 경첩 = 오른쪽
  for _, y in ipairs(STRAPS) do
    px(bk, 27, y, I_MID); px(bk, 26, y, I_MID); px(bk, 25, y, I_HL); px(bk, 27, y + 1, I_DK); px(bk, 26, y + 1, I_DK)
  end

  local t = slices["door_leaf.top"]
  for ly = 0, 6 do
    for lx = 0, 27 do
      local p = lx % 4
      local col = W_BASE
      if p == 3 or lx == 27 then col = W_SEAM
      elseif (p == 1 and ly % 2 == 1) then col = W_DK
      elseif p == 0 then col = W_HL end
      if ly == 0 or ly == 6 then col = W_DK end
      px(t, lx, ly, col)
    end
  end

  local s = slices["door_leaf.side"]
  for ly = 0, 31 do
    for lx = 0, 6 do
      local col = W_BASE
      if lx == 0 then col = W_HL elseif lx == 6 then col = W_DK
      elseif rnd(720, lx, ly // 3) < 0.15 then col = W_DK end
      if ly == 0 then col = W_DK end
      for _, y in ipairs(STRAPS) do
        if ly == y and lx <= 1 then col = I_MID end
        if ly == y + 1 and lx <= 1 then col = I_DK end
      end
      px(s, lx, ly, col)
    end
  end
end

-- ---------------------------------------------------------------- 바닥 가장자리 데칼 (위쪽 변 = 벽 쪽)
local BAYER = {{0,8,2,10},{12,4,14,6},{3,11,1,9},{15,7,13,5}}
local DENS = {1, 1, 1, 0.62, 0.34}
for v = 1, 4 do
  local b = slices["floor_edge_" .. v .. ".top"]
  for ly = 0, 4 do
    for lx = 0, 31 do
      local t = (BAYER[(ly + lx // 4) % 4 + 1][lx % 4 + 1] + 0.5) / 16
      if t < DENS[ly + 1] then px(b, lx, ly, DUST[ly + 1]) end
    end
  end
  local function put(list, col) for _, p in ipairs(list) do px(b, p[1], p[2], col) end end
  if v == 1 then   -- 자갈
    put({{6,1},{7,1},{6,2},{7,2},{19,2},{20,2},{24,1}}, PEB)
    put({{6,1},{19,2},{24,1}}, PEB_HL); put({{7,2},{20,3}}, PEB_DK)
    put({{12,3},{25,2}}, PEB_DK)
  elseif v == 2 then   -- 이끼 덩이
    put({{9,0},{10,0},{11,0},{12,0},{13,0},{14,0},{15,0},{16,0},{10,1},{11,1},{12,1},{13,1},{14,1},{15,1},{16,1},{17,1},{11,2},{12,2},{14,2},{15,2},{13,3}}, MOSS)
    put({{11,0},{13,1},{15,0},{16,1}}, MOSS_HL)
    put({{9,1},{10,2},{13,2},{16,2},{12,3},{14,3},{17,2}}, MOSS_DK)
    put({{23,0},{24,0},{24,1}}, MOSS_DK); put({{23,1}}, MOSS)
  elseif v == 3 then   -- 흙더미
    put({{14,0},{15,0},{16,0},{17,0},{18,0},{19,0},{20,0},{21,0},{15,1},{16,1},{17,1},{18,1},{19,1},{20,1},{16,2},{17,2},{18,2},{19,2},{17,3}}, P_BASE)
    put({{16,1},{17,1},{17,2},{18,0}}, P_HL); put({{20,1},{19,2},{18,3}}, P_DK)
    put({{6,2},{7,2}}, PEB); put({{6,2}}, PEB_HL)
  elseif v == 4 then   -- 뼛조각 + 작은 이끼
    put({{8,2},{9,2},{10,2},{11,2},{12,2},{13,2}}, BONE)
    put({{7,1},{7,3},{14,1},{14,3}}, BONE_HL); put({{9,2},{11,2}}, BONE_HL)
    put({{22,0},{23,0},{24,0},{25,0},{23,1},{24,1}}, MOSS); put({{24,0}}, MOSS_HL); put({{25,1},{22,1}}, MOSS_DK)
  end
end

spr:newCel(spr.layers[1], 1, img, Point(0, 0))
spr:saveAs(PATH)
print("ok")
