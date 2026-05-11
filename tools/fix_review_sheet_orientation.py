from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


@dataclass(frozen=True)
class FixBox:
    name: str
    box: tuple[int, int, int, int]
    flame: tuple[int, int, int]
    nozzles: int = 3
    nose_clean: float = 0.3


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "game" / "assets" / "reviews" / "ai-ships-review-v1.png"
OUT = ROOT / "game" / "assets" / "reviews" / "ai-ships-review-v13-inpaint-nose-glow.png"
PATCH_DIR = ROOT / "game" / "assets" / "reviews" / "manual-rotated-patches"


FIXES = [
    FixBox("gold_c5", (908, 243, 1139, 457), (80, 245, 255), 3, 0.31),
    FixBox("red_c4", (684, 468, 902, 670), (255, 112, 24), 3, 0.25),
    FixBox("red_c5", (908, 468, 1139, 678), (255, 112, 24), 3, 0.25),
    FixBox("green_c1", (14, 667, 226, 882), (80, 255, 220), 3, 0.31),
    FixBox("green_c2", (227, 662, 470, 879), (80, 255, 220), 2, 0.28),
    FixBox("green_c3", (471, 676, 666, 884), (80, 255, 220), 2, 0.3),
    FixBox("green_c4", (683, 676, 895, 884), (80, 255, 220), 3, 0.28),
    FixBox("green_c5", (906, 682, 1133, 888), (80, 255, 220), 2, 0.28),
    FixBox("green_c6", (1141, 668, 1380, 899), (80, 255, 220), 3, 0.31),
    FixBox("human_c1", (15, 886, 219, 1106), (70, 150, 255), 3, 0.28),
    FixBox("human_c2", (227, 879, 470, 1104), (70, 150, 255), 2, 0.28),
    FixBox("human_c3", (471, 880, 679, 1114), (70, 150, 255), 1, 0.28),
    FixBox("human_c4", (694, 885, 886, 1119), (70, 150, 255), 2, 0.28),
    FixBox("human_c5", (909, 889, 1130, 1119), (70, 150, 255), 3, 0.28),
]


def main() -> None:
    PATCH_DIR.mkdir(parents=True, exist_ok=True)

    sheet = Image.open(SOURCE).convert("RGB")
    for fix in FIXES:
        patch = sheet.crop(fix.box)
        rotated = rotate_and_relight(patch, fix)
        rotated.save(PATCH_DIR / f"{fix.name}.png")
        sheet.paste(rotated, fix.box)

    sheet.save(OUT)
    print(f"Wrote {OUT}")


def rotate_and_relight(patch: Image.Image, fix: FixBox) -> Image.Image:
    clean_background = erase_source_ship_from_patch(patch)
    ship = extract_ship_without_exhaust(patch)
    rotated_ship = ship.rotate(180, resample=Image.Resampling.BICUBIC, expand=False)
    result = clean_background.convert("RGBA")
    result.alpha_composite(rotated_ship)
    result = erase_space_above_rotated_hull(result.convert("RGB"))
    result = mute_nose_glow(result, fix.nose_clean)
    result = add_engine_glow(result, fix.flame, fix.nozzles)
    return result


def erase_source_ship_from_patch(image: Image.Image) -> Image.Image:
    rgb = image.convert("RGB")
    px = rgb.load()
    width, height = rgb.size
    bg = sample_background(px, width, height)
    mask = Image.new("L", (width, height), 0)
    mask_px = mask.load()

    for y in range(height):
        for x in range(width):
            r, g, b = px[x, y]
            diff = abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2])
            if diff > 24 and max(r, g, b) > 14:
                mask_px[x, y] = 255

    mask = mask.filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.GaussianBlur(1.1))
    bg_layer = Image.new("RGB", (width, height), bg)
    return Image.composite(bg_layer, rgb, mask)


def extract_ship_without_exhaust(image: Image.Image) -> Image.Image:
    rgb = image.convert("RGB")
    px = rgb.load()
    width, height = rgb.size
    bg = sample_background(px, width, height)
    foreground = Image.new("L", (width, height), 0)
    solid = Image.new("L", (width, height), 0)
    fg_px = foreground.load()
    solid_px = solid.load()

    for y in range(height):
        for x in range(width):
            r, g, b = px[x, y]
            mx = max(r, g, b)
            mn = min(r, g, b)
            diff = abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2])
            saturation = 0 if mx == 0 else (mx - mn) / mx
            if diff <= 18 or mx <= 10:
                continue
            fg_px[x, y] = 255
            engine_like = is_engine_glow_color(r, g, b) and saturation > 0.22
            if not engine_like or y < height * 0.55:
                solid_px[x, y] = 255

    foreground = foreground.filter(ImageFilter.MaxFilter(3))
    fg_px = foreground.load()
    solid_px = solid.load()
    bottom_limits = find_bottom_limits(solid_px, width, height)

    mask = Image.new("L", (width, height), 0)
    mask_px = mask.load()
    for y in range(height):
        for x in range(width):
            if fg_px[x, y] == 0:
                continue
            limit = bottom_limits[x]
            if limit is None or y <= limit + 2:
                mask_px[x, y] = 255

    mask = mask.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.35))
    rgba = rgb.convert("RGBA")
    rgba.putalpha(mask)
    return rgba


