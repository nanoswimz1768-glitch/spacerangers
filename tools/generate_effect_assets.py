from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "game" / "assets" / "effects"
SIZE = 256
CENTER = SIZE / 2
SHIELD_SIZE = 384
SHIELD_CENTER = SHIELD_SIZE / 2


def clamp(value: float, low: float = 0.0, high: float = 1.0) -> float:
    return max(low, min(high, value))


def smoothstep(edge0: float, edge1: float, value: float) -> float:
    if edge0 == edge1:
        return 0.0
    t = clamp((value - edge0) / (edge1 - edge0))
    return t * t * (3.0 - 2.0 * t)


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
    sx = tx * tx * (3.0 - 2.0 * tx)
    sy = ty * ty * (3.0 - 2.0 * ty)
    a = hash01(x0, y0, seed)
    b = hash01(x0 + 1, y0, seed)
    c = hash01(x0, y0 + 1, seed)
    d = hash01(x0 + 1, y0 + 1, seed)
    return (a * (1 - sx) + b * sx) * (1 - sy) + (c * (1 - sx) + d * sx) * sy


def fbm(x: float, y: float, seed: int) -> float:
    total = 0.0
    amplitude = 0.58
    scale = 72.0
    weight = 0.0
    for octave in range(5):
        total += value_noise(x, y, seed + octave * 97, scale) * amplitude
        weight += amplitude
        amplitude *= 0.54
        scale *= 0.52
    return total / weight


def save_fire_plume() -> None:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(SIZE):
        ny = (y + 0.5) / SIZE
        for x in range(SIZE):
            nx = (x + 0.5) / SIZE
            axis = nx
            width = 0.34 * (1.0 - axis) ** 0.92 + 0.028
            center = 0.5 + math.sin(axis * math.tau * 1.45) * 0.034
            dy = abs(ny - center) / width
            body = smoothstep(1.0, 0.04, dy)
            taper = smoothstep(1.0, 0.08, axis) * smoothstep(-0.02, 0.20, axis)
            noise = fbm(x, y, 611)
            vertical_wisp = math.sin(ny * math.tau * 4.0 + axis * 8.5 + noise * 2.2)
            lick = smoothstep(0.58, 1.06, noise + vertical_wisp * 0.13 + (1.0 - axis) * 0.20 - dy * 0.12)
            soft_edge = smoothstep(1.05, 0.28, dy + noise * 0.16)
            alpha = clamp(body * taper * soft_edge * (0.34 + lick * 1.18))
            alpha *= smoothstep(1.04, 0.84, axis + dy * 0.08)
            heat = clamp((1.0 - axis) * 1.08 + (1.0 - dy) * 0.42 + noise * 0.20)
            r = 255
            g = int(46 + 190 * heat)
            b = int(8 + 56 * heat)
            pixels[x, y] = (r, g, b, int(alpha * 255))

    image = image.filter(ImageFilter.GaussianBlur(0.38))
    image.save(OUT_DIR / "asteroid_fire_plume.png")


def save_flame_lobe(index: int, seed: int) -> None:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(SIZE):
        ny = (y + 0.5) / SIZE
        for x in range(SIZE):
            nx = (x + 0.5) / SIZE
            axis = nx
            center = 0.5 + math.sin(axis * math.tau * (0.75 + index * 0.13)) * (0.035 + index * 0.006)
            width = 0.22 * (1.0 - axis) ** 1.05 + 0.032
            dy = abs(ny - center) / width
            silhouette = smoothstep(1.0, 0.10, dy) * smoothstep(1.0, 0.08, axis) * smoothstep(-0.02, 0.16, axis)
            noise = fbm(x, y, seed)
            ribbon = math.sin(ny * math.tau * (3.2 + index * 0.27) + axis * 9.0 + index)
            holes = smoothstep(0.52, 1.02, noise + ribbon * 0.14 + (1.0 - axis) * 0.14 - dy * 0.10)
            lick = smoothstep(0.56, 0.88, noise + ribbon * 0.08)
            alpha = clamp(silhouette * holes * (0.62 + lick * 0.52))
            heat = clamp((1.0 - axis) * 1.24 + (1.0 - dy) * 0.32 + noise * 0.2)
            pixels[x, y] = (
                255,
                int(35 + 205 * heat),
                int(5 + 62 * heat),
                int(alpha * 255),
            )

    image = image.filter(ImageFilter.GaussianBlur(0.32))
    image.save(OUT_DIR / f"asteroid_flame_lobe_{index:02d}.png")


