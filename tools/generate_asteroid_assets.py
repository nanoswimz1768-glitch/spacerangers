from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "game" / "assets" / "asteroids"
SIZE = 256
CENTER = SIZE / 2
ASTEROID_COUNT = 32


PALETTES = [
    ((96, 92, 84), (205, 198, 178), (36, 34, 31), (192, 176, 145), (236, 225, 195)),
    ((64, 63, 61), (164, 160, 150), (18, 18, 17), (108, 101, 88), (222, 218, 204)),
    ((43, 47, 50), (138, 150, 156), (13, 16, 18), (82, 176, 212), (122, 226, 255)),
    ((60, 50, 46), (158, 129, 109), (24, 19, 17), (238, 112, 48), (255, 182, 82)),
    ((50, 48, 56), (138, 128, 158), (16, 15, 22), (128, 86, 234), (205, 156, 255)),
    ((47, 61, 53), (132, 163, 139), (15, 22, 17), (64, 214, 128), (148, 255, 186)),
    ((70, 63, 48), (183, 160, 112), (28, 24, 18), (232, 178, 64), (255, 230, 128)),
    ((78, 58, 44), (178, 121, 86), (29, 20, 16), (213, 61, 46), (255, 154, 82)),
    ((49, 67, 72), (142, 181, 188), (17, 25, 28), (66, 202, 190), (145, 255, 244)),
    ((88, 90, 84), (190, 194, 182), (31, 32, 30), (86, 122, 148), (196, 229, 255)),
    ((44, 43, 41), (126, 124, 118), (12, 12, 11), (67, 82, 104), (184, 206, 240)),
    ((64, 57, 49), (154, 138, 115), (21, 18, 15), (222, 122, 44), (255, 202, 92)),
    ((57, 71, 48), (146, 166, 119), (18, 25, 15), (245, 128, 38), (122, 255, 82)),
    ((86, 82, 74), (196, 184, 156), (31, 30, 27), (228, 205, 82), (255, 248, 148)),
    ((58, 54, 68), (156, 148, 188), (17, 16, 24), (88, 190, 255), (206, 132, 255)),
    ((42, 48, 60), (128, 145, 178), (12, 16, 25), (58, 128, 255), (154, 218, 255)),
]


