from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

from fix_review_sheet_orientation import FIXES, blend_color, is_engine_glow_color, sample_background


@dataclass(frozen=True)
class GlowMask:
    fix_name: str
    ellipses: tuple[tuple[float, float, float, float], ...]


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "game" / "assets" / "reviews" / "ai-ships-review-v11-muted-nose-glow.png"
OUT = ROOT / "game" / "assets" / "reviews" / "ai-ships-review-v14-manual-nose-glow.png"


# Ellipses are relative to each patch: cx, cy, rx, ry.
MASKS = [
    GlowMask("gold_c5", ((0.45, 0.08, 0.12, 0.11), (0.55, 0.08, 0.12, 0.11), (0.50, 0.12, 0.18, 0.12))),
    GlowMask("red_c4", ((0.43, 0.10, 0.14, 0.10), (0.57, 0.10, 0.14, 0.10), (0.50, 0.13, 0.2, 0.1))),
    GlowMask("red_c5", ((0.43, 0.10, 0.12, 0.10), (0.57, 0.10, 0.12, 0.10), (0.50, 0.14, 0.2, 0.1))),
    GlowMask("green_c1", ((0.39, 0.08, 0.12, 0.12), (0.61, 0.08, 0.12, 0.12), (0.50, 0.13, 0.2, 0.11))),
    GlowMask("green_c2", ((0.46, 0.09, 0.12, 0.1), (0.54, 0.09, 0.12, 0.1), (0.50, 0.14, 0.19, 0.1))),
    GlowMask("green_c3", ((0.42, 0.10, 0.12, 0.11), (0.58, 0.10, 0.12, 0.11), (0.50, 0.15, 0.2, 0.11))),
    GlowMask("green_c4", ((0.42, 0.09, 0.14, 0.10), (0.58, 0.09, 0.14, 0.10), (0.50, 0.14, 0.24, 0.1))),
    GlowMask("green_c5", ((0.41, 0.10, 0.12, 0.1), (0.59, 0.10, 0.12, 0.1), (0.50, 0.15, 0.2, 0.1))),
    GlowMask("green_c6", ((0.43, 0.09, 0.13, 0.11), (0.57, 0.09, 0.13, 0.11), (0.50, 0.15, 0.22, 0.11))),
    GlowMask("human_c1", ((0.38, 0.08, 0.12, 0.12), (0.62, 0.08, 0.12, 0.12), (0.50, 0.13, 0.25, 0.11))),
    GlowMask("human_c2", ((0.38, 0.09, 0.11, 0.13), (0.62, 0.09, 0.11, 0.13), (0.50, 0.16, 0.25, 0.1))),
    GlowMask("human_c3", ((0.50, 0.10, 0.13, 0.15),)),
    GlowMask("human_c4", ((0.50, 0.10, 0.13, 0.14), (0.32, 0.10, 0.07, 0.1), (0.68, 0.10, 0.07, 0.1))),
    GlowMask("human_c5", ((0.34, 0.09, 0.10, 0.13), (0.66, 0.09, 0.10, 0.13), (0.50, 0.14, 0.22, 0.12))),
]


def main() -> None:
    sheet = Image.open(SOURCE).convert("RGB")
    fixes_by_name = {fix.name: fix for fix in FIXES}
    for mask in MASKS:
        fix = fixes_by_name[mask.fix_name]
        patch = sheet.crop(fix.box)
        patch = clean_patch(patch, mask)
        sheet.paste(patch, fix.box)

    sheet.save(OUT)
    print(f"Wrote {OUT}")


def clean_patch(image: Image.Image, mask: GlowMask) -> Image.Image:
    rgb = image.convert("RGB")
    width, height = rgb.size
    px = rgb.load()
    bg = sample_background(px, width, height)

    area = Image.new("L", (width, height), 0)
    draw = ImageDraw.Draw(area)
    for cx, cy, rx, ry in mask.ellipses:
        draw.ellipse(
            (
                int((cx - rx) * width),
                int((cy - ry) * height),
                int((cx + rx) * width),
                int((cy + ry) * height),
            ),
            fill=255,
        )
    area = area.filter(ImageFilter.GaussianBlur(2.0))
    area_px = area.load()

    for y in range(height):
        for x in range(width):
            influence = area_px[x, y] / 255.0
            if influence <= 0.01:
                continue
            r, g, b = px[x, y]
            mx = max(r, g, b)
            mn = min(r, g, b)
            saturation = 0 if mx == 0 else (mx - mn) / mx
            flame = is_engine_glow_color(r, g, b) or (mx > 95 and saturation < 0.26 and (b > 70 or g > 70))
            if not flame:
                continue

            # Preserve the silhouette by not deleting the pixel outright; turn
            # engine glow into a subdued metal/window highlight or background.
            amount = min(0.88, 0.42 + influence * 0.44)
            px[x, y] = blend_color((r, g, b), bg, amount)

    return rgb


if __name__ == "__main__":
    main()
