from __future__ import annotations

from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageSequence

ROOT = Path(__file__).resolve().parents[1]
BACKGROUND_OUT = ROOT / "game" / "assets" / "backgrounds" / "space_nebula_tile.png"
AI_BACKGROUND_SOURCE = ROOT / "game" / "assets" / "backgrounds" / "space_nebula_source_ai.png"
SUN_OUT = ROOT / "game" / "assets" / "backgrounds" / "sun"
SUN_BASE_SOURCE = SUN_OUT / "sun_base.png"
SUN_DYNAMICS_SOURCE = ROOT / "tools" / "source_assets" / "sun_dynamics_reference.gif"
SUN_FRAME_COUNT = 96
SUN_FRAME_SIZE = 1024


def main() -> None:
    BACKGROUND_OUT.parent.mkdir(parents=True, exist_ok=True)
    if AI_BACKGROUND_SOURCE.exists():
        image = prepare_ai_space_texture(AI_BACKGROUND_SOURCE)
    else:
        image = generate_space_texture(3072, 2048)
    image.save(BACKGROUND_OUT)
    print(f"Wrote {BACKGROUND_OUT}")

    SUN_OUT.mkdir(parents=True, exist_ok=True)
    if SUN_DYNAMICS_SOURCE.exists():
        write_sun_frames_from_gif(SUN_DYNAMICS_SOURCE, SUN_BASE_SOURCE, SUN_OUT)
    elif SUN_BASE_SOURCE.exists():
        write_sun_frames_from_source(SUN_BASE_SOURCE, SUN_OUT)
    else:
        for frame in range(SUN_FRAME_COUNT):
            sun = generate_sun_frame(SUN_FRAME_SIZE, frame, SUN_FRAME_COUNT)
            out = SUN_OUT / f"sun_{frame:02d}.png"
            sun.save(out)
    print(f"Wrote {SUN_FRAME_COUNT} sun frames into {SUN_OUT}")


def prepare_ai_space_texture(source_path: Path) -> Image.Image:
    source = Image.open(source_path).convert("RGB")
    base_size = (2048, 2048)
    cover_scale = max(base_size[0] / source.width, base_size[1] / source.height)
    resized = source.resize(
        (round(source.width * cover_scale), round(source.height * cover_scale)),
        Image.Resampling.LANCZOS,
    )
    left = (resized.width - base_size[0]) // 2
    top = (resized.height - base_size[1]) // 2
    image = resized.crop((left, top, left + base_size[0], top + base_size[1]))
    image = ImageEnhance.Contrast(image).enhance(0.92)
    image = ImageEnhance.Brightness(image).enhance(0.82)
    image = ImageEnhance.Color(image).enhance(1.08)

    top_row = image
    bottom_row = image.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    seamless = Image.new("RGB", (base_size[0] * 2, base_size[1] * 2))
    seamless.paste(top_row, (0, 0))
    seamless.paste(top_row.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (base_size[0], 0))
    seamless.paste(bottom_row, (0, base_size[1]))
    seamless.paste(bottom_row.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (base_size[0], base_size[1]))
    return seamless


