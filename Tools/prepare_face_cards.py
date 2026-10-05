"""Build the 14 selected face cards with paper tones matched to number cards."""

from pathlib import Path
import re

import numpy as np
from PIL import Image, ImageDraw


PROJECT = Path(__file__).resolve().parents[1]
ROOT = PROJECT / "Assets/Art/Cards"
SOURCE = PROJECT / "ArtSource/Cards"
OUTPUT = ROOT / "FourBeasts"
NUMBERED = OUTPUT

SOURCES = {
    **{
        f"{suit}_{rank}.png": SOURCE / "Court" / f"{suit}_{rank}{suffix}.png"
        for suit, suffix in (
            ("spades", "_v3"),
            ("hearts", ""),
            ("clubs", ""),
            ("diamonds", "_v2"),
        )
        for rank in ("j", "q", "k")
    },
    "joker_big.png": SOURCE / "Joker" / "joker_cheshire_big.png",
}


def smoothstep(value: np.ndarray, low: float, high: float) -> np.ndarray:
    t = np.clip((value - low) / (high - low), 0, 1)
    return t * t * (3 - 2 * t)


def paper_median(image: np.ndarray) -> np.ndarray:
    # This upper-right patch is clear of the indices and character artwork.
    patch = image[90:180, 700:850, :3].reshape(-1, 3)
    r, g, b = patch.T
    paper = (r > 150) & (g > 125) & (b > 65) & (r > g + 15) & (g > b + 15)
    return np.median(patch[paper], axis=0)


def target_paper() -> np.ndarray:
    samples = []
    for path in NUMBERED.glob("*.png"):
        if not re.fullmatch(r"(spades|hearts|clubs|diamonds)_(0[2-9]|10)", path.stem):
            continue
        image = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)
        samples.append(paper_median(image))
    return np.rint(np.median(samples, axis=0))


def normalize(image: np.ndarray, target: np.ndarray) -> np.ndarray:
    pixels = image.astype(np.float32)
    r, g, b = pixels.transpose(2, 0, 1)
    rg = r - g
    gb = g - b
    paper_weight = (
        smoothstep(g, 100, 140)
        * smoothstep(b, 60, 100)
        * smoothstep(rg, 12, 26)
        * (1 - smoothstep(rg, 75, 95))
        * smoothstep(gb, 12, 26)
        * (1 - smoothstep(gb, 100, 120))
    )
    delta = target - paper_median(image)
    adjusted = np.clip(pixels + paper_weight[..., None] * delta, 0, 255)
    correction = target - paper_median(np.rint(adjusted).astype(np.uint8))
    adjusted = np.clip(adjusted + paper_weight[..., None] * correction, 0, 255)
    return np.rint(adjusted).astype(np.uint8)


def recolor_joker_red_to_blue(image: np.ndarray) -> np.ndarray:
    pixels = image.astype(np.float32)
    r, g, b = pixels.transpose(2, 0, 1)
    red_weight = (
        smoothstep(r / (g + 1), 1.7, 2.0)
        * smoothstep(r - g, 35, 55)
        * smoothstep(r - b, 35, 55)
        * smoothstep(r, 45, 75)
        * (1 - smoothstep(g, 80, 105))
    )
    # The worn frame has a few reddish-brown pixels that are paper, not ink.
    red_weight[:50, :] = 0
    red_weight[-50:, :] = 0
    red_weight[:, :55] = 0
    red_weight[:, 970:] = 0
    blue = np.stack((0.30 * r + 0.30 * g, 0.48 * r + 0.30 * g, 0.65 * r + 0.30 * g), axis=-1)
    return np.rint(np.clip(pixels * (1 - red_weight[..., None]) + blue * red_weight[..., None], 0, 255)).astype(np.uint8)


def main() -> None:
    target = target_paper()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    print("number-card paper target:", tuple(target.astype(int)))
    for name, source in SOURCES.items():
        image = np.asarray(Image.open(source).convert("RGB"))
        if image.shape != (1536, 1024, 3):
            raise ValueError(f"unexpected dimensions: {source}: {image.shape}")
        output = normalize(image, target)
        dest = OUTPUT / name
        Image.fromarray(output).save(dest)
        print(name, "paper:", tuple(paper_median(image).astype(int)), "->", tuple(paper_median(output).astype(int)))
    big = np.asarray(Image.open(OUTPUT / "joker_big.png").convert("RGB"))
    small = recolor_joker_red_to_blue(big)
    Image.fromarray(small).save(OUTPUT / "joker_small.png")
    print("joker_small.png paper:", tuple(paper_median(small).astype(int)), "(same source pixels as Big Joker)")
    names = list(SOURCES) + ["joker_small.png"]
    thumb = (192, 288)
    gap = 16
    row_height = thumb[1] + 28
    sheet = Image.new("RGB", (7 * thumb[0] + 8 * gap, 2 * row_height + 3 * gap), "#d5c7a8")
    draw = ImageDraw.Draw(sheet)
    for i, name in enumerate(names):
        x = gap + (i % 7) * (thumb[0] + gap)
        y = gap + (i // 7) * (row_height + gap)
        image = Image.open(OUTPUT / name).convert("RGB").resize(thumb, Image.Resampling.NEAREST)
        sheet.paste(image, (x, y))
        draw.text((x, y + thumb[1] + 4), name, fill="#2b2119")
    previews = SOURCE / "Previews"
    previews.mkdir(parents=True, exist_ok=True)
    sheet.save(previews / "face_overview.png")


if __name__ == "__main__":
    main()
