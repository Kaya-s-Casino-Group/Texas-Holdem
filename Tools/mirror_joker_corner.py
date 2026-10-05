"""Copy a Joker's upper-left index to the opposite corner at 180 degrees.

Only the dark index ink is copied. The illustration and parchment remain
asymmetric, unlike the court cards.
"""

from pathlib import Path
import sys

import numpy as np
from PIL import Image


def mirror_corner(source: Path, destination: Path) -> None:
    with Image.open(source) as image:
        pixels = np.asarray(image.convert("RGB"))

    height, width, _ = pixels.shape
    if (width, height) != (1024, 1536):
        raise ValueError(f"Expected 1024x1536: {source}")

    x1, y1, x2, y2 = 55, 55, 335, 305
    index = pixels[y1:y2, x1:x2]
    # The lettering and star are much darker than the parchment in this area.
    ink = ((index[:, :, 0] < 175) &
           (index[:, :, 1] < 145) &
           (index[:, :, 2] < 130))
    if np.count_nonzero(ink) < 1000:
        raise ValueError(f"Could not locate the Joker index: {source}")

    result = pixels.copy()
    opposite = result[height - y2:height - y1, width - x2:width - x1]
    rotated_ink = ink[::-1, ::-1]
    rotated_pixels = index[::-1, ::-1]
    opposite[rotated_ink] = rotated_pixels[rotated_ink]

    destination.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(result).save(destination)

    with Image.open(destination) as saved:
        checked = np.asarray(saved.convert("RGB"))
    checked_corner = checked[height - y2:height - y1, width - x2:width - x1]
    if not np.array_equal(checked_corner[rotated_ink], rotated_pixels[rotated_ink]):
        raise AssertionError(f"Corner rotation failed: {destination}")
    print(f"{destination}: {np.count_nonzero(ink)} index pixels mirrored")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: mirror_joker_corner.py SOURCE DESTINATION")
    mirror_corner(Path(sys.argv[1]), Path(sys.argv[2]))
