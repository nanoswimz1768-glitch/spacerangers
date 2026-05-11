from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np
import requests
from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "game" / "assets" / "planets"
SOURCE_DIR = ROOT / "tools" / "source_assets" / "nasa"
SIZE = 768

SPACEPLACE_URL = "https://spaceplace.nasa.gov/gallery-solar-system/en/planets.en.jpg"
EARTH_URL = "https://eoimages.gsfc.nasa.gov/images/imagerecords/57000/57730/land_ocean_ice_2048.jpg"
JUPITER_URL = "https://assets.science.nasa.gov/content/dam/science/missions/hubble/releases/2019/08/STScI-01EVSV97WZ61MB1MXJK1MDBPEE.tif/jcr:content/renditions/cq5dam.web.1280.1280.jpeg"
SATURN_URL = "https://assets.science.nasa.gov/content/dam/science/missions/webb/outreach/migrated/2023/STScI-01H41MPWAVF7SRQSHZWQNYV2J0.png/jcr:content/renditions/Unannotated.jpg"

SPACEPLACE_CROPS = {
    "mercury": (112, 34, 304, 226),
    "venus": (453, 34, 645, 226),
    "earth": (795, 34, 987, 228),
    "mars": (111, 226, 305, 421),
    "saturn": (721, 236, 1049, 418),
    "uranus": (154, 414, 268, 624),
    "neptune": (455, 423, 644, 613),
}


@dataclass(frozen=True)
class PlanetSpec:
    name: str
    seed: int
    radius: int
    kind: str


PLANETS = [
    PlanetSpec("mercury", 1141, 300, "mercury"),
    PlanetSpec("venus", 2207, 312, "venus"),
    PlanetSpec("earth", 3319, 314, "earth"),
    PlanetSpec("mars", 4421, 304, "mars"),
    PlanetSpec("jupiter", 5501, 330, "jupiter"),
    PlanetSpec("saturn", 6619, 220, "saturn"),
    PlanetSpec("uranus", 7723, 316, "uranus"),
    PlanetSpec("neptune", 8849, 316, "neptune"),
]


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)

    spaceplace = Image.open(download_source(SPACEPLACE_URL, SOURCE_DIR / "spaceplace_planets.jpg")).convert("RGB")
    earth_source = Image.open(download_source(EARTH_URL, SOURCE_DIR / "earth_blue_marble.jpg")).convert("RGB")
    jupiter_source = Image.open(download_source(JUPITER_URL, SOURCE_DIR / "jupiter_hubble_2019.jpg")).convert("RGB")
    saturn_source = Image.open(download_source(SATURN_URL, SOURCE_DIR / "saturn_webb_unannotated.jpg")).convert("RGB")

    for spec in PLANETS:
        if spec.name == "earth":
            image = render_equirectangular_sphere(
                earth_source,
                target_fill=0.84,
                central_longitude_degrees=20.0,
                atmosphere=(56, 155, 255),
                color_boost=1.08,
                contrast_boost=1.04,
            )
        elif spec.name == "jupiter":
            source = crop_largest_body(jupiter_source, padding=18)
            image = cutout_to_planet_sprite(source, target_fill=0.9, color_boost=1.05, contrast_boost=1.04)
        elif spec.name == "saturn":
            source = crop_largest_body(saturn_source, padding=18)
            image = cutout_to_planet_sprite(source, target_fill=0.92, color_boost=1.04, contrast_boost=1.08)
        else:
            crop = spaceplace.crop(SPACEPLACE_CROPS[spec.name])
            image = cutout_to_planet_sprite(crop, target_fill=0.84, color_boost=1.06, contrast_boost=1.04)

        image.save(OUTPUT / f"{spec.name}.png")
        print(f"Wrote {OUTPUT / f'{spec.name}.png'}")

    moon_source = spaceplace.crop(SPACEPLACE_CROPS["mercury"])
    moon = cutout_to_planet_sprite(moon_source, target_fill=0.68, color_boost=0.25, contrast_boost=0.92).convert("RGBA")
    moon_arr = np.array(moon)
    gray = (moon_arr[:, :, 0] * 0.34 + moon_arr[:, :, 1] * 0.36 + moon_arr[:, :, 2] * 0.30).astype(np.uint8)
    moon_arr[:, :, 0] = np.clip(gray * 0.92 + 24, 0, 255)
    moon_arr[:, :, 1] = np.clip(gray * 0.94 + 22, 0, 255)
    moon_arr[:, :, 2] = np.clip(gray * 0.90 + 20, 0, 255)
    Image.fromarray(moon_arr, "RGBA").save(OUTPUT / "moon.png")
    print(f"Wrote {OUTPUT / 'moon.png'}")