def write_sun_frames_from_source(source_path: Path, out_dir: Path) -> None:
    for old_frame in out_dir.glob("sun_*.png"):
        frame_id = old_frame.stem.removeprefix("sun_")
        if frame_id.isdigit():
            old_frame.unlink()

    sun = Image.open(source_path).convert("RGBA").resize((SUN_FRAME_SIZE, SUN_FRAME_SIZE), Image.Resampling.LANCZOS)
    base = np.asarray(sun).astype(np.float32).copy()
    base_alpha = base[:, :, 3].copy()
    despill_sun(base, base_alpha)

    yy, xx = np.mgrid[0:SUN_FRAME_SIZE, 0:SUN_FRAME_SIZE].astype(np.float32)
    nx = (xx - SUN_FRAME_SIZE * 0.5) / (SUN_FRAME_SIZE * 0.5)
    ny = (yy - SUN_FRAME_SIZE * 0.5) / (SUN_FRAME_SIZE * 0.5)
    radius = np.sqrt(nx * nx + ny * ny)
    theta = np.arctan2(ny, nx)
    tangent_x = -ny / np.maximum(radius, 0.035)
    tangent_y = nx / np.maximum(radius, 0.035)
    disk_weight = np.clip(1.08 - radius, 0.0, 1.0)
    corona_weight = np.clip((radius - 0.88) / 0.32, 0.0, 1.0)
    source_rgb = base[:, :, :3].astype(np.float32)

    for frame in range(SUN_FRAME_COUNT):
        phase = frame / SUN_FRAME_COUNT * np.pi * 2.0
        swirl = (
            np.sin(radius * 12.0 + theta * 2.4 - phase * 1.10) * 5.9
            + np.sin(radius * 25.0 - theta * 3.9 + phase * 0.78) * 2.8
            + np.sin(radius * 42.0 + theta * 5.2 - phase * 1.36) * 1.1
        ) * disk_weight
        boil_x = np.sin(theta * 7.0 + radius * 18.0 + phase * 0.58) * (2.1 + 4.8 * corona_weight)
        boil_y = np.cos(theta * 6.0 - radius * 16.0 + phase * 0.66) * (2.0 + 4.2 * corona_weight)
        map_x = xx + tangent_x * swirl + nx * boil_x
        map_y = yy + tangent_y * swirl + ny * boil_y
        remapped = cv2.remap(
            source_rgb,
            map_x.astype(np.float32),
            map_y.astype(np.float32),
            interpolation=cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_REFLECT_101,
        )
        remapped_alpha = cv2.remap(
            base_alpha,
            map_x.astype(np.float32),
            map_y.astype(np.float32),
            interpolation=cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_REFLECT_101,
        )

        streams = (
            np.sin(theta * 8.0 + radius * 24.0 - phase * 0.88)
            + np.sin((nx - ny) * 18.0 + phase * 0.42)
            + np.sin((nx * 0.74 + ny * 1.18) * 28.0 + np.sin(theta * 5.0 + phase * 0.38) * 1.4 + phase * 0.76)
        ) / 3.0
        hot_streams = np.maximum(streams, 0.0) ** 1.45
        cool_streams = np.maximum(-streams, 0.0) ** 1.35
        local_bright = (
            1.0
            + hot_streams[:, :, None] * 0.034 * (0.28 + disk_weight[:, :, None] * 0.72)
            - cool_streams[:, :, None] * 0.012 * disk_weight[:, :, None]
        )
        tint = np.zeros_like(remapped)
        tint[:, :, 0] = 2 + hot_streams * 5
        tint[:, :, 1] = 1 + hot_streams * 4
        tint[:, :, 2] = -1

        flame_tongues = np.zeros_like(radius)
        for tongue in range(10):
            base_angle = tongue / 10.0 * np.pi * 2.0 + (tongue % 3) * 0.08
            angle_now = base_angle + 0.19 * np.sin(phase * 0.34 + tongue * 1.31)
            width = 0.030 + 0.022 * (0.5 + 0.5 * np.sin(tongue * 2.17))
            reach = 0.022 + 0.038 * (0.5 + 0.5 * np.sin(phase * 0.58 + tongue * 1.79))
            radial_center = 0.852 + reach * 0.72
            angular = np.exp(-((angle_delta(theta, angle_now) / width) ** 2))
            radial = np.exp(-(((radius - radial_center) / (0.020 + reach * 0.45)) ** 2))
            lick = angular * radial * (0.38 + 0.42 * np.sin(phase * 0.70 + tongue * 0.83) ** 2)
            flame_tongues += lick

        flame_tongues = np.clip(flame_tongues, 0.0, 0.78)
        flame_tongues *= smoothstep(0.785, 0.842, radius) * (1.0 - smoothstep(0.944, 0.985, radius))
        flame_mix = np.clip(flame_tongues[:, :, None] * 0.48, 0.0, 0.48)
        flame_color = np.zeros_like(remapped)
        flame_color[:, :, 0] = 255
        flame_color[:, :, 1] = 86 + flame_tongues * 78
        flame_color[:, :, 2] = 2

        frame_pixels = base.copy()
        frame_pixels[:, :, :3] = np.clip(remapped * 0.62 + source_rgb * 0.38, 0, 255)
        frame_pixels[:, :, :3] = np.clip(frame_pixels[:, :, :3] * local_bright + tint, 0, 255)
        frame_pixels[:, :, :3] = np.clip(frame_pixels[:, :, :3] * (1.0 - flame_mix) + flame_color * flame_mix, 0, 255)
        dynamic_alpha = np.maximum(remapped_alpha * 0.78 + base_alpha * 0.22, flame_tongues * 145.0)
        frame_pixels[:, :, 3] = np.where(base_alpha > 225.0, base_alpha, np.maximum(base_alpha * 0.94, dynamic_alpha))
        despill_sun(frame_pixels, frame_pixels[:, :, 3].copy())
        image = Image.fromarray(frame_pixels.astype(np.uint8), "RGBA")
        image = image.filter(ImageFilter.UnsharpMask(radius=0.48, percent=54, threshold=4))
        image.save(out_dir / f"sun_{frame:02d}.png")

    Image.fromarray(base.astype(np.uint8), "RGBA").save(source_path)


