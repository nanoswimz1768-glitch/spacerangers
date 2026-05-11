#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
CATALOG_PATH = ROOT / "tools" / "star_system_catalog.json"
DEFAULT_OUT = ROOT / "tools" / "generated" / "highres_generation_batch.jsonl"

STARTING_STARS = ["orange_dwarf", "blue_white_star"]
STARTING_BACKGROUNDS = ["smoky_amber_cloud", "cold_blue_void"]
STARTING_PLANETS = ["desert", "cold_gas_giant", "shattered_world", "barren_rock", "earthlike"]

EXTRA_STAR_PRIORITY = ["yellow_main_sequence", "red_dwarf", "white_dwarf", "amber_giant"]
EXTRA_BACKGROUND_PRIORITY = ["deep_indigo_field", "black_silent_reach"]
EXTRA_PLANET_PRIORITY = [
    "desert",
    "earthlike",
    "barren_rock",
    "cold_gas_giant",
    "shattered_world",
    "toxic",
    "ocean",
    "ringed_giant",
]


def main() -> None:
    parser = argparse.ArgumentParser(description="Prepare an exact-count high-res imagegen batch from the galaxy catalog.")
    parser.add_argument("--out", default=str(DEFAULT_OUT), help="Output JSONL batch path.")
    parser.add_argument("--stars", type=int, default=14, help="Number of star source assets.")
    parser.add_argument("--backgrounds", type=int, default=14, help="Number of background source assets.")
    parser.add_argument("--planets", type=int, default=32, help="Number of planet source assets.")
    parser.add_argument("--variants", type=int, default=3, help="Max variants per archetype before cycling.")
    args = parser.parse_args()

    catalog = load_json(CATALOG_PATH)
    rows: list[dict[str, Any]] = []
    rows.extend(star_rows(catalog, args.stars, args.variants))
    rows.extend(background_rows(catalog, args.backgrounds, args.variants))
    rows.extend(planet_rows(catalog, args.planets, args.variants))

    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8", newline="\n") as stream:
        for row in rows:
            stream.write(json.dumps(row, ensure_ascii=False))
            stream.write("\n")

    print(f"Wrote {len(rows)} high-res prompts into {out_path}")


def star_rows(catalog: dict[str, Any], count: int, variants: int) -> list[dict[str, Any]]:
    stars = catalog["starArchetypes"]
    ids = exact_ids([star["id"] for star in stars], STARTING_STARS + EXTRA_STAR_PRIORITY, count)
    return [
        row("star", star, variant, f"game/assets/generated/star_sources_4k/{star['id']}_{variant:02d}.png", star_prompt(star, variant))
        for star, variant in assign_variants(stars, ids, variants)
    ]


def background_rows(catalog: dict[str, Any], count: int, variants: int) -> list[dict[str, Any]]:
    backgrounds = catalog["spaceBackdropArchetypes"]
    ids = exact_ids([item["id"] for item in backgrounds], STARTING_BACKGROUNDS + EXTRA_BACKGROUND_PRIORITY, count)
    return [
        row(
            "background",
            background,
            variant,
            f"game/assets/generated/background_sources_4k/{background['id']}_{variant:02d}.png",
            background_prompt(background, variant),
        )
        for background, variant in assign_variants(backgrounds, ids, variants)
    ]


def planet_rows(catalog: dict[str, Any], count: int, variants: int) -> list[dict[str, Any]]:
    planets = catalog["planetArchetypes"]
    ids = exact_ids([planet["id"] for planet in planets], STARTING_PLANETS + EXTRA_PLANET_PRIORITY, count)
    return [
        row("planet", planet, variant, f"game/assets/generated/planet_sources_4k/{planet['id']}_{variant:02d}.png", planet_prompt(planet, variant))
        for planet, variant in assign_variants(planets, ids, variants)
    ]


