"""Make court-card artwork exactly invariant under a 180-degree rotation.

The upper side of the diagonal is the source artwork. Its lower side is
copied from that source after a 180-degree turn; a narrow seam hides the cut.
"""

from pathlib import Path
import sys

import numpy as np
from PIL import Image


def build(source: Path, destination: Path | None = None) -> Path:
    with Image.open(source) as image:
        pixels = np.asarray(image.convert("RGB"))

    height, width, _ = pixels.shape
    if (width, height) != (1024, 1536):
        raise ValueError(f"Expected 1024x1536: {source}")

    y, x = np.indices((height, width), dtype=np.int32)
    # The diagonal goes through the exact center. A 180-degree turn negates d.
    d = 20 * (2 * y - (height - 1)) + 7 * (2 * x - (width - 1))
    rotated = pixels[::-1, ::-1]
    result = np.where((d < 0)[..., None], pixels, rotated).copy()

    # Keep the seam inside the card border. Both masks are rotation invariant.
    inside = (x >= 48) & (x < width - 48) & (y >= 48) & (y < height - 48)
    result[inside & (np.abs(d) < 240)] = (43, 38, 32)
    result[inside & (np.abs(d) < 160)] = (217, 189, 148)

    if not np.array_equal(result, result[::-1, ::-1]):
        raise AssertionError(f"Rotation check failed: {source}")

    if destination is None:
        destination = source.with_name(source.stem.replace("_v2", "_v3") + ".png")
    destination.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(result).save(destination)
    print(f"{destination}: 180-degree pixel symmetry verified")
    return destination


if __name__ == "__main__":
    for filename in sys.argv[1:]:
        build(Path(filename))