def download_source(url: str, out_path: Path) -> Path:
    if out_path.exists() and out_path.stat().st_size > 1024:
        return out_path

    response = requests.get(url, timeout=60)
    response.raise_for_status()
    out_path.write_bytes(response.content)
    return out_path


def crop_largest_body(source: Image.Image, padding: int) -> Image.Image:
    arr = np.array(source.convert("RGB"))
    mask = (arr.max(axis=2) > 25).astype(np.uint8) * 255
    num_labels, labels, stats, _centroids = cv2.connectedComponentsWithStats(mask, 8)
    best_label = max(range(1, num_labels), key=lambda label: int(stats[label, cv2.CC_STAT_AREA]))
    x = int(stats[best_label, cv2.CC_STAT_LEFT])
    y = int(stats[best_label, cv2.CC_STAT_TOP])
    w = int(stats[best_label, cv2.CC_STAT_WIDTH])
    h = int(stats[best_label, cv2.CC_STAT_HEIGHT])
    left = max(0, x - padding)
    top = max(0, y - padding)
    right = min(source.width, x + w + padding)
    bottom = min(source.height, y + h + padding)
    return source.crop((left, top, right, bottom))


def cutout_to_planet_sprite(
    source: Image.Image,
    target_fill: float,
    color_boost: float,
    contrast_boost: float,
) -> Image.Image:
    source = source.convert("RGB")
    arr = np.array(source)
    brightness = arr.max(axis=2)
    mask = (brightness > 18).astype(np.uint8)
    kernel = np.ones((3, 3), np.uint8)
    mask = cv2.morphologyEx(mask * 255, cv2.MORPH_CLOSE, kernel, iterations=2)

    num_labels, labels, stats, _centroids = cv2.connectedComponentsWithStats(mask, 8)
    if num_labels > 1:
        keep = np.zeros_like(mask)
        valid = [label for label in range(1, num_labels) if stats[label, cv2.CC_STAT_AREA] > 32]
        if valid:
            largest = max(valid, key=lambda label: int(stats[label, cv2.CC_STAT_AREA]))
            keep[labels == largest] = 255
            mask = keep

    alpha = Image.fromarray(mask, "L").filter(ImageFilter.GaussianBlur(0.65))
    rgba = source.convert("RGBA")
    rgba.putalpha(alpha)
    bbox = rgba.getchannel("A").getbbox()
    if bbox is None:
        return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))

    rgba = rgba.crop(bbox)
    max_side = max(rgba.width, rgba.height)
    target_side = int(SIZE * target_fill)
    scale = target_side / max_side
    new_size = (max(1, round(rgba.width * scale)), max(1, round(rgba.height * scale)))
    rgba = rgba.resize(new_size, Image.Resampling.LANCZOS)

    rgb = rgba.convert("RGB")
    if color_boost != 1.0:
        from PIL import ImageEnhance

        rgb = ImageEnhance.Color(rgb).enhance(color_boost)
    if contrast_boost != 1.0:
        from PIL import ImageEnhance

        rgb = ImageEnhance.Contrast(rgb).enhance(contrast_boost)

    rgb = rgb.filter(ImageFilter.UnsharpMask(radius=0.42, percent=68, threshold=3))
    rgb.putalpha(rgba.getchannel("A"))

    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(rgb, ((SIZE - rgb.width) // 2, (SIZE - rgb.height) // 2))
    return clear_transparent_rgb(keep_largest_alpha_component(canvas))


def render_equirectangular_sphere(
    source: Image.Image,
    target_fill: float,
    central_longitude_degrees: float,
    atmosphere: tuple[int, int, int],
    color_boost: float,
    contrast_boost: float,
) -> Image.Image:
    source_arr = np.array(source.convert("RGB"))
    height, width, _ = source_arr.shape
    diameter = int(SIZE * target_fill)
    yy, xx = np.mgrid[0:diameter, 0:diameter].astype(np.float32)
    center = (diameter - 1) * 0.5
    radius = diameter * 0.5 - 1.0
    nx = (xx - center) / radius
    ny = (yy - center) / radius
    r2 = nx * nx + ny * ny
    mask = r2 <= 1.0
    nz = np.sqrt(np.clip(1.0 - r2, 0.0, 1.0))

    central = np.deg2rad(central_longitude_degrees)
    lon = np.arctan2(nx, nz) + central
    lat = np.arcsin(np.clip(-ny, -1.0, 1.0))
    map_x = ((lon + np.pi) / (np.pi * 2.0) % 1.0) * (width - 1)
    map_y = ((np.pi * 0.5 - lat) / np.pi) * (height - 1)
    sampled = cv2.remap(
        source_arr,
        map_x.astype(np.float32),
        map_y.astype(np.float32),
        cv2.INTER_CUBIC,
        borderMode=cv2.BORDER_REPLICATE,
    ).astype(np.float32)

    light = normalize(np.array([-0.56, -0.36, 0.74], dtype=np.float32))
    diffuse = np.clip(nx * light[0] + ny * light[1] + nz * light[2], 0.0, 1.0)
    limb = np.clip(1.0 - np.sqrt(np.clip(r2, 0.0, 1.0)), 0.0, 1.0)
    shade = np.clip(0.30 + diffuse * 0.88 + limb * 0.14, 0.0, 1.05)
    rgb = sampled * shade[:, :, None]
    edge = np.clip((np.sqrt(np.clip(r2, 0.0, 1.0)) - 0.70) / 0.30, 0.0, 1.0)
    atmosphere_rgb = np.array(atmosphere, dtype=np.float32)
    rgb = np.where(mask[:, :, None], rgb * (1.0 - edge[:, :, None] * 0.20) + atmosphere_rgb * edge[:, :, None] * 0.20, rgb)

    rgba = np.dstack([np.clip(rgb, 0, 255), np.where(mask, 255, 0)]).astype(np.uint8)
    sphere = Image.fromarray(rgba, "RGBA")
    sphere = sphere.filter(ImageFilter.UnsharpMask(radius=0.58, percent=72, threshold=3))

    visible = sphere.convert("RGB")
    if color_boost != 1.0:
        from PIL import ImageEnhance

        visible = ImageEnhance.Color(visible).enhance(color_boost)
    if contrast_boost != 1.0:
        from PIL import ImageEnhance

        visible = ImageEnhance.Contrast(visible).enhance(contrast_boost)
    visible.putalpha(sphere.getchannel("A").filter(ImageFilter.GaussianBlur(0.25)))

    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(visible, ((SIZE - visible.width) // 2, (SIZE - visible.height) // 2))
    return clear_transparent_rgb(canvas)


def clear_transparent_rgb(image: Image.Image) -> Image.Image:
    arr = np.array(image.convert("RGBA"))
    transparent = arr[:, :, 3] < 2
    arr[transparent] = (0, 0, 0, 0)
    return Image.fromarray(arr, "RGBA")


def keep_largest_alpha_component(image: Image.Image) -> Image.Image:
    arr = np.array(image.convert("RGBA"))
    mask = (arr[:, :, 3] > 2).astype(np.uint8)
    num_labels, labels, stats, _centroids = cv2.connectedComponentsWithStats(mask, 8)
    if num_labels <= 2:
        return Image.fromarray(arr, "RGBA")

    largest = max(range(1, num_labels), key=lambda label: int(stats[label, cv2.CC_STAT_AREA]))
    arr[(labels != largest) & (labels != 0), 3] = 0
    return Image.fromarray(arr, "RGBA")


def render_planet(spec: PlanetSpec) -> Image.Image:
    if spec.kind == "saturn":
        return render_saturn(spec)

    sphere = planet_sphere(spec, spec.radius)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(sphere, ((SIZE - sphere.width) // 2, (SIZE - sphere.height) // 2))
    return canvas


def render_saturn(spec: PlanetSpec) -> Image.Image:
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    ring = saturn_rings(spec.seed)
    back = ring.copy()
    front = ring.copy()
    split = SIZE // 2 + 8
    back_arr = np.array(back)
    front_arr = np.array(front)
    back_arr[split:, :, 3] = 0
    front_arr[:split, :, 3] = 0
    canvas.alpha_composite(Image.fromarray(back_arr, "RGBA"))

    sphere = planet_sphere(spec, spec.radius)
    canvas.alpha_composite(sphere, ((SIZE - sphere.width) // 2, (SIZE - sphere.height) // 2))
    canvas.alpha_composite(Image.fromarray(front_arr, "RGBA"))
    return canvas


def planet_sphere(spec: PlanetSpec, radius: int) -> Image.Image:
    rng = np.random.default_rng(spec.seed)
    diameter = radius * 2
    yy, xx = np.mgrid[0:diameter, 0:diameter].astype(np.float32)
    center = radius - 0.5
    nx = (xx - center) / radius
    ny = (yy - center) / radius
    r2 = nx * nx + ny * ny
    mask = r2 <= 1.0
    nz = np.sqrt(np.clip(1.0 - r2, 0.0, 1.0))
    theta = np.arctan2(ny, nx)

    light = normalize(np.array([-0.58, -0.42, 0.70], dtype=np.float32))
    diffuse = np.clip(nx * light[0] + ny * light[1] + nz * light[2], 0.0, 1.0)
    limb = np.clip(1.0 - np.sqrt(np.clip(r2, 0.0, 1.0)), 0.0, 1.0)
    shade = np.clip(0.24 + diffuse * 0.88 + limb * 0.18, 0.0, 1.12)
    specular = np.power(np.clip(nx * -0.36 + ny * -0.22 + nz * 0.86, 0.0, 1.0), 34.0)

    texture = surface_rgb(spec, diameter, rng, nx, ny, nz, theta, mask)
    rgb = texture * shade[:, :, None]
    rgb += specular[:, :, None] * specular_tint(spec.kind)
    rgb = apply_limb_atmosphere(rgb, spec.kind, r2, mask)

    alpha = np.where(mask, 255, 0).astype(np.uint8)
    rgba = np.dstack([np.clip(rgb, 0, 255), alpha]).astype(np.uint8)
    image = Image.fromarray(rgba, "RGBA")
    image = image.filter(ImageFilter.UnsharpMask(radius=0.65, percent=46, threshold=4))
    return image


def surface_rgb(
    spec: PlanetSpec,
    size: int,
    rng: np.random.Generator,
    nx: np.ndarray,
    ny: np.ndarray,
    nz: np.ndarray,
    theta: np.ndarray,
    mask: np.ndarray,
) -> np.ndarray:
    if spec.kind == "mercury":
        n1 = blur_array(fractal_noise(size, spec.seed, 5), 2.0)
        n2 = blur_array(fractal_noise(size, spec.seed + 5, 3), 4.0)
        base = mix_color((78, 72, 65), (178, 166, 146), n1 * 0.72 + n2 * 0.28)
        return add_craters(base, mask, rng, count=88, color=(62, 56, 52))

    if spec.kind == "venus":
        n1 = blur_array(fractal_noise(size, spec.seed, 5), 3.2)
        waves = 0.5 + 0.5 * np.sin(nx * 10.0 + np.sin(ny * 6.0) * 1.5 + n1 * 3.2)
        clouds = np.clip(n1 * 0.58 + waves * 0.42, 0.0, 1.0)
        return mix_color((148, 91, 32), (252, 200, 94), clouds)

    if spec.kind == "earth":
        n1 = blur_array(fractal_noise(size, spec.seed, 5), 5.5)
        n2 = blur_array(fractal_noise(size, spec.seed + 11, 4), 7.0)
        land_field = n1 * 0.72 + n2 * 0.38 + np.sin(nx * 4.5 + ny * 2.2) * 0.10
        land_amount = smoothstep(0.56, 0.66, land_field)
        ocean = mix_color((7, 38, 102), (20, 116, 184), np.clip(n2 * 1.18, 0, 1))
        land = mix_color((34, 103, 54), (176, 148, 83), np.clip(n1 * 1.05, 0, 1))
        rgb = ocean * (1.0 - land_amount[:, :, None]) + land * land_amount[:, :, None]
        ice_amount = smoothstep(0.66, 0.87, np.abs(ny))
        rgb = rgb * (1.0 - ice_amount[:, :, None]) + np.array([236, 245, 247], dtype=np.float32) * ice_amount[:, :, None]
        cloud_noise = blur_array(fractal_noise(size, spec.seed + 29, 5), 3.8)
        cloud_bands = cloud_noise + 0.14 * np.sin(nx * 13.0 + ny * 5.8)
        cloud_amount = smoothstep(0.66, 0.79, cloud_bands) * (1.0 - ice_amount * 0.7)
        rgb = rgb * (1.0 - cloud_amount[:, :, None] * 0.42) + np.array([245, 250, 255], dtype=np.float32) * cloud_amount[:, :, None] * 0.42
        return rgb

    if spec.kind == "mars":
        n1 = blur_array(fractal_noise(size, spec.seed, 5), 2.8)
        n2 = blur_array(fractal_noise(size, spec.seed + 17, 4), 5.0)
        rgb = mix_color((102, 41, 24), (221, 112, 48), np.clip(n1 * 0.78 + n2 * 0.32, 0, 1))
        dark_patches = (n2 + np.sin(nx * 9.0 - ny * 5.0) * 0.12) > 0.84
        rgb = np.where(dark_patches[:, :, None], rgb * np.array([0.58, 0.46, 0.38], dtype=np.float32), rgb)
        cap = smoothstep(0.78, 0.94, np.abs(ny))
        rgb = rgb * (1.0 - cap[:, :, None] * 0.58) + np.array([238, 214, 184], dtype=np.float32) * cap[:, :, None] * 0.58
        return add_craters(rgb, mask, rng, count=34, color=(78, 35, 25), max_radius=13)

    if spec.kind == "jupiter":
        bands = horizontal_bands(size, spec.seed, [
            (84, 54, 42),
            (226, 197, 146),
            (147, 82, 54),
            (241, 217, 168),
            (186, 105, 63),
            (245, 224, 176),
            (124, 76, 57),
            (232, 196, 136),
            (96, 62, 50),
        ], wobble=0.06)
        return add_jupiter_storm(bands, nx, ny)

    if spec.kind == "saturn":
        return horizontal_bands(size, spec.seed, [
            (130, 92, 52),
            (225, 185, 104),
            (248, 226, 158),
            (216, 168, 82),
            (151, 105, 58),
        ], wobble=0.035)

    if spec.kind == "uranus":
        n1 = fractal_noise(size, spec.seed, 4)
        bands = 0.5 + 0.5 * np.sin((ny + nx * 0.18) * 18.0)
        return mix_color((72, 168, 178), (164, 235, 224), np.clip(n1 * 0.35 + bands * 0.18 + 0.42, 0, 1))

    if spec.kind == "neptune":
        bands = horizontal_bands(size, spec.seed, [
            (18, 44, 124),
            (32, 90, 210),
            (62, 132, 248),
            (38, 96, 220),
            (10, 30, 92),
        ], wobble=0.055)
        storm = np.exp(-(((nx + 0.27) / 0.18) ** 2 + ((ny - 0.18) / 0.07) ** 2))
        bands = bands * (1.0 - storm[:, :, None] * 0.42)
        bright = np.exp(-(((nx - 0.18) / 0.24) ** 2 + ((ny + 0.36) / 0.04) ** 2))
        bands += bright[:, :, None] * np.array([58, 112, 210], dtype=np.float32)
        return bands

    raise ValueError(spec.kind)


def saturn_rings(seed: int) -> Image.Image:
    rng = np.random.default_rng(seed + 99)
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    cx = SIZE * 0.5
    cy = SIZE * 0.5 + 8
    rx = 340.0
    ry = 98.0
    q = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2
    inner = ((xx - cx) / 198.0) ** 2 + ((yy - cy) / 52.0) ** 2
    ring = (q <= 1.0) & (inner >= 1.0)
    grain = fractal_noise(SIZE, seed + 119, 4)
    stripe = 0.58 + 0.42 * np.sin(q * 52.0 + grain * 3.0)
    alpha = np.where(ring, np.clip((1.0 - q) * 220.0 + 70.0, 34.0, 190.0) * stripe, 0.0)
    gap = (q > 0.58) & (q < 0.64)
    alpha = np.where(gap, alpha * 0.26, alpha)
    color = mix_color((143, 109, 61), (245, 222, 158), np.clip(grain * 0.55 + stripe * 0.45, 0, 1))
    color += rng.normal(0, 2.2, color.shape)
    rgba = np.dstack([np.clip(color, 0, 255), np.clip(alpha, 0, 255)]).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA").filter(ImageFilter.GaussianBlur(0.35))


def horizontal_bands(size: int, seed: int, colors: list[tuple[int, int, int]], wobble: float) -> np.ndarray:
    n1 = blur_array(fractal_noise(size, seed, 5), 4.2)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    y = yy / size
    x = xx / size
    warped = y + wobble * np.sin(x * 10.0 + y * 4.2) + wobble * 0.28 * np.sin(x * 27.0 + n1 * 1.8)
    warped = np.clip(warped, 0.0, 1.0)
    palette = np.array(colors, dtype=np.float32)
    stops = np.linspace(0.0, 1.0, len(colors), dtype=np.float32)
    rgb = np.empty((size, size, 3), dtype=np.float32)
    for channel in range(3):
        rgb[:, :, channel] = np.interp(warped, stops, palette[:, channel])

    fine_belts = np.sin(warped * np.pi * 42.0 + np.sin(x * 18.0) * 0.45)
    broad_belts = np.sin(warped * np.pi * 13.0)
    rgb += fine_belts[:, :, None] * np.array([7.0, 5.5, 3.0], dtype=np.float32)
    rgb += broad_belts[:, :, None] * np.array([5.0, 3.5, 1.4], dtype=np.float32)
    rgb += (n1[:, :, None] - 0.5) * 3.0
    return rgb


def add_jupiter_storm(rgb: np.ndarray, nx: np.ndarray, ny: np.ndarray) -> np.ndarray:
    storm = np.exp(-(((nx - 0.30) / 0.24) ** 2 + ((ny - 0.20) / 0.105) ** 2))
    rim = np.exp(-(((nx - 0.30) / 0.29) ** 2 + ((ny - 0.20) / 0.135) ** 2)) - storm * 0.45
    rgb = rgb * (1.0 - storm[:, :, None] * 0.54) + np.array([184, 72, 42], dtype=np.float32) * storm[:, :, None] * 0.92
    rgb += rim[:, :, None] * np.array([86, 50, 24], dtype=np.float32)
    return rgb


def add_craters(
    rgb: np.ndarray,
    mask: np.ndarray,
    rng: np.random.Generator,
    count: int,
    color: tuple[int, int, int],
    max_radius: int = 24,
) -> np.ndarray:
    height, width, _ = rgb.shape
    yy, xx = np.mgrid[0:height, 0:width].astype(np.float32)
    for _ in range(count):
        cx = rng.uniform(width * 0.12, width * 0.88)
        cy = rng.uniform(height * 0.12, height * 0.88)
        radius = rng.uniform(3.0, max_radius)
        dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / radius
        crater = (dist < 1.0) & mask
        rim = (dist >= 0.82) & (dist < 1.12) & mask
        rgb[crater] = rgb[crater] * 0.62 + np.array(color, dtype=np.float32) * 0.38
        rgb[rim] = rgb[rim] * 0.72 + np.array([235, 222, 194], dtype=np.float32) * 0.28
    return rgb


def apply_limb_atmosphere(rgb: np.ndarray, kind: str, r2: np.ndarray, mask: np.ndarray) -> np.ndarray:
    edge = np.clip((np.sqrt(np.clip(r2, 0, 1)) - 0.72) / 0.28, 0.0, 1.0)
    atmosphere = {
        "earth": np.array([56, 155, 255], dtype=np.float32),
        "venus": np.array([255, 190, 92], dtype=np.float32),
        "uranus": np.array([110, 234, 224], dtype=np.float32),
        "neptune": np.array([74, 136, 255], dtype=np.float32),
    }.get(kind)
    if atmosphere is not None:
        rgb = np.where(mask[:, :, None], rgb * (1.0 - edge[:, :, None] * 0.18) + atmosphere * edge[:, :, None] * 0.18, rgb)
    return rgb


def fractal_noise(size: int, seed: int, octaves: int) -> np.ndarray:
    rng = np.random.default_rng(seed)
    total = np.zeros((size, size), dtype=np.float32)
    weight_total = 0.0
    for octave in range(octaves):
        scale = 2 ** (octave + 2)
        small = rng.random((size // scale + 3, size // scale + 3), dtype=np.float32)
        image = Image.fromarray((small * 255).astype(np.uint8), "L").resize((size, size), Image.Resampling.BICUBIC)
        layer = np.array(image.filter(ImageFilter.GaussianBlur(max(0.35, scale * 0.14))), dtype=np.float32) / 255.0
        weight = 1.0 / (octave + 1)
        total += layer * weight
        weight_total += weight
    total /= weight_total
    return np.clip((total - total.min()) / max(0.0001, total.max() - total.min()), 0.0, 1.0)


def blur_array(values: np.ndarray, radius: float) -> np.ndarray:
    image = Image.fromarray((np.clip(values, 0.0, 1.0) * 255).astype(np.uint8), "L")
    blurred = image.filter(ImageFilter.GaussianBlur(radius))
    arr = np.array(blurred, dtype=np.float32) / 255.0
    return np.clip((arr - arr.min()) / max(0.0001, arr.max() - arr.min()), 0.0, 1.0)


def smoothstep(edge0: float, edge1: float, value: np.ndarray) -> np.ndarray:
    t = np.clip((value - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def mix_color(a: tuple[int, int, int], b: tuple[int, int, int], amount: np.ndarray) -> np.ndarray:
    aa = np.array(a, dtype=np.float32)
    bb = np.array(b, dtype=np.float32)
    return aa + (bb - aa) * amount[:, :, None]


def specular_tint(kind: str) -> np.ndarray:
    if kind in {"earth", "venus", "uranus", "neptune"}:
        return np.array([42, 66, 80], dtype=np.float32)
    if kind in {"jupiter", "saturn"}:
        return np.array([38, 28, 14], dtype=np.float32)
    return np.array([16, 14, 12], dtype=np.float32)


def normalize(value: np.ndarray) -> np.ndarray:
    return value / max(0.0001, float(np.linalg.norm(value)))


if __name__ == "__main__":
    main()