STYLE_PRESETS = [
    {"palette": 0, "shape": 0.18, "elong": 1.00, "flat": 1.00, "craters": 28, "caves": 4, "rubble": 22, "ore": 0, "veins": 3, "facets": 4, "grit": 1.10},
    {"palette": 1, "shape": 0.48, "elong": 1.34, "flat": 0.86, "craters": 18, "caves": 5, "rubble": 30, "ore": 0, "veins": 2, "facets": 8, "grit": 1.18},
    {"palette": 2, "shape": 0.36, "elong": 1.10, "flat": 0.96, "craters": 13, "caves": 3, "rubble": 24, "ore": 8, "veins": 9, "facets": 7, "grit": 1.06},
    {"palette": 3, "shape": 0.58, "elong": 1.22, "flat": 0.82, "craters": 10, "caves": 2, "rubble": 22, "ore": 9, "veins": 12, "facets": 9, "grit": 1.00},
    {"palette": 4, "shape": 0.30, "elong": 1.02, "flat": 1.04, "craters": 16, "caves": 3, "rubble": 26, "ore": 7, "veins": 10, "facets": 5, "grit": 1.08},
    {"palette": 5, "shape": 0.44, "elong": 1.16, "flat": 0.92, "craters": 12, "caves": 2, "rubble": 28, "ore": 10, "veins": 11, "facets": 6, "grit": 1.00},
    {"palette": 6, "shape": 0.52, "elong": 1.07, "flat": 0.90, "craters": 11, "caves": 2, "rubble": 24, "ore": 8, "veins": 8, "facets": 9, "grit": 0.98},
    {"palette": 7, "shape": 0.64, "elong": 1.46, "flat": 0.76, "craters": 8, "caves": 1, "rubble": 20, "ore": 6, "veins": 14, "facets": 12, "grit": 0.92},
    {"palette": 8, "shape": 0.26, "elong": 0.94, "flat": 1.08, "craters": 15, "caves": 3, "rubble": 26, "ore": 6, "veins": 8, "facets": 5, "grit": 1.04},
    {"palette": 9, "shape": 0.34, "elong": 1.28, "flat": 0.96, "craters": 19, "caves": 4, "rubble": 34, "ore": 3, "veins": 5, "facets": 7, "grit": 1.20},
    {"palette": 10, "shape": 0.72, "elong": 1.62, "flat": 0.70, "craters": 6, "caves": 2, "rubble": 18, "ore": 5, "veins": 7, "facets": 14, "grit": 0.88},
    {"palette": 11, "shape": 0.56, "elong": 1.14, "flat": 0.88, "craters": 9, "caves": 2, "rubble": 21, "ore": 9, "veins": 13, "facets": 10, "grit": 0.96},
    {"palette": 12, "shape": 0.42, "elong": 1.06, "flat": 1.02, "craters": 14, "caves": 3, "rubble": 28, "ore": 11, "veins": 10, "facets": 7, "grit": 1.00},
    {"palette": 13, "shape": 0.22, "elong": 0.92, "flat": 1.12, "craters": 25, "caves": 5, "rubble": 36, "ore": 4, "veins": 4, "facets": 5, "grit": 1.22},
    {"palette": 14, "shape": 0.38, "elong": 1.18, "flat": 0.90, "craters": 12, "caves": 2, "rubble": 22, "ore": 9, "veins": 11, "facets": 8, "grit": 0.96},
    {"palette": 15, "shape": 0.50, "elong": 1.36, "flat": 0.82, "craters": 10, "caves": 2, "rubble": 20, "ore": 7, "veins": 9, "facets": 12, "grit": 0.92},
    {"palette": 0, "shape": 0.60, "elong": 1.52, "flat": 0.72, "craters": 17, "caves": 6, "rubble": 25, "ore": 1, "veins": 4, "facets": 11, "grit": 1.08},
    {"palette": 1, "shape": 0.28, "elong": 0.88, "flat": 1.18, "craters": 31, "caves": 7, "rubble": 42, "ore": 0, "veins": 2, "facets": 5, "grit": 1.28},
    {"palette": 2, "shape": 0.68, "elong": 1.70, "flat": 0.66, "craters": 7, "caves": 1, "rubble": 16, "ore": 10, "veins": 12, "facets": 15, "grit": 0.88},
    {"palette": 3, "shape": 0.40, "elong": 0.96, "flat": 1.08, "craters": 11, "caves": 3, "rubble": 24, "ore": 12, "veins": 16, "facets": 7, "grit": 1.02},
    {"palette": 4, "shape": 0.78, "elong": 1.42, "flat": 0.74, "craters": 8, "caves": 3, "rubble": 18, "ore": 12, "veins": 13, "facets": 16, "grit": 0.86},
    {"palette": 5, "shape": 0.24, "elong": 1.04, "flat": 1.00, "craters": 18, "caves": 4, "rubble": 36, "ore": 8, "veins": 8, "facets": 4, "grit": 1.18},
    {"palette": 6, "shape": 0.46, "elong": 1.26, "flat": 0.84, "craters": 14, "caves": 3, "rubble": 30, "ore": 10, "veins": 10, "facets": 9, "grit": 1.02},
    {"palette": 7, "shape": 0.70, "elong": 1.18, "flat": 0.76, "craters": 7, "caves": 2, "rubble": 19, "ore": 8, "veins": 15, "facets": 14, "grit": 0.90},
    {"palette": 8, "shape": 0.54, "elong": 1.54, "flat": 0.80, "craters": 9, "caves": 1, "rubble": 18, "ore": 11, "veins": 13, "facets": 13, "grit": 0.88},
    {"palette": 9, "shape": 0.32, "elong": 1.08, "flat": 1.04, "craters": 22, "caves": 5, "rubble": 40, "ore": 3, "veins": 5, "facets": 6, "grit": 1.20},
    {"palette": 10, "shape": 0.62, "elong": 1.36, "flat": 0.78, "craters": 8, "caves": 4, "rubble": 20, "ore": 6, "veins": 8, "facets": 15, "grit": 0.94},
    {"palette": 11, "shape": 0.36, "elong": 0.98, "flat": 1.14, "craters": 13, "caves": 3, "rubble": 31, "ore": 12, "veins": 14, "facets": 5, "grit": 1.10},
    {"palette": 12, "shape": 0.58, "elong": 1.44, "flat": 0.82, "craters": 10, "caves": 2, "rubble": 24, "ore": 13, "veins": 12, "facets": 10, "grit": 0.98},
    {"palette": 13, "shape": 0.26, "elong": 1.00, "flat": 1.00, "craters": 26, "caves": 6, "rubble": 38, "ore": 5, "veins": 6, "facets": 5, "grit": 1.24},
    {"palette": 14, "shape": 0.74, "elong": 1.56, "flat": 0.68, "craters": 6, "caves": 2, "rubble": 18, "ore": 13, "veins": 15, "facets": 16, "grit": 0.88},
    {"palette": 15, "shape": 0.44, "elong": 1.12, "flat": 0.92, "craters": 12, "caves": 3, "rubble": 26, "ore": 11, "veins": 11, "facets": 9, "grit": 0.98},
]


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


