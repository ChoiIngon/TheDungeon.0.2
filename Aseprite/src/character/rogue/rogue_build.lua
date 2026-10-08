-- rogue_gen.py 가 만든 레이어 스트립(strip_<layer>.png)과 meta.lua 로
-- rogue_1x1t.aseprite 를 조립한다. 실행 전에 STRIP_DIR, OUT_FILE 전역을 지정한다.
local ok, err = pcall(function()
  local meta = dofile(STRIP_DIR .. "/meta.lua")
  local spr = Sprite(32, 32, ColorMode.RGB)
  -- 팔레트
  local pal = Palette(#meta.palette + 1)
  pal:setColor(0, Color{r=0, g=0, b=0, a=0})
  for i, hex in ipairs(meta.palette) do
    local h = hex:gsub("#", "")
    pal:setColor(i, Color{r=tonumber(h:sub(1,2),16), g=tonumber(h:sub(3,4),16), b=tonumber(h:sub(5,6),16)})
  end
  spr:setPalette(pal)
  -- 프레임
  for i = 2, meta.frames do spr:newEmptyFrame() end
  for i = 1, meta.frames do spr.frames[i].duration = meta.durations[i] / 1000 end
  -- 레이어 (아래 -> 위)
  local first = spr.layers[1]
  for li, name in ipairs(meta.layers) do
    local layer = (li == 1) and first or spr:newLayer()
    layer.name = name
    local strip = Image{fromFile = STRIP_DIR .. "/strip_" .. name .. ".png"}
    for f = 1, meta.frames do
      local img = Image(32, 32, ColorMode.RGB)
      img:drawImage(strip, Point(-(f - 1) * 32, 0))
      if not img:isEmpty() then
        spr:newCel(layer, f, img, Point(0, 0))
      end
    end
  end
  -- 빈 영역 잘라내기 (cel 경계를 내용에 맞춤)
  for _, layer in ipairs(spr.layers) do
    for _, cel in ipairs(layer.cels) do
      local r = cel.image:shrinkBounds()
      if not r.isEmpty and (r.width < 32 or r.height < 32) then
        local img = Image(r.width, r.height, ColorMode.RGB)
        img:drawImage(cel.image, Point(-r.x, -r.y))
        cel.image = img
        cel.position = Point(cel.position.x + r.x, cel.position.y + r.y)
      end
    end
  end
  for _, t in ipairs(meta.tags) do
    local tag = spr:newTag(t.from, t.to)
    tag.name = t.name
    tag.aniDir = AniDir.FORWARD
  end
  spr:saveAs(OUT_FILE)
  print("saved", OUT_FILE, #spr.frames, "frames", #spr.tags, "tags", #spr.layers, "layers")
end)
if not ok then print("ERROR: " .. tostring(err)) end
