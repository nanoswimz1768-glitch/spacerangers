from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
APPROVED_SHEET = ROOT / "game" / "assets" / "reviews" / "ai-ships-review-v1.png"
FIXED_PATCH_SHEET = ROOT / "game" / "assets" / "reviews" / "problem-patches-ai-clean-v1.png"
OUTPUT = ROOT / "game" / "assets" / "ships"
FINAL_REVIEW = ROOT / "game" / "assets" / "reviews" / "approved-ships-final-sheet.png"

CANVAS_SIZE = 256
TARGET_OCCUPANCY = 0.82


SHIP_IDS = [
    "2FeiD",
    "2FeiL",
    "2FeiP",
    "2FeiR",
    "2FeiT",
    "2FeiW",
    "2GaalD",
    "2GaalL",
    "2GaalP",
    "2GaalR",
    "2GaalT",
    "2GaalW",
    "2MalocD",
    "2MalocL",
    "2MalocP",
    "2MalocR",
    "2MalocT",
    "2MalocW",
    "2PelengD",
    "2PelengL",
    "2PelengP",
    "2PelengR",
    "2PelengT",
    "2PelengW",
    "2PeopleD",
    "2PeopleL",
    "2PeopleP",
    "2PeopleR",
    "2PeopleT",
    "2PeopleW",
]


FIXED_TO_SHIP_ID = {
    "gold_c5": "2GaalT",
    "red_c4": "2MalocR",
    "red_c5": "2MalocT",
    "green_c1": "2PelengD",
    "green_c2": "2PelengL",
    "green_c3": "2PelengP",
    "green_c4": "2PelengR",
    "green_c5": "2PelengT",
    "green_c6": "2PelengW",
    "human_c1": "2PeopleD",
    "human_c2": "2PeopleL",
    "human_c3": "2PeopleP",
    "human_c4": "2PeopleR",
    "human_c5": "2PeopleT",
}


V1_COLS = [0, 227, 471, 684, 906, 1139, 1387]
V1_ROWS = [0, 243, 468, 667, 886, 1134]


@dataclass(frozen=True)
class FixedCell:
    name: str
    crop: tuple[int, int, int, int]


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)

    approved = Image.open(APPROVED_SHEET).convert("RGB")
    fixed = Image.open(FIXED_PATCH_SHEET).convert("RGB")
    fixed_cells = load_fixed_cells(fixed)

    final_sheet = approved.copy()
    manifest = []

    for index, ship_id in enumerate(SHIP_IDS):
        source_crop = get_approved_cell(approved, index)
        fixed_name = next((name for name, mapped in FIXED_TO_SHIP_ID.items() if mapped == ship_id), None)
        source = source_crop
        source_kind = "approved-v1"

        if fixed_name is not None:
            source = remove_ai_card_lines(fixed.crop(fixed_cells[fixed_name].crop))
            source_kind = "ai-clean-patch"
            paste_box = get_v1_box(index)
            normalized_for_sheet = fit_for_box(source, paste_box[2] - paste_box[0], paste_box[3] - paste_box[1])
            final_sheet.paste(normalized_for_sheet, paste_box)

        sprite, bounds = make_sprite(source)
        exhaust_ports = detect_exhaust_ports(sprite)
        out_path = OUTPUT / f"{ship_id}.png"
        sprite.save(out_path)

        race, hull = parse_ship_name(ship_id)
        manifest.append(
            {
                "id": ship_id,
                "race": race,
                "hull": hull,
                "path": f"res://assets/ships/{ship_id}.png",
                "source": source_kind,
                "content_bounds": list(bounds),
                "exhaust_ports": exhaust_ports,
            }
        )

    FINAL_REVIEW.parent.mkdir(parents=True, exist_ok=True)
    final_sheet.save(FINAL_REVIEW)
    (OUTPUT / "ships_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Wrote {len(manifest)} ship sprites into {OUTPUT}")
    print(f"Wrote final review sheet to {FINAL_REVIEW}")


