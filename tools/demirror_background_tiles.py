from __future__ import annotations

import argparse
import hashlib
import json
import shutil
from datetime import datetime
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image, ImageEnhance, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
SOL_TILE_PATH = ROOT / "game" / "assets" / "backgrounds" / "space_nebula_tile.png"
GENERATED_SYSTEMS_DIR = ROOT / "game" / "assets" / "generated" / "systems"
GENERATED_BACKGROUND_TILE_DIR = ROOT / "game" / "assets" / "generated" / "background_tiles"
BACKUP_ROOT = ROOT / "tools" / "generated" / "backups"
DEFAULT_REPORT_PATH = ROOT / "tools" / "generated" / "background_demirror_report.json"


def main() -> None:
    args = parse_args()
    if args.restore:
        restore_backup(Path(args.restore))
        return

    targets = collect_targets(args.target_set)
    if not targets:
        raise SystemExit("No background tiles matched the requested target set.")

    backup_dir = backup_targets(targets, args.backup_id)
    report = {
        "backupDir": str(backup_dir.relative_to(ROOT)),
        "targetSet": args.target_set,
        "strength": args.strength,
        "brightness": args.brightness,
        "vibrance": args.vibrance,
        "contrast": args.contrast,
        "clarity": args.clarity,
        "assets": [],
    }

    for target in targets:
        image = Image.open(target).convert("RGB")
        before = measure_tile(image)
        if args.synthesize_asymmetric:
            processed = synthesize_asymmetric_nebula_tile(image, target.stem, strength=args.strength)
        else:
            processed = reduce_mirror_artifacts(image, target.stem, strength=args.strength)

        processed = enhance_background_clarity(
            processed,
            brightness=args.brightness,
            vibrance=args.vibrance,
            contrast=args.contrast,
            clarity=args.clarity,
        )
        processed.save(target, compress_level=6)
        after = measure_tile(processed)
        report["assets"].append(
            {
                "path": str(target.relative_to(ROOT)),
                "before": before,
                "after": after,
            }
        )
        print(
            f"{target.relative_to(ROOT)}: mirror {before['mirrorMax']:.3f} -> {after['mirrorMax']:.3f}; "
            f"seam {before['seamMax']:.5f} -> {after['seamMax']:.5f}"
        )

    write_json(Path(args.report), report)
    print(f"Backup written to {backup_dir}")
    print(f"Report written to {args.report}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Reduce mirror/kaleidoscope artifacts in tileable space backgrounds.")
    parser.add_argument(
        "--target-set",
        choices=("active", "all-generated", "sol"),
        default="active",
        help="active = Sol plus generated systems referenced by runtime JSON; all-generated = every generated background tile; sol = Sol only.",
    )
    parser.add_argument("--strength", type=float, default=0.22, help="Demirror strength; keep this subtle for accepted backgrounds.")
    parser.add_argument("--brightness", type=float, default=1.0, help="Post-pass brightness multiplier.")
    parser.add_argument("--vibrance", type=float, default=1.0, help="Post-pass saturation/vibrance multiplier.")
    parser.add_argument("--contrast", type=float, default=1.0, help="Post-pass contrast multiplier.")
    parser.add_argument("--clarity", type=float, default=0.0, help="Post-pass local contrast/sharpness amount.")
    parser.add_argument(
        "--synthesize-asymmetric",
        action="store_true",
        help="Replace the mirrored source layout with a phase-synthesized asymmetric tile while preserving color/frequency character.",
    )
    parser.add_argument("--backup-id", default="", help="Optional stable backup id. Default uses a timestamp.")
    parser.add_argument("--report", default=str(DEFAULT_REPORT_PATH), help="Report JSON path.")
    parser.add_argument("--restore", default="", help="Restore files from a previous backup directory and exit.")
    return parser.parse_args()


def collect_targets(target_set: str) -> list[Path]:
    targets: list[Path] = []
    if target_set in {"active", "sol"}:
        targets.append(SOL_TILE_PATH)

    if target_set == "active":
        targets.extend(active_generated_backgrounds())
    elif target_set == "all-generated":
        targets.extend(sorted(GENERATED_BACKGROUND_TILE_DIR.glob("*.png")))

    existing: list[Path] = []
    seen: set[Path] = set()
    for target in targets:
        resolved = target.resolve()
        if resolved in seen or not target.exists():
            continue

        seen.add(resolved)
        existing.append(target)

    return existing


