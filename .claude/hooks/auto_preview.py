"""PostToolUse hook: aseprite MCP 편집 도구 호출 후 확대 미리보기 PNG를 갱신한다.

stdin: Claude Code 훅 JSON ({"tool_name", "tool_input": {"filename": ...}, "cwd", ...})
결과: Aseprite/preview/<분류>/<stem>.preview.png (첫 프레임, 전체 레이어 합성)
      원본 Aseprite/src/<분류>/... 에 대응하는 preview 폴더 (character, tileset 등)
배율: 긴 변이 약 1024px가 되도록 1~8배 (32px 스프라이트 = 8배, 512px 던전 시트 = 2배)
※ 파일명이 숫자로 끝나면 Aseprite가 프레임 시퀀스로 저장하므로 끝을 숫자로 두지 않는다.
실패해도 작업을 막지 않도록 항상 exit 0.
"""
import json
import os
import subprocess
import sys
from pathlib import Path

# Aseprite 실행 파일: 환경변수 ASEPRITE_PATH (.claude/settings.local.json의 env), 없으면 PATH의 aseprite
ASEPRITE = os.environ.get("ASEPRITE_PATH") or "aseprite"
MAX_SCALE = 8
TARGET = 1024


def sprite_size(path: Path) -> tuple[int, int] | None:
    """.aseprite 헤더: DWORD 파일크기, WORD 매직(0xA5E0), WORD 프레임수, WORD 폭, WORD 높이."""
    with open(path, "rb") as f:
        head = f.read(12)
    if len(head) < 12 or int.from_bytes(head[4:6], "little") != 0xA5E0:
        return None
    return int.from_bytes(head[8:10], "little"), int.from_bytes(head[10:12], "little")


def main() -> None:
    data = json.load(sys.stdin)
    tool_input = data.get("tool_input") or {}
    filename = tool_input.get("filename") or tool_input.get("target_filename")
    if not filename or not filename.endswith(".aseprite"):
        return

    root = Path(os.environ.get("CLAUDE_PROJECT_DIR") or data.get("cwd") or ".")
    src = Path(filename)
    if not src.is_absolute():
        src = root / src
    if not src.exists():
        return

    size = sprite_size(src)
    scale = max(1, min(MAX_SCALE, TARGET // max(size))) if size else MAX_SCALE
    prev = next((p.parent / "preview" / src.parent.relative_to(p) for p in src.parents if p.name == "src"), src.parent / "preview")
    out = prev / f"{src.stem}.preview.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(
        [ASEPRITE, "-b", str(src), "--frame-range", "0,0", "--scale", str(scale), "--save-as", str(out)],
        capture_output=True,
        text=True,
        timeout=60,
    )
    if result.returncode != 0 or not out.exists():
        return

    rel = out.relative_to(root).as_posix()
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PostToolUse",
            "additionalContext": f"미리보기 갱신됨: {rel} (1프레임, {scale}x). 단계가 끝났으면 Read로 확인할 것.",
        }
    }, ensure_ascii=False))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    try:
        main()
    except Exception as e:
        print(f"auto_preview 실패: {e}", file=sys.stderr)
    sys.exit(0)