def write_sun_frames_from_gif(gif_path: Path, base_source_path: Path, out_dir: Path) -> None:
    for old_frame in out_dir.glob("sun_*.png"):
        frame_id = old_frame.stem.removeprefix("sun_")
        if frame_id.isdigit():
            old_frame.unlink()

    reference = Image.open(gif_path)
    reference_frames = [
        prepare_reference_sun_frame(frame.convert("RGB"))
        for frame in ImageSequence.Iterator(reference)
    ]
    if not reference_frames:
        raise ValueError(f"No frames found in {gif_path}")

    if base_source_path.exists():
        base_image = Image.open(base_source_path).convert("RGBA").resize(
            (SUN_FRAME_SIZE, SUN_FRAME_SIZE),
            Image.Resampling.LANCZOS,
        )
    else:
        base_image = generate_sun_frame(SUN_FRAME_SIZE, 0, 1)

    base = np.asarray(base_image).astype(np.float32).copy()
    base_alpha = base[:, :, 3].copy()
    despill_sun(base, base_alpha)
    source_rgb = base[:, :, :3].astype(np.float32)
    detail = source_rgb - cv2.GaussianBlur(source_rgb, (0, 0), 3.2)

    yy, xx = np.mgrid[0:SUN_FRAME_SIZE, 0:SUN_FRAME_SIZE].astype(np.float32)
    nx = (xx - SUN_FRAME_SIZE * 0.5) / (SUN_FRAME_SIZE * 0.5)
    ny = (yy - SUN_FRAME_SIZE * 0.5) / (SUN_FRAME_SIZE * 0.5)
    radius = np.sqrt(nx * nx + ny * ny)
    disk = 1.0 - smoothstep(0.762, 0.800, radius)
    rim = np.exp(-((radius - 0.772) / 0.018) ** 2)
    corona_window = smoothstep(0.708, 0.778, radius) * (1.0 - smoothstep(0.982, 1.055, radius))

    frame_total = len(reference_frames)
    for frame in range(SUN_FRAME_COUNT):
        source_pos = frame / SUN_FRAME_COUNT * frame_total
        source_index = int(np.floor(source_pos)) % frame_total
        next_index = (source_index + 1) % frame_total
        blend = smoothstep_scalar(source_pos - np.floor(source_pos))
        ref_rgb = reference_frames[source_index] * (1.0 - blend) + reference_frames[next_index] * blend

        energy = np.clip((ref_rgb[:, :, 0] * 0.72 + ref_rgb[:, :, 1] * 0.50 - ref_rgb[:, :, 2] * 0.16) / 255.0, 0.0, 1.0)
        hot = smoothstep(0.48, 0.92, energy)
        dark = 1.0 - smoothstep(0.18, 0.42, energy)

        frame_rgb = ref_rgb * 0.86 + source_rgb * 0.14
        frame_rgb = (frame_rgb - 112.0) * 1.20 + 112.0
        frame_rgb += detail * disk[:, :, None] * 0.34
        frame_rgb += rim[:, :, None] * np.array([82.0, 52.0, 2.0], dtype=np.float32)
        frame_rgb -= dark[:, :, None] * disk[:, :, None] * np.array([20.0, 12.0, 5.0], dtype=np.float32)
        frame_rgb += hot[:, :, None] * np.array([16.0, 9.0, 1.0], dtype=np.float32)
        frame_rgb[:, :, 0] *= 1.08
        frame_rgb[:, :, 1] *= 1.02
        frame_rgb[:, :, 2] *= 0.82

        corona_alpha = np.clip((energy - 0.18) / 0.58, 0.0, 1.0) ** 1.25
        alpha = np.maximum(disk * 255.0, corona_alpha * corona_window * 235.0)
        alpha = np.where(radius > 1.055, 0.0, alpha)
        alpha = np.maximum(alpha, np.clip(base_alpha * 0.16, 0.0, 255.0) * (1.0 - smoothstep(0.88, 1.08, radius)))

        frame_pixels = np.dstack([np.clip(frame_rgb, 0, 255), np.clip(alpha, 0, 255)]).astype(np.float32)
        despill_sun(frame_pixels, frame_pixels[:, :, 3].copy())
        image = Image.fromarray(frame_pixels.astype(np.uint8), "RGBA")
        image = image.filter(ImageFilter.UnsharpMask(radius=0.55, percent=68, threshold=3))
        image.save(out_dir / f"sun_{frame:02d}.png")

    Image.fromarray(base.astype(np.uint8), "RGBA").save(base_source_path)