def active_generated_backgrounds() -> list[Path]:
    result: list[Path] = []
    if not GENERATED_SYSTEMS_DIR.exists():
        return result

    for system_path in sorted(GENERATED_SYSTEMS_DIR.glob("*.json")):
        data = read_json(system_path)
        texture_path = str(data.get("background", {}).get("texturePath", ""))
        if not texture_path.startswith("res://"):
            continue

        local = ROOT / "game" / texture_path.removeprefix("res://")
        if GENERATED_BACKGROUND_TILE_DIR in local.parents or local.parent == GENERATED_BACKGROUND_TILE_DIR:
            result.append(local)

    return result


def backup_targets(targets: list[Path], backup_id: str) -> Path:
    backup_name = backup_id.strip() or f"background_tiles_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
    backup_dir = BACKUP_ROOT / backup_name
    backup_dir.mkdir(parents=True, exist_ok=False)
    manifest = {"createdAt": datetime.now().isoformat(timespec="seconds"), "files": []}

    for target in targets:
        rel = target.relative_to(ROOT)
        backup_path = backup_dir / rel
        backup_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(target, backup_path)

        import_path = Path(str(target) + ".import")
        import_backup_path = None
        if import_path.exists():
            import_rel = import_path.relative_to(ROOT)
            import_backup_path = backup_dir / import_rel
            import_backup_path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(import_path, import_backup_path)

        manifest["files"].append(
            {
                "target": str(rel).replace("\\", "/"),
                "backup": str(backup_path.relative_to(backup_dir)).replace("\\", "/"),
                "importTarget": str(import_path.relative_to(ROOT)).replace("\\", "/") if import_path.exists() else "",
                "importBackup": str(import_backup_path.relative_to(backup_dir)).replace("\\", "/") if import_backup_path else "",
            }
        )

    write_json(backup_dir / "manifest.json", manifest)
    return backup_dir


def restore_backup(backup_dir: Path) -> None:
    backup_dir = backup_dir if backup_dir.is_absolute() else ROOT / backup_dir
    manifest_path = backup_dir / "manifest.json"
    if not manifest_path.exists():
        raise SystemExit(f"Backup manifest not found: {manifest_path}")

    manifest = read_json(manifest_path)
    for entry in manifest.get("files", []):
        target = ROOT / entry["target"]
        backup = backup_dir / entry["backup"]
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(backup, target)
        print(f"Restored {target.relative_to(ROOT)}")

        import_target = str(entry.get("importTarget", ""))
        import_backup = str(entry.get("importBackup", ""))
        if import_target and import_backup:
            import_target_path = ROOT / import_target
            import_backup_path = backup_dir / import_backup
            if import_backup_path.exists():
                shutil.copy2(import_backup_path, import_target_path)
                print(f"Restored {import_target_path.relative_to(ROOT)}")