def find_bottom_limits(solid_px, width: int, height: int) -> list[int | None]:
    raw: list[int | None] = []
    for x in range(width):
        value: int | None = None
        for y in range(height - 1, -1, -1):
            if solid_px[x, y] > 0:
                value = y
                break
        raw.append(value)

    radius = max(4, width // 70)
    smoothed: list[int | None] = []
    for x in range(width):
        local = [
            value
            for value in raw[max(0, x - radius) : min(width, x + radius + 1)]
            if value is not None
        ]
        if not local:
            smoothed.append(None)
        else:
            smoothed.append(int(sorted(local)[len(local) // 2]))
    return smoothed


def erase_space_above_rotated_hull(image: Image.Image) -> Image.Image:
    # After rotation, the old exhaust sits in front of the new nose. Instead of
    # tinting it down, find the first real hull pixel in each column and replace
    # every stray glow/halo pixel above that boundary with the local background.
    rgb = image.convert("RGB")
    px = rgb.load()
    width, height = rgb.size
    bg = sample_background(px, width, height)

    top_scan = int(height * 0.42)
    boundaries: list[int | None] = []
    for x in range(width):
        boundary: int | None = None
        for y in range(top_scan):
            if is_hull_pixel(px[x, y], bg):
                boundary = y
                break
        boundaries.append(boundary)

    radius = max(3, width // 80)
    for x in range(width):
        local = [
            boundary
            for boundary in boundaries[max(0, x - radius) : min(width, x + radius + 1)]
            if boundary is not None
        ]
        if not local:
            limit = top_scan
        else:
            limit = max(0, min(local) - 2)

        for y in range(limit):
            current = px[x, y]
            if not is_hull_pixel(current, bg):
                px[x, y] = bg

    return rgb


def add_engine_glow(image: Image.Image, color: tuple[int, int, int], nozzle_count: int) -> Image.Image:
    base = image.convert("RGBA")
    width, height = base.size
    bbox = content_bbox(image)
    if bbox is None:
        return image

    left, top, right, bottom_content = bbox
    center_x = (left + right) // 2
    content_width = max(1, right - left)
    bottom = min(height - 10, bottom_content - 4)
    spacing = max(8, min(24, int(content_width * 0.16)))

    if nozzle_count == 1:
        xs = [center_x]
    elif nozzle_count == 2:
        xs = [center_x - spacing // 2, center_x + spacing // 2]
    else:
        xs = [center_x - spacing, center_x, center_x + spacing]

    glow = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(glow, "RGBA")

    for x in xs:
        draw.ellipse(
            (x - 7, bottom - 2, x + 7, bottom + 15),
            fill=(color[0], color[1], color[2], 22),
        )
        draw.ellipse(
            (x - 3, bottom - 1, x + 3, bottom + 10),
            fill=(color[0], color[1], color[2], 72),
        )
        draw.polygon(
            [(x - 3, bottom), (x + 3, bottom), (x, min(height - 2, bottom + 18))],
            fill=(color[0], color[1], color[2], 105),
        )
        draw.polygon(
            [(x - 1, bottom + 1), (x + 1, bottom + 1), (x, min(height - 3, bottom + 10))],
            fill=(230, 250, 255, 135),
        )
        draw.ellipse(
            (x - 3, bottom - 2, x + 3, bottom + 2),
            fill=(210, 255, 255, 112),
        )

    glow = glow.filter(ImageFilter.GaussianBlur(0.35))
    return Image.alpha_composite(base, glow).convert("RGB")


def mute_nose_glow(image: Image.Image, height_fraction: float) -> Image.Image:
    try:
        return inpaint_nose_glow(image, height_fraction)
    except Exception:
        return mute_nose_glow_fallback(image, height_fraction)


def inpaint_nose_glow(image: Image.Image, height_fraction: float) -> Image.Image:
    import cv2
    import numpy as np

    rgb = image.convert("RGB")
    arr = np.array(rgb)
    height, width, _ = arr.shape
    max_y = int(height * height_fraction)
    mask = np.zeros((height, width), dtype=np.uint8)
    bg = np.array(sample_background(rgb.load(), width, height), dtype=np.int16)

    top = arr[:max_y].astype(np.int16)
    r = top[:, :, 0]
    g = top[:, :, 1]
    b = top[:, :, 2]
    mx = np.maximum(np.maximum(r, g), b)
    mn = np.minimum(np.minimum(r, g), b)
    saturation = np.where(mx == 0, 0.0, (mx - mn) / np.maximum(mx, 1))
    diff = np.abs(top - bg).sum(axis=2)

    cool_flame = (b > 54) & (b >= g * 0.82) & (b > r * 1.06)
    teal_flame = (g > 54) & (b > 44) & (r < np.maximum(g, b) * 0.9)
    green_flame = (g > 66) & (g > r * 1.06) & (g > b * 0.8)
    orange_flame = (r > 105) & (g > 32) & (b < 100) & (r > b * 1.28)
    white_core = (mx > 118) & ((mx - mn) < 64) & ((b > 86) | (g > 86))

    flame = (
        (diff > 24)
        & (mx > 34)
        & (saturation > 0.1)
        & (cool_flame | teal_flame | green_flame | orange_flame | white_core)
    )
    mask[:max_y][flame] = 255

    kernel = np.ones((3, 3), np.uint8)
    mask = cv2.dilate(mask, kernel, iterations=1)
    mask = cv2.GaussianBlur(mask, (3, 3), 0)
    _, mask = cv2.threshold(mask, 24, 255, cv2.THRESH_BINARY)

    # OpenCV expects BGR; convert back to RGB after inpaint.
    bgr = cv2.cvtColor(arr, cv2.COLOR_RGB2BGR)
    fixed = cv2.inpaint(bgr, mask, 3, cv2.INPAINT_TELEA)
    fixed = cv2.cvtColor(fixed, cv2.COLOR_BGR2RGB)
    return Image.fromarray(fixed, "RGB")


def mute_nose_glow_fallback(image: Image.Image, height_fraction: float) -> Image.Image:
    rgb = image.convert("RGB")
    px = rgb.load()
    width, height = rgb.size
    bg = sample_background(px, width, height)
    max_y = int(height * height_fraction)
    mask = Image.new("L", (width, height), 0)
    mask_px = mask.load()

    for y in range(max_y):
        vertical = 1.0 - y / max(1, max_y)
        for x in range(width):
            r, g, b = px[x, y]
            mx = max(r, g, b)
            mn = min(r, g, b)
            saturation = 0 if mx == 0 else (mx - mn) / mx
            cool_flame = b > 58 and b >= g * 0.85 and b > r * 1.08
            teal_flame = g > 58 and b > 48 and r < max(g, b) * 0.9
            green_flame = g > 70 and g > r * 1.08 and g > b * 0.82
            orange_flame = r > 112 and g > 34 and b < 95 and r > b * 1.35
            white_core = mx > 112 and (mx - mn) < 58 and (b > 82 or g > 82)
            diff = abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2])
            if diff > 26 and saturation > 0.12 and (cool_flame or teal_flame or green_flame or orange_flame or white_core):
                mask_px[x, y] = int(180 + vertical * 75)

    mask = mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(1.2))
    mask_px = mask.load()
    for y in range(max_y + 4):
        for x in range(width):
            alpha = mask_px[x, y] / 255.0
            if alpha <= 0.01:
                continue
            r, g, b = px[x, y]
            amount = min(0.92, alpha * 0.88)
            px[x, y] = blend_color((r, g, b), bg, amount)

    return rgb


def content_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    rgb = image.convert("RGB")
    px = rgb.load()
    width, height = rgb.size
    bg = sample_background(px, width, height)

    xs: list[int] = []
    ys: list[int] = []
    for y in range(height):
        for x in range(width):
            r, g, b = px[x, y]
            diff = abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2])
            if diff > 46 and max(r, g, b) > 24:
                xs.append(x)
                ys.append(y)

    if not xs:
        return None

    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def is_engine_glow_color(r: int, g: int, b: int) -> bool:
    cyan = g > 42 and b > 42 and r < max(g, b) * 0.82
    blue = b > 58 and b > r * 1.16 and b > g * 0.82
    green = g > 64 and g > r * 1.12 and g > b * 0.95
    orange = r > 105 and g > 34 and b < 90 and r > b * 1.45
    return cyan or blue or green or orange


def is_hull_pixel(color: tuple[int, int, int], bg: tuple[int, int, int]) -> bool:
    r, g, b = color
    mx = max(r, g, b)
    mn = min(r, g, b)
    diff = abs(r - bg[0]) + abs(g - bg[1]) + abs(b - bg[2])
    saturation = 0 if mx == 0 else (mx - mn) / mx
    if diff <= 42 or mx <= 22:
        return False
    if is_engine_glow_color(r, g, b) and saturation > 0.26:
        return False
    # Metallic hull highlights can be bright, but they are usually less pure
    # than exhaust cores. Keep those as body pixels.
    return True


def sample_background(px, width: int, height: int) -> tuple[int, int, int]:
    samples = []
    for x, y in (
        (2, 2),
        (width - 3, 2),
        (2, height - 3),
        (width - 3, height - 3),
        (width // 2, 2),
        (width // 2, height - 3),
    ):
        samples.append(px[x, y])
    return tuple(sum(sample[i] for sample in samples) // len(samples) for i in range(3))


def blend_color(color: tuple[int, int, int], bg: tuple[int, int, int], amount: float) -> tuple[int, int, int]:
    return (
        int(color[0] * (1.0 - amount) + bg[0] * amount),
        int(color[1] * (1.0 - amount) + bg[1] * amount),
        int(color[2] * (1.0 - amount) + bg[2] * amount),
    )


if __name__ == "__main__":
    main()
