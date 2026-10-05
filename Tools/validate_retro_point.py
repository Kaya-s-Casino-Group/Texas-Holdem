"""Verify exact nearest-neighbor 2x enlargement in the captured retro output.
Run after Tools/Rendering/Capture Retro 540p Validation (and optional Game Output).
"""
import json
from pathlib import Path
from PIL import Image

folder = Path(__file__).resolve().parents[1] / 'ArtSource/Cards/Retro540Validation'
rows = []
for name in ['retro_1920x1080.png', 'retro_2560x1080.png', 'game_output.png']:
    path = folder / name
    if not path.exists() and name == 'game_output.png':
        continue
    image = Image.open(path).convert('RGB')
    width, height = image.size
    if width % 2 or height % 2:
        raise ValueError(f'{name}: expected an even 2x output size')
    pixels = image.load()
    mismatches = sum(
        any(pixels[x + dx, y + dy] != pixels[x, y] for dx, dy in [(1, 0), (0, 1), (1, 1)])
        for y in range(0, height, 2) for x in range(0, width, 2)
    )
    rows.append(dict(file=name, size=[width, height], testedBlocks=width * height // 4, mismatches=mismatches))
(folder / 'point_output_validation.json').write_text(json.dumps(rows, indent=2) + '\n')
print(json.dumps(rows, indent=2))
if any(row['mismatches'] for row in rows):
    raise SystemExit('Point enlargement failed: output contains mixed 2x2 blocks.')
