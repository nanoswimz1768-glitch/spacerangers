from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path
from typing import Any

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE_DIR = Path(r"C:\CodexHome\generated_images\019e0db7-b04c-72f3-8af7-99f8ceb05064")
CATALOG_PATH = ROOT / "tools" / "star_system_catalog.json"
SHEETS_DIR = ROOT / "tools" / "generated" / "imagegen_sheets"
ASSET_ROOT = ROOT / "game" / "assets" / "generated"


STAR_ASSETS = [
    ("red_dwarf_sheet01", "red_dwarf"),
    ("orange_dwarf_sheet01", "orange_dwarf"),
    ("yellow_main_sequence_sheet01", "yellow_main_sequence"),
    ("blue_white_star_sheet01", "blue_white_star"),
    ("white_dwarf_sheet01", "white_dwarf"),
    ("red_giant_sheet01", "red_giant"),
    ("amber_giant_sheet01", "amber_giant"),
    ("violet_anomaly_sheet01", "violet_anomaly"),
    ("green_exotic_sheet01", "green_exotic"),
    ("neutron_like_sheet01", "neutron_like"),
    ("ember_remnant_sheet01", ""),
    ("cyan_compact_sheet01", ""),
]

PLANET_ASSETS = [
    ("scorched_rock_sheet01", ["scorched_rock"]),
    ("barren_rock_sheet01", ["barren_rock"]),
    ("desert_sheet01", ["desert"]),
    ("volcanic_sheet01", ["volcanic"]),
    ("ocean_sheet01", ["ocean"]),
    ("earthlike_sheet01", ["earthlike"]),
    ("ice_sheet01", ["ice"]),
    ("toxic_sheet01", ["toxic"]),
    ("warm_gas_giant_sheet01", ["warm_gas_giant"]),
    ("cold_gas_giant_sheet01", ["cold_gas_giant"]),
    ("ringed_giant_sheet01", ["ringed_giant"]),
    ("shattered_world_sheet01", ["shattered_world"]),
    ("purple_storm_giant_sheet01", ["cold_gas_giant", "ringed_giant"]),
    ("rust_dust_world_sheet01", ["desert", "barren_rock"]),
    ("dark_carbon_world_sheet01", ["barren_rock", "shattered_world"]),
    ("turquoise_methane_world_sheet01", ["toxic", "cold_gas_giant"]),
]

BACKGROUND_ASSETS = [
    ("cold_blue_void_sheet01", "cold_blue_void"),
    ("rust_dust_lane_sheet01", "rust_dust_lane"),
    ("crimson_sparse_field_sheet01", "crimson_sparse_field"),
    ("emerald_mist_sheet01", "emerald_mist"),
    ("violet_rift_sheet01", "violet_rift"),
    ("golden_cluster_sheet01", "golden_cluster"),
    ("black_silent_reach_sheet01", "black_silent_reach"),
    ("teal_nebula_shelf_sheet01", "teal_nebula_shelf"),
    ("pale_star_nursery_sheet01", "pale_star_nursery"),
    ("deep_indigo_field_sheet01", "deep_indigo_field"),
    ("smoky_amber_cloud_sheet01", "smoky_amber_cloud"),
    ("icy_cyan_starfield_sheet01", "icy_cyan_starfield"),
]

PLANET_CROP_X = [(23, 378), (400, 754), (777, 1130), (1153, 1509)]
PLANET_CROP_Y = [(30, 246), (272, 484), (510, 721), (747, 974)]
BACKGROUND_CROP_X = [(14, 379), (395, 759), (775, 1139), (1156, 1521)]
BACKGROUND_CROP_Y = [(52, 337), (361, 645), (671, 956)]