def save_heat_corona() -> None:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(SIZE):
        dy = (y + 0.5 - CENTER) / CENTER
        for x in range(SIZE):
            dx = (x + 0.5 - CENTER) / CENTER
            distance = math.sqrt(dx * dx + dy * dy)
            angle = math.atan2(dy, dx)
            noise = fbm(x, y, 1543)
            ring = smoothstep(1.02, 0.22, distance) * smoothstep(0.02, 0.36, distance)
            rays = 0.58 + math.sin(angle * 9.0 + noise * 4.2) * 0.15
            alpha = clamp(ring * rays * (0.24 + noise * 0.34))
            heat = clamp(1.1 - distance * 0.55 + noise * 0.12)
            pixels[x, y] = (255, int(44 + 188 * heat), int(4 + 58 * heat), int(alpha * 215))

    image = image.filter(ImageFilter.GaussianBlur(1.25))
    image.save(OUT_DIR / "asteroid_heat_corona.png")


def save_fire_glow() -> None:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(SIZE):
        dy = (y + 0.5 - CENTER) / CENTER
        for x in range(SIZE):
            dx = (x + 0.5 - CENTER) / CENTER
            distance = math.sqrt(dx * dx + dy * dy)
            noise = fbm(x, y, 733)
            alpha = clamp((1.0 - distance) ** 2.6 if distance < 1.0 else 0.0)
            alpha *= 0.72 + noise * 0.42
            hot = clamp(1.0 - distance * 0.82 + noise * 0.12)
            pixels[x, y] = (255, int(42 + 176 * hot), int(5 + 54 * hot), int(alpha * 255))

    image = image.filter(ImageFilter.GaussianBlur(1.2))
    image.save(OUT_DIR / "asteroid_fire_glow.png")


def save_spark() -> None:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(SIZE):
        dy = (y + 0.5 - CENTER) / CENTER
        for x in range(SIZE):
            dx = (x + 0.5 - CENTER) / CENTER
            distance = math.sqrt(dx * dx + dy * dy)
            alpha = clamp((1.0 - distance) ** 4.2 if distance < 1.0 else 0.0)
            core = clamp((1.0 - distance * 2.1) if distance < 0.48 else 0.0)
            pixels[x, y] = (255, int(128 + 120 * core), int(24 + 90 * core), int(alpha * 255))

    image = image.filter(ImageFilter.GaussianBlur(0.35))
    image.save(OUT_DIR / "asteroid_fire_spark.png")


def save_smoke_puff() -> None:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(SIZE):
        dy = (y + 0.5 - CENTER) / CENTER
        for x in range(SIZE):
            dx = (x + 0.5 - CENTER) / CENTER
            distance = math.sqrt(dx * dx + dy * dy)
            angle = math.atan2(dy, dx)
            noise = fbm(x, y, 877)
            ragged_distance = distance + (noise - 0.5) * 0.18 + math.sin(angle * 7.0 + noise * 3.0) * 0.035
            alpha = clamp((1.0 - ragged_distance) ** 2.35 if ragged_distance < 1.0 else 0.0)
            alpha *= smoothstep(0.30, 0.86, noise + (1.0 - distance) * 0.10)
            shade = int(34 + 68 * noise)
            pixels[x, y] = (shade, max(0, shade - 5), max(0, shade - 12), int(alpha * 158))

    image = image.filter(ImageFilter.GaussianBlur(1.25))
    image.save(OUT_DIR / "asteroid_smoke_puff.png")


def save_dust_ring() -> None:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(SIZE):
        dy = (y + 0.5 - CENTER) / CENTER
        for x in range(SIZE):
            dx = (x + 0.5 - CENTER) / CENTER
            distance = math.sqrt(dx * dx + dy * dy)
            angle = math.atan2(dy, dx)
            noise = fbm(x, y, 2027)
            ring = smoothstep(0.74, 0.38, abs(distance - 0.58))
            ragged = 0.52 + noise * 0.72 + math.sin(angle * 17.0 + noise * 4.0) * 0.16
            alpha = clamp(ring * ragged * smoothstep(0.08, 0.22, distance) * smoothstep(1.02, 0.86, distance))
            shade = int(62 + noise * 72)
            pixels[x, y] = (shade, int(shade * 0.9), int(shade * 0.76), int(alpha * 180))

    image = image.filter(ImageFilter.GaussianBlur(1.1))
    image.save(OUT_DIR / "asteroid_dust_ring.png")