def exact_ids(base_ids: list[str], priority_ids: list[str], count: int) -> list[str]:
    result = list(base_ids)
    priority = [item for item in priority_ids if item in base_ids]
    index = 0
    while len(result) < count:
        if priority:
            result.append(priority[index % len(priority)])
        else:
            result.append(base_ids[index % len(base_ids)])
        index += 1
    return result[:count]


def assign_variants(items: list[dict[str, Any]], ids: list[str], max_variant: int) -> list[tuple[dict[str, Any], int]]:
    by_id = {item["id"]: item for item in items}
    seen: dict[str, int] = {}
    result: list[tuple[dict[str, Any], int]] = []
    for item_id in ids:
        seen[item_id] = seen.get(item_id, 0) + 1
        variant = ((seen[item_id] - 1) % max(1, max_variant)) + 1
        result.append((by_id[item_id], variant))
    return result


def row(kind: str, item: dict[str, Any], variant: int, target_path: str, prompt: str) -> dict[str, Any]:
    return {
        "assetId": f"{kind}/{item['id']}_{variant:02d}",
        "kind": kind,
        "archetype": item["id"],
        "variant": variant,
        "targetPath": target_path,
        "prompt": prompt,
    }


def star_prompt(star: dict[str, Any], variant: int) -> str:
    return "\n".join(
        [
            "Use case: stylized-concept",
            "Asset type: high-resolution star texture source for Space Rangers Prototype",
            f"Primary request: {star['imagePrompt']}",
            f"Variant: {variant:02d}, keep the same stellar archetype but change plasma pattern and granular surface detail.",
            "Resolution target: square high-resolution source, suitable for a 2048x2048 runtime star disk.",
            "Composition/framing: centered circular stellar disk with generous removable black padding, no cropped edges.",
            "Lighting/mood: luminous astronomical plasma, crisp readable rim and corona, not overexposed.",
            "Materials/textures: sharp plasma cells, granular surface turbulence, detailed rim structure, no smeared low-frequency-only detail.",
            "Constraints: no text, no labels, no watermark, no spacecraft, no planets, no UI.",
        ]
    )


def background_prompt(backdrop: dict[str, Any], variant: int) -> str:
    return "\n".join(
        [
            "Use case: stylized-concept",
            "Asset type: high-resolution seamless space background source for Space Rangers Prototype",
            f"Primary request: {backdrop['imagePrompt']}",
            f"Variant: {variant:02d}, preserve the archetype but change star placement and nebula shape.",
            "Resolution target: 4K landscape source, processed later into a 4096x4096 runtime tile.",
            "Composition/framing: full rectangular seamless background, no obvious focal object, no hard borders.",
            "Lighting/mood: atmospheric deep space with enough dark negative space for ships and projectiles.",
            "Materials/textures: crisp small stars, readable dust lanes and nebula structure, no stretched blur, no compression artifacts.",
            "Constraints: no text, no labels, no watermark, no planets, no ships, no UI, no giant single star.",
        ]
    )


def planet_prompt(planet: dict[str, Any], variant: int) -> str:
    return "\n".join(
        [
            "Use case: stylized-concept",
            "Asset type: high-resolution seamless equirectangular planet surface source for Space Rangers Prototype",
            f"Primary request: {planet['imagePrompt']}",
            f"Variant: {variant:02d}, keep the archetype recognizable but vary terrain and weather pattern.",
            "Resolution target: 2:1 high-resolution source, ideally 3840x1920 or larger, processed later to 4096x2048.",
            "Composition/framing: full rectangular equirectangular map, seamless left-right wrap, no circular planet silhouette.",
            "Lighting/mood: albedo texture only, no baked directional shadow, no atmosphere glow, no star field.",
            "Materials/textures: crisp coastlines, terrain cracks, ridges, dunes, cloud bands or gas bands where appropriate; no soft upscaled look.",
            "Constraints: no text, no labels, no watermark, no terminator shadow, no UI, no borders, no black edge seams.",
        ]
    )


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


if __name__ == "__main__":
    main()
