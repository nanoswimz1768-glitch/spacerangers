#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable

import numpy as np
from PIL import Image, ImageEnhance, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
CATALOG_PATH = ROOT / "tools" / "star_system_catalog.json"
BASE_BACKGROUND_TILE_PATH = ROOT / "game" / "assets" / "backgrounds" / "space_nebula_tile.png"
SOL_FRAME_SOURCE_DIR = ROOT / "game" / "assets" / "backgrounds" / "sun"

STAR_SOURCE_DIR = ROOT / "game" / "assets" / "generated" / "star_sources_4k"
PLANET_SOURCE_DIR = ROOT / "game" / "assets" / "generated" / "planet_sources_4k"
BACKGROUND_SOURCE_DIR = ROOT / "game" / "assets" / "generated" / "background_sources_4k"

STAR_OUTPUT_DIR = ROOT / "game" / "assets" / "generated" / "stars"
STAR_FRAME_OUTPUT_ROOT = ROOT / "game" / "assets" / "generated" / "star_frames"
STAR_EXPERIMENTAL_FRAME_OUTPUT_ROOT = ROOT / "game" / "assets" / "generated" / "star_frames_experimental"
PLANET_OUTPUT_DIR = ROOT / "game" / "assets" / "generated" / "planet_surfaces"
BACKGROUND_OUTPUT_DIR = ROOT / "game" / "assets" / "generated" / "background_tiles"

STAR_RES_ROOT = "res://assets/generated/stars"
STAR_FRAME_RES_ROOT = "res://assets/generated/star_frames"
STAR_EXPERIMENTAL_FRAME_RES_ROOT = "res://assets/generated/star_frames_experimental"
PLANET_SOURCE_RES_ROOT = "res://assets/generated/planet_sources_4k"
PLANET_OUTPUT_RES_ROOT = "res://assets/generated/planet_surfaces"
BACKGROUND_RES_ROOT = "res://assets/generated/background_tiles"

STAR_TARGET_SIZE = 2048
STAR_FRAME_SIZE = 1024
STAR_FRAME_COUNT = 96
STAR_FRAME_PREFIX = "sun_"
PLANET_TARGET_SIZE = (4096, 2048)
BACKGROUND_TARGET_SIZE = 4096

STAR_MIN_SHARPNESS = 24.0
PLANET_MIN_SHARPNESS = 18.0
BACKGROUND_MIN_SHARPNESS = 8.5
BACKGROUND_DIRECT_SOURCE_MIN_SHARPNESS = 1.5
STAR_MIN_EDGE_MARGIN_RATIO = 0.045
PLANET_MAX_SEAM_DELTA = 0.075
BACKGROUND_MAX_SEAM_DELTA = 0.090


@dataclass(frozen=True)
class ProcessedAsset:
    kind: str
    archetype: str
    variant_id: str
    source_path: Path
    output_path: Path
    width: int
    height: int
    sharpness: float
    seam_delta: float | None
    status: str
    notes: tuple[str, ...]
    source_label: str = ""


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Process direct high-resolution imagegen outputs and optionally register them in the generated star-system catalog."
    )
    parser.add_argument("--update-catalog", action="store_true", help="Register processed high-res assets in tools/star_system_catalog.json.")
    parser.add_argument(
        "--replace-lowres",
        action="store_true",
        help="When registering high-res planet maps, remove old generated *_sheet01 surface maps for the same archetypes.",
    )
    parser.add_argument(
        "--replace-backgrounds",
        action="store_true",
        help="Use processed direct imagegen background tiles as active backdrop texture sets.",
    )
    parser.add_argument("--report", default=str(ROOT / "tools" / "generated" / "highres_asset_report.json"), help="Validation report path.")
    parser.add_argument("--planet-width", type=int, default=PLANET_TARGET_SIZE[0], help="Processed planet map width.")
    parser.add_argument("--planet-height", type=int, default=PLANET_TARGET_SIZE[1], help="Processed planet map height.")
    parser.add_argument("--background-size", type=int, default=BACKGROUND_TARGET_SIZE, help="Processed square background tile size.")
    parser.add_argument(
        "--background-mode",
        choices=("sol-style", "direct-source"),
        default="direct-source",
        help="Background processing mode. direct-source is the current baseline; sol-style is the legacy Sol-tile translation path.",
    )
    parser.add_argument(
        "--only-backgrounds",
        action="store_true",
        help="Process/register only background sources. Useful when iterating on space backdrop tiles.",
    )
    parser.add_argument("--star-size", type=int, default=STAR_TARGET_SIZE, help="Processed square star source size.")
    parser.add_argument(
        "--write-experimental-star-frames",
        action="store_true",
        help="Also write opt-in experimental star frame sequences that preserve more direct high-res source character.",
    )
    parser.add_argument(
        "--experimental-star-frames-only",
        action="store_true",
        help="Only write opt-in experimental star frame sequences; do not update catalog or process planets/backgrounds.",
    )
    args = parser.parse_args()

    ensure_source_dirs()
    STAR_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    STAR_FRAME_OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    if args.write_experimental_star_frames or args.experimental_star_frames_only:
        STAR_EXPERIMENTAL_FRAME_OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    PLANET_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    BACKGROUND_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    catalog = load_json(CATALOG_PATH)
    if args.experimental_star_frames_only:
        written = write_experimental_star_frames(catalog, args.star_size)
        print(f"Wrote experimental star frames for {len(written)} variants into {STAR_EXPERIMENTAL_FRAME_OUTPUT_ROOT}")
        return

    assets: list[ProcessedAsset] = []
    if not args.only_backgrounds:
        assets.extend(process_stars(catalog, args.star_size, write_experimental_frames=args.write_experimental_star_frames))
        assets.extend(process_planets(catalog, (args.planet_width, args.planet_height)))
    assets.extend(process_backgrounds(catalog, args.background_size, args.background_mode))

    report_path = Path(args.report)
    write_report(report_path, assets)
    print_report_summary(assets, report_path)

    failed = [asset for asset in assets if asset.status == "fail"]
    if failed:
        raise SystemExit(f"{len(failed)} high-res assets failed validation; catalog was not updated.")

    if args.update_catalog:
        register_assets(catalog, assets, replace_lowres=args.replace_lowres, replace_backgrounds=args.replace_backgrounds)
        write_json(CATALOG_PATH, catalog)
        print(f"Updated {CATALOG_PATH}")


def ensure_source_dirs() -> None:
    for path in (STAR_SOURCE_DIR, PLANET_SOURCE_DIR, BACKGROUND_SOURCE_DIR):
        path.mkdir(parents=True, exist_ok=True)


def process_stars(catalog: dict[str, Any], target_size: int, write_experimental_frames: bool = False) -> list[ProcessedAsset]:
    valid_archetypes = {star["id"] for star in catalog.get("starArchetypes", [])}
    assets: list[ProcessedAsset] = []
    for source_path in png_files(STAR_SOURCE_DIR):
        archetype = archetype_from_stem(source_path.stem, valid_archetypes)
        if archetype is None:
            assets.append(failed_asset("star", source_path, "unknown", "Unknown star archetype in filename."))
            continue

        image = prepare_star_source_image(source_path, target_size)
        output_path = STAR_OUTPUT_DIR / f"{source_path.stem}.png"
        image.save(output_path, compress_level=6)
        write_star_animation_frames(image, STAR_FRAME_OUTPUT_ROOT / source_path.stem)
        if write_experimental_frames:
            write_experimental_star_animation_frames(image, STAR_EXPERIMENTAL_FRAME_OUTPUT_ROOT / source_path.stem)
        assets.append(validate_asset("star", archetype, source_path, output_path, image, None))

    return assets


