#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
CATALOG_PATH = ROOT / "tools" / "star_system_catalog.json"
INPUT_DIR = ROOT / "game" / "assets" / "generated" / "planets"
OUTPUT_DIR = ROOT / "game" / "assets" / "generated" / "planet_surfaces"
OUTPUT_RES_ROOT = "res://assets/generated/planet_surfaces"
INPUT_RES_ROOT = "res://assets/generated/planets"
TARGET_SIZE = (2048, 1024)
EDGE_CROP_X = 10
EDGE_CROP_Y = 3


def main() -> None:
    parser = argparse.ArgumentParser(description="Convert imagegen planet crops into sharper seamless runtime surface maps.")
    parser.add_argument("--update-catalog", action="store_true", help="Point generated planet surface maps at processed runtime assets.")
    parser.add_argument("--width", type=int, default=TARGET_SIZE[0], help="Processed map width in pixels.")
    parser.add_argument("--height", type=int, default=TARGET_SIZE[1], help="Processed map height in pixels.")
    args = parser.parse_args()

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    processed_paths: dict[str, str] = {}

    for source_path in sorted(INPUT_DIR.glob("*.png")):
        output_path = OUTPUT_DIR / source_path.name
        image = process_surface_map(source_path, (args.width, args.height))
        image.save(output_path, compress_level=6)
        processed_paths[f"{INPUT_RES_ROOT}/{source_path.name}"] = f"{OUTPUT_RES_ROOT}/{source_path.name}"
        print(f"Wrote {output_path}")

    if args.update_catalog:
        catalog = load_json(CATALOG_PATH)
        update_catalog_surface_maps(catalog, processed_paths)
        catalog.setdefault("assetRoots", {})["generatedPlanetSurfaceTextures"] = OUTPUT_RES_ROOT
        write_json(CATALOG_PATH, catalog)
        print(f"Updated {CATALOG_PATH}")


def process_surface_map(path: Path, target_size: tuple[int, int]) -> Image.Image:
    image = Image.open(path).convert("RGB")
    image = crop_edge_artifacts(image)
    image = image.resize(target_size, Image.Resampling.LANCZOS)
    image = make_horizontal_seamless(image)
    image = ImageEnhance.Contrast(image).enhance(1.035)
    image = ImageEnhance.Color(image).enhance(1.035)
    image = image.filter(ImageFilter.UnsharpMask(radius=0.85, percent=115, threshold=2))
    return image


def crop_edge_artifacts(image: Image.Image) -> Image.Image:
    left = min(EDGE_CROP_X, image.width // 16)
    top = min(EDGE_CROP_Y, image.height // 24)
    right = image.width - left
    bottom = image.height - top
    return image.crop((left, top, right, bottom))


def make_horizontal_seamless(image: Image.Image) -> Image.Image:
    arr = np.asarray(image).astype(np.float32) / 255.0
    height, width, _ = arr.shape
    seam_width = max(64, min(192, width // 9))

    for index in range(seam_width):
        seam_weight = 1.0 - smoothstep(index / max(1, seam_width - 1))
        left_index = index
        right_index = width - 1 - index
        average = (arr[:, left_index, :] + arr[:, right_index, :]) * 0.5
        arr[:, left_index, :] = arr[:, left_index, :] * (1.0 - seam_weight) + average * seam_weight
        arr[:, right_index, :] = arr[:, right_index, :] * (1.0 - seam_weight) + average * seam_weight

    arr = np.clip(arr, 0.0, 1.0)
    return Image.fromarray((arr * 255.0 + 0.5).astype(np.uint8), mode="RGB")


def smoothstep(value: float) -> float:
    t = max(0.0, min(1.0, value))
    return t * t * (3.0 - 2.0 * t)


def update_catalog_surface_maps(catalog: dict[str, Any], processed_paths: dict[str, str]) -> None:
    for archetype in catalog.get("planetArchetypes", []):
        for surface_map in archetype.get("surfaceMaps", []):
            path = surface_map.get("path")
            if path not in processed_paths:
                continue

            surface_map["rawPath"] = path
            surface_map["path"] = processed_paths[path]
            surface_map["source"] = "processed_planet_surface"


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path: Path, value: dict[str, Any]) -> None:
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


if __name__ == "__main__":
    main()