def main() -> None:
    args = parse_args()
    source_dir = Path(args.source_dir)
    sheets = latest_imagegen_sheets(source_dir)

    SHEETS_DIR.mkdir(parents=True, exist_ok=True)
    (ASSET_ROOT / "stars").mkdir(parents=True, exist_ok=True)
    (ASSET_ROOT / "planets").mkdir(parents=True, exist_ok=True)
    (ASSET_ROOT / "backgrounds").mkdir(parents=True, exist_ok=True)

    sheet_targets = {
        "pilot": SHEETS_DIR / "pilot_concept_sheet.png",
        "stars": SHEETS_DIR / "stars_concept_sheet.png",
        "planets": SHEETS_DIR / "planets_concept_sheet.png",
        "backgrounds": SHEETS_DIR / "backgrounds_concept_sheet.png",
    }
    for key, source in zip(sheet_targets.keys(), sheets):
        shutil.copy2(source, sheet_targets[key])

    star_paths = slice_star_sheet(sheet_targets["stars"])
    planet_paths = slice_planet_sheet(sheet_targets["planets"])
    background_paths = slice_background_sheet(sheet_targets["backgrounds"])

    update_catalog(star_paths, planet_paths, background_paths)
    write_manifest(sheet_targets, star_paths, planet_paths, background_paths)

    print(f"Imported {len(star_paths)} star assets")
    print(f"Imported {len(planet_paths)} planet assets")
    print(f"Imported {len(background_paths)} background assets")
    print(f"Updated {CATALOG_PATH}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Slice imagegen concept sheets into project assets.")
    parser.add_argument("--source-dir", default=str(DEFAULT_SOURCE_DIR), help="Folder with generated image sheets.")
    return parser.parse_args()


def latest_imagegen_sheets(source_dir: Path) -> list[Path]:
    images = sorted(source_dir.glob("*.png"), key=lambda path: path.stat().st_mtime)
    if len(images) < 4:
        raise ValueError(f"Expected at least 4 generated PNG sheets in {source_dir}, found {len(images)}")

    return images[-4:]


def slice_star_sheet(sheet_path: Path) -> dict[str, str]:
    out_paths: dict[str, str] = {}
    with Image.open(sheet_path) as image:
        image = image.convert("RGBA")
        cell_w = image.width / 4
        cell_h = image.height / 3
        for index, (asset_id, _) in enumerate(STAR_ASSETS):
            col = index % 4
            row = index // 4
            crop = image.crop(
                (
                    round(col * cell_w),
                    round(row * cell_h),
                    round((col + 1) * cell_w),
                    round((row + 1) * cell_h),
                )
            )
            crop = pad_to_square(crop)
            crop = crop.resize((512, 512), Image.Resampling.LANCZOS)
            crop = remove_dark_background(crop)
            path = ASSET_ROOT / "stars" / f"{asset_id}.png"
            crop.save(path)
            out_paths[asset_id] = to_res_path(path)

    return out_paths


def slice_planet_sheet(sheet_path: Path) -> dict[str, str]:
    out_paths: dict[str, str] = {}
    with Image.open(sheet_path) as image:
        image = image.convert("RGB")
        for index, (asset_id, _) in enumerate(PLANET_ASSETS):
            col = index % 4
            row = index // 4
            left, right = PLANET_CROP_X[col]
            top, bottom = PLANET_CROP_Y[row]
            crop = image.crop((left, top, right, bottom))
            crop = crop.resize((1024, 512), Image.Resampling.LANCZOS)
            path = ASSET_ROOT / "planets" / f"{asset_id}.png"
            crop.save(path)
            out_paths[asset_id] = to_res_path(path)

    return out_paths


def slice_background_sheet(sheet_path: Path) -> dict[str, str]:
    out_paths: dict[str, str] = {}
    with Image.open(sheet_path) as image:
        image = image.convert("RGB")
        for index, (asset_id, _) in enumerate(BACKGROUND_ASSETS):
            col = index % 4
            row = index // 4
            left, right = BACKGROUND_CROP_X[col]
            top, bottom = BACKGROUND_CROP_Y[row]
            crop = image.crop((left, top, right, bottom))
            crop = crop.resize((1024, 768), Image.Resampling.LANCZOS)
            path = ASSET_ROOT / "backgrounds" / f"{asset_id}.png"
            crop.save(path)
            out_paths[asset_id] = to_res_path(path)

    return out_paths