def save_impact_flash() -> None:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(SIZE):
        dy = (y + 0.5 - CENTER) / CENTER
        for x in range(SIZE):
            dx = (x + 0.5 - CENTER) / CENTER
            distance = math.sqrt(dx * dx + dy * dy)
            angle = math.atan2(dy, dx)
            noise = fbm(x, y, 2311)
            ragged = distance + (noise - 0.5) * 0.18 + math.sin(angle * 11.0 + noise * 2.8) * 0.025
            core = smoothstep(0.76, 0.0, ragged)
            alpha = clamp(core ** 2.05 * (0.55 + noise * 0.46))
            heat = clamp(1.0 - distance * 0.9 + noise * 0.14)
            pixels[x, y] = (255, int(105 + 145 * heat), int(22 + 90 * heat), int(alpha * 235))

    image = image.filter(ImageFilter.GaussianBlur(0.55))
    image.save(OUT_DIR / "asteroid_impact_flash.png")


def save_shield_bubble() -> None:
    image = Image.new("RGBA", (SHIELD_SIZE, SHIELD_SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(SHIELD_SIZE):
        dy = (y + 0.5 - SHIELD_CENTER) / SHIELD_CENTER
        for x in range(SHIELD_SIZE):
            dx = (x + 0.5 - SHIELD_CENTER) / SHIELD_CENTER
            distance = math.sqrt(dx * dx + dy * dy)
            if distance > 1.08:
                continue

            angle = math.atan2(dy, dx)
            noise = fbm(x * 0.66, y * 0.66, 8017)
            edge = smoothstep(1.02, 0.84, distance) * smoothstep(0.62, 0.88, distance)
            inner_haze = smoothstep(0.98, 0.10, distance) * smoothstep(0.04, 0.54, distance) * 0.22
            caustic_a = abs(math.sin(angle * 3.0 + noise * 5.4 + distance * 4.6))
            caustic_b = abs(math.sin(angle * 5.0 - noise * 3.6 + distance * 7.2))
            filaments = smoothstep(0.92, 0.985, caustic_a) * 0.34 + smoothstep(0.91, 0.985, caustic_b) * 0.22
            filaments *= smoothstep(0.22, 0.88, distance) * smoothstep(1.02, 0.72, distance)
            rim_noise = 0.72 + noise * 0.42 + math.sin(angle * 17.0 + noise * 4.0) * 0.08
            alpha = clamp(edge * rim_noise + inner_haze + filaments)
            pixels[x, y] = (255, 255, 255, int(alpha * 205))

    image = image.filter(ImageFilter.GaussianBlur(0.45))
    image.save(OUT_DIR / "shield_bubble.png")


def save_shield_ripple() -> None:
    image = Image.new("RGBA", (SHIELD_SIZE, SHIELD_SIZE), (0, 0, 0, 0))
    pixels = image.load()
    for y in range(SHIELD_SIZE):
        dy = (y + 0.5 - SHIELD_CENTER) / SHIELD_CENTER
        for x in range(SHIELD_SIZE):
            dx = (x + 0.5 - SHIELD_CENTER) / SHIELD_CENTER
            distance = math.sqrt(dx * dx + dy * dy)
            if distance > 1.0:
                continue

            angle = math.atan2(dy, dx)
            noise = fbm(x * 0.92, y * 0.92, 9137)
            ring = 0.0
            for center, width, power in [(0.28, 0.034, 0.38), (0.52, 0.030, 0.55), (0.77, 0.026, 0.72), (0.93, 0.022, 0.94)]:
                ring += smoothstep(width, 0.0, abs(distance - center)) * power
            radial_lines = smoothstep(0.955, 0.998, abs(math.sin(angle * 10.0 + noise * 3.2))) * smoothstep(0.20, 0.96, distance) * 0.16
            alpha = clamp((ring + radial_lines) * (0.80 + noise * 0.35))
            pixels[x, y] = (255, 255, 255, int(alpha * 230))

    image = image.filter(ImageFilter.GaussianBlur(0.34))
    image.save(OUT_DIR / "shield_ripple.png")


def save_shard(index: int, seed: int) -> None:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()
    points: list[tuple[float, float]] = []
    count = 7 + index % 3
    for i in range(count):
        angle = i / count * math.tau
        radius = 0.48 + hash01(i, index, seed) * 0.34
        if i % 2 == 1:
            radius *= 0.72
        points.append((
            CENTER + math.cos(angle) * CENTER * radius,
            CENTER + math.sin(angle) * CENTER * radius,
        ))

    min_x = min(p[0] for p in points)
    max_x = max(p[0] for p in points)
    min_y = min(p[1] for p in points)
    max_y = max(p[1] for p in points)

    base = [(86, 79, 70), (112, 104, 92), (64, 61, 58), (104, 73, 62), (78, 92, 94), (128, 109, 82)][index % 6]
    light = tuple(min(255, int(channel * 1.52 + 28)) for channel in base)
    dark = tuple(max(0, int(channel * 0.42)) for channel in base)

    def inside_polygon(px: float, py: float) -> bool:
        inside = False
        j = len(points) - 1
        for i, point in enumerate(points):
            xi, yi = point
            xj, yj = points[j]
            intersects = (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / max(0.0001, yj - yi) + xi
            if intersects:
                inside = not inside
            j = i
        return inside

    for y in range(SIZE):
        for x in range(SIZE):
            if not inside_polygon(x + 0.5, y + 0.5):
                continue

            nx = (x - min_x) / max(1.0, max_x - min_x)
            ny = (y - min_y) / max(1.0, max_y - min_y)
            noise = fbm(x, y, seed + index * 31)
            shade = clamp(0.25 + nx * 0.42 + (1.0 - ny) * 0.3 + noise * 0.24)
            if noise > 0.72:
                shade *= 0.72
            color = (
                int(dark[0] + (light[0] - dark[0]) * shade),
                int(dark[1] + (light[1] - dark[1]) * shade),
                int(dark[2] + (light[2] - dark[2]) * shade),
                255,
            )
            pixels[x, y] = color

    image = image.filter(ImageFilter.GaussianBlur(0.35))
    image.save(OUT_DIR / f"asteroid_shard_{index:02d}.png")


def save_ship_debris(index: int, seed: int) -> None:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    pixels = image.load()
    half_length = 0.20 + hash01(index, 11, seed) * 0.13
    half_width = 0.050 + hash01(index, 17, seed) * 0.045
    shear = (hash01(index, 23, seed) - 0.5) * 0.32
    rotation = (hash01(index, 31, seed) - 0.5) * 0.9
    cos_r = math.cos(rotation)
    sin_r = math.sin(rotation)
    base_palette = [
        (93, 106, 116),
        (116, 123, 126),
        (80, 88, 101),
        (121, 111, 98),
        (70, 96, 112),
        (130, 132, 126),
    ]
    base = base_palette[index % len(base_palette)]
    hot_edge = (255, 137, 36)

    for y in range(SIZE):
        ny_world = (y + 0.5 - CENTER) / CENTER
        for x in range(SIZE):
            nx_world = (x + 0.5 - CENTER) / CENTER
            lx = nx_world * cos_r + ny_world * sin_r
            ly = -nx_world * sin_r + ny_world * cos_r
            lx -= ly * shear
            axis = abs(lx) / half_length
            if axis > 1.0:
                continue

            noise = fbm(x, y, seed + index * 73)
            edge_warp = (noise - 0.5) * 0.018 + math.sin((lx + ly) * 41.0 + index) * 0.006
            local_width = half_width * (0.62 + (1.0 - axis) * 0.52) + edge_warp
            if abs(ly) > max(0.012, local_width):
                continue

            if noise < 0.16 and axis > 0.58:
                continue

            cross = abs(ly) / max(0.0001, local_width)
            bevel = max(axis, cross)
            highlight = clamp(0.28 + (lx / half_length + 1.0) * 0.16 + (1.0 - cross) * 0.24 + noise * 0.18)
            scorch = smoothstep(0.54, 0.86, noise + (1.0 - axis) * 0.12)
            edge_heat = smoothstep(0.70, 1.02, bevel + noise * 0.16) * (0.12 + hash01(index, 37, seed) * 0.24)
            r = int(base[0] * (0.46 + highlight) + 34 * bevel - 22 * scorch)
            g = int(base[1] * (0.46 + highlight) + 34 * bevel - 26 * scorch)
            b = int(base[2] * (0.46 + highlight) + 38 * bevel - 30 * scorch)
            r = int(r * (1.0 - edge_heat) + hot_edge[0] * edge_heat)
            g = int(g * (1.0 - edge_heat) + hot_edge[1] * edge_heat)
            b = int(b * (1.0 - edge_heat) + hot_edge[2] * edge_heat)
            pixels[x, y] = (max(0, min(255, r)), max(0, min(255, g)), max(0, min(255, b)), 238)

    image = image.filter(ImageFilter.GaussianBlur(0.22))
    image.save(OUT_DIR / f"ship_debris_{index:02d}.png")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    save_fire_plume()
    for index in range(4):
        save_flame_lobe(index, 1701 + index * 43)
    save_heat_corona()
    save_fire_glow()
    save_spark()
    save_smoke_puff()
    save_dust_ring()
    save_impact_flash()
    save_shield_bubble()
    save_shield_ripple()
    for index in range(6):
        save_shard(index, 1201 + index * 37)
    for index in range(6):
        save_ship_debris(index, 2501 + index * 61)
    print(f"Generated effect assets in {OUT_DIR}")


if __name__ == "__main__":
    main()
