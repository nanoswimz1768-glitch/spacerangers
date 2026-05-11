from __future__ import annotations

import argparse
import json
import math
import random
from pathlib import Path
from typing import Any, Callable, Iterable


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CATALOG = ROOT / "tools" / "star_system_catalog.json"
DEFAULT_OUT = ROOT / "game" / "assets" / "generated"
DEFAULT_PROMPT_OUT = ROOT / "tools" / "generated" / "imagegen_prompts.jsonl"
SOL_BACKGROUND_TEXTURE_ALPHA = 1.0
SOL_BACKGROUND_TEXTURE_PARALLAX = 0.08
SOL_BACKGROUND_STAR_PARALLAX = 0.32


def main() -> None:
    args = parse_args()
    catalog_path = Path(args.catalog)
    out_root = Path(args.out)
    catalog = load_json(catalog_path)

    generation = catalog["generation"]
    seed = args.seed if args.seed is not None else int(generation["defaultSeed"])
    sector_count = args.sectors if args.sectors is not None else int(generation.get("defaultSectorCount", 1))
    sector_count = max(1, sector_count)
    starting_sector = generation.get(
        "startingSector",
        {"id": "orion", "displayName": "Orion", "generatedSystemCount": 2},
    )
    if args.planet_showcase_highres:
        sector_count = 1
        starting_generated_count = 1
    elif args.coverage_highres_assets:
        sector_count = 1
        starting_generated_count = highres_coverage_system_count(catalog)
    else:
        requested_starting_generated = (
            args.count
            if args.count is not None
            else int(starting_sector.get("generatedSystemCount", 2))
        )
        sector_min, sector_max = sector_system_count_range(catalog)
        starting_total = clamp_int(requested_starting_generated + 1, sector_min, sector_max)
        starting_generated_count = max(1, starting_total - 1)

    if args.write_image_prompts:
        prompts_path = Path(args.prompt_out)
        write_image_prompts(catalog, prompts_path, args.prompt_variants)
        print(f"Wrote {prompts_path}")

    systems_dir = out_root / "systems"
    systems_dir.mkdir(parents=True, exist_ok=True)
    if args.clean:
        clean_generated_systems(systems_dir)

    if args.planet_showcase_highres:
        galaxy = generate_highres_planet_showcase_galaxy(catalog, seed)
    elif args.coverage_highres_assets:
        galaxy = generate_highres_coverage_galaxy(catalog, seed)
    else:
        galaxy = generate_galaxy(catalog, sector_count, starting_generated_count, seed)
    generated_systems = [
        system
        for sector in galaxy["sectors"]
        for system in sector["generatedSystems"]
    ]
    for system in generated_systems:
        system_path = systems_dir / f"{system['id']}.json"
        write_json(system_path, system)

    index = build_galaxy_index(galaxy, seed)
    write_json(out_root / "galaxy.json", index)
    print(f"Wrote {len(galaxy['sectors'])} sectors into {out_root / 'galaxy.json'}")
    print(f"Wrote {len(generated_systems)} generated systems into {systems_dir}")
    print(f"Wrote {out_root / 'galaxy.json'}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate offline star-system JSON files from the curated asset catalog."
    )
    parser.add_argument("--catalog", default=str(DEFAULT_CATALOG), help="Path to catalog.json.")
    parser.add_argument("--out", default=str(DEFAULT_OUT), help="Output root for galaxy.json and systems/.")
    parser.add_argument("--prompt-out", default=str(DEFAULT_PROMPT_OUT), help="Output path for imagegen prompt JSONL.")
    parser.add_argument(
        "--count",
        type=int,
        default=None,
        help="Generated systems in the starting sector, excluding the preset Sol system.",
    )
    parser.add_argument(
        "--coverage-highres-assets",
        action="store_true",
        help="Generate the smallest starting-sector set that covers all direct high-res stars, backdrops, and planet surfaces.",
    )
    parser.add_argument(
        "--planet-showcase-highres",
        action="store_true",
        help="Generate one starting-sector test system containing every direct high-res planet archetype.",
    )
    parser.add_argument("--sectors", type=int, default=None, help="Number of sectors to generate.")
    parser.add_argument("--seed", type=int, default=None, help="Galaxy seed.")
    parser.add_argument("--clean", action="store_true", help="Remove previously generated system JSON files first.")
    parser.add_argument(
        "--write-image-prompts",
        action="store_true",
        help="Also write imagegen_prompts.jsonl for future raster asset generation.",
    )
    parser.add_argument(
        "--prompt-variants",
        type=int,
        default=3,
        help="Prompt variants per planet/star archetype when --write-image-prompts is used.",
    )
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as stream:
        data = json.load(stream)
    if not isinstance(data, dict):
        raise ValueError(f"Expected object JSON in {path}")
    return data


