#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
CATALOG_PATH = ROOT / "tools" / "star_system_catalog.json"
BASE_TILE_PATH = ROOT / "game" / "assets" / "backgrounds" / "space_nebula_tile.png"
OUTPUT_DIR = ROOT / "game" / "assets" / "generated" / "background_tiles"
OUTPUT_RES_ROOT = "res://assets/generated/background_tiles"
TILE_SIZE = 4096


VARIANT_SETTINGS: dict[str, dict[str, float]] = {
    "cold_blue_void": {"strength": 0.60, "brightness": 0.62, "saturation": 0.78, "contrast": 1.08},
    "rust_dust_lane": {"strength": 0.58, "brightness": 0.72, "saturation": 0.92, "contrast": 1.06},
    "crimson_sparse_field": {"strength": 0.68, "brightness": 0.56, "saturation": 0.86, "contrast": 1.12},
    "emerald_mist": {"strength": 0.66, "brightness": 0.60, "saturation": 0.80, "contrast": 1.05},
    "violet_rift": {"strength": 0.62, "brightness": 0.64, "saturation": 0.94, "contrast": 1.08},
    "golden_cluster": {"strength": 0.52, "brightness": 0.78, "saturation": 0.96, "contrast": 1.02},
    "black_silent_reach": {"strength": 0.36, "brightness": 0.42, "saturation": 0.62, "contrast": 1.18},
    "teal_nebula_shelf": {"strength": 0.58, "brightness": 0.66, "saturation": 0.84, "contrast": 1.05},
    "pale_star_nursery": {"strength": 0.42, "brightness": 0.82, "saturation": 0.74, "contrast": 0.96},
    "deep_indigo_field": {"strength": 0.64, "brightness": 0.50, "saturation": 0.82, "contrast": 1.14},
    "smoky_amber_cloud": {"strength": 0.56, "brightness": 0.66, "saturation": 0.82, "contrast": 1.08},
    "icy_cyan_starfield": {"strength": 0.54, "brightness": 0.70, "saturation": 0.74, "contrast": 1.04},
}


def main() -> None:
    parser = argparse.ArgumentParser(description="Create Sol-style tileable background variants for generated star systems.")
    parser.add_argument("--update-catalog", action="store_true", help="Point generated backdrop texture sets at the new tile assets.")
    parser.add_argument("--size", type=int, default=TILE_SIZE, help="Square output tile size in pixels.")
    args = parser.parse_args()

    catalog = load_json(CATALOG_PATH)
    base = load_base_tile(args.size)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    generated_paths: dict[str, str] = {}
    for texture_set in catalog["spaceBackdropTextureSets"]:
        if not should_create_tile_variant(texture_set):
            continue

        variant_id = variant_id_from_texture_set(texture_set)
        output_name = f"{variant_id}_tile.png"
        output_path = OUTPUT_DIR / output_name
        tint = backdrop_tint_for_texture_set(catalog, texture_set["id"])
        settings = VARIANT_SETTINGS.get(variant_id, {})
        image = create_variant(base, variant_id, tint, settings)
        image.save(output_path, compress_level=6)
        generated_paths[texture_set["id"]] = f"{OUTPUT_RES_ROOT}/{output_name}"
        print(f"Wrote {output_path}")

    if args.update_catalog:
        for texture_set in catalog["spaceBackdropTextureSets"]:
            if texture_set["id"] in generated_paths:
                texture_set["path"] = generated_paths[texture_set["id"]]
                texture_set["source"] = "generated_tile_from_sol"

        write_json(CATALOG_PATH, catalog)
        print(f"Updated {CATALOG_PATH}")


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path: Path, value: dict[str, Any]) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def load_base_tile(size: int) -> np.ndarray:
    image = Image.open(BASE_TILE_PATH).convert("RGB")
    if image.size != (size, size):
        image = image.resize((size, size), Image.Resampling.LANCZOS)
    return np.asarray(image).astype(np.float32) / 255.0


