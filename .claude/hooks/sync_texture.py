"""게임용 결과물을 Unity 프로젝트 텍스처 폴더로 복사한다.

  Aseprite/export/<분류>/...  →  Assets/Texture/<분류>/...   (분류 = export/ 아래 character를 뺀 모든 폴더: tileset, item …)
  Aseprite/src/character/<char>/*.aseprite  →  Assets/Texture/character/<char>/   (캐릭터는 .aseprite 원본만)

- PostToolUse 훅: aseprite MCP export 도구(export_sprite, export_spritesheet, export_tag …)와 run_lua_script 호출 후 실행
- 직접 실행: python .claude/hooks/sync_texture.py  (dungeon_atlas.py build가 끝에 호출한다)
복사 전에 export/character·export/tileset의 JSON에 "type"·"name"이 없으면 맨 앞에 넣는다 (Unity 우클릭 메뉴 구분용).
  character: Aseprite 시트 JSON(frames + meta). name = 캐릭터 폴더 이름 (export/character/<char>/…), 폴더가 없으면 파일명에서 _sheet·_<W>x<H>t를 뗀 것
  tileset:   dungeon_atlas.py가 직접 넣는다. 없으면 objects가 있는 JSON에 name = 테마 이름(파일명)
새 파일이거나 크기·수정 시각이 다른 파일만 복사한다. 원본에서 지운 파일은 지우지 않는다.
export/ 바로 아래 파일(분류 폴더 밖의 이전 결과물)은 복사하지 않는다.
GIF는 Unity에서 애니메이션으로 못 쓰므로 복사하지 않는다 (Aseprite/export에만 남는다).
캐릭터는 원본 .aseprite만 복사한다. export/character의 png·시트 png·시트 json은 Unity로 복사하지 않는다
  (Unity Aseprite Importer(com.unity.2d.aseprite)가 .aseprite에서 스프라이트·태그별 AnimationClip·AnimatorController를 만든다).
실패해도 작업을 막지 않도록 항상 exit 0.
"""
import json
import os
import re
import shutil
import sys
from pathlib import Path

SKIP_SUFFIXES = {".gif"}
TYPED = ("character", "tileset")


def asset_name(path: Path, cat_dir: Path) -> str:
    rel = path.relative_to(cat_dir)
    if len(rel.parts) > 1:
        return rel.parts[0]
    return re.sub(r"(_\d+x\d+t)?(_sheet)?$", "", path.stem)


def tag_json(path: Path, cat: str, cat_dir: Path) -> None:
    """JSON 맨 앞에 type·name을 넣는다. 이미 있거나 해당 형식이 아니면 그대로 둔다."""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (ValueError, UnicodeDecodeError):
        return
    if not isinstance(data, dict) or "type" in data:
        return
    if cat == "character" and not ("frames" in data and "meta" in data):
        return
    if cat == "tileset" and "objects" not in data:
        return
    data = {"type": cat, "name": asset_name(path, cat_dir), **data}
    path.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")


def copy_if_changed(src: Path, dst: Path) -> bool:
    s = src.stat()
    if dst.exists():
        d = dst.stat()
        if d.st_size == s.st_size and int(d.st_mtime) == int(s.st_mtime):
            return False
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(src, dst)
    return True


def sync(root: Path) -> list[str]:
    copied = []
    char_src = root / "Aseprite" / "src" / "character"
    if char_src.is_dir():
        for src in char_src.glob("*/*.aseprite"):
            dst = root / "Assets" / "Texture" / "character" / src.relative_to(char_src)
            if copy_if_changed(src, dst):
                copied.append(dst.relative_to(root).as_posix())
    export_root = root / "Aseprite" / "export"
    if not export_root.is_dir():
        return copied
    for src_dir in sorted(p for p in export_root.iterdir() if p.is_dir()):
        dst_dir = root / "Assets" / "Texture" / src_dir.name
        if src_dir.name in TYPED:
            for j in src_dir.rglob("*.json"):
                tag_json(j, src_dir.name, src_dir)
        if src_dir.name == "character":
            continue   # 캐릭터는 위에서 .aseprite 원본만 복사했다
        for src in src_dir.rglob("*"):
            if not src.is_file() or src.suffix.lower() in SKIP_SUFFIXES:
                continue
            dst = dst_dir / src.relative_to(src_dir)
            if copy_if_changed(src, dst):
                copied.append(dst.relative_to(root).as_posix())
    return copied


def main() -> None:
    hook = not sys.stdin.isatty() and "--cli" not in sys.argv
    data = {}
    if hook:
        try:
            data = json.load(sys.stdin)
        except ValueError:
            hook = False
    root = Path(os.environ.get("CLAUDE_PROJECT_DIR") or data.get("cwd") or Path(__file__).resolve().parents[2])
    copied = sync(root)
    if not copied:
        return
    msg = f"Assets/Texture로 복사됨 ({len(copied)}개): " + ", ".join(copied[:10]) + (" …" if len(copied) > 10 else "")
    if hook:
        print(json.dumps({"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": msg}}, ensure_ascii=False))
    else:
        print(msg)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    try:
        main()
    except Exception as e:
        print(f"sync_texture 실패: {e}", file=sys.stderr)
    sys.exit(0)