def smoothstep(edge0: float, edge1: float, value: float) -> float:
    if edge0 == edge1:
        return 0.0

    t = max(0.0, min(1.0, (value - edge0) / (edge1 - edge0)))
    return t * t * (3.0 - 2.0 * t)


def mix_color(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    return (
        int(lerp(a[0], b[0], t)),
        int(lerp(a[1], b[1], t)),
        int(lerp(a[2], b[2], t)),
    )


def hash01(x: int, y: int, seed: int) -> float:
    n = (x * 374761393 + y * 668265263 + seed * 1442695041) & 0xFFFFFFFF
    n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFFFF) / float(0xFFFFFF)


def value_noise(x: float, y: float, seed: int, scale: float) -> float:
    px = x / scale
    py = y / scale
    x0 = math.floor(px)
    y0 = math.floor(py)
    tx = px - x0
    ty = py - y0
    sx = tx * tx * (3 - 2 * tx)
    sy = ty * ty * (3 - 2 * ty)
    a = hash01(x0, y0, seed)
    b = hash01(x0 + 1, y0, seed)
    c = hash01(x0, y0 + 1, seed)
    d = hash01(x0 + 1, y0 + 1, seed)
    return lerp(lerp(a, b, sx), lerp(c, d, sx), sy)


def fbm(x: float, y: float, seed: int, scale: float = 74.0, octaves: int = 5) -> float:
    total = 0.0
    amplitude = 0.56
    weight = 0.0
    for octave in range(octaves):
        total += value_noise(x, y, seed + octave * 103, scale) * amplitude
        weight += amplitude
        amplitude *= 0.53
        scale *= 0.52
    return total / max(0.001, weight)


def build_outline(rng: random.Random, style: dict[str, float | int]) -> list[float]:
    point_count = 192
    roughness = float(style["shape"])
    lobes = rng.randint(4, 8)
    phase = rng.random() * math.tau
    points: list[float] = []
    for i in range(point_count):
        angle = i / point_count * math.tau
        base = 0.77 + rng.uniform(-0.020, 0.020)
        base += math.sin(angle * lobes + phase) * lerp(0.028, 0.090, roughness)
        base += math.sin(angle * (lobes + 3) - phase * 0.63) * lerp(0.020, 0.070, roughness)
        base += math.sin(angle * (lobes * 2 + 1) + phase * 1.7) * lerp(0.010, 0.048, roughness)
        base += rng.uniform(-0.018, 0.055) * roughness
        points.append(max(0.50, min(0.97, base)))

    passes = max(1, int(round(5 - roughness * 4)))
    for _ in range(passes):
        smoothed: list[float] = []
        for i in range(point_count):
            value = 0.0
            weight = 0.0
            for offset, w in [(-3, 1), (-2, 2), (-1, 3), (0, 5), (1, 3), (2, 2), (3, 1)]:
                value += points[(i + offset) % point_count] * w
                weight += w
            smoothed.append(value / weight)
        points = smoothed

    notch_count = int(lerp(2, 8, roughness))
    for _ in range(notch_count):
        center = rng.random() * point_count
        width = rng.uniform(5.0, 17.0)
        depth = rng.uniform(0.025, 0.080) * (0.7 + roughness)
        for i in range(point_count):
            delta = abs(((i - center + point_count * 0.5) % point_count) - point_count * 0.5)
            if delta < width:
                points[i] -= depth * (1.0 - delta / width) ** 2

    return [max(0.46, min(0.99, point)) for point in points]


