#!/usr/bin/env python3
"""Validate canonical source cards; optionally write a source-only manifest.

Requires Pillow. Never edits images, creates atlases or touches Unity assets.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import sys

from PIL import Image


SUITS = ("spades", "hearts", "clubs", "diamonds")
RANKS = ("a", *(f"{n:02}" for n in range(2, 11)), "j", "q", "k")
IDS = ("card_back", *(f"{s}_{r}" for s in SUITS for r in RANKS),
       "joker_big", "joker_small")
COURTS = {f"{s}_{r}" for s in SUITS for r in ("j", "q", "k")}
SIZE = (1024, 1536)


def validate(folder, expected, require_symmetry=True):
    if not folder.is_dir():
        raise ValueError(f"原始图目录不存在：{folder}")
    files = [p for p in folder.iterdir() if p.is_file() and p.name != ".DS_Store"]
    actual = {p.name for p in files}
    wanted = {f"{card_id}.png" for card_id in expected}
    errors = []
    if wanted - actual:
        errors.append("缺少：" + ", ".join(sorted(wanted - actual)))
    if actual - wanted:
        errors.append("额外或非标准命名：" + ", ".join(sorted(actual - wanted)))
    if any(p.is_dir() for p in folder.iterdir()):
        errors.append("原始图目录不能包含子目录，所有最终 PNG 应放在同一层")
    cards = []
    for card_id in (card_id for card_id in IDS if card_id in expected):
        path = folder / f"{card_id}.png"
        if not path.is_file():
            continue
        try:
            with Image.open(path) as image:
                image.load()
                if image.format != "PNG":
                    raise ValueError("实际编码不是 PNG")
                if image.size != SIZE:
                    raise ValueError(f"尺寸 {image.size}，要求 {SIZE}")
                if image.mode not in ("RGB", "RGBA"):
                    raise ValueError(f"模式 {image.mode}，要求 RGB/RGBA")
                if image.getexif().get(274, 1) != 1:
                    raise ValueError("EXIF 朝向非正常；像素须直接朝上")
                rgba = image.convert("RGBA")
                if rgba.getchannel("A").getextrema() != (255, 255):
                    raise ValueError("基础纸牌要求完整不透明画布")
                symmetric = None
                if card_id in COURTS:
                    symmetric = (rgba.tobytes() ==
                                 rgba.transpose(Image.Transpose.ROTATE_180).tobytes())
                    if require_symmetry and not symmetric:
                        raise ValueError("J/Q/K 不满足逐像素 180° 对称")
                cards.append({"id": card_id, "filename": path.name,
                              "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                              "rotation180Symmetric": symmetric})
        except (OSError, ValueError, SyntaxError) as error:
            errors.append(f"{path.name}：{error}")
    if errors:
        raise ValueError("\n".join(errors))
    return cards


def write_manifest(destination, folder, cards, style, require_symmetry):
    if destination.suffix.lower() != ".json":
        raise ValueError("配置文本使用 .json 扩展名")
    # Preserve existing delivery configuration; each build uses a fresh path.
    if destination.exists():
        raise ValueError(f"配置已存在，使用新的路径或先移走旧配置：{destination}")
    manifest = {
        "version": 1,
        "kind": "card-art-source",
        "sourceFolder": Path(os.path.relpath(folder, destination.parent)).as_posix(),
        "sourceSize": {"width": SIZE[0], "height": SIZE[1]},
        "aspectRatio": "2:3",
        "format": "PNG",
        "opaque": True,
        "style": style,
        "completeStandardDeck": len(cards) == len(IDS),
        "courtRotation180Required": require_symmetry,
        "sampleContract": {
            "resizeFilter": "Nearest", "filterMode": "Point", "wrapMode": "Clamp",
            "sRGB": True, "mipmaps": False, "compression": "Uncompressed",
            "anisoLevel": 1, "npotScale": "None",
            "appliedBy": "existing Atlas Builder, not this source manifest",
        },
        "validation": {
            "structural": "passed",
            "visualReview": "not assessed by this script",
        },
        "cards": cards,
    }
    destination.parent.mkdir(parents=True, exist_ok=True)
    # Exclusive creation protects an existing manifest even if it appeared later.
    with destination.open("x", encoding="utf-8") as output:
        json.dump(manifest, output, ensure_ascii=False, indent=2)
        output.write("\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("folder", type=Path)
    parser.add_argument("--ids", nargs="+", help="明确指定子集；默认完整 55 张")
    parser.add_argument("--manifest", type=Path, help="全部通过后写配置；不生成其他文件")
    parser.add_argument("--style", default="", help="用户选择的主题与美术基准")
    parser.add_argument("--allow-asymmetric-court", action="store_true",
                        help="仅在用户明确选择非对称 J/Q/K 时使用")
    args = parser.parse_args()
    expected = args.ids if args.ids is not None else IDS
    if len(set(expected)) != len(expected) or set(expected) - set(IDS):
        parser.error("--ids 必须是无重复的标准牌 ID")
    try:
        folder = args.folder.resolve()
        require_symmetry = not args.allow_asymmetric_court
        cards = validate(folder, set(expected), require_symmetry)
        if args.manifest:
            write_manifest(args.manifest.resolve(), folder, cards, args.style,
                           require_symmetry)
        print(f"原图结构校验通过：{len(cards)} 张，1024×1536 PNG；美术语义需目视检查。")
    except (ValueError, OSError) as error:
        print(str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