def prepare_reference_sun_frame(frame: Image.Image) -> np.ndarray:
    size = max(frame.width, frame.height)
    square = Image.new("RGB", (size, size), (0, 0, 0))
    square.paste(frame, ((size - frame.width) // 2, (size - frame.height) // 2))
    square = square.resize((SUN_FRAME_SIZE, SUN_FRAME_SIZE), Image.Resampling.LANCZOS)
    square = ImageEnhance.Color(square).enhance(1.14)
    square = ImageEnhance.Contrast(square).enhance(1.10)
    square = ImageEnhance.Brightness(square).enhance(1.04)
    return np.asarray(square).astype(np.float32)


def despill_sun(pixels: np.ndarray, alpha: np.ndarray) -> None:
    transparent = alpha < 2
    edge = (alpha > 0) & (alpha < 225)
    soft_edge = (alpha > 0) & (alpha < 120)
    red = pixels[:, :, 0]
    green = pixels[:, :, 1]
    blue = pixels[:, :, 2]
    red[edge] = np.maximum(red[edge], green[edge] * 1.04)
    green[edge] = np.minimum(green[edge], red[edge] * 0.66)
    blue[edge] = np.minimum(blue[edge], red[edge] * 0.12)
    green[soft_edge] = np.minimum(green[soft_edge], red[soft_edge] * 0.48)
    red[transparent] = 0
    green[transparent] = 0
    blue[transparent] = 0
    pixels[:, :, 0] = red
    pixels[:, :, 1] = green
    pixels[:, :, 2] = blue
    pixels[:, :, 3] = alpha


def generate_space_texture(width: int, height: int) -> Image.Image:
    rng = np.random.default_rng(424242)
    base = np.zeros((height, width, 3), dtype=np.float32)
    base[:] = np.array([1, 4, 9], dtype=np.float32)

    for scale, strength, color in (
        (18, 0.30, (10, 32, 58)),
        (36, 0.28, (28, 18, 58)),
        (72, 0.22, (60, 22, 18)),
        (128, 0.18, (6, 66, 76)),
    ):
        small = rng.random((height // scale + 2, width // scale + 2), dtype=np.float32)
        noise = Image.fromarray((small * 255).astype(np.uint8), "L").resize((width, height), Image.Resampling.BICUBIC)
        noise = noise.filter(ImageFilter.GaussianBlur(scale * 0.64))
        layer = np.array(noise, dtype=np.float32) / 255.0
        layer = np.clip((layer - 0.36) / 0.64, 0.0, 1.0)
        base += layer[:, :, None] * np.array(color, dtype=np.float32) * strength

    image = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB")

    veil = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    veil_draw = ImageDraw.Draw(veil, "RGBA")
    palette = [
        (16, 125, 190, 24),
        (80, 45, 185, 17),
        (210, 62, 54, 18),
        (255, 154, 42, 15),
        (18, 176, 142, 16),
    ]
    for _ in range(58):
        x = int(rng.integers(-width // 4, width + width // 4))
        y = int(rng.integers(-height // 5, height + height // 5))
        points = []
        drift = int(rng.integers(-220, 220))
        for i in range(8):
            points.append(
                (
                    x + i * int(rng.integers(120, 220)) + int(np.sin(i * 1.3) * drift),
                    y + int(np.sin(i * 0.8 + rng.random() * 4.0) * rng.integers(80, 260)),
                )
            )
        veil_draw.line(
            points,
            fill=palette[int(rng.integers(0, len(palette)))],
            width=int(rng.integers(54, 170)),
            joint="curve",
        )

    draw_nebula_rift(veil_draw, rng, width, height)
    veil = veil.filter(ImageFilter.GaussianBlur(54))
    image = Image.alpha_composite(image.convert("RGBA"), veil).convert("RGB")

    filaments = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    filament_draw = ImageDraw.Draw(filaments, "RGBA")
    draw_filament_field(filament_draw, rng, width, height)
    soft_filaments = filaments.filter(ImageFilter.GaussianBlur(5.0))
    image = Image.alpha_composite(image.convert("RGBA"), soft_filaments)
    image = Image.alpha_composite(image, filaments.filter(ImageFilter.GaussianBlur(2.1))).convert("RGB")
    image = image.filter(ImageFilter.GaussianBlur(0.35))

    dust = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    dust_draw = ImageDraw.Draw(dust, "RGBA")
    for _ in range(46):
        x0 = int(rng.integers(0, width))
        y0 = int(rng.integers(0, height))
        points = []
        for i in range(9):
            x = x0 + int(np.sin(i * 0.9 + rng.random()) * rng.integers(40, 210)) + i * int(rng.integers(-70, 90))
            y = y0 + int(np.cos(i * 0.7 + rng.random()) * rng.integers(35, 180))
            points.append((x, y))
        dust_draw.line(points, fill=(0, 3, 8, int(rng.integers(32, 72))), width=int(rng.integers(8, 24)), joint="curve")

    image = Image.alpha_composite(image.convert("RGBA"), dust.filter(ImageFilter.GaussianBlur(5.2))).convert("RGB")
    draw = ImageDraw.Draw(image, "RGBA")

    for _ in range(1700):
        x = int(rng.integers(0, width))
        y = int(rng.integers(0, height))
        radius = rng.choice([1, 1, 1, 2, 2, 3], p=[0.44, 0.25, 0.14, 0.10, 0.05, 0.02])
        tint = int(rng.integers(170, 256))
        blue = min(255, tint + int(rng.integers(0, 34)))
        warm = int(rng.integers(-22, 28))
        alpha = int(rng.integers(80, 240))
        red = max(150, min(255, tint + warm))
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=(tint, min(255, tint + 12), blue, alpha))
        if radius >= 2 and rng.random() < 0.18:
            draw.ellipse((x - radius * 3, y - radius * 3, x + radius * 3, y + radius * 3), fill=(red, min(255, tint + 18), blue, 22))

    for _ in range(74):
        x = int(rng.integers(0, width))
        y = int(rng.integers(0, height))
        color = (155, 228, 255, int(rng.integers(58, 132)))
        arm = int(rng.integers(3, 8))
        draw.line((x - arm, y, x + arm, y), fill=color, width=1)
        draw.line((x, y - arm, x, y + arm), fill=color, width=1)

    return image


def draw_nebula_rift(draw: ImageDraw.ImageDraw, rng: np.random.Generator, width: int, height: int) -> None:
    for _ in range(18):
        x0 = int(rng.integers(-width // 5, width))
        y0 = int(rng.integers(0, height))
        points = []
        for i in range(10):
            points.append(
                (
                    x0 + i * int(rng.integers(80, 170)),
                    y0 + int(np.sin(i * 0.8 + rng.random() * 4.0) * rng.integers(60, 220)),
                )
            )
        draw.line(points, fill=(0, 3, 8, int(rng.integers(44, 88))), width=int(rng.integers(26, 90)), joint="curve")


def draw_filament_field(draw: ImageDraw.ImageDraw, rng: np.random.Generator, width: int, height: int) -> None:
    anchor_y = int(height * 0.52)
    for layer, (color, width_min, width_max, alpha_boost) in enumerate(
        [
            ((255, 72, 60), 18, 38, 24),
            ((255, 156, 52), 8, 20, 36),
            ((255, 226, 104), 2, 7, 58),
            ((92, 206, 255), 3, 10, 34),
        ]
    ):
        for _ in range(8 if layer < 2 else 12):
            points = []
            y = anchor_y + int(rng.integers(-320, 220))
            curvature = rng.uniform(-0.9, 0.9)
            for i in range(12):
                x = int(-width * 0.16 + i * width * 0.12)
                bend = np.sin(i * 0.75 + curvature) * rng.integers(24, 92)
                diagonal = (i - 5.5) * rng.integers(-30, 22)
                points.append((x, int(y + bend + diagonal)))
            alpha = int(rng.integers(max(18, alpha_boost - 18), min(118, alpha_boost + 34)))
            draw.line(
                points,
                fill=(color[0], color[1], color[2], alpha),
                width=int(rng.integers(width_min, width_max)),
                joint="curve",
            )


def generate_sun_frame(size: int, frame: int, frame_count: int) -> Image.Image:
    phase = frame / frame_count * np.pi * 2.0
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    center = (size - 1) * 0.5
    radius = size * 0.365
    nx = (xx - center) / radius
    ny = (yy - center) / radius
    r = np.sqrt(nx * nx + ny * ny)
    theta = np.arctan2(ny, nx)

    plasma = (
        np.sin(nx * 19.0 + np.sin(ny * 8.0 + phase * 0.9) * 2.0 + phase * 1.2)
        + np.sin(ny * 24.0 + np.cos(nx * 6.0 - phase * 0.8) * 2.4)
        + np.sin((nx + ny) * 38.0 + phase * 1.7)
        + np.sin(np.sin(theta * 7.0 + phase) * 5.5 + r * 46.0 - phase * 1.4)
    )
    plasma = (plasma + 4.0) / 8.0

    rng = np.random.default_rng(88001 + frame)
    speckle_small = rng.random((size // 8 + 2, size // 8 + 2), dtype=np.float32)
    speckle = Image.fromarray((speckle_small * 255).astype(np.uint8), "L").resize((size, size), Image.Resampling.BICUBIC)
    speckle = np.array(speckle.filter(ImageFilter.GaussianBlur(1.8)), dtype=np.float32) / 255.0

    active = np.zeros((size, size), dtype=np.float32)
    spots = [
        (-0.42, -0.24, 0.13, 0.32),
        (0.08, -0.34, 0.09, 0.24),
        (0.36, -0.05, 0.12, 0.28),
        (-0.17, 0.22, 0.11, 0.25),
        (0.28, 0.34, 0.10, 0.22),
        (-0.50, 0.42, 0.12, 0.20),
    ]
    for index, (sx, sy, spread, power) in enumerate(spots):
        wobble = 0.035 * np.sin(phase + index * 1.7)
        dist = ((nx - sx - wobble) ** 2 + (ny - sy + wobble * 0.6) ** 2) / (spread * spread)
        active += np.exp(-dist) * power * (0.75 + 0.25 * np.sin(phase * 1.4 + index))

    limb = smoothstep(0.70, 1.0, r)
    rim = np.exp(-((r - 0.985) / 0.034) ** 2)
    disk = r <= 1.0
    corona = np.clip(np.exp(-(r - 1.0) * 9.0), 0.0, 1.0) * (r > 1.0) * (r < 1.24)

    texture = np.clip(plasma * 0.78 + speckle * 0.36 + active + rim * 0.44 - limb * 0.12, 0.0, 1.0)
    hot = smoothstep(0.62, 0.95, texture)
    dark = np.array([124, 20, 0], dtype=np.float32)
    mid = np.array([244, 78, 0], dtype=np.float32)
    bright = np.array([255, 183, 28], dtype=np.float32)
    white_hot = np.array([255, 242, 134], dtype=np.float32)

    rgb = dark + (mid - dark) * texture[:, :, None]
    rgb = rgb * (1.0 - hot[:, :, None]) + bright * hot[:, :, None]
    core_hot = smoothstep(0.88, 1.0, texture)
    rgb = rgb * (1.0 - core_hot[:, :, None]) + white_hot * core_hot[:, :, None]
    rgb += rim[:, :, None] * np.array([80, 52, 8], dtype=np.float32)

    prominence = np.zeros((size, size), dtype=np.float32)
    prominence_angles = [-2.85, -1.55, -0.22, 0.72, 1.86, 2.62]
    for index, angle in enumerate(prominence_angles):
        angle_now = angle + 0.06 * np.sin(phase * 0.7 + index)
        angular = np.exp(-(angle_delta(theta, angle_now) / 0.055) ** 2)
        radial = np.exp(-((r - (1.035 + 0.035 * np.sin(phase + index))) / 0.045) ** 2)
        prominence += angular * radial * (0.36 + 0.22 * np.sin(phase * 1.1 + index * 0.9))

    corona_noise = 0.62 + 0.38 * np.sin(theta * 13.0 + phase * 1.8 + np.sin(r * 24.0))
    corona_alpha = np.clip((corona * corona_noise + prominence) * 205.0, 0, 205)
    alpha = np.where(disk, 255.0, corona_alpha)

    corona_rgb = np.zeros_like(rgb)
    corona_rgb[:, :, 0] = 255
    corona_rgb[:, :, 1] = 44 + prominence * 120
    corona_rgb[:, :, 2] = 0
    outside = ~disk
    rgb[outside] = corona_rgb[outside]

    alpha = np.where(r > 1.24, 0, alpha)
    rgba = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha, 0, 255)]).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def smoothstep(edge0: float, edge1: float, value: np.ndarray) -> np.ndarray:
    t = np.clip((value - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def smoothstep_scalar(value: float) -> float:
    t = min(1.0, max(0.0, value))
    return t * t * (3.0 - 2.0 * t)


def angle_delta(angle: np.ndarray, target: float) -> np.ndarray:
    return np.arctan2(np.sin(angle - target), np.cos(angle - target))


if __name__ == "__main__":
    main()