def load_fixed_cells(image: Image.Image) -> dict[str, FixedCell]:
    labels = detect_label_boxes(image)
    cells: dict[str, FixedCell] = {}
    for i, (left, label_top, right, _label_bottom) in enumerate(labels, start=1):
        label_name = read_known_label(i)
        row_top = row_top_for_label(label_top)
        cells[label_name] = FixedCell(label_name, (left, row_top, right, label_top))
    return cells


def detect_label_boxes(image: Image.Image) -> list[tuple[int, int, int, int]]:
    arr = np.array(image)
    r = arr[:, :, 0]
    g = arr[:, :, 1]
    b = arr[:, :, 2]
    mask = ((r < 42) & (g > 95) & (g < 235) & (b > 115) & (b > g * 0.72)).astype(np.uint8) * 255

    num_labels, labels, stats, _centroids = cv2.connectedComponentsWithStats(mask, 8)
    boxes = []
    for label in range(1, num_labels):
        x, y, w, h, area = stats[label]
        if area < 240 or w < 180 or h < 18:
            continue
        boxes.append((x, y, x + w, y + h))

    boxes.sort(key=lambda box: (box[1], box[0]))
    return boxes[:14]


def read_known_label(index: int) -> str:
    names = [
        "gold_c5",
        "red_c4",
        "red_c5",
        "green_c1",
        "green_c2",
        "green_c3",
        "green_c4",
        "green_c5",
        "green_c6",
        "human_c1",
        "human_c2",
        "human_c3",
        "human_c4",
        "human_c5",
    ]
    return names[index - 1]


def row_top_for_label(label_top: int) -> int:
    # The AI-clean patch sheet kept the same grid spacing but slightly changed
    # the absolute size. These tops are inferred from the detected label rows.
    if label_top < 420:
        return 18
    if label_top < 760:
        return 358
    if label_top < 1100:
        return 678
    return 997


def get_approved_cell(sheet: Image.Image, index: int) -> Image.Image:
    left, top, right, bottom = get_v1_box(index)
    return sheet.crop((left, top, right, bottom))


def get_v1_box(index: int) -> tuple[int, int, int, int]:
    row = index // 6
    col = index % 6
    return V1_COLS[col], V1_ROWS[row], V1_COLS[col + 1], V1_ROWS[row + 1]