def variant_id_from_texture_set(texture_set: dict[str, Any]) -> str:
    path = str(texture_set["path"])
    name = Path(path).name
    if name.endswith("_sheet01.png"):
        return name[:-len("_sheet01.png")]
    if name.endswith("_tile.png"):
        return name[:-len("_tile.png")]
    return name.removesuffix(".png")


def should_create_tile_variant(texture_set: dict[str, Any]) -> bool:
    source = texture_set.get("source")
    path = str(texture_set.get("path", ""))
    return source in {"imagegen_sheet", "generated_tile_from_sol"} or "assets/generated/background_tiles" in path


def backdrop_tint_for_texture_set(catalog: dict[str, Any], texture_set_id: str) -> np.ndarray:
    for backdrop in catalog["spaceBackdropArchetypes"]:
        if backdrop.get("textureSet") == texture_set_id:
            return parse_hex_color(backdrop.get("textureTint", "#ffffff"))

    return np.array([1.0, 1.0, 1.0], dtype=np.float32)


def parse_hex_color(value: str) -> np.ndarray:
    text = value.strip().lstrip("#")
    if len(text) != 6:
        return np.array([1.0, 1.0, 1.0], dtype=np.float32)

    return np.array(
        [int(text[0:2], 16) / 255.0, int(text[2:4], 16) / 255.0, int(text[4:6], 16) / 255.0],
        dtype=np.float32,
    )


def create_variant(base: np.ndarray, variant_id: str, tint: np.ndarray, settings: dict[str, float]) -> Image.Image:
    arr = deterministic_transform(base, variant_id)
    strength = float(settings.get("strength", 0.55))
    brightness = float(settings.get("brightness", 0.65))
    saturation = float(settings.get("saturation", 0.86))
    contrast = float(settings.get("contrast", 1.06))

    lum = np.dot(arr, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    lum3 = lum[:, :, None]
    saturated = lum3 + (arr - lum3) * saturation
    colorized = saturated * (1.0 - strength) + lum3 * tint[None, None, :] * (1.26 + strength * 0.22) * strength
    out = (colorized - 0.5) * contrast + 0.5
    out *= brightness

    h, w = lum.shape
    x = np.linspace(0.0, 1.0, w, endpoint=False, dtype=np.float32)
    y = np.linspace(0.0, 1.0, h, endpoint=False, dtype=np.float32)
    xx, yy = np.meshgrid(x, y)
    seed = int(hashlib.sha256(variant_id.encode("utf-8")).hexdigest()[:8], 16)
    phase_a = (seed % 997) / 997.0
    phase_b = ((seed >> 10) % 991) / 991.0
    wave = (
        np.sin((xx * 2.0 + phase_a) * np.pi * 2.0)
        * np.cos((yy * 2.0 + phase_b) * np.pi * 2.0)
        * 0.5
        + 0.5
    )
    out += wave[:, :, None] * tint[None, None, :] * 0.035 * strength

    star_mask = smoothstep(0.62, 0.92, lum)[:, :, None]
    star_color = arr * 0.70 + tint[None, None, :] * lum3 * 0.55
    out = out * (1.0 - star_mask * 0.36) + np.maximum(out, star_color) * (star_mask * 0.36)

    out = np.clip(out, 0.0, 1.0)
    return Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), mode="RGB")


def deterministic_transform(base: np.ndarray, variant_id: str) -> np.ndarray:
    seed = int(hashlib.sha256(variant_id.encode("utf-8")).hexdigest()[:8], 16)
    arr = np.roll(base, shift=seed % base.shape[0], axis=0)
    arr = np.roll(arr, shift=(seed >> 8) % base.shape[1], axis=1)
    if seed & 1:
        arr = arr[:, ::-1, :]
    if seed & 2:
        arr = arr[::-1, :, :]
    if seed & 4:
        arr = np.rot90(arr, 2)
    return np.ascontiguousarray(arr)


def smoothstep(edge0: float, edge1: float, value: np.ndarray) -> np.ndarray:
    t = np.clip((value - edge0) / max(0.0001, edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


if __name__ == "__main__":
    main()