def write_experimental_star_frames(catalog: dict[str, Any], target_size: int) -> list[str]:
    valid_archetypes = {star["id"] for star in catalog.get("starArchetypes", [])}
    written: list[str] = []
    for source_path in png_files(STAR_SOURCE_DIR):
        if archetype_from_stem(source_path.stem, valid_archetypes) is None:
            continue

        image = prepare_star_source_image(source_path, target_size)
        write_experimental_star_animation_frames(image, STAR_EXPERIMENTAL_FRAME_OUTPUT_ROOT / source_path.stem)
        written.append(source_path.stem)
    return written


def prepare_star_source_image(source_path: Path, target_size: int) -> Image.Image:
    image = Image.open(source_path).convert("RGBA")
    image = crop_transparent_or_dark_bounds(image)
    image = pad_to_square(image)
    image = image.resize((target_size, target_size), Image.Resampling.LANCZOS)
    image = remove_dark_background(image)
    return image.filter(ImageFilter.UnsharpMask(radius=0.65, percent=90, threshold=2))


def process_planets(catalog: dict[str, Any], target_size: tuple[int, int]) -> list[ProcessedAsset]:
    valid_archetypes = {planet["id"] for planet in catalog.get("planetArchetypes", [])}
    assets: list[ProcessedAsset] = []
    for source_path in png_files(PLANET_SOURCE_DIR):
        archetype = archetype_from_stem(source_path.stem, valid_archetypes)
        if archetype is None:
            assets.append(failed_asset("planet", source_path, "unknown", "Unknown planet archetype in filename."))
            continue

        image = Image.open(source_path).convert("RGB")
        image = crop_to_aspect(image, target_size[0] / target_size[1])
        image = image.resize(target_size, Image.Resampling.LANCZOS)
        image = make_horizontal_seamless(image, max(96, target_size[0] // 14))
        image = ImageEnhance.Contrast(image).enhance(1.055)
        image = ImageEnhance.Color(image).enhance(1.025)
        image = image.filter(ImageFilter.UnsharpMask(radius=0.72, percent=135, threshold=2))

        output_path = PLANET_OUTPUT_DIR / f"{source_path.stem}.png"
        image.save(output_path, compress_level=6)
        seam = horizontal_seam_delta(image)
        assets.append(validate_asset("planet", archetype, source_path, output_path, image, seam))

    return assets


def process_backgrounds(catalog: dict[str, Any], target_size: int, mode: str = "direct-source") -> list[ProcessedAsset]:
    valid_archetypes = {backdrop["id"] for backdrop in catalog.get("spaceBackdropArchetypes", [])}
    assets: list[ProcessedAsset] = []
    for source_path in png_files(BACKGROUND_SOURCE_DIR):
        archetype = archetype_from_stem(source_path.stem, valid_archetypes)
        if archetype is None:
            assets.append(failed_asset("background", source_path, "unknown", "Unknown background archetype in filename."))
            continue

        source = Image.open(source_path).convert("RGB")
        if mode == "direct-source":
            image = create_direct_source_background_tile(source, source_path.stem, target_size)
            source_label = "imagegen_direct_highres_tile"
        else:
            image = create_sol_style_background_tile(source, source_path.stem, target_size)
            source_label = "imagegen_solstyle_highres_tile"

        output_path = BACKGROUND_OUTPUT_DIR / f"{source_path.stem}_tile.png"
        image.save(output_path, compress_level=6)
        seam = max(horizontal_seam_delta(image), vertical_seam_delta(image))
        assets.append(validate_asset("background", archetype, source_path, output_path, image, seam, source_label=source_label))

    return assets


def create_sol_style_background_tile(source: Image.Image, variant_id: str, target_size: int) -> Image.Image:
    """Transfer source nebula color and broad dust shapes into Sol's tiled texture language."""
    base = Image.open(BASE_BACKGROUND_TILE_PATH).convert("RGB")
    if base.size != (target_size, target_size):
        base = base.resize((target_size, target_size), Image.Resampling.LANCZOS)

    base_arr = deterministic_tile_transform(np.asarray(base).astype(np.float32) / 255.0, variant_id)
    source_square = crop_to_aspect(source, 1.0).resize((target_size, target_size), Image.Resampling.LANCZOS)
    low_frequency = source_square.filter(ImageFilter.GaussianBlur(radius=max(24, target_size // 26)))
    source_arr = np.asarray(low_frequency).astype(np.float32) / 255.0

    luminance_weights = np.array([0.299, 0.587, 0.114], dtype=np.float32)
    base_luminance = np.dot(base_arr, luminance_weights)
    source_luminance = np.dot(source_arr, luminance_weights)
    source_mask = (source_luminance > 0.045) & (source_luminance < 0.78)
    if np.any(source_mask):
        source_tint = source_arr[source_mask].mean(axis=0)
    else:
        source_tint = source_arr.reshape(-1, 3).mean(axis=0)

    source_tint = normalize_color(source_tint, fallback=np.array([0.56, 0.70, 0.88], dtype=np.float32))
    source_chroma = source_arr / np.maximum(source_luminance[:, :, None], 0.045)
    source_chroma = np.clip(source_chroma, 0.22, 2.2)

    base_gray = base_luminance[:, :, None]
    source_colored = base_gray * source_chroma * (0.54 + source_tint[None, None, :] * 0.72)
    global_colored = base_gray * source_tint[None, None, :] * 1.24
    structural_base = base_arr * 0.68 + global_colored * 0.32
    out = structural_base * 0.72 + source_colored * 0.28

    detail = base_arr - base_luminance[:, :, None]
    out += detail * 0.38
    out = (out - 0.5) * 1.08 + 0.5
    out *= 0.78

    # Keep a restrained baked star/glint contribution; runtime layers handle parallax and de-mirroring.
    bright_mask = smoothstep_range(0.58, 0.92, base_luminance)[:, :, None]
    star_glints = np.maximum(base_arr, source_tint[None, None, :] * base_gray * 1.55)
    out = out * (1.0 - bright_mask * 0.22) + star_glints * (bright_mask * 0.22)

    out = np.clip(out, 0.0, 1.0)
    image = Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), mode="RGB")
    image = make_tileable(image, max(128, target_size // 12))
    image = ImageEnhance.Contrast(image).enhance(1.035)
    return image.filter(ImageFilter.UnsharpMask(radius=0.45, percent=45, threshold=4))


def create_direct_source_background_tile(source: Image.Image, variant_id: str, target_size: int) -> Image.Image:
    """Build a native-scale source mosaic tile without fullscreen stretching or mirror synthesis."""
    source_image = ImageEnhance.Contrast(source.convert("RGB")).enhance(1.014)
    source_image = ImageEnhance.Color(source_image).enhance(1.010)
    image = make_source_mosaic_tile(source_image, variant_id, target_size)
    image = ImageEnhance.Contrast(image).enhance(1.018)
    image = ImageEnhance.Color(image).enhance(1.006)
    image = image.filter(ImageFilter.UnsharpMask(radius=0.28, percent=118, threshold=1))
    return make_tileable(image, max(28, target_size // 128))


def make_source_mosaic_tile(source: Image.Image, variant_id: str, target_size: int) -> Image.Image:
    source_arr = np.asarray(source.convert("RGB")).astype(np.float32) / 255.0
    source_height, source_width, _ = source_arr.shape
    seed = int(hashlib.sha256(f"direct-source-mosaic:{variant_id}".encode("utf-8")).hexdigest()[:8], 16)
    rng = np.random.default_rng(seed)

    base_color = source_arr.reshape(-1, 3).mean(axis=0)
    canvas = np.zeros((target_size, target_size, 3), dtype=np.float32)
    weights = np.zeros((target_size, target_size, 1), dtype=np.float32)
    canvas[:, :, :] = base_color[None, None, :] * 0.035
    weights[:, :, :] = 0.035

    broad_source = np.asarray(
        source.filter(ImageFilter.GaussianBlur(radius=max(0.8, min(source_width, source_height) / 300.0)))
    ).astype(np.float32) / 255.0

    quilt_rectangular_source_layer(
        canvas,
        weights,
        broad_source,
        target_size,
        rng,
        step_x=max(820, int(source_width * 0.78)),
        step_y=max(460, int(source_height * 0.78)),
        opacity=0.12,
        feather_x=max(220, int(source_width * 0.18)),
        feather_y=max(130, int(source_height * 0.18)),
        use_organic_mask=True,
        jitter_scale=0.10,
        row_offset_scale=0.42,
    )
    quilt_rectangular_source_layer(
        canvas,
        weights,
        source_arr,
        target_size,
        rng,
        step_x=max(1180, int(source_width * 0.84)),
        step_y=max(660, int(source_height * 0.84)),
        opacity=1.0,
        feather_x=max(150, int(source_width * 0.105)),
        feather_y=max(90, int(source_height * 0.105)),
        use_organic_mask=False,
        jitter_scale=0.035,
        row_offset_scale=0.50,
    )

    out = canvas / np.maximum(weights, 0.001)
    out = match_source_statistics(source_arr, out, strength=0.20)
    image = array_to_rgb(out)
    return make_tileable(image, max(48, target_size // 96))


def quilt_rectangular_source_layer(
    canvas: np.ndarray,
    weights: np.ndarray,
    source_arr: np.ndarray,
    target_size: int,
    rng: np.random.Generator,
    step_x: int,
    step_y: int,
    opacity: float,
    feather_x: int,
    feather_y: int,
    use_organic_mask: bool,
    jitter_scale: float,
    row_offset_scale: float,
) -> None:
    if step_x <= 0 or step_y <= 0:
        return

    patch_height, patch_width, _ = source_arr.shape
    base_mask = rectangular_feather_mask(patch_height, patch_width, feather_y, feather_x)[:, :, None] * opacity
    columns = int(math.ceil(target_size / step_x)) + 3
    rows = int(math.ceil(target_size / step_y)) + 3
    jitter_x = max(4, int(step_x * jitter_scale))
    jitter_y = max(4, int(step_y * jitter_scale))

    for row in range(rows):
        for column in range(columns):
            row_offset = int((row % 2) * step_x * row_offset_scale)
            x = column * step_x - patch_width // 2 + row_offset + int(rng.integers(-jitter_x, jitter_x + 1))
            y = row * step_y - patch_height // 2 + int(rng.integers(-jitter_y, jitter_y + 1))
            patch = source_arr
            mask = base_mask
            if use_organic_mask:
                mask = base_mask * organic_patch_mask(patch_height, patch_width, rng)[:, :, None]
            paste_wrapped_patch(canvas, weights, patch, mask, x, y, target_size)


def rectangular_feather_mask(height: int, width: int, feather_y: int, feather_x: int) -> np.ndarray:
    yy, xx = np.mgrid[0:height, 0:width].astype(np.float32)
    distance_x = np.minimum(xx, width - 1 - xx)
    distance_y = np.minimum(yy, height - 1 - yy)
    mask_x = smoothstep_range(0.0, max(1.0, float(feather_x)), distance_x)
    mask_y = smoothstep_range(0.0, max(1.0, float(feather_y)), distance_y)
    mask = np.minimum(mask_x, mask_y)
    return np.clip(mask, 0.0, 1.0).astype(np.float32)


def organic_patch_mask(height: int, width: int, rng: np.random.Generator) -> np.ndarray:
    grid_width = max(5, width // 180)
    grid_height = max(5, height // 180)
    noise = rng.random((grid_height, grid_width), dtype=np.float32)
    image = Image.fromarray((noise * 255.0 + 0.5).astype(np.uint8), mode="L")
    image = image.resize((width, height), Image.Resampling.BICUBIC)
    smooth = np.asarray(image).astype(np.float32) / 255.0
    smooth = smoothstep_range(0.18, 0.88, smooth)
    return np.clip(0.22 + smooth * 0.78, 0.0, 1.0).astype(np.float32)


def paste_wrapped_patch(
    canvas: np.ndarray,
    weights: np.ndarray,
    patch: np.ndarray,
    mask: np.ndarray,
    x: int,
    y: int,
    target_size: int,
) -> None:
    patch_height, patch_width, _ = patch.shape
    start_x = x % target_size
    start_y = y % target_size
    for patch_y, canvas_y, height in wrapped_ranges(start_y, patch_height, target_size):
        for patch_x, canvas_x, width in wrapped_ranges(start_x, patch_width, target_size):
            patch_slice = patch[patch_y:patch_y + height, patch_x:patch_x + width, :]
            mask_slice = mask[patch_y:patch_y + height, patch_x:patch_x + width, :]
            canvas[canvas_y:canvas_y + height, canvas_x:canvas_x + width, :] += patch_slice * mask_slice
            weights[canvas_y:canvas_y + height, canvas_x:canvas_x + width, :] += mask_slice


def wrapped_ranges(start: int, length: int, limit: int) -> list[tuple[int, int, int]]:
    ranges: list[tuple[int, int, int]] = []
    consumed = 0
    target = start
    while consumed < length:
        chunk = min(length - consumed, limit - target)
        ranges.append((consumed, target, chunk))
        consumed += chunk
        target = 0
    return ranges


def match_source_statistics(source: np.ndarray, tile: np.ndarray, strength: float) -> np.ndarray:
    source_mean = source.reshape(-1, 3).mean(axis=0)
    source_std = source.reshape(-1, 3).std(axis=0)
    tile_mean = tile.reshape(-1, 3).mean(axis=0)
    tile_std = np.maximum(tile.reshape(-1, 3).std(axis=0), 0.001)
    matched = (tile - tile_mean[None, None, :]) * (source_std / tile_std)[None, None, :] + source_mean[None, None, :]
    return np.clip(tile * (1.0 - strength) + matched * strength, 0.0, 1.0)


def normalize_color(color: np.ndarray, fallback: np.ndarray) -> np.ndarray:
    if not np.all(np.isfinite(color)) or float(color.max()) <= 0.001:
        return fallback

    color = np.clip(color, 0.0, 1.0)
    luminance = float(np.dot(color, np.array([0.299, 0.587, 0.114], dtype=np.float32)))
    if luminance <= 0.001:
        return fallback

    normalized = color / luminance * 0.42
    return np.clip(normalized, 0.08, 1.0)


def deterministic_tile_transform(arr: np.ndarray, variant_id: str) -> np.ndarray:
    seed = int(hashlib.sha256(variant_id.encode("utf-8")).hexdigest()[:8], 16)
    result = np.roll(arr, shift=seed % arr.shape[0], axis=0)
    result = np.roll(result, shift=(seed >> 8) % arr.shape[1], axis=1)
    if seed & 1:
        result = result[:, ::-1, :]
    if seed & 2:
        result = result[::-1, :, :]
    if seed & 4:
        result = np.rot90(result, 2)
    return np.ascontiguousarray(result)


def smoothstep_range(edge0: float, edge1: float, value: np.ndarray) -> np.ndarray:
    t = np.clip((value - edge0) / max(0.0001, edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def png_files(path: Path) -> list[Path]:
    return sorted(item for item in path.glob("*.png") if item.is_file())


def archetype_from_stem(stem: str, valid_archetypes: set[str]) -> str | None:
    candidates = [stem]
    candidates.append(re.sub(r"_(?:v|variant)?\d+$", "", stem))
    candidates.append(re.sub(r"_sheet\d+$", "", stem))
    candidates.append(re.sub(r"_(?:source|highres|4k)$", "", stem))
    for candidate in candidates:
        if candidate in valid_archetypes:
            return candidate

    parts = stem.split("_")
    for end in range(len(parts), 0, -1):
        candidate = "_".join(parts[:end])
        if candidate in valid_archetypes:
            return candidate

    return None


def crop_to_aspect(image: Image.Image, target_aspect: float) -> Image.Image:
    width, height = image.size
    current = width / max(1, height)
    if math.isclose(current, target_aspect, rel_tol=0.005, abs_tol=0.005):
        return image

    if current > target_aspect:
        new_width = round(height * target_aspect)
        left = (width - new_width) // 2
        return image.crop((left, 0, left + new_width, height))

    new_height = round(width / target_aspect)
    top = (height - new_height) // 2
    return image.crop((0, top, width, top + new_height))


def crop_transparent_or_dark_bounds(image: Image.Image) -> Image.Image:
    arr = np.asarray(image).astype(np.float32)
    alpha = arr[:, :, 3] / 255.0
    brightness = arr[:, :, :3].max(axis=2) / 255.0
    mask = (alpha > 0.04) & (brightness > 0.018)
    if not np.any(mask):
        return image

    ys, xs = np.where(mask)
    pad_x = max(16, int((xs.max() - xs.min() + 1) * 0.08))
    pad_y = max(16, int((ys.max() - ys.min() + 1) * 0.08))
    left = max(0, int(xs.min()) - pad_x)
    right = min(image.width, int(xs.max()) + pad_x + 1)
    top = max(0, int(ys.min()) - pad_y)
    bottom = min(image.height, int(ys.max()) + pad_y + 1)
    return image.crop((left, top, right, bottom))


def pad_to_square(image: Image.Image) -> Image.Image:
    side = max(image.width, image.height)
    result = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    result.alpha_composite(image, ((side - image.width) // 2, (side - image.height) // 2))
    return result


def remove_dark_background(image: Image.Image) -> Image.Image:
    arr = np.asarray(image).astype(np.float32)
    rgb = arr[:, :, :3]
    existing_alpha = arr[:, :, 3:4] / 255.0
    brightness = rgb.max(axis=2, keepdims=True)
    alpha = np.clip((brightness - 5.0) / 30.0, 0.0, 1.0) * existing_alpha
    out = np.concatenate([rgb, alpha * 255.0], axis=2)
    return Image.fromarray(np.clip(out + 0.5, 0, 255).astype(np.uint8), mode="RGBA")


def write_star_animation_frames(source: Image.Image, out_dir: Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    for old_frame in out_dir.glob(f"{STAR_FRAME_PREFIX}*.png"):
        frame_id = old_frame.stem.removeprefix(STAR_FRAME_PREFIX)
        if frame_id.isdigit():
            old_frame.unlink()

    variant_id = out_dir.name
    reference_frames = load_reference_sun_frames()
    base_image = source.convert("RGBA").resize((STAR_FRAME_SIZE, STAR_FRAME_SIZE), Image.Resampling.LANCZOS)
    base = np.asarray(base_image).astype(np.float32)
    source_rgb = base[:, :, :3].copy()
    source_alpha = base[:, :, 3].copy() / 255.0
    source_luma = np.dot(source_rgb / 255.0, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    reference_shape = (
        deterministic_star_frame_transform(reference_frames[0], variant_id)[:, :, 3] / 255.0
        if reference_frames
        else source_alpha
    )
    visible_weight = smoothstep_range(0.025, 0.20, reference_shape)

    broad = np.asarray(base_image.convert("RGB").filter(ImageFilter.GaussianBlur(radius=5.5))).astype(np.float32)
    detail = source_rgb - np.asarray(base_image.convert("RGB").filter(ImageFilter.GaussianBlur(radius=1.25))).astype(np.float32)

    yy, xx = np.mgrid[0:STAR_FRAME_SIZE, 0:STAR_FRAME_SIZE].astype(np.float32)
    nx = (xx - STAR_FRAME_SIZE * 0.5) / (STAR_FRAME_SIZE * 0.5)
    ny = (yy - STAR_FRAME_SIZE * 0.5) / (STAR_FRAME_SIZE * 0.5)
    radius = np.sqrt(nx * nx + ny * ny)
    theta = np.arctan2(ny, nx)
    surface_weight = 1.0 - smoothstep_range(0.80, 0.98, radius)
    disk_weight = visible_weight * (1.0 - smoothstep_range(0.94, 1.08, radius))
    rim_window = visible_weight * smoothstep_range(0.70, 0.86, radius) * (1.0 - smoothstep_range(0.98, 1.08, radius))
    alpha_window = np.clip(reference_shape * (0.22 + surface_weight * 0.74) + rim_window * 0.14, 0.0, 1.0)

    bright_pixels = source_rgb[(source_alpha > 0.04) & (source_luma > 0.025)]
    if bright_pixels.size:
        dark_color = np.percentile(bright_pixels, 18, axis=0).astype(np.float32)
        mean_color = np.percentile(bright_pixels, 66, axis=0).astype(np.float32)
        bright_color = np.percentile(bright_pixels, 95, axis=0).astype(np.float32)
    else:
        dark_color = np.array([96.0, 18.0, 2.0], dtype=np.float32)
        mean_color = np.array([255.0, 92.0, 16.0], dtype=np.float32)
        bright_color = np.array([255.0, 190.0, 42.0], dtype=np.float32)

    normalized_hot = normalize_color(bright_color / 255.0, fallback=np.array([1.0, 0.48, 0.10], dtype=np.float32)) * 255.0
    dark_color = np.clip(dark_color * 0.34 + mean_color * 0.10, 0.0, 255.0)
    mid_color = np.clip(mean_color * 1.05 + bright_color * 0.10, 0.0, 255.0)
    hot_color = np.clip(normalized_hot * 1.20 + bright_color * 0.42 + 18.0, 0.0, 255.0)
    flare_color = np.clip(hot_color * 1.04 + mean_color * 0.18, 0.0, 255.0)
    white_hot = np.clip(hot_color * 0.68 + np.array([255.0, 248.0, 188.0], dtype=np.float32) * 0.42, 0.0, 255.0)

    for frame in range(STAR_FRAME_COUNT):
        phase = frame / STAR_FRAME_COUNT * math.tau
        reference = reference_frames[frame % len(reference_frames)] if reference_frames else base
        reference = deterministic_star_frame_transform(reference, variant_id)
        reference_rgb = reference[:, :, :3] / 255.0
        reference_alpha = reference[:, :, 3] / 255.0
        reference_energy = np.clip(np.dot(reference_rgb, np.array([0.299, 0.587, 0.114], dtype=np.float32)) * 1.12, 0.0, 1.0)

        streams = (
            np.sin(theta * 8.4 + radius * 24.0 - phase)
            + np.sin(theta * -5.1 + radius * 38.0 + phase * 2.0)
            + np.sin((nx * 0.78 - ny * 1.22) * 30.0 + np.sin(theta * 4.0 + phase * 2.0) * 1.45 + phase * 3.0)
            + np.sin((nx * 1.32 + ny * 0.64) * 20.0 - phase * 2.0)
        ) / 4.0
        hot = np.maximum(streams, 0.0) ** 1.45
        cool = np.maximum(-streams, 0.0) ** 1.35
        radial_pulse = 0.5 + 0.5 * np.sin(radius * 23.0 - phase * 2.0)

        detail_x = int(round((math.sin(phase) * 5.0) + (math.sin(phase * 2.0 + 1.2) * 3.0)))
        detail_y = int(round((math.cos(phase) * 4.0) + (math.cos(phase * 3.0 + 0.4) * 3.0)))
        shifted_detail = np.roll(detail, shift=(detail_y, detail_x), axis=(0, 1))

        broad_luma = np.dot(broad / 255.0, np.array([0.299, 0.587, 0.114], dtype=np.float32))
        source_modulation = np.clip(0.74 + broad_luma * 0.54 + source_luma * 0.18, 0.70, 1.35)
        energy = np.clip(
            reference_energy * (0.16 + surface_weight * 0.84)
            + hot * (0.23 + radial_pulse * 0.07) * disk_weight
            - cool * 0.08 * disk_weight
            + source_luma * 0.10 * disk_weight,
            0.0,
            1.0,
        )

        mid_mix = smoothstep_range(0.08, 0.72, energy)[:, :, None]
        hot_mix = smoothstep_range(0.56, 0.90, energy)[:, :, None]
        white_mix = smoothstep_range(0.82, 0.995, energy)[:, :, None]
        frame_rgb = dark_color[None, None, :] * (1.0 - mid_mix) + mid_color[None, None, :] * mid_mix
        frame_rgb = frame_rgb * (1.0 - hot_mix) + hot_color[None, None, :] * hot_mix
        frame_rgb = frame_rgb * (1.0 - white_mix * surface_weight[:, :, None]) + white_hot[None, None, :] * (white_mix * surface_weight[:, :, None])
        frame_rgb *= source_modulation[:, :, None]
        frame_rgb += shifted_detail * (0.20 + hot[:, :, None] * 0.18) * disk_weight[:, :, None]
        frame_rgb += hot_color[None, None, :] * (hot * radial_pulse * 0.12 * disk_weight)[:, :, None]
        corona_rgb = (dark_color * 0.25 + mid_color * 0.75)[None, None, :] * (0.10 + reference_energy[:, :, None] * 0.34)
        frame_rgb = frame_rgb * surface_weight[:, :, None] + corona_rgb * (1.0 - surface_weight[:, :, None])

        tongues = np.zeros_like(radius)
        for tongue in range(7):
            base_angle = tongue / 7.0 * math.tau + (tongue % 3) * 0.17
            angle_now = base_angle + 0.24 * math.sin(phase + tongue * 1.21)
            width = 0.030 + 0.026 * (0.5 + 0.5 * math.sin(tongue * 2.03))
            reach = 0.032 + 0.052 * (0.5 + 0.5 * math.sin(phase * 2.0 + tongue * 1.47))
            angular = np.exp(-((angle_delta(theta, angle_now) / width) ** 2))
            radial_attach = smoothstep_range(0.78, 0.86, radius)
            radial_fade = 1.0 - smoothstep_range(0.90 + reach * 0.45, 1.00 + reach * 0.85, radius)
            radial = radial_attach * radial_fade
            filament = 0.66 + 0.34 * np.sin(radius * 42.0 + phase * 2.0 + tongue * 1.73)
            tongues += angular * radial * filament * (0.12 + 0.24 * math.sin(phase * 3.0 + tongue * 0.81) ** 2)

        tongues = np.clip(tongues, 0.0, 0.52) * np.maximum(rim_window, smoothstep_range(0.82, 0.93, radius) * (1.0 - smoothstep_range(0.99, 1.10, radius)))
        flame_mix = np.clip(tongues[:, :, None] * 0.32, 0.0, 0.32)
        frame_rgb = frame_rgb * (1.0 - flame_mix) + flare_color[None, None, :] * flame_mix
        dynamic_alpha = np.maximum(reference_alpha * (0.18 + surface_weight * 0.74), tongues * 0.55)
        frame_alpha = np.maximum(alpha_window, dynamic_alpha)

        out = np.dstack((np.clip(frame_rgb, 0.0, 255.0), np.clip(frame_alpha * 255.0, 0.0, 255.0)))
        image = Image.fromarray((out + 0.5).astype(np.uint8), mode="RGBA")
        image = image.filter(ImageFilter.UnsharpMask(radius=0.32, percent=92, threshold=2))
        image.save(out_dir / f"{STAR_FRAME_PREFIX}{frame:02d}.png", compress_level=6)


def write_experimental_star_animation_frames(source: Image.Image, out_dir: Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    for old_frame in out_dir.glob(f"{STAR_FRAME_PREFIX}*.png"):
        frame_id = old_frame.stem.removeprefix(STAR_FRAME_PREFIX)
        if frame_id.isdigit():
            old_frame.unlink()

    variant_id = f"{out_dir.name}:experimental"
    reference_frames = load_reference_sun_frames()
    base_image = source.convert("RGBA").resize((STAR_FRAME_SIZE, STAR_FRAME_SIZE), Image.Resampling.LANCZOS)
    base = np.asarray(base_image).astype(np.float32)
    source_rgb = base[:, :, :3].copy()
    source_alpha = base[:, :, 3].copy() / 255.0
    luminance_weights = np.array([0.299, 0.587, 0.114], dtype=np.float32)
    source_luma = np.dot(source_rgb / 255.0, luminance_weights)

    soft_rgb = np.asarray(base_image.convert("RGB").filter(ImageFilter.GaussianBlur(radius=1.15))).astype(np.float32)
    broad_rgb = np.asarray(base_image.convert("RGB").filter(ImageFilter.GaussianBlur(radius=6.5))).astype(np.float32)
    extra_broad_rgb = np.asarray(base_image.convert("RGB").filter(ImageFilter.GaussianBlur(radius=22.0))).astype(np.float32)
    detail = source_rgb - soft_rgb
    macro_detail = broad_rgb - extra_broad_rgb
    broad_luma = np.dot(broad_rgb / 255.0, luminance_weights)

    reference_shape = (
        deterministic_star_frame_transform(reference_frames[0], variant_id)[:, :, 3] / 255.0
        if reference_frames
        else source_alpha
    )

    yy, xx = np.mgrid[0:STAR_FRAME_SIZE, 0:STAR_FRAME_SIZE].astype(np.float32)
    nx = (xx - STAR_FRAME_SIZE * 0.5) / (STAR_FRAME_SIZE * 0.5)
    ny = (yy - STAR_FRAME_SIZE * 0.5) / (STAR_FRAME_SIZE * 0.5)
    radius = np.sqrt(nx * nx + ny * ny)
    theta = np.arctan2(ny, nx)

    source_shape = smoothstep_range(0.020, 0.17, source_alpha)
    solar_shape = smoothstep_range(0.015, 0.18, reference_shape)
    surface_weight = 1.0 - smoothstep_range(0.80, 0.99, radius)
    disk_weight = np.maximum(source_shape * 0.92, solar_shape * 0.38) * (1.0 - smoothstep_range(0.96, 1.10, radius))
    rim_window = np.maximum(source_shape * 0.72, solar_shape * 0.54) * smoothstep_range(0.71, 0.88, radius) * (1.0 - smoothstep_range(1.00, 1.12, radius))
    alpha_window = np.clip(
        np.maximum(source_shape * (0.24 + surface_weight * 0.72), solar_shape * (0.12 + surface_weight * 0.50))
        + rim_window * 0.14,
        0.0,
        1.0,
    )

    bright_pixels = source_rgb[(source_alpha > 0.04) & (source_luma > 0.025)]
    if bright_pixels.size:
        dark_color = np.percentile(bright_pixels, 14, axis=0).astype(np.float32)
        mean_color = np.percentile(bright_pixels, 58, axis=0).astype(np.float32)
        bright_color = np.percentile(bright_pixels, 95, axis=0).astype(np.float32)
    else:
        dark_color = np.array([82.0, 18.0, 4.0], dtype=np.float32)
        mean_color = np.array([228.0, 92.0, 20.0], dtype=np.float32)
        bright_color = np.array([255.0, 188.0, 54.0], dtype=np.float32)

    tint_color = normalize_color(mean_color / 255.0, fallback=np.array([1.0, 0.42, 0.09], dtype=np.float32))
    hot_tint = normalize_color(bright_color / 255.0, fallback=np.array([1.0, 0.55, 0.14], dtype=np.float32))
    dark_color = np.clip(dark_color * 0.62 + mean_color * 0.08, 0.0, 255.0)
    mid_color = np.clip(mean_color * 1.05 + bright_color * 0.08, 0.0, 255.0)
    hot_color = np.clip(bright_color * 0.92 + hot_tint * 255.0 * 0.30 + 12.0, 0.0, 255.0)
    flare_color = np.clip(hot_color * 1.08 + tint_color * 255.0 * 0.12, 0.0, 255.0)
    white_hot = np.clip(hot_color * 0.56 + np.array([255.0, 252.0, 224.0], dtype=np.float32) * 0.44, 0.0, 255.0)

    source_chroma = source_rgb / np.maximum(source_luma[:, :, None] * 255.0, 18.0)
    source_chroma = np.clip(source_chroma, 0.30, 2.55)

    for frame in range(STAR_FRAME_COUNT):
        phase = frame / STAR_FRAME_COUNT * math.tau
        reference = reference_frames[frame % len(reference_frames)] if reference_frames else base
        reference = deterministic_star_frame_transform(reference, variant_id)
        reference_rgb = reference[:, :, :3] / 255.0
        reference_alpha = reference[:, :, 3] / 255.0
        reference_energy = np.clip(np.dot(reference_rgb, luminance_weights) * 1.05, 0.0, 1.0)

        streams = (
            np.sin(theta * 5.2 + radius * 21.0 + phase)
            + np.sin(theta * -9.0 + radius * 33.0 - phase * 2.0)
            + np.sin((nx * 1.10 + ny * 0.64) * 18.0 + np.sin(theta * 3.0 - phase) * 1.25 + phase * 3.0)
            + np.sin((nx * -0.55 + ny * 1.34) * 27.0 - phase * 2.0)
        ) / 4.0
        hot = np.maximum(streams, 0.0) ** 1.34
        cool = np.maximum(-streams, 0.0) ** 1.26
        radial_pulse = 0.5 + 0.5 * np.sin(radius * 25.0 + phase * 2.0)

        flow_x = int(round(math.sin(phase) * 7.0 + math.sin(phase * 2.0 + 0.7) * 3.0))
        flow_y = int(round(math.cos(phase) * 5.0 + math.cos(phase * 3.0 + 0.3) * 3.0))
        counter_x = int(round(math.sin(phase * 2.0 + 1.4) * 4.0))
        counter_y = int(round(math.cos(phase + 1.1) * 4.0))
        shifted_rgb = np.roll(source_rgb, shift=(flow_y, flow_x), axis=(0, 1))
        shifted_luma = np.roll(source_luma, shift=(flow_y, flow_x), axis=(0, 1))
        shifted_broad = np.roll(broad_luma, shift=(counter_y, counter_x), axis=(0, 1))
        shifted_detail = np.roll(detail, shift=(flow_y, flow_x), axis=(0, 1))
        shifted_macro = np.roll(macro_detail, shift=(counter_y, counter_x), axis=(0, 1))

        advected_source = source_rgb * 0.78 + shifted_rgb * 0.22
        advected_luma = source_luma * 0.76 + shifted_luma * 0.18 + shifted_broad * 0.06
        source_modulation = np.clip(0.78 + advected_luma * 0.42 + broad_luma * 0.28 + hot * 0.16 - cool * 0.08, 0.58, 1.46)
        energy = np.clip(
            advected_luma * (0.48 + surface_weight * 0.34)
            + reference_energy * (0.24 + surface_weight * 0.32)
            + hot * (0.19 + radial_pulse * 0.07) * disk_weight
            - cool * 0.06 * disk_weight,
            0.0,
            1.0,
        )

        mid_mix = smoothstep_range(0.07, 0.70, energy)[:, :, None]
        hot_mix = smoothstep_range(0.55, 0.91, energy)[:, :, None]
        white_mix = smoothstep_range(0.84, 0.995, energy)[:, :, None]
        palette_rgb = dark_color[None, None, :] * (1.0 - mid_mix) + mid_color[None, None, :] * mid_mix
        palette_rgb = palette_rgb * (1.0 - hot_mix) + hot_color[None, None, :] * hot_mix
        palette_rgb = palette_rgb * (1.0 - white_mix * surface_weight[:, :, None]) + white_hot[None, None, :] * (white_mix * surface_weight[:, :, None])
        chroma_palette = palette_rgb * (source_chroma * 0.58 + 0.42)

        source_based = advected_source * (0.62 + reference_energy[:, :, None] * 0.52 + hot[:, :, None] * 0.18)
        frame_rgb = source_based * 0.64 + chroma_palette * 0.36
        frame_rgb *= source_modulation[:, :, None]
        frame_rgb += shifted_detail * (0.27 + hot[:, :, None] * 0.22) * disk_weight[:, :, None]
        frame_rgb += shifted_macro * (0.13 + radial_pulse[:, :, None] * 0.05) * disk_weight[:, :, None]
        frame_rgb += hot_color[None, None, :] * (hot * radial_pulse * 0.10 * disk_weight)[:, :, None]

        corona_source = advected_source * (0.18 + reference_energy[:, :, None] * 0.22)
        corona_palette = (dark_color * 0.38 + mid_color * 0.62)[None, None, :] * (0.08 + reference_energy[:, :, None] * 0.28)
        corona_rgb = corona_source * 0.62 + corona_palette * 0.38
        frame_rgb = frame_rgb * surface_weight[:, :, None] + corona_rgb * (1.0 - surface_weight[:, :, None])

        tongues = np.zeros_like(radius)
        tongue_count = 6
        for tongue in range(tongue_count):
            base_angle = tongue / tongue_count * math.tau + (tongue % 2) * 0.23
            angle_now = base_angle + 0.32 * math.sin(phase + tongue * 1.33)
            width = 0.024 + 0.033 * (0.5 + 0.5 * math.sin(tongue * 2.41))
            reach = 0.036 + 0.068 * (0.5 + 0.5 * math.sin(phase * 2.0 + tongue * 1.57))
            angular = np.exp(-((angle_delta(theta, angle_now) / width) ** 2))
            radial_attach = smoothstep_range(0.79, 0.88, radius)
            radial_fade = 1.0 - smoothstep_range(0.92 + reach * 0.35, 1.03 + reach * 0.85, radius)
            filament = 0.62 + 0.38 * np.sin(radius * 45.0 - phase * 2.0 + tongue * 1.71)
            tongues += angular * radial_attach * radial_fade * filament * (0.12 + 0.25 * math.sin(phase * 3.0 + tongue * 0.79) ** 2)

        tongues = np.clip(tongues, 0.0, 0.56) * np.maximum(rim_window, smoothstep_range(0.83, 0.94, radius) * (1.0 - smoothstep_range(1.00, 1.12, radius)))
        flame_mix = np.clip(tongues[:, :, None] * 0.38, 0.0, 0.38)
        frame_rgb = frame_rgb * (1.0 - flame_mix) + flare_color[None, None, :] * flame_mix
        dynamic_alpha = np.maximum(source_alpha * (0.26 + surface_weight * 0.66), reference_alpha * (0.12 + surface_weight * 0.52))
        dynamic_alpha = np.maximum(dynamic_alpha, tongues * 0.58)
        frame_alpha = np.maximum(alpha_window, dynamic_alpha)

        out = np.dstack((np.clip(frame_rgb, 0.0, 255.0), np.clip(frame_alpha * 255.0, 0.0, 255.0)))
        image = Image.fromarray((out + 0.5).astype(np.uint8), mode="RGBA")
        image = image.filter(ImageFilter.UnsharpMask(radius=0.28, percent=82, threshold=2))
        image.save(out_dir / f"{STAR_FRAME_PREFIX}{frame:02d}.png", compress_level=6)


def load_reference_sun_frames() -> list[np.ndarray]:
    frames: list[np.ndarray] = []
    for frame in range(STAR_FRAME_COUNT):
        path = SOL_FRAME_SOURCE_DIR / f"sun_{frame:02d}.png"
        if not path.exists():
            break

        image = Image.open(path).convert("RGBA")
        if image.size != (STAR_FRAME_SIZE, STAR_FRAME_SIZE):
            image = image.resize((STAR_FRAME_SIZE, STAR_FRAME_SIZE), Image.Resampling.LANCZOS)
        frames.append(np.asarray(image).astype(np.float32))
    return frames


def deterministic_star_frame_transform(frame: np.ndarray, variant_id: str) -> np.ndarray:
    seed = int(hashlib.sha256(variant_id.encode("utf-8")).hexdigest()[:8], 16)
    result = frame
    rotation = seed % 4
    if rotation:
        result = np.rot90(result, rotation)
    if seed & 0x10:
        result = result[:, ::-1, :]
    if seed & 0x20:
        result = result[::-1, :, :]
    return np.ascontiguousarray(result)


def angle_delta(angle: np.ndarray, target: float) -> np.ndarray:
    return np.arctan2(np.sin(angle - target), np.cos(angle - target))


def make_horizontal_seamless(image: Image.Image, seam_width: int) -> Image.Image:
    arr = np.asarray(image).astype(np.float32) / 255.0
    height, width, _ = arr.shape
    width_limit = min(seam_width, width // 3)
    for index in range(width_limit):
        weight = 1.0 - smoothstep(index / max(1, width_limit - 1))
        left_index = index
        right_index = width - 1 - index
        average = (arr[:, left_index, :] + arr[:, right_index, :]) * 0.5
        arr[:, left_index, :] = arr[:, left_index, :] * (1.0 - weight) + average * weight
        arr[:, right_index, :] = arr[:, right_index, :] * (1.0 - weight) + average * weight
    return array_to_rgb(arr)


def make_tileable(image: Image.Image, seam_width: int) -> Image.Image:
    arr = np.asarray(make_horizontal_seamless(image, seam_width)).astype(np.float32) / 255.0
    height, width, _ = arr.shape
    height_limit = min(seam_width, height // 3)
    for index in range(height_limit):
        weight = 1.0 - smoothstep(index / max(1, height_limit - 1))
        top_index = index
        bottom_index = height - 1 - index
        average = (arr[top_index, :, :] + arr[bottom_index, :, :]) * 0.5
        arr[top_index, :, :] = arr[top_index, :, :] * (1.0 - weight) + average * weight
        arr[bottom_index, :, :] = arr[bottom_index, :, :] * (1.0 - weight) + average * weight
    return array_to_rgb(arr)


def array_to_rgb(arr: np.ndarray) -> Image.Image:
    arr = np.clip(arr, 0.0, 1.0)
    return Image.fromarray((arr * 255.0 + 0.5).astype(np.uint8), mode="RGB")


def smoothstep(value: float) -> float:
    t = max(0.0, min(1.0, value))
    return t * t * (3.0 - 2.0 * t)


def sharpness_score(image: Image.Image) -> float:
    arr = np.asarray(image.convert("L")).astype(np.float32)
    if arr.shape[0] < 3 or arr.shape[1] < 3:
        return 0.0

    laplacian = (
        -4.0 * arr[1:-1, 1:-1]
        + arr[:-2, 1:-1]
        + arr[2:, 1:-1]
        + arr[1:-1, :-2]
        + arr[1:-1, 2:]
    )
    return float(laplacian.var())


def horizontal_seam_delta(image: Image.Image) -> float:
    arr = np.asarray(image.convert("RGB")).astype(np.float32) / 255.0
    delta = np.abs(arr[:, 0, :] - arr[:, -1, :]).mean()
    return float(delta)


def vertical_seam_delta(image: Image.Image) -> float:
    arr = np.asarray(image.convert("RGB")).astype(np.float32) / 255.0
    delta = np.abs(arr[0, :, :] - arr[-1, :, :]).mean()
    return float(delta)


def star_edge_margin_ratio(image: Image.Image) -> float:
    rgba = np.asarray(image.convert("RGBA")).astype(np.float32)
    brightness = rgba[:, :, :3].max(axis=2)
    alpha = rgba[:, :, 3]
    mask = (alpha > 18.0) & (brightness > 28.0)
    if not np.any(mask):
        return 0.0

    ys, xs = np.where(mask)
    width = image.width
    height = image.height
    margin = min(xs.min(), ys.min(), width - 1 - xs.max(), height - 1 - ys.max())
    return float(margin / max(1, min(width, height)))


def validate_asset(
    kind: str,
    archetype: str,
    source_path: Path,
    output_path: Path,
    image: Image.Image,
    seam_delta: float | None,
    source_label: str = "",
) -> ProcessedAsset:
    sharpness = sharpness_score(image)
    width, height = image.size
    notes: list[str] = []

    min_sharpness = {
        "star": STAR_MIN_SHARPNESS,
        "planet": PLANET_MIN_SHARPNESS,
        "background": (
            BACKGROUND_DIRECT_SOURCE_MIN_SHARPNESS
            if source_label == "imagegen_direct_highres_tile"
            else BACKGROUND_MIN_SHARPNESS
        ),
    }[kind]
    if sharpness < min_sharpness:
        notes.append(f"sharpness {sharpness:.2f} below {min_sharpness:.2f}")

    if kind == "planet":
        aspect = width / max(1, height)
        if not math.isclose(aspect, 2.0, rel_tol=0.005, abs_tol=0.005):
            notes.append(f"planet aspect {aspect:.3f} is not 2:1")
        if seam_delta is not None and seam_delta > PLANET_MAX_SEAM_DELTA:
            notes.append(f"horizontal seam delta {seam_delta:.4f} above {PLANET_MAX_SEAM_DELTA:.4f}")

    if kind == "background":
        if width != height:
            notes.append("background tile is not square")
        if seam_delta is not None and seam_delta > BACKGROUND_MAX_SEAM_DELTA:
            notes.append(f"tile seam delta {seam_delta:.4f} above {BACKGROUND_MAX_SEAM_DELTA:.4f}")

    if kind == "star" and width != height:
        notes.append("star source is not square")
    if kind == "star":
        margin = star_edge_margin_ratio(image)
        if margin < STAR_MIN_EDGE_MARGIN_RATIO:
            notes.append(f"star bright content margin {margin:.4f} below {STAR_MIN_EDGE_MARGIN_RATIO:.4f}")

    return ProcessedAsset(
        kind=kind,
        archetype=archetype,
        variant_id=source_path.stem,
        source_path=source_path,
        output_path=output_path,
        width=width,
        height=height,
        sharpness=sharpness,
        seam_delta=seam_delta,
        status="fail" if notes else "ok",
        notes=tuple(notes),
        source_label=source_label,
    )


def failed_asset(kind: str, source_path: Path, archetype: str, note: str) -> ProcessedAsset:
    return ProcessedAsset(
        kind=kind,
        archetype=archetype,
        variant_id=source_path.stem,
        source_path=source_path,
        output_path=Path(),
        width=0,
        height=0,
        sharpness=0.0,
        seam_delta=None,
        status="fail",
        notes=(note,),
    )


def register_assets(catalog: dict[str, Any], assets: list[ProcessedAsset], replace_lowres: bool, replace_backgrounds: bool) -> None:
    for asset in assets:
        if asset.status != "ok":
            continue

        if asset.kind == "star":
            register_star(catalog, asset)
        elif asset.kind == "planet":
            register_planet(catalog, asset, replace_lowres=replace_lowres)
        elif asset.kind == "background":
            register_background(catalog, asset, replace_backgrounds=replace_backgrounds)


def register_star(catalog: dict[str, Any], asset: ProcessedAsset) -> None:
    set_id = f"generated_highres_{asset.variant_id}"
    upsert_by_id(
        catalog.setdefault("starTextureSets", []),
        {
            "id": set_id,
            "path": f"{STAR_RES_ROOT}/{asset.output_path.name}",
            "frameDirectory": f"{STAR_FRAME_RES_ROOT}/{asset.variant_id}",
            "framePrefix": STAR_FRAME_PREFIX,
            "frameCount": STAR_FRAME_COUNT,
            "source": "imagegen_direct_highres",
        },
    )

    star = find_by_id(catalog.get("starArchetypes", []), asset.archetype)
    if star is None:
        return

    texture_sets = star.setdefault("textureSets", [])
    upsert_by_id(texture_sets, {"id": set_id, "weight": 3.0})
    if "textureSet" not in star:
        star["textureSet"] = set_id


def register_planet(catalog: dict[str, Any], asset: ProcessedAsset, replace_lowres: bool) -> None:
    planet = find_by_id(catalog.get("planetArchetypes", []), asset.archetype)
    if planet is None:
        return

    surface_maps = planet.setdefault("surfaceMaps", [])
    if replace_lowres:
        planet["surfaceMaps"] = [
            surface
            for surface in surface_maps
            if not is_lowres_generated_planet_surface(surface)
        ]
        surface_maps = planet["surfaceMaps"]

    path = f"{PLANET_OUTPUT_RES_ROOT}/{asset.output_path.name}"
    raw_path = f"{PLANET_SOURCE_RES_ROOT}/{asset.source_path.name}"
    upsert_by_path(
        surface_maps,
        {
            "path": path,
            "weight": 3.0,
            "source": "processed_planet_surface",
            "rawPath": raw_path,
            "quality": "direct_highres",
            "validatedSharpness": round(asset.sharpness, 3),
            "validatedSeamDelta": round(asset.seam_delta or 0.0, 5),
        },
    )


def register_background(catalog: dict[str, Any], asset: ProcessedAsset, replace_backgrounds: bool) -> None:
    set_id = f"generated_highres_{asset.variant_id}"
    upsert_by_id(
        catalog.setdefault("spaceBackdropTextureSets", []),
        {
            "id": set_id,
            "path": f"{BACKGROUND_RES_ROOT}/{asset.output_path.name}",
            "source": asset.source_label or "imagegen_solstyle_highres_tile",
            "quality": "direct_highres",
            "validatedSharpness": round(asset.sharpness, 3),
            "validatedSeamDelta": round(asset.seam_delta or 0.0, 5),
        },
    )

    backdrop = find_by_id(catalog.get("spaceBackdropArchetypes", []), asset.archetype)
    if replace_backgrounds and backdrop is not None:
        backdrop["textureSet"] = set_id


def is_lowres_generated_planet_surface(surface: dict[str, Any]) -> bool:
    raw_path = str(surface.get("rawPath", ""))
    path = str(surface.get("path", ""))
    return "/generated/planets/" in raw_path or path.endswith("_sheet01.png")


def upsert_by_id(items: list[dict[str, Any]], entry: dict[str, Any]) -> None:
    existing = find_by_id(items, entry["id"])
    if existing is None:
        items.append(entry)
    else:
        existing.update(entry)


def upsert_by_path(items: list[dict[str, Any]], entry: dict[str, Any]) -> None:
    for item in items:
        if item.get("path") == entry["path"]:
            item.update(entry)
            return
    items.append(entry)


def find_by_id(items: Iterable[dict[str, Any]], item_id: str) -> dict[str, Any] | None:
    for item in items:
        if item.get("id") == item_id:
            return item
    return None


def write_report(path: Path, assets: list[ProcessedAsset]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    data = {
        "assetCount": len(assets),
        "failedCount": sum(1 for asset in assets if asset.status == "fail"),
        "assets": [
            {
                "kind": asset.kind,
                "archetype": asset.archetype,
                "variantId": asset.variant_id,
                "sourcePath": str(asset.source_path.relative_to(ROOT)) if asset.source_path.is_absolute() else str(asset.source_path),
                "outputPath": str(asset.output_path.relative_to(ROOT)) if asset.output_path and asset.output_path.is_absolute() else str(asset.output_path),
                "width": asset.width,
                "height": asset.height,
                "sharpness": round(asset.sharpness, 3),
                "seamDelta": None if asset.seam_delta is None else round(asset.seam_delta, 5),
                "status": asset.status,
                "notes": list(asset.notes),
                "sourceLabel": asset.source_label,
            }
            for asset in assets
        ],
    }
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(data, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def print_report_summary(assets: list[ProcessedAsset], report_path: Path) -> None:
    by_kind = {kind: sum(1 for asset in assets if asset.kind == kind) for kind in ("star", "planet", "background")}
    failed = [asset for asset in assets if asset.status == "fail"]
    print(
        "Processed high-res assets: "
        f"stars {by_kind['star']}, planets {by_kind['planet']}, backgrounds {by_kind['background']}; "
        f"failed {len(failed)}"
    )
    for asset in failed:
        print(f"FAIL {asset.kind}/{asset.variant_id}: {'; '.join(asset.notes)}")
    print(f"Wrote {report_path}")


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path: Path, value: dict[str, Any]) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


if __name__ == "__main__":
    main()