def reduce_mirror_artifacts(image: Image.Image, variant_id: str, strength: float = 0.22) -> Image.Image:
    """Keep the image tileable while breaking obvious bilateral symmetry."""
    image = image.convert("RGB")
    if strength <= 0.0001:
        return image.copy()

    arr = np.asarray(image).astype(np.float32) / 255.0
    height, width, _ = arr.shape
    seed = int(hashlib.sha256(variant_id.encode("utf-8")).hexdigest()[:8], 16)
    strength = float(np.clip(strength, 0.0, 0.75))

    low_radius = max(24, min(height, width) // 48)
    low = np.asarray(image.filter(ImageFilter.GaussianBlur(radius=low_radius))).astype(np.float32) / 255.0
    high = arr - low

    roll_a = np.roll(
        arr,
        shift=(
            signed_shift(height, 0.173, seed),
            signed_shift(width, 0.317, seed >> 7),
        ),
        axis=(0, 1),
    )
    roll_b = np.roll(
        arr,
        shift=(
            signed_shift(height, 0.289, seed >> 13),
            signed_shift(width, 0.131, seed >> 19),
        ),
        axis=(0, 1),
    )
    low_roll_a = np.roll(
        low,
        shift=(
            signed_shift(height, 0.173, seed),
            signed_shift(width, 0.317, seed >> 7),
        ),
        axis=(0, 1),
    )
    low_roll_b = np.roll(
        low,
        shift=(
            signed_shift(height, 0.289, seed >> 13),
            signed_shift(width, 0.131, seed >> 19),
        ),
        axis=(0, 1),
    )

    lum = np.dot(arr, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    midtone_mask = smoothstep_array(0.018, 0.18, lum) * (1.0 - smoothstep_array(0.58, 0.92, lum))
    mask = periodic_mask(height, width, seed)
    mirror_score = max(mirror_correlation(image, axis="horizontal"), mirror_correlation(image, axis="vertical"))
    mirror_boost = float(np.clip((mirror_score - 0.58) / 0.42, 0.0, 1.0))
    low_amount = strength * (0.18 + 0.82 * mask) * (0.84 + mirror_boost * 0.54)
    low_amount = np.clip(low_amount, 0.0, 0.86)

    low_mixed = low_roll_a * (0.58 + 0.20 * mask[:, :, None]) + low_roll_b * (0.42 - 0.20 * mask[:, :, None])
    out = low * (1.0 - low_amount[:, :, None]) + low_mixed * low_amount[:, :, None] + high * (1.0 - strength * 0.05)

    amount = strength * (0.28 + mirror_boost * 0.24) * midtone_mask * (0.32 + 0.68 * mask)
    mixed = roll_a * (0.58 + 0.20 * mask[:, :, None]) + roll_b * (0.42 - 0.20 * mask[:, :, None])
    out = out * (1.0 - amount[:, :, None]) + mixed * amount[:, :, None]

    structure_mask = smoothstep_array(0.010, 0.20, lum) * (1.0 - smoothstep_array(0.70, 0.96, lum))
    modulation_strength = strength * (0.10 + mirror_boost * 0.28)
    modulation = (periodic_mask(height, width, seed ^ 0xA53C91) - 0.5) * modulation_strength * structure_mask
    out *= 1.0 + modulation[:, :, None]

    color_bias = seed_color_bias(seed)
    out += modulation[:, :, None] * color_bias[None, None, :] * 0.22
    out = attenuate_baked_star_symmetry(out, mirror_boost, strength)
    out = np.clip(out, 0.0, 1.0)

    result = Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), mode="RGB")
    result = result.filter(ImageFilter.UnsharpMask(radius=0.35, percent=18, threshold=5))
    result = ImageEnhance.Contrast(result).enhance(1.01)
    return result