def fit_for_box(source: Image.Image, width: int, height: int) -> Image.Image:
    sprite, _bounds = make_sprite(source, canvas_size=max(width, height), target_occupancy=0.78)
    bbox = sprite.getchannel("A").getbbox()
    if bbox is None:
        return Image.new("RGB", (width, height), (2, 9, 14))

    content = sprite.crop(bbox)
    content.thumbnail((int(width * 0.92), int(height * 0.9)), Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", (width, height), sample_edge_color(source))
    canvas.paste(
        content.convert("RGB"),
        ((width - content.width) // 2, (height - content.height) // 2),
        content.getchannel("A"),
    )
    return canvas


def make_sprite(
    source: Image.Image,
    canvas_size: int = CANVAS_SIZE,
    target_occupancy: float = TARGET_OCCUPANCY,
) -> tuple[Image.Image, tuple[int, int, int, int]]:
    image = source.convert("RGB")
    mask = foreground_mask(image)
    bbox = mask.getbbox()
    if bbox is None:
        bbox = (0, 0, image.width, image.height)

    rgba = image.convert("RGBA")
    alpha = mask.filter(ImageFilter.GaussianBlur(0.55))
    rgba.putalpha(alpha)
    cropped = rgba.crop(bbox)
    cropped = cropped.filter(ImageFilter.UnsharpMask(radius=0.45, percent=112, threshold=2))
    cropped = ImageEnhance.Contrast(cropped).enhance(1.04)

    max_side = max(cropped.width, cropped.height)
    target_side = int(canvas_size * target_occupancy)
    scale = target_side / max_side
    new_size = (
        max(1, round(cropped.width * scale)),
        max(1, round(cropped.height * scale)),
    )
    cropped = cropped.resize(new_size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    canvas.alpha_composite(
        cropped,
        ((canvas_size - cropped.width) // 2, (canvas_size - cropped.height) // 2),
    )
    canvas = remove_horizontal_card_artifacts(canvas)
    canvas = remove_bottom_card_components(canvas)
    canvas = clear_transparent_rgb(canvas)
    return canvas, bbox


def remove_ai_card_lines(image: Image.Image) -> Image.Image:
    rgb = image.convert("RGB")
    arr = np.array(rgb)
    r = arr[:, :, 0]
    g = arr[:, :, 1]
    b = arr[:, :, 2]
    cyan = (r < 45) & (g > 80) & (b > 95) & (b >= g * 0.72)
    bg = np.array(sample_edge_color(rgb), dtype=np.uint8)

    # Remove long UI/card separator lines while keeping small engine glows.
    for y in range(arr.shape[0]):
        if cyan[y].sum() > arr.shape[1] * 0.22:
            y0 = max(0, y - 1)
            y1 = min(arr.shape[0], y + 2)
            arr[y0:y1][cyan[y0:y1]] = bg

    return Image.fromarray(arr, "RGB")


def remove_horizontal_card_artifacts(image: Image.Image) -> Image.Image:
    arr = np.array(image.convert("RGBA"))
    r = arr[:, :, 0]
    g = arr[:, :, 1]
    b = arr[:, :, 2]
    a = arr[:, :, 3]
    cyan = (a > 18) & (r < 70) & (g > 58) & (b > 70) & (b >= g * 0.62)
    h, w = cyan.shape
    for y in range(int(h * 0.72), h):
        if cyan[y].sum() <= w * 0.075 or longest_run(cyan[y]) <= w * 0.18:
            continue

        y0 = max(0, y - 1)
        y1 = min(h, y + 2)
        arr[y0:y1, :, 3] = np.where(cyan[y0:y1], 0, arr[y0:y1, :, 3])
    return Image.fromarray(arr, "RGBA")


def remove_bottom_card_components(image: Image.Image) -> Image.Image:
    arr = np.array(image.convert("RGBA"))
    alpha = arr[:, :, 3]
    candidate = (alpha > 0).astype(np.uint8)
    h, w = candidate.shape
    num_labels, labels, stats, _centroids = cv2.connectedComponentsWithStats(candidate, 8)

    for label in range(1, num_labels):
        x = stats[label, cv2.CC_STAT_LEFT]
        y = stats[label, cv2.CC_STAT_TOP]
        width = stats[label, cv2.CC_STAT_WIDTH]
        height = stats[label, cv2.CC_STAT_HEIGHT]
        area = stats[label, cv2.CC_STAT_AREA]
        bottom_zone = y > h * 0.66
        long_thin = width >= 18 and height <= 7 and width / max(1, height) >= 6
        sparse_strip = width >= 44 and height <= 13 and area <= width * height * 0.42
        tiny_tail = y > h * 0.86 and height <= 5 and area <= 70
        if bottom_zone and (long_thin or sparse_strip or tiny_tail):
            arr[labels == label, 3] = 0

    alpha = arr[:, :, 3]
    for y in range(int(h * 0.78), h):
        row = alpha[y] > 0
        if row.sum() > w * 0.58:
            arr[y, row, 3] = 0

    return Image.fromarray(arr, "RGBA")


def clear_transparent_rgb(image: Image.Image) -> Image.Image:
    arr = np.array(image.convert("RGBA"))
    transparent = arr[:, :, 3] == 0
    arr[transparent] = (0, 0, 0, 0)
    return Image.fromarray(arr, "RGBA")


def detect_exhaust_ports(image: Image.Image) -> list[dict[str, float]]:
    arr = np.array(image.convert("RGBA"))
    alpha = arr[:, :, 3]
    return fallback_exhaust_ports(alpha)


def fallback_exhaust_ports(alpha: np.ndarray) -> list[dict[str, float]]:
    visible = alpha > 20
    if not visible.any():
        return [
            {"x": -25.0, "y": 94.0, "radius": 11.0},
            {"x": 25.0, "y": 94.0, "radius": 11.0},
        ]

    ys, xs = np.where(visible)
    bottom = int(ys.max())
    zone = visible & (np.indices(alpha.shape)[0] >= bottom - 32)
    column_strength = zone.sum(axis=0)
    active_columns = column_strength >= 2
    runs: list[tuple[int, int]] = []
    start: int | None = None
    for x, active in enumerate(active_columns):
        if active and start is None:
            start = x
        elif not active and start is not None:
            if x - start >= 5:
                runs.append((start, x - 1))
            start = None
    if start is not None and len(active_columns) - start >= 5:
        runs.append((start, len(active_columns) - 1))

    if not runs:
        x_center = float(np.median(xs))
        return [{"x": round(x_center - CANVAS_SIZE * 0.5, 2), "y": round(bottom - CANVAS_SIZE * 0.5 + 4.0, 2), "radius": 11.0}]

    ports = []
    for left, right in runs[:5]:
        local = zone[:, left : right + 1]
        local_ys, local_xs = np.where(local)
        if len(local_xs) == 0:
            continue
        cx = left + float(np.mean(local_xs))
        cy = float(np.percentile(local_ys, 92))
        radius = float(np.clip((right - left + 1) * 0.22, 6.0, 15.0))
        ports.append({"x": round(cx - CANVAS_SIZE * 0.5, 2), "y": round(cy - CANVAS_SIZE * 0.5 + 4.0, 2), "radius": round(radius, 2)})

    return ports or [{"x": 0.0, "y": 96.0, "radius": 12.0}]


def longest_run(row: np.ndarray) -> int:
    longest = 0
    current = 0
    for value in row:
        if value:
            current += 1
            longest = max(longest, current)
        else:
            current = 0
    return longest


def foreground_mask(image: Image.Image) -> Image.Image:
    arr = np.array(image)
    bg = np.array(sample_edge_color(image), dtype=np.int16)
    diff = np.abs(arr.astype(np.int16) - bg).sum(axis=2)
    bright = arr.max(axis=2)
    mask = ((diff > 26) | (bright > 34)).astype(np.uint8) * 255

    kernel = np.ones((5, 5), np.uint8)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel, iterations=2)
    mask = cv2.dilate(mask, kernel, iterations=1)

    flood = mask.copy()
    h, w = flood.shape
    flood_mask = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(flood, flood_mask, (0, 0), 255)
    holes = cv2.bitwise_not(flood)
    mask = cv2.bitwise_or(mask, holes)

    num_labels, labels, stats, _centroids = cv2.connectedComponentsWithStats(mask, 8)
    cleaned = np.zeros_like(mask)
    for label in range(1, num_labels):
        area = stats[label, cv2.CC_STAT_AREA]
        if area >= 32:
            cleaned[labels == label] = 255

    return Image.fromarray(cleaned, "L")


def sample_edge_color(image: Image.Image) -> tuple[int, int, int]:
    arr = np.array(image.convert("RGB"))
    edge = np.concatenate([arr[:4].reshape(-1, 3), arr[-4:].reshape(-1, 3), arr[:, :4].reshape(-1, 3), arr[:, -4:].reshape(-1, 3)])
    return tuple(int(v) for v in np.median(edge, axis=0))


def parse_ship_name(name: str) -> tuple[str, str]:
    match = re.match(r"^2([A-Za-z]+)([A-Z])$", name)
    if not match:
        return "Unknown", "Unknown"

    return match.group(1), match.group(2)


if __name__ == "__main__":
    main()
