# Aseprite Lua 실무 함정

| 함정 | 증상 | 대처 |
|---|---|---|
| `math.pow` | Lua 5.4에서 **제거됨**. 스크립트가 빈 에러로 죽음 | `a^b` 사용 |
| 에러 메시지 없음 | `Script failed:` 만 출력 | 전체를 `pcall`로 감싸고 `print(err)` |
| hex 파싱 | `#` 유무에 따라 채널이 밀려 색이 전혀 다르게 나옴 | `h:gsub("#","")` 후 1-2/3-4/5-6 슬라이스. **채널값을 print로 검산** |
| `drawPixel(x,y,nil)` | 지워버림. 클리핑용으로 쓰면 프레임까지 날아감 | 애초에 범위를 벗어나지 않게 가드 |
| cel 중복 | `newCel`이 기존 cel과 충돌 | `if l:cel(1) then spr:deleteCel(l,1) end` 선행 |
| 저장 | 배치 모드는 **자동 저장 안 함** | 끝에 `spr:saveAs(spr.filename)` |
| 좌표계 | cel 이미지는 cel-local | 전체 캔버스 크기 Image를 만들고 `Point(0,0)`에 커밋하면 전역 좌표로 작업 가능 |
| 재현성 | 랜덤 배치가 매번 바뀜 | `math.randomseed(고정값)` — 재실행해도 같은 그림 |
| 레이어 순서 | `newLayer`는 맨 위에 추가 | 아래→위 순서로 생성. `stackIndex`로 이동 시 다른 레이어가 밀리므로 결과를 print로 확인 |