def synthesize_asymmetric_nebula_tile(image: Image.Image, variant_id: str, strength: float = 0.92) -> Image.Image:
    """Break mirror axes while preserving the source palette and baked star style."""
    image = image.convert("RGB")
    arr = np.asarray(image).astype(np.float32) / 255.0
    height, width, _ = arr.shape
    seed = int(hashlib.sha256(f"preserve-art-asymmetric|{variant_id}".encode("utf-8")).hexdigest()[:8], 16)
    strength = float(np.clip(strength, 0.0, 1.0))

    starless, star_layer = separate_baked_star_layer(arr)
    base_image = Image.fromarray((starless * 255.0 + 0.5).astype(np.uint8), mode="RGB")
    low = np.asarray(
        base_image.filter(ImageFilter.GaussianBlur(radius=max(12.0, min(height, width) / 150.0)))
    ).astype(np.float32) / 255.0
    high = starless - low

    mirror_score = max(mirror_correlation(image, axis="horizontal"), mirror_correlation(image, axis="vertical"))
    mirror_boost = float(np.clip((mirror_score - 0.18) / 0.55, 0.0, 1.0))
    mask_a = periodic_mask(height, width, seed ^ 0x171B21)
    mask_b = periodic_mask(height, width, seed ^ 0xAC53F2)
    amount = np.clip(strength * (0.26 + mirror_boost * 0.30) * (0.45 + mask_a * 0.55), 0.0, 0.62)

    low_roll_a = np.roll(
        low,
        shift=(signed_shift(height, 0.173, seed >> 3), signed_shift(width, 0.317, seed >> 11)),
        axis=(0, 1),
    )
    low_roll_b = np.roll(
        low,
        shift=(signed_shift(height, 0.289, seed >> 17), signed_shift(width, 0.131, seed >> 23)),
        axis=(0, 1),
    )
    low_roll_c = np.roll(
        low,
        shift=(signed_shift(height, 0.407, seed >> 5), signed_shift(width, 0.223, seed >> 29)),
        axis=(0, 1),
    )
    low_mixed = low_roll_a * (0.50 + mask_b[:, :, None] * 0.10)
    low_mixed += low_roll_b * (0.30 - mask_b[:, :, None] * 0.06)
    low_mixed += low_roll_c * 0.20
    out_low = low * (1.0 - amount[:, :, None]) + low_mixed * amount[:, :, None]

    high_roll = np.roll(
        high,
        shift=(signed_shift(height, 0.061, seed >> 7), signed_shift(width, 0.097, seed >> 13)),
        axis=(0, 1),
    )
    high_amount = np.clip(strength * 0.12 * (0.35 + mask_b * 0.65), 0.0, 0.18)
    out = out_low + high * (1.0 - high_amount[:, :, None]) + high_roll * high_amount[:, :, None]

    lum = np.dot(out, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    midtone = smoothstep_array(0.014, 0.34, lum) * (1.0 - smoothstep_array(0.66, 0.94, lum))
    luminance_modulation = 1.0 + (mask_b - 0.5) * midtone * strength * 0.055
    out *= luminance_modulation[:, :, None]
    out = match_channel_stats(out, starless) * 0.72 + out * 0.28
    out += redistribute_baked_star_layer(star_layer, seed) * (0.94 + strength * 0.04)
    out = np.clip(out, 0.0, 1.0)

    result = Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), mode="RGB")
    result = result.filter(ImageFilter.UnsharpMask(radius=0.38, percent=18, threshold=5))
    return result