def write_json(path: Path, data: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as stream:
        json.dump(data, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def clean_generated_systems(systems_dir: Path) -> None:
    for path in systems_dir.glob("*.json"):
        if path.is_file():
            path.unlink()


def generate_galaxy(
    catalog: dict[str, Any],
    sector_count: int,
    starting_generated_count: int,
    seed: int,
) -> dict[str, Any]:
    rng = random.Random(seed)
    used_names: set[str] = {"Sol"}
    used_sector_ids: set[str] = set()
    sectors: list[dict[str, Any]] = []

    generation = catalog["generation"]
    starting_sector = generation.get(
        "startingSector",
        {"id": "orion", "displayName": "Orion", "generatedSystemCount": 2},
    )
    starting_sector_id = slug(str(starting_sector.get("id", "orion"))) or "orion"
    starting_sector_name = str(starting_sector.get("displayName", "Orion"))
    starting_star_sequence = [
        str(item)
        for item in starting_sector.get("generatedStarArchetypes", [])
        if str(item).strip()
    ]
    used_sector_ids.add(starting_sector_id)
    sectors.append(
        generate_sector(
            catalog,
            starting_sector_id,
            starting_sector_name,
            starting_generated_count,
            rng,
            used_names,
            include_sol=True,
            star_sequence=starting_star_sequence,
        )
    )

    sector_min, sector_max = sector_system_count_range(catalog)
    sector_names = [
        str(name)
        for name in catalog.get("sectorNames", [])
        if str(name).strip() and str(name) != starting_sector_name
    ]
    for sector_index in range(2, sector_count + 1):
        fallback_name = f"Sector {sector_index:02d}"
        sector_name = sector_names[sector_index - 2] if sector_index - 2 < len(sector_names) else fallback_name
        sector_id = unique_sector_id(sector_name, used_sector_ids)
        generated_count = rng.randint(sector_min, sector_max)
        sectors.append(
            generate_sector(
                catalog,
                sector_id,
                sector_name,
                generated_count,
                rng,
                used_names,
                include_sol=False,
                star_sequence=[],
            )
        )

    return {
        "startingSectorId": starting_sector_id,
        "sectors": sectors,
    }


def highres_coverage_system_count(catalog: dict[str, Any]) -> int:
    return max(
        len(highres_star_archetypes(catalog)),
        len(highres_backdrop_archetypes(catalog)),
        len(highres_planet_archetypes(catalog)),
    )


def generate_highres_coverage_galaxy(catalog: dict[str, Any], seed: int) -> dict[str, Any]:
    rng = random.Random(seed)
    used_names: set[str] = {"Sol"}
    generation = catalog["generation"]
    starting_sector = generation.get(
        "startingSector",
        {"id": "orion", "displayName": "Orion", "generatedSystemCount": 2},
    )
    starting_sector_id = slug(str(starting_sector.get("id", "orion"))) or "orion"
    starting_sector_name = str(starting_sector.get("displayName", "Orion"))

    return {
        "startingSectorId": starting_sector_id,
        "sectors": [
            generate_highres_coverage_sector(
                catalog,
                starting_sector_id,
                starting_sector_name,
                rng,
                used_names,
            )
        ],
    }


def generate_highres_planet_showcase_galaxy(catalog: dict[str, Any], seed: int) -> dict[str, Any]:
    generation = catalog["generation"]
    starting_sector = generation.get(
        "startingSector",
        {"id": "orion", "displayName": "Orion", "generatedSystemCount": 2},
    )
    starting_sector_id = slug(str(starting_sector.get("id", "orion"))) or "orion"
    starting_sector_name = str(starting_sector.get("displayName", "Orion"))
    system = generate_highres_planet_showcase_system(
        catalog,
        f"{starting_sector_id}_0001",
        "Planet Showcase",
        seed,
        random.Random(seed),
        starting_sector_id,
        starting_sector_name,
    )
    return {
        "startingSectorId": starting_sector_id,
        "sectors": [
            {
                "id": starting_sector_id,
                "name": starting_sector_name,
                "isStartingSector": True,
                "systemCount": 2,
                "systems": [
                    sol_index_entry(starting_sector_id, starting_sector_name),
                    generated_system_index_entry(system),
                ],
                "generatedSystems": [system],
            }
        ],
    }


def generate_highres_coverage_sector(
    catalog: dict[str, Any],
    sector_id: str,
    sector_name: str,
    rng: random.Random,
    used_names: set[str],
) -> dict[str, Any]:
    stars = highres_star_archetypes(catalog)
    backdrops = highres_backdrop_archetypes(catalog)
    planets = highres_planet_archetypes(catalog)
    generated_count = max(len(stars), len(backdrops), len(planets))

    systems: list[dict[str, Any]] = [sol_index_entry(sector_id, sector_name)]
    generated_systems: list[dict[str, Any]] = []
    for local_index in range(1, generated_count + 1):
        system_seed = rng.randrange(1, 2**31 - 1)
        system_rng = random.Random(system_seed)
        name = unique_system_name(catalog, system_rng, used_names)
        system = generate_highres_coverage_system(
            catalog,
            f"{sector_id}_{local_index:04d}",
            name,
            system_seed,
            system_rng,
            sector_id,
            sector_name,
            stars[(local_index - 1) % len(stars)],
            backdrops[(local_index - 1) % len(backdrops)],
            planets[(local_index - 1) % len(planets)],
        )
        generated_systems.append(system)
        systems.append(generated_system_index_entry(system))

    return {
        "id": sector_id,
        "name": sector_name,
        "isStartingSector": True,
        "systemCount": len(systems),
        "systems": systems,
        "generatedSystems": generated_systems,
    }


def generate_sector(
    catalog: dict[str, Any],
    sector_id: str,
    sector_name: str,
    generated_count: int,
    rng: random.Random,
    used_names: set[str],
    include_sol: bool,
    star_sequence: list[str],
) -> dict[str, Any]:
    systems: list[dict[str, Any]] = []
    generated_systems: list[dict[str, Any]] = []

    if include_sol:
        systems.append(sol_index_entry(sector_id, sector_name))

    for local_index in range(1, generated_count + 1):
        system_seed = rng.randrange(1, 2**31 - 1)
        system_rng = random.Random(system_seed)
        name = unique_system_name(catalog, system_rng, used_names)
        system_id = f"{sector_id}_{local_index:04d}"
        forced_star_id = star_sequence[(local_index - 1) % len(star_sequence)] if star_sequence else ""
        system = generate_system(
            catalog,
            system_id,
            name,
            system_seed,
            system_rng,
            sector_id,
            sector_name,
            forced_star_id,
        )
        generated_systems.append(system)
        systems.append(generated_system_index_entry(system))

    return {
        "id": sector_id,
        "name": sector_name,
        "isStartingSector": include_sol,
        "systemCount": len(systems),
        "systems": systems,
        "generatedSystems": generated_systems,
    }


def build_galaxy_index(galaxy: dict[str, Any], seed: int) -> dict[str, Any]:
    sectors: list[dict[str, Any]] = []
    generated_count = 0
    system_count = 0
    for sector in galaxy["sectors"]:
        systems = sector["systems"]
        generated_count += sum(1 for system in systems if system["source"] == "generated")
        system_count += len(systems)
        sectors.append(
            {
                "id": sector["id"],
                "name": sector["name"],
                "isStartingSector": sector["isStartingSector"],
                "systemCount": len(systems),
                "systems": systems,
            }
        )

    return {
        "schemaVersion": 2,
        "seed": seed,
        "startingSectorId": galaxy["startingSectorId"],
        "sectorCount": len(sectors),
        "systemCount": system_count,
        "generatedSystemCount": generated_count,
        "sectors": sectors,
    }


def sol_index_entry(sector_id: str, sector_name: str) -> dict[str, Any]:
    return {
        "id": "sol",
        "name": "Sol",
        "source": "preset",
        "sectorId": sector_id,
        "sectorName": sector_name,
        "seed": 0,
        "starArchetype": "yellow_main_sequence",
        "backgroundArchetype": "sol_nebula",
        "planetCount": 8,
        "file": "",
    }


def generated_system_index_entry(system: dict[str, Any]) -> dict[str, Any]:
    return {
        "id": system["id"],
        "name": system["name"],
        "source": "generated",
        "sectorId": system["sectorId"],
        "sectorName": system["sectorName"],
        "seed": system["seed"],
        "starArchetype": system["star"]["archetype"],
        "backgroundArchetype": system["background"]["archetype"],
        "planetCount": len(system["planets"]),
        "file": f"res://assets/generated/systems/{system['id']}.json",
    }


def generate_system(
    catalog: dict[str, Any],
    system_id: str,
    name: str,
    seed: int,
    rng: random.Random,
    sector_id: str,
    sector_name: str,
    forced_star_id: str = "",
) -> dict[str, Any]:
    star = star_archetype_by_id(catalog["starArchetypes"], forced_star_id) if forced_star_id else None
    if star is None:
        star = weighted_choice(catalog["starArchetypes"], rng, lambda item: float(item["weight"]))
    star_texture_set = star_texture_set_for_star(catalog, star, rng)
    star_size = round(rand_range(rng, star["worldSizeRange"]), 2)
    planet_count = planet_count_for_star(catalog, star, rng)
    orbit_min, orbit_max = star["orbitRadiusRange"]
    orbit_radii = generate_orbits(
        rng,
        planet_count,
        float(orbit_min),
        float(orbit_max),
        float(catalog["generation"].get("orbitJitter", 0.22)),
    )

    planets = [
        generate_planet(catalog, star, star_size, name, planet_index, radius, rng)
        for planet_index, radius in enumerate(orbit_radii, start=1)
    ]
    background_rng = random.Random(seed ^ 0x5A17BACC)
    backdrop = choose_backdrop(catalog, star, background_rng)

    return {
        "schemaVersion": 2,
        "id": system_id,
        "name": name,
        "sectorId": sector_id,
        "sectorName": sector_name,
        "seed": seed,
        "star": {
            "archetype": star["id"],
            "displayName": star["displayName"],
            "textureSet": star_texture_set["id"],
            "texturePath": star_texture_set.get("path", ""),
            "frameDirectory": star_texture_set.get("frameDirectory", ""),
            "framePrefix": star_texture_set.get("framePrefix", "sun_"),
            "frameCount": int(star_texture_set.get("frameCount", 0)),
            "source": star_texture_set.get("source", "catalog"),
            "worldSize": star_size,
            "diskTint": star["diskTint"],
            "coronaColor": star["coronaColor"],
            "coronaIntensity": round(rand_range(rng, star["coronaIntensityRange"]), 3),
            "animationSpeed": round(rand_range(rng, star["animationSpeedRange"]), 3),
            "mapColor": star["coronaColor"],
        },
        "background": generate_background(catalog, star, backdrop, rng),
        "planets": planets,
    }


def generate_highres_coverage_system(
    catalog: dict[str, Any],
    system_id: str,
    name: str,
    seed: int,
    rng: random.Random,
    sector_id: str,
    sector_name: str,
    star: dict[str, Any],
    backdrop: dict[str, Any],
    planet_archetype: dict[str, Any],
) -> dict[str, Any]:
    star_texture_set = highres_star_texture_set_for_star(catalog, star)
    star_size = round(rand_range(rng, star["worldSizeRange"]), 2)
    orbit_radius = coverage_orbit_radius(star, preferred_planet_zone(planet_archetype))
    planet = generate_planet(
        catalog,
        star,
        star_size,
        name,
        1,
        orbit_radius,
        rng,
        forced_archetype=planet_archetype,
        forced_surface=highres_surface_map_for_planet(planet_archetype),
    )
    background_rng = random.Random(seed ^ 0x5A17BACC)

    return {
        "schemaVersion": 2,
        "id": system_id,
        "name": name,
        "sectorId": sector_id,
        "sectorName": sector_name,
        "seed": seed,
        "star": {
            "archetype": star["id"],
            "displayName": star["displayName"],
            "textureSet": star_texture_set["id"],
            "texturePath": star_texture_set.get("path", ""),
            "frameDirectory": star_texture_set.get("frameDirectory", ""),
            "framePrefix": star_texture_set.get("framePrefix", "sun_"),
            "frameCount": int(star_texture_set.get("frameCount", 0)),
            "source": star_texture_set.get("source", "catalog"),
            "worldSize": star_size,
            "diskTint": star["diskTint"],
            "coronaColor": star["coronaColor"],
            "coronaIntensity": round(rand_range(rng, star["coronaIntensityRange"]), 3),
            "animationSpeed": round(rand_range(rng, star["animationSpeedRange"]), 3),
            "mapColor": star["coronaColor"],
        },
        "background": generate_background(catalog, star, backdrop, background_rng),
        "planets": [planet],
    }


def generate_highres_planet_showcase_system(
    catalog: dict[str, Any],
    system_id: str,
    name: str,
    seed: int,
    rng: random.Random,
    sector_id: str,
    sector_name: str,
) -> dict[str, Any]:
    star = star_archetype_by_id(catalog["starArchetypes"], "yellow_main_sequence") or highres_star_archetypes(catalog)[0]
    star_texture_set = highres_star_texture_set_for_star(catalog, star)
    star_size = round((float(star["worldSizeRange"][0]) + float(star["worldSizeRange"][1])) * 0.5, 2)
    backdrop = backdrop_archetype_by_id(catalog["spaceBackdropArchetypes"], "black_silent_reach") or highres_backdrop_archetypes(catalog)[0]
    planets: list[dict[str, Any]] = []
    planet_archetypes = highres_planet_archetypes(catalog)
    orbit_start = 1450.0
    orbit_end = 7600.0
    orbit_step = (orbit_end - orbit_start) / max(1, len(planet_archetypes) - 1)
    for index, planet_archetype in enumerate(planet_archetypes):
        planet = generate_planet(
            catalog,
            star,
            star_size,
            name,
            index + 1,
            orbit_start + orbit_step * index,
            rng,
            forced_archetype=planet_archetype,
            forced_surface=highres_surface_map_for_planet(planet_archetype),
        )
        planet["id"] = f"{slug(name)}_{planet_archetype['id']}"
        planet["name"] = str(planet_archetype.get("displayName", planet_archetype["id"]))
        planet["initialAngle"] = round(index / max(1, len(planet_archetypes)) * math.tau, 4)
        planets.append(planet)

    return {
        "schemaVersion": 2,
        "id": system_id,
        "name": name,
        "sectorId": sector_id,
        "sectorName": sector_name,
        "seed": seed,
        "star": {
            "archetype": star["id"],
            "displayName": star["displayName"],
            "textureSet": star_texture_set["id"],
            "texturePath": star_texture_set.get("path", ""),
            "frameDirectory": star_texture_set.get("frameDirectory", ""),
            "framePrefix": star_texture_set.get("framePrefix", "sun_"),
            "frameCount": int(star_texture_set.get("frameCount", 0)),
            "source": star_texture_set.get("source", "catalog"),
            "worldSize": star_size,
            "diskTint": star["diskTint"],
            "coronaColor": star["coronaColor"],
            "coronaIntensity": round(rand_range(rng, star["coronaIntensityRange"]), 3),
            "animationSpeed": round(rand_range(rng, star["animationSpeedRange"]), 3),
            "mapColor": star["coronaColor"],
        },
        "background": generate_background(catalog, star, backdrop, random.Random(seed ^ 0x5A17BACC)),
        "planets": planets,
    }


def planet_count_for_star(catalog: dict[str, Any], star: dict[str, Any], rng: random.Random) -> int:
    global_min, global_max = catalog["generation"].get("planetCountRange", [1, 10])
    star_min, star_max = star["planetCountRange"]
    low = max(int(global_min), int(star_min))
    high = min(int(global_max), int(star_max))
    if high < low:
        high = low
    return rng.randint(low, high)


def choose_backdrop(catalog: dict[str, Any], star: dict[str, Any], rng: random.Random) -> dict[str, Any]:
    star_id = star["id"]
    return weighted_choice(
        catalog["spaceBackdropArchetypes"],
        rng,
        lambda item: float(item["weight"]) * float(item.get("starWeights", {}).get(star_id, 1.0)),
    )


def generate_background(
    catalog: dict[str, Any],
    star: dict[str, Any],
    backdrop: dict[str, Any],
    rng: random.Random,
) -> dict[str, Any]:
    texture_set = texture_set_by_id(catalog["spaceBackdropTextureSets"], backdrop["textureSet"])
    palette = unique_palette(backdrop["nebulaPalette"], star["nebulaPalette"])

    return {
        "archetype": backdrop["id"],
        "displayName": backdrop["displayName"],
        "textureSet": backdrop["textureSet"],
        "texturePath": texture_set["path"],
        "source": texture_set.get("source", "catalog"),
        "textureTint": backdrop["textureTint"],
        "textureAlpha": SOL_BACKGROUND_TEXTURE_ALPHA,
        "textureParallax": SOL_BACKGROUND_TEXTURE_PARALLAX,
        "starParallax": SOL_BACKGROUND_STAR_PARALLAX,
        "starfieldSeed": rng.randrange(1, 2**31 - 1),
        "starCount": rng.randint(int(backdrop["starCountRange"][0]), int(backdrop["starCountRange"][1])),
        "nebulaSeed": rng.randrange(1, 2**31 - 1),
        "nebulaBlobCount": rng.randint(
            int(backdrop["nebulaBlobCountRange"][0]),
            int(backdrop["nebulaBlobCountRange"][1]),
        ),
        "dustDensity": round(rand_range(rng, backdrop["dustDensityRange"]), 3),
        "nebulaPalette": palette,
    }


def texture_set_by_id(texture_sets: list[dict[str, Any]], texture_set_id: str) -> dict[str, Any]:
    for texture_set in texture_sets:
        if texture_set["id"] == texture_set_id:
            return texture_set
    raise ValueError(f"Unknown texture set: {texture_set_id}")


def highres_star_archetypes(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    stars = list(catalog["starArchetypes"])
    for star in stars:
        highres_star_texture_set_for_star(catalog, star)
    return stars


def highres_backdrop_archetypes(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    backdrops = list(catalog["spaceBackdropArchetypes"])
    for backdrop in backdrops:
        texture_set = texture_set_by_id(catalog["spaceBackdropTextureSets"], backdrop["textureSet"])
        if texture_set.get("quality") != "direct_highres":
            raise ValueError(f"Backdrop {backdrop['id']} does not use a direct high-res texture set.")
    return backdrops


def highres_planet_archetypes(catalog: dict[str, Any]) -> list[dict[str, Any]]:
    planets = list(catalog["planetArchetypes"])
    for planet in planets:
        highres_surface_map_for_planet(planet)
    return planets


def highres_star_texture_set_for_star(catalog: dict[str, Any], star: dict[str, Any]) -> dict[str, Any]:
    texture_set_ids: list[str] = []
    for option in star.get("textureSets", []):
        texture_set_id = str(option.get("id", "")).strip()
        if texture_set_id:
            texture_set_ids.append(texture_set_id)

    default_texture_set = str(star.get("textureSet", "")).strip()
    if default_texture_set:
        texture_set_ids.append(default_texture_set)

    expected_id = f"generated_highres_{star['id']}_01"
    texture_set_ids.append(expected_id)

    seen: set[str] = set()
    for texture_set_id in texture_set_ids:
        if texture_set_id in seen:
            continue
        seen.add(texture_set_id)
        try:
            texture_set = texture_set_by_id(catalog["starTextureSets"], texture_set_id)
        except ValueError:
            continue
        if texture_set.get("source") == "imagegen_direct_highres":
            return texture_set

    raise ValueError(f"Star {star['id']} has no direct high-res texture set.")


def star_texture_set_for_star(catalog: dict[str, Any], star: dict[str, Any], rng: random.Random) -> dict[str, Any]:
    texture_sets = catalog["starTextureSets"]
    options = star.get("textureSets")
    if isinstance(options, list) and options:
        selected = weighted_choice(options, rng, lambda item: float(item.get("weight", 1.0)))
        return texture_set_by_id(texture_sets, selected["id"])

    return texture_set_by_id(texture_sets, star["textureSet"])


def star_archetype_by_id(stars: list[dict[str, Any]], star_id: str) -> dict[str, Any] | None:
    for star in stars:
        if star["id"] == star_id:
            return star
    return None


def backdrop_archetype_by_id(backdrops: list[dict[str, Any]], backdrop_id: str) -> dict[str, Any] | None:
    for backdrop in backdrops:
        if backdrop["id"] == backdrop_id:
            return backdrop
    return None


def choose_surface_map(archetype: dict[str, Any], rng: random.Random) -> dict[str, Any]:
    surface_maps = archetype["surfaceMaps"]
    processed_maps = [
        surface
        for surface in surface_maps
        if surface.get("source") == "processed_planet_surface"
    ]
    if processed_maps:
        return weighted_choice(processed_maps, rng, lambda item: float(item["weight"]))

    generated_maps = [
        surface
        for surface in surface_maps
        if surface.get("source") == "imagegen_sheet"
    ]
    pool = generated_maps if generated_maps else surface_maps
    return weighted_choice(pool, rng, lambda item: float(item["weight"]))


def highres_surface_map_for_planet(archetype: dict[str, Any]) -> dict[str, Any]:
    for surface in archetype["surfaceMaps"]:
        if surface.get("quality") == "direct_highres":
            return surface
    raise ValueError(f"Planet {archetype['id']} has no direct high-res surface map.")


def preferred_planet_zone(archetype: dict[str, Any]) -> str:
    weights = archetype.get("zoneWeights", {})
    if not weights:
        return "habitable"
    return max(("inner", "habitable", "outer"), key=lambda zone: float(weights.get(zone, 0.0)))


def coverage_orbit_radius(star: dict[str, Any], zone: str) -> float:
    orbit_min, orbit_max = (float(star["orbitRadiusRange"][0]), float(star["orbitRadiusRange"][1]))
    hab_inner, hab_outer = (float(star["habitableOrbitRange"][0]), float(star["habitableOrbitRange"][1]))
    if zone == "inner" and orbit_min < hab_inner:
        return round((orbit_min + hab_inner) * 0.5, 2)
    if zone == "outer" and hab_outer < orbit_max:
        return round((hab_outer + orbit_max) * 0.5, 2)
    return round((max(orbit_min, hab_inner) + min(orbit_max, hab_outer)) * 0.5, 2)


def unique_palette(primary: list[str], secondary: list[str]) -> list[str]:
    result: list[str] = []
    for color in [*primary, *secondary]:
        if color not in result:
            result.append(color)
    return result[:6]


def generate_orbits(
    rng: random.Random,
    count: int,
    orbit_min: float,
    orbit_max: float,
    jitter: float,
) -> list[float]:
    if count <= 0:
        return []

    span = orbit_max - orbit_min
    step = span / max(count, 1)
    radii: list[float] = []
    min_gap = 420.0
    previous = orbit_min - min_gap

    for index in range(count):
        slot_start = orbit_min + step * index
        radius = slot_start + step * rng.uniform(0.18, 0.82)
        radius += step * rng.uniform(-jitter, jitter) * 0.5
        radius = max(radius, previous + min_gap)
        radius = min(radius, orbit_max - (count - index - 1) * min_gap)
        radii.append(round(radius, 2))
        previous = radius

    return radii


def generate_planet(
    catalog: dict[str, Any],
    star: dict[str, Any],
    star_size: float,
    system_name: str,
    planet_index: int,
    orbit_radius: float,
    rng: random.Random,
    forced_archetype: dict[str, Any] | None = None,
    forced_surface: dict[str, Any] | None = None,
) -> dict[str, Any]:
    zone = orbit_zone(star, orbit_radius)
    archetype = forced_archetype or weighted_choice(
        catalog["planetArchetypes"],
        rng,
        lambda item: float(item["weight"]) * float(item["zoneWeights"].get(zone, 0.0)),
    )
    visual = archetype["visual"]
    body_diameter = planet_diameter(catalog, archetype, star_size, rng)
    surface = forced_surface or choose_surface_map(archetype, rng)
    map_color = rng.choice(archetype["mapColors"])
    atmosphere_color = rng.choice(visual["atmosphereColors"])
    rings = maybe_generate_rings(catalog, visual, rng)

    orbit_period = 100.0 * math.pow(max(0.2, orbit_radius / 2360.0), 1.5) * rng.uniform(0.84, 1.16)
    rotation_period = rng.uniform(3.5, 14.0) * (-1.0 if rng.random() < 0.14 else 1.0)

    return {
        "id": f"{slug(system_name)}_{planet_index}",
        "name": f"{system_name} {roman(planet_index)}",
        "archetype": archetype["id"],
        "zone": zone,
        "surfaceMap": surface["path"],
        "source": surface.get("source", "catalog"),
        "orbitRadius": round(orbit_radius, 2),
        "bodyRadius": round(body_diameter * 0.5, 2),
        "referenceTextureWorldSize": round(body_diameter, 2),
        "textureWorldSize": round(body_diameter, 2),
        "orbitPeriodSeconds": round(max(38.0, orbit_period), 2),
        "rotationPeriodSeconds": round(rotation_period, 2),
        "initialAngle": round(rng.random() * math.tau, 4),
        "mapColor": map_color,
        "visual": {
            "atmosphereColor": atmosphere_color,
            "atmosphereStrength": round(rand_range(rng, visual["atmosphereStrengthRange"]), 4),
            "rotationSpeed": round(rand_range(rng, visual["rotationSpeedRange"]), 5),
            "flowStrength": round(rand_range(rng, visual["flowStrengthRange"]), 5),
            "contrast": round(rand_range(rng, visual["contrastRange"]), 3),
            "saturation": round(rand_range(rng, visual["saturationRange"]), 3),
            "glowStrength": round(rand_range(rng, visual["glowStrengthRange"]), 3),
            "rings": rings,
        },
    }


def planet_diameter(
    catalog: dict[str, Any],
    archetype: dict[str, Any],
    star_size: float,
    rng: random.Random,
) -> float:
    requested = rand_range(rng, archetype["bodyDiameterRange"])
    max_ratio = float(catalog["generation"].get("maxPlanetDiameterToStarSizeRatio", 0.82))
    max_diameter = max(80.0, star_size * max_ratio)
    return min(requested, max_diameter)


def maybe_generate_rings(
    catalog: dict[str, Any],
    visual: dict[str, Any],
    rng: random.Random,
) -> dict[str, Any] | None:
    ring_chance = float(visual.get("ringChance", 0.0))
    if rng.random() > ring_chance:
        return None

    weights = visual.get("ringProfileWeights", {})
    profiles = catalog["ringProfiles"]
    if weights:
        profile = weighted_choice(profiles, rng, lambda item: float(weights.get(item["id"], 0.0)))
    else:
        profile = rng.choice(profiles)

    return {
        "profile": profile["id"],
        "innerRadiusFactor": profile["innerRadiusFactor"],
        "outerRadiusFactor": profile["outerRadiusFactor"],
        "flattening": profile["flattening"],
        "rotation": round(float(profile["rotation"]) + rng.uniform(-0.08, 0.08), 4),
        "alpha": round(rand_range(rng, profile["alphaRange"]), 3),
        "color": profile["color"],
        "accentColor": profile["accentColor"],
    }


def orbit_zone(star: dict[str, Any], orbit_radius: float) -> str:
    inner_edge, outer_edge = star["habitableOrbitRange"]
    if orbit_radius < float(inner_edge):
        return "inner"
    if orbit_radius <= float(outer_edge):
        return "habitable"
    return "outer"


def unique_system_name(catalog: dict[str, Any], rng: random.Random, used_names: set[str]) -> str:
    parts = catalog["nameParts"]
    for _ in range(100):
        name = (
            rng.choice(parts["prefixes"])
            + " "
            + (rng.choice(parts["cores"]) + rng.choice(parts["suffixes"])).capitalize()
        )
        if name not in used_names:
            used_names.add(name)
            return name

    fallback = f"System {len(used_names) + 1:04d}"
    used_names.add(fallback)
    return fallback


def weighted_choice(
    items: Iterable[dict[str, Any]],
    rng: random.Random,
    weight: Callable[[dict[str, Any]], float],
) -> dict[str, Any]:
    pool = list(items)
    total = sum(max(0.0, weight(item)) for item in pool)
    if total <= 0.0:
        return rng.choice(pool)

    marker = rng.random() * total
    running = 0.0
    for item in pool:
        running += max(0.0, weight(item))
        if marker <= running:
            return item

    return pool[-1]


def rand_range(rng: random.Random, pair: list[float] | tuple[float, float]) -> float:
    return rng.uniform(float(pair[0]), float(pair[1]))


def sector_system_count_range(catalog: dict[str, Any]) -> tuple[int, int]:
    raw = catalog["generation"].get("sectorSystemCountRange", [2, 5])
    low = max(2, int(raw[0]))
    high = max(low, int(raw[1]))
    return low, high


def clamp_int(value: int, low: int, high: int) -> int:
    return max(low, min(high, value))


def unique_sector_id(name: str, used_ids: set[str]) -> str:
    base = slug(name) or "sector"
    candidate = base
    suffix = 2
    while candidate in used_ids:
        candidate = f"{base}_{suffix}"
        suffix += 1

    used_ids.add(candidate)
    return candidate


def slug(value: str) -> str:
    return "".join(char.lower() if char.isalnum() else "_" for char in value).strip("_")


def roman(value: int) -> str:
    numerals = [
        (10, "X"),
        (9, "IX"),
        (5, "V"),
        (4, "IV"),
        (1, "I"),
    ]
    result = []
    remaining = value
    for amount, label in numerals:
        while remaining >= amount:
            result.append(label)
            remaining -= amount
    return "".join(result)


def write_image_prompts(catalog: dict[str, Any], out_path: Path, variants: int) -> None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8") as stream:
        for star in catalog["starArchetypes"]:
            for variant in range(1, variants + 1):
                write_json_line(
                    stream,
                    {
                        "assetId": f"star/{star['id']}_{variant:02d}",
                        "kind": "star",
                        "archetype": star["id"],
                        "variant": variant,
                        "targetPath": f"game/assets/generated/star_sources_4k/{star['id']}_{variant:02d}.png",
                        "prompt": star_prompt(star, variant),
                    },
                )

        for backdrop in catalog["spaceBackdropArchetypes"]:
            for variant in range(1, variants + 1):
                write_json_line(
                    stream,
                    {
                        "assetId": f"background/{backdrop['id']}_{variant:02d}",
                        "kind": "background",
                        "archetype": backdrop["id"],
                        "variant": variant,
                        "targetPath": f"game/assets/generated/background_sources_4k/{backdrop['id']}_{variant:02d}.png",
                        "prompt": background_prompt(backdrop, variant),
                    },
                )

        for planet in catalog["planetArchetypes"]:
            for variant in range(1, variants + 1):
                write_json_line(
                    stream,
                    {
                        "assetId": f"planet/{planet['id']}_{variant:02d}",
                        "kind": "planet",
                        "archetype": planet["id"],
                        "variant": variant,
                        "targetPath": f"game/assets/generated/planet_sources_4k/{planet['id']}_{variant:02d}.png",
                        "prompt": planet_prompt(planet, variant),
                    },
                )


def write_json_line(stream: Any, data: dict[str, Any]) -> None:
    stream.write(json.dumps(data, ensure_ascii=False))
    stream.write("\n")


def star_prompt(star: dict[str, Any], variant: int) -> str:
    return "\n".join(
        [
            "Use case: stylized-concept",
            "Asset type: high-resolution star texture source for a 2D space game",
            f"Primary request: {star['imagePrompt']}",
            f"Variant: {variant:02d}, keep the same archetype but change plasma pattern and surface details.",
            "Resolution target: square high-resolution source, suitable for a 2048x2048 runtime star disk.",
            "Composition/framing: centered circular stellar disk, generous removable black padding, no cropped edges.",
            "Lighting/mood: luminous astronomical plasma, crisp readable rim and corona, not overexposed.",
            "Materials/textures: sharp plasma cells, granular surface turbulence, no smeared or low-frequency-only detail.",
            "Constraints: no text, no labels, no watermark, no spacecraft, no planets, no UI.",
        ]
    )


def background_prompt(backdrop: dict[str, Any], variant: int) -> str:
    return "\n".join(
        [
            "Use case: stylized-concept",
            "Asset type: high-resolution seamless 2D space background source for top-down flight",
            f"Primary request: {backdrop['imagePrompt']}",
            f"Variant: {variant:02d}, preserve the archetype but change star placement and nebula shape.",
            "Resolution target: 4K landscape source; it will be processed into a 4096x4096 runtime tile.",
            "Composition/framing: full rectangular seamless background, no obvious focal object, no hard borders.",
            "Lighting/mood: atmospheric deep space with enough dark negative space for ships and projectiles.",
            "Materials/textures: crisp small stars and readable nebula structure, no stretched blur, no compression artifacts.",
            "Constraints: no text, no labels, no watermark, no planets, no ships, no UI, no giant single star.",
        ]
    )


def planet_prompt(planet: dict[str, Any], variant: int) -> str:
    return "\n".join(
        [
            "Use case: stylized-concept",
            "Asset type: high-resolution seamless equirectangular planet surface texture for an animated sphere shader",
            f"Primary request: {planet['imagePrompt']}",
            f"Variant: {variant:02d}, keep the archetype recognizable but vary terrain/cloud pattern.",
            "Resolution target: 2:1 high-resolution source, ideally 3840x1920 or larger, processed to 4096x2048.",
            "Composition/framing: full rectangular equirectangular map, seamless left-right wrap, no circular planet silhouette.",
            "Lighting/mood: albedo texture only, no baked directional shadow, no atmosphere glow, no star field.",
            "Materials/textures: crisp coastlines, terrain cracks, ridges, cloud bands or gas bands where appropriate; no soft upscaled look.",
            "Constraints: no text, no labels, no watermark, no terminator shadow, no UI, no borders, no black edge seams.",
        ]
    )


if __name__ == "__main__":
    main()