def pad_to_square(image: Image.Image) -> Image.Image:
    side = max(image.width, image.height)
    result = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    result.alpha_composite(image, ((side - image.width) // 2, (side - image.height) // 2))
    return result


def remove_dark_background(image: Image.Image) -> Image.Image:
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, _ = pixels[x, y]
            brightness = max(r, g, b)
            alpha = int(max(0, min(255, (brightness - 4) * 7.5)))
            pixels[x, y] = (r, g, b, alpha)

    return image


def update_catalog(
    star_paths: dict[str, str],
    planet_paths: dict[str, str],
    background_paths: dict[str, str],
) -> None:
    with CATALOG_PATH.open("r", encoding="utf-8") as stream:
        catalog = json.load(stream)

    ensure_star_texture_sets(catalog, star_paths)
    ensure_planet_surface_maps(catalog, planet_paths)
    ensure_background_texture_sets(catalog, background_paths)
    ensure_extra_background_archetypes(catalog)

    with CATALOG_PATH.open("w", encoding="utf-8") as stream:
        json.dump(catalog, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def ensure_star_texture_sets(catalog: dict[str, Any], star_paths: dict[str, str]) -> None:
    texture_sets = catalog["starTextureSets"]
    by_id = {item["id"]: item for item in texture_sets}
    for asset_id, archetype in STAR_ASSETS:
        set_id = f"generated_{asset_id}"
        entry = {
            "id": set_id,
            "path": star_paths[asset_id],
            "source": "imagegen_sheet",
        }
        if set_id in by_id:
            by_id[set_id].update(entry)
        else:
            texture_sets.append(entry)

        if archetype:
            for star in catalog["starArchetypes"]:
                if star["id"] == archetype:
                    star["textureSet"] = set_id
                    break


def ensure_planet_surface_maps(catalog: dict[str, Any], planet_paths: dict[str, str]) -> None:
    for asset_id, archetypes in PLANET_ASSETS:
        for archetype_id in archetypes:
            archetype = find_by_id(catalog["planetArchetypes"], archetype_id)
            if archetype is None:
                continue

            surface_maps = archetype["surfaceMaps"]
            if any(item["path"] == planet_paths[asset_id] for item in surface_maps):
                continue

            surface_maps.append(
                {
                    "path": planet_paths[asset_id],
                    "weight": 1.65 if len(archetypes) == 1 else 0.82,
                    "source": "imagegen_sheet",
                }
            )


def ensure_background_texture_sets(catalog: dict[str, Any], background_paths: dict[str, str]) -> None:
    texture_sets = catalog["spaceBackdropTextureSets"]
    by_id = {item["id"]: item for item in texture_sets}
    for asset_id, archetype_id in BACKGROUND_ASSETS:
        set_id = f"generated_{asset_id}"
        entry = {
            "id": set_id,
            "path": background_paths[asset_id],
            "source": "imagegen_sheet",
        }
        if set_id in by_id:
            by_id[set_id].update(entry)
        else:
            texture_sets.append(entry)

        archetype = find_by_id(catalog["spaceBackdropArchetypes"], archetype_id)
        if archetype is not None:
            archetype["textureSet"] = set_id


def ensure_extra_background_archetypes(catalog: dict[str, Any]) -> None:
    extras = [
        {
            "id": "pale_star_nursery",
            "displayName": "Pale Star Nursery",
            "weight": 0.54,
            "textureSet": "generated_pale_star_nursery_sheet01",
            "starWeights": {"white_dwarf": 1.5, "blue_white_star": 1.2, "yellow_main_sequence": 0.75},
            "textureTint": "#d9e8f0",
            "textureAlphaRange": [0.22, 0.38],
            "parallaxRange": [0.05, 0.09],
            "starParallaxRange": [0.26, 0.38],
            "starCountRange": [760, 1120],
            "nebulaBlobCountRange": [10, 22],
            "dustDensityRange": [0.08, 0.18],
            "nebulaPalette": ["#26323a", "#3d4650", "#172438", "#203840"],
            "imagePrompt": "A seamless pale white star nursery background tile, silver gas haze, readable dark lanes, no planets, no text, no watermark.",
        },
        {
            "id": "deep_indigo_field",
            "displayName": "Deep Indigo Field",
            "weight": 0.62,
            "textureSet": "generated_deep_indigo_field_sheet01",
            "starWeights": {"violet_anomaly": 1.5, "blue_white_star": 1.1, "neutron_like": 1.2},
            "textureTint": "#746aff",
            "textureAlphaRange": [0.18, 0.34],
            "parallaxRange": [0.04, 0.08],
            "starParallaxRange": [0.24, 0.36],
            "starCountRange": [620, 980],
            "nebulaBlobCountRange": [8, 18],
            "dustDensityRange": [0.06, 0.16],
            "nebulaPalette": ["#171844", "#1d225c", "#132942", "#27183d"],
            "imagePrompt": "A seamless deep indigo open starfield tile, faint violet dust, sparse readable stars, no planets, no text, no watermark.",
        },
        {
            "id": "smoky_amber_cloud",
            "displayName": "Smoky Amber Cloud",
            "weight": 0.64,
            "textureSet": "generated_smoky_amber_cloud_sheet01",
            "starWeights": {"orange_dwarf": 1.35, "amber_giant": 1.45, "red_giant": 1.25},
            "textureTint": "#d08b42",
            "textureAlphaRange": [0.24, 0.42],
            "parallaxRange": [0.055, 0.095],
            "starParallaxRange": [0.28, 0.42],
            "starCountRange": [680, 1040],
            "nebulaBlobCountRange": [18, 34],
            "dustDensityRange": [0.18, 0.34],
            "nebulaPalette": ["#422516", "#5c3518", "#19333a", "#2a2218"],
            "imagePrompt": "A seamless smoky amber molecular cloud background tile, warm dust with dark teal shadows, no planets, no text, no watermark.",
        },
        {
            "id": "icy_cyan_starfield",
            "displayName": "Icy Cyan Starfield",
            "weight": 0.58,
            "textureSet": "generated_icy_cyan_starfield_sheet01",
            "starWeights": {"blue_white_star": 1.45, "white_dwarf": 1.35, "neutron_like": 1.25},
            "textureTint": "#85e6ff",
            "textureAlphaRange": [0.22, 0.38],
            "parallaxRange": [0.05, 0.09],
            "starParallaxRange": [0.28, 0.4],
            "starCountRange": [740, 1120],
            "nebulaBlobCountRange": [14, 28],
            "dustDensityRange": [0.08, 0.2],
            "nebulaPalette": ["#123845", "#174d5b", "#192f4a", "#253049"],
            "imagePrompt": "A seamless icy cyan starfield background tile, cool nebula haze, crisp stars, readable dark gaps, no planets, no text, no watermark.",
        },
    ]

    archetypes = catalog["spaceBackdropArchetypes"]
    for extra in extras:
        existing = find_by_id(archetypes, extra["id"])
        if existing is None:
            archetypes.append(extra)
        else:
            existing.update(extra)


def find_by_id(items: list[dict[str, Any]], item_id: str) -> dict[str, Any] | None:
    for item in items:
        if item.get("id") == item_id:
            return item
    return None


def write_manifest(
    sheet_targets: dict[str, Path],
    star_paths: dict[str, str],
    planet_paths: dict[str, str],
    background_paths: dict[str, str],
) -> None:
    manifest = {
        "sheets": {key: str(path.relative_to(ROOT)).replace("\\", "/") for key, path in sheet_targets.items()},
        "stars": star_paths,
        "planets": planet_paths,
        "backgrounds": background_paths,
    }
    path = ROOT / "tools" / "generated" / "imagegen_asset_manifest.json"
    with path.open("w", encoding="utf-8") as stream:
        json.dump(manifest, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def to_res_path(path: Path) -> str:
    relative = path.relative_to(ROOT / "game").as_posix()
    return f"res://{relative}"


if __name__ == "__main__":
    main()