def separate_baked_star_layer(arr: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    source = Image.fromarray((np.clip(arr, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), mode="RGB")
    low_small = np.asarray(source.filter(ImageFilter.GaussianBlur(radius=max(4.0, min(source.size) / 320.0)))).astype(np.float32) / 255.0
    low_large = np.asarray(source.filter(ImageFilter.GaussianBlur(radius=max(18.0, min(source.size) / 96.0)))).astype(np.float32) / 255.0
    lum = np.dot(arr, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    low_lum = np.dot(low_small, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    large_lum = np.dot(low_large, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    sparkle = np.maximum(0.0, lum - low_lum)
    star_mask = smoothstep_array(0.014, 0.075, sparkle) * smoothstep_array(0.070, 0.24, lum)
    star_mask *= 1.0 - smoothstep_array(0.34, 0.82, large_lum)
    star_mask = np.clip(star_mask * 1.7, 0.0, 1.0)
    starless = arr * (1.0 - star_mask[:, :, None]) + low_large * star_mask[:, :, None]
    star_layer = np.maximum(arr - starless, 0.0)
    return starless, star_layer


def redistribute_baked_star_layer(star_layer: np.ndarray, seed: int) -> np.ndarray:
    height, width, _ = star_layer.shape
    if float(star_layer.max()) <= 0.0001:
        return star_layer

    masks = [
        periodic_mask(height, width, seed ^ 0x171B21),
        periodic_mask(height, width, seed ^ 0xAC53F2),
        periodic_mask(height, width, seed ^ 0x59D2F1),
    ]
    shifts = [
        (signed_shift(height, 0.113, seed >> 3), signed_shift(width, 0.371, seed >> 11)),
        (signed_shift(height, 0.347, seed >> 17), signed_shift(width, 0.193, seed >> 23)),
        (signed_shift(height, 0.251, seed >> 5), signed_shift(width, 0.487, seed >> 29)),
    ]
    weights = [0.42, 0.34, 0.20]
    out = star_layer * 0.04
    for mask, shift, weight in zip(masks, shifts, weights):
        rolled = np.roll(star_layer, shift=shift, axis=(0, 1))
        out += rolled * weight * (0.74 + mask[:, :, None] * 0.52)

    source_energy = float(star_layer.sum())
    output_energy = float(out.sum())
    if output_energy > 0.0001 and source_energy > 0.0001:
        out *= np.clip(source_energy / output_energy, 0.72, 1.18)

    return np.clip(out, 0.0, 1.0)


def phase_synthesize_channel(channel: np.ndarray, rng: np.random.Generator) -> np.ndarray:
    spectrum = np.fft.rfft2(channel)
    amplitude = np.abs(spectrum)
    phase = rng.uniform(-np.pi, np.pi, size=spectrum.shape)
    randomized = amplitude * np.exp(1j * phase)
    randomized[0, 0] = spectrum[0, 0]

    if randomized.shape[0] > 1:
        randomized[channel.shape[0] // 2, 0] = spectrum[channel.shape[0] // 2, 0]

    out = np.fft.irfft2(randomized, s=channel.shape).astype(np.float32)
    return out


def match_channel_stats(candidate: np.ndarray, reference: np.ndarray) -> np.ndarray:
    out = np.empty_like(candidate)
    for channel in range(3):
        src = candidate[:, :, channel]
        ref = reference[:, :, channel]
        src_std = float(src.std())
        ref_std = float(ref.std())
        if src_std <= 0.00001:
            out[:, :, channel] = float(ref.mean())
        else:
            out[:, :, channel] = (src - float(src.mean())) / src_std * ref_std + float(ref.mean())

    return np.clip(out, 0.0, 1.0)


def enhance_background_clarity(
    image: Image.Image,
    brightness: float = 1.0,
    vibrance: float = 1.0,
    contrast: float = 1.0,
    clarity: float = 0.0,
) -> Image.Image:
    image = image.convert("RGB")
    if (
        abs(brightness - 1.0) <= 0.0001
        and abs(vibrance - 1.0) <= 0.0001
        and abs(contrast - 1.0) <= 0.0001
        and clarity <= 0.0001
    ):
        return image.copy()

    arr = np.asarray(image).astype(np.float32) / 255.0
    brightness = float(np.clip(brightness, 0.75, 1.35))
    vibrance = float(np.clip(vibrance, 0.70, 1.55))
    contrast = float(np.clip(contrast, 0.80, 1.35))
    clarity = float(np.clip(clarity, 0.0, 0.75))

    lum = np.dot(arr, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    midtone = smoothstep_array(0.018, 0.32, lum) * (1.0 - smoothstep_array(0.78, 0.98, lum))
    shadow_guard = smoothstep_array(0.006, 0.045, lum)

    out = arr * (1.0 + (brightness - 1.0) * (0.28 + 0.72 * midtone)[:, :, None])
    lum_after = np.dot(out, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    gray = lum_after[:, :, None]
    saturation_amount = 1.0 + (vibrance - 1.0) * (0.42 + 0.58 * midtone)[:, :, None]
    out = gray + (out - gray) * saturation_amount

    pivot = 0.18
    out = pivot + (out - pivot) * (1.0 + (contrast - 1.0) * shadow_guard[:, :, None])

    if clarity > 0.0001:
        source = Image.fromarray((np.clip(out, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), mode="RGB")
        blur_radius = max(5, min(source.size) // 220)
        low = np.asarray(source.filter(ImageFilter.GaussianBlur(radius=blur_radius))).astype(np.float32) / 255.0
        detail = out - low
        clarity_mask = midtone * shadow_guard * (1.0 - smoothstep_array(0.70, 0.95, lum_after))
        out += detail * (clarity * 1.35) * clarity_mask[:, :, None]
        out = Image.fromarray((np.clip(out, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), mode="RGB")
        out = out.filter(ImageFilter.UnsharpMask(radius=0.48, percent=round(clarity * 85), threshold=5))
        return out

    return Image.fromarray((np.clip(out, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), mode="RGB")


def attenuate_baked_star_symmetry(arr: np.ndarray, mirror_boost: float, strength: float) -> np.ndarray:
    low_radius = max(6, min(arr.shape[0], arr.shape[1]) // 180)
    source = Image.fromarray((np.clip(arr, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), mode="RGB")
    low = np.asarray(source.filter(ImageFilter.GaussianBlur(radius=low_radius))).astype(np.float32) / 255.0
    lum = np.dot(arr, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    low_lum = np.dot(low, np.array([0.299, 0.587, 0.114], dtype=np.float32))
    sparkle = np.maximum(0.0, lum - low_lum)
    star_mask = smoothstep_array(0.035, 0.16, sparkle) * smoothstep_array(0.12, 0.42, lum)
    star_mask *= 1.0 - smoothstep_array(0.62, 0.92, low_lum)
    amount = np.clip((0.18 + mirror_boost * 0.46) * strength, 0.0, 0.54)
    return arr * (1.0 - star_mask[:, :, None] * amount) + low * (star_mask[:, :, None] * amount)


def signed_shift(size: int, ratio: float, seed: int) -> int:
    base = max(1, round(size * ratio))
    jitter = seed % max(1, round(size * 0.031))
    shift = (base + jitter) % size
    return -shift if seed & 1 else shift


def periodic_mask(height: int, width: int, seed: int) -> np.ndarray:
    x = np.linspace(0.0, 1.0, width, endpoint=False, dtype=np.float32)
    y = np.linspace(0.0, 1.0, height, endpoint=False, dtype=np.float32)
    phase_a = ((seed >> 3) % 997) / 997.0
    phase_b = ((seed >> 11) % 991) / 991.0
    phase_c = ((seed >> 19) % 983) / 983.0
    mask = (
        np.sin((x[None, :] * 1.0 + y[:, None] * 2.0 + phase_a) * np.pi * 2.0)
        + 0.62 * np.cos((x[None, :] * 3.0 - y[:, None] * 1.0 + phase_b) * np.pi * 2.0)
        + 0.38 * np.sin((x[None, :] * 2.0 + y[:, None] * 3.0 + phase_c) * np.pi * 2.0)
    )
    mask -= float(mask.min())
    peak = float(mask.max())
    if peak > 0.0001:
        mask /= peak
    return smoothstep_array(0.12, 0.88, mask.astype(np.float32))


def seed_color_bias(seed: int) -> np.ndarray:
    r = 0.70 + ((seed >> 2) & 0xFF) / 255.0 * 0.60
    g = 0.70 + ((seed >> 10) & 0xFF) / 255.0 * 0.60
    b = 0.70 + ((seed >> 18) & 0xFF) / 255.0 * 0.60
    return np.array([r, g, b], dtype=np.float32)


def measure_tile(image: Image.Image) -> dict[str, float]:
    image = image.convert("RGB")
    return {
        "mirrorHorizontal": round(mirror_correlation(image, axis="horizontal"), 5),
        "mirrorVertical": round(mirror_correlation(image, axis="vertical"), 5),
        "mirrorMax": round(max(mirror_correlation(image, axis="horizontal"), mirror_correlation(image, axis="vertical")), 5),
        "seamHorizontal": round(horizontal_seam_delta(image), 6),
        "seamVertical": round(vertical_seam_delta(image), 6),
        "seamMax": round(max(horizontal_seam_delta(image), vertical_seam_delta(image)), 6),
    }


def mirror_correlation(image: Image.Image, axis: str) -> float:
    sample = image.resize((512, 512), Image.Resampling.BILINEAR).filter(ImageFilter.GaussianBlur(radius=3.0))
    lum = np.asarray(sample.convert("L")).astype(np.float32) / 255.0
    mirrored = lum[:, ::-1] if axis == "horizontal" else lum[::-1, :]
    a = lum.reshape(-1) - float(lum.mean())
    b = mirrored.reshape(-1) - float(mirrored.mean())
    denom = float(np.linalg.norm(a) * np.linalg.norm(b))
    if denom <= 0.000001:
        return 0.0
    return float(np.dot(a, b) / denom)


def horizontal_seam_delta(image: Image.Image) -> float:
    arr = np.asarray(image.convert("RGB")).astype(np.float32) / 255.0
    return float(np.abs(arr[:, 0, :] - arr[:, -1, :]).mean())


def vertical_seam_delta(image: Image.Image) -> float:
    arr = np.asarray(image.convert("RGB")).astype(np.float32) / 255.0
    return float(np.abs(arr[0, :, :] - arr[-1, :, :]).mean())


def smoothstep_array(edge0: float, edge1: float, value: np.ndarray) -> np.ndarray:
    t = np.clip((value - edge0) / max(0.0001, edge1 - edge0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def read_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)
    if not isinstance(data, dict):
        raise ValueError(f"Expected object JSON in {path}")
    return data


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


if __name__ == "__main__":
    main()