def outline_radius(outline: list[float], angle: float, nominal_radius: float) -> float:
    wrapped = angle % math.tau
    index = wrapped / math.tau * len(outline)
    i0 = int(index) % len(outline)
    i1 = (i0 + 1) % len(outline)
    t = index - int(index)
    return lerp(outline[i0], outline[i1], t) * nominal_radius


def rotated_scaled_coordinates(x: float, y: float, tilt: float, elong: float, flat: float) -> tuple[float, float]:
    ca = math.cos(tilt)
    sa = math.sin(tilt)
    rx = ca * x + sa * y
    ry = -sa * x + ca * y
    return rx / max(0.001, elong), ry / max(0.001, flat)


def generate_feature_point(rng: random.Random, nominal_radius: float) -> tuple[float, float]:
    angle = rng.random() * math.tau
    distance = nominal_radius * (rng.random() ** 0.58) * rng.uniform(0.05, 0.82)
    return math.cos(angle) * distance, math.sin(angle) * distance


def generate_asteroid(index: int) -> Image.Image:
    style = STYLE_PRESETS[index % len(STYLE_PRESETS)]
    seed = 90210 + index * 9973
    rng = random.Random(seed)
    base, high, low, accent, glow = PALETTES[int(style["palette"])]
    nominal_radius = rng.uniform(73.0, 89.0)
    tilt = rng.random() * math.tau
    elong = float(style["elong"]) * rng.uniform(0.94, 1.06)
    flat = float(style["flat"]) * rng.uniform(0.94, 1.06)
    outline = build_outline(rng, style)
    light = (-0.56, -0.74)

    craters = []
    for _ in range(int(style["craters"])):
        cx, cy = generate_feature_point(rng, nominal_radius)
        craters.append((
            cx,
            cy,
            rng.uniform(4.2, 18.5),
            rng.uniform(0.15, 0.52),
            rng.uniform(0.72, 1.36),
            rng.random() * math.tau,
        ))

    caves = []
    for _ in range(int(style["caves"])):
        cx, cy = generate_feature_point(rng, nominal_radius)
        caves.append((
            cx,
            cy,
            rng.uniform(9.5, 26.0),
            rng.uniform(0.48, 0.92),
            rng.uniform(0.70, 1.45),
            rng.random() * math.tau,
        ))

    boulders = []
    for _ in range(int(style["rubble"])):
        cx, cy = generate_feature_point(rng, nominal_radius)
        boulders.append((cx, cy, rng.uniform(1.6, 6.8), rng.uniform(0.10, 0.34)))

    ore_pockets = []
    for _ in range(int(style["ore"])):
        cx, cy = generate_feature_point(rng, nominal_radius)
        ore_pockets.append((
            cx,
            cy,
            rng.uniform(5.0, 17.5),
            rng.uniform(0.32, 0.78),
            rng.uniform(0.68, 1.42),
            rng.random() * math.tau,
            rng.random(),
        ))

    veins = []
    for _ in range(int(style["veins"])):
        cx, cy = generate_feature_point(rng, nominal_radius)
        veins.append((
            cx,
            cy,
            rng.uniform(14.0, 48.0),
            rng.uniform(0.9, 3.3),
            rng.random() * math.tau,
            rng.uniform(0.18, 0.48),
        ))

    facets = []
    for _ in range(int(style["facets"])):
        angle = rng.random() * math.tau
        normal = (math.cos(angle), math.sin(angle))
        facets.append((normal[0], normal[1], rng.uniform(-42.0, 42.0), rng.uniform(-0.13, 0.16)))

    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()

    for py in range(SIZE):
        y = py - CENTER
        for px in range(SIZE):
            x = px - CENTER
            lx, ly = rotated_scaled_coordinates(x, y, tilt, elong, flat)
            angle = math.atan2(ly, lx)
            distance = math.hypot(lx, ly)
            radius = outline_radius(outline, angle, nominal_radius)
            edge = 1.0 - smoothstep(radius - 2.6, radius + 1.2, distance)
            if edge <= 0.0:
                continue

            radial = max(0.0, min(1.0, distance / max(1.0, radius)))
            coarse = fbm(px + index * 21.0, py - index * 17.0, seed, 82.0, 5)
            fine = fbm(px * 2.4, py * 2.4, seed + 701, 38.0, 4)
            grit = hash01(px, py, seed + 1201)
            shade = 0.54 + coarse * 0.60 + fine * 0.22
            shade += (lx * light[0] + ly * light[1]) / max(1.0, radius) * 0.34
            shade -= radial * 0.25
            shade += (grit - 0.5) * 0.20 * float(style["grit"])

            for nx, ny, offset, strength in facets:
                plane = nx * lx + ny * ly + offset
                band = 1.0 - smoothstep(4.0, 22.0, abs(plane))
                side = 1.0 if plane > 0 else -1.0
                shade += band * side * strength

            crater_tint = 0.0
            rim_tint = 0.0
            for cx, cy, cr, depth, oval, cangle in craters:
                dx = lx - cx
                dy = ly - cy
                ca = math.cos(cangle)
                sa = math.sin(cangle)
                ox = ca * dx + sa * dy
                oy = (-sa * dx + ca * dy) * oval
                cd = math.hypot(ox, oy)
                crater_tint += (1.0 - smoothstep(cr * 0.12, cr * 0.95, cd)) * depth
                rim_distance = abs(cd - cr * 0.82)
                if rim_distance < cr * 0.20:
                    rim_tint += (1.0 - rim_distance / max(1.0, cr * 0.20)) * depth

            for cx, cy, cr, depth, oval, cangle in caves:
                dx = lx - cx
                dy = ly - cy
                ca = math.cos(cangle)
                sa = math.sin(cangle)
                ox = ca * dx + sa * dy
                oy = (-sa * dx + ca * dy) * oval
                cd = math.hypot(ox, oy)
                inner = 1.0 - smoothstep(cr * 0.14, cr * 0.86, cd)
                crater_tint += inner * depth * 1.18
                rim_distance = abs(cd - cr * 0.76)
                if rim_distance < cr * 0.17:
                    rim_tint += (1.0 - rim_distance / max(1.0, cr * 0.17)) * depth * 0.90

            shade -= crater_tint * 0.52
            shade += rim_tint * 0.18

            for bx, by, br, height in boulders:
                bd = math.hypot(lx - bx, ly - by)
                if bd < br:
                    bump = 1.0 - bd / br
                    shade += bump * height
                    shade -= smoothstep(br * 0.55, br, bd) * height * 0.25

            shade = max(0.0, min(1.55, shade))
            color = mix_color(low, high, max(0.0, min(1.0, shade * 0.64)))
            color = mix_color(base, color, 0.78)

            for cx, cy, length, width, vangle, strength in veins:
                dx = lx - cx
                dy = ly - cy
                ca = math.cos(vangle)
                sa = math.sin(vangle)
                along = ca * dx + sa * dy
                across = -sa * dx + ca * dy
                if abs(along) < length and abs(across) < width:
                    vein = (1.0 - abs(along) / length) * (1.0 - abs(across) / max(0.001, width))
                    if vein > 0.03:
                        color = mix_color(color, accent, vein * strength)

            for cx, cy, cr, strength, oval, oangle, warm in ore_pockets:
                dx = lx - cx
                dy = ly - cy
                ca = math.cos(oangle)
                sa = math.sin(oangle)
                ox = ca * dx + sa * dy
                oy = (-sa * dx + ca * dy) * oval
                od = math.hypot(ox, oy)
                if od < cr:
                    pocket = 1.0 - smoothstep(cr * 0.15, cr, od)
                    ore_color = mix_color(accent, glow, 0.26 + warm * 0.58)
                    color = mix_color(color, ore_color, pocket * strength)
                    shade += pocket * 0.12

            if grit > 0.993:
                color = mix_color(color, glow, 0.55)
            elif grit > 0.985 and int(style["ore"]) > 0:
                color = mix_color(color, accent, 0.36)
            elif grit < 0.016:
                color = mix_color(color, low, 0.42)

            edge_shadow = smoothstep(0.76, 1.0, radial)
            color = mix_color(color, low, edge_shadow * 0.18)
            alpha = int(255 * edge)
            pixels[px, py] = (color[0], color[1], color[2], alpha)

    image = image.filter(ImageFilter.UnsharpMask(radius=1.05, percent=135, threshold=2))
    return image


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for index in range(ASTEROID_COUNT):
        image = generate_asteroid(index)
        image.save(OUT_DIR / f"asteroid_{index:02d}.png")


if __name__ == "__main__":
    main()
