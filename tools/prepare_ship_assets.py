from __future__ import annotations

import json
import re
from pathlib import Path

from PIL import Image, ImageEnhance, ImageFilter


CANVAS_SIZE = 256
TARGET_OCCUPANCY = 0.76


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    source = find_source_dir(root)
    output = root / "game" / "assets" / "ships"
    output.mkdir(parents=True, exist_ok=True)

    manifest = []
    for source_file in sorted(source.glob("*.webp")):
        image = Image.open(source_file).convert("RGBA")
        prepared, bounds = prepare_image(image)
        slug = source_file.stem
        output_file = output / f"{slug}.png"
        prepared.save(output_file)

        race, hull = parse_ship_name(slug)
        manifest.append(
            {
                "id": slug,
                "race": race,
                "hull": hull,
                "path": f"res://assets/ships/{slug}.png",
                "source_size": list(image.size),
                "content_bounds": list(bounds),
            }
        )

    (output / "ships_manifest.json").write_text(
        json.dumps(manifest, indent=2),
        encoding="utf-8",
    )
    print(f"Prepared {len(manifest)} ship sprites from {source} into {output}")


def find_source_dir(root: Path) -> Path:
    candidates = [
        path
        for path in root.iterdir()
        if path.is_dir() and any(child.suffix.lower() == ".webp" for child in path.iterdir())
    ]
    if not candidates:
        raise FileNotFoundError("No source directory with .webp ship files was found.")

    return sorted(candidates, key=lambda path: path.name.casefold())[0]


def parse_ship_name(name: str) -> tuple[str, str]:
    match = re.match(r"^2([A-Za-z]+)([A-Z])$", name)
    if not match:
        return "Unknown", "Unknown"

    return match.group(1), match.group(2)


def prepare_image(image: Image.Image) -> tuple[Image.Image, tuple[int, int, int, int]]:
    alpha = image.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        bounds = (0, 0, image.width, image.height)

    cropped = image.crop(bounds)
    cropped = cropped.filter(ImageFilter.UnsharpMask(radius=0.65, percent=118, threshold=2))
    cropped = ImageEnhance.Contrast(cropped).enhance(1.08)
    cropped = ImageEnhance.Color(cropped).enhance(1.05)

    max_side = max(cropped.width, cropped.height)
    target_side = int(CANVAS_SIZE * TARGET_OCCUPANCY)
    scale = target_side / max_side
    new_size = (max(1, round(cropped.width * scale)), max(1, round(cropped.height * scale)))
    cropped = cropped.resize(new_size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(
        cropped,
        ((CANVAS_SIZE - cropped.width) // 2, (CANVAS_SIZE - cropped.height) // 2),
    )
    return canvas, bounds


if __name__ == "__main__":
    main()

