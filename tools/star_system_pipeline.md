# Generated Star-System Content

This folder is the offline content pipeline for star systems. The game loads only the
finished runtime JSON files from `game/assets/generated`; it should not roll random
systems at runtime.

Flow:

1. Curate or generate raster assets for stars, planet surface maps, and space background tiles.
2. Prefer direct high-resolution imagegen outputs over old multi-asset sheets:
   - `game/assets/generated/star_sources_4k/*.png`
   - `game/assets/generated/planet_sources_4k/*.png`
   - `game/assets/generated/background_sources_4k/*.png`
3. Process and validate direct high-res outputs with `tools/process_highres_imagegen_assets.py`.
4. Register or adjust those assets in `tools/star_system_catalog.json` with tags, ranges, weights, and prompts.
5. Run `tools/generate_star_systems.py`.
6. Review `galaxy.json` sectors and `systems/orion_*.json`.
7. Keep good systems, regenerate weak batches, or hand-edit specific JSON files.

Useful commands from the repository root:

```powershell
python tools/prepare_highres_generation_batch.py
python tools/process_highres_imagegen_assets.py --update-catalog --replace-lowres --replace-backgrounds
python tools/process_highres_imagegen_assets.py --only-backgrounds --background-mode direct-source --update-catalog --replace-backgrounds
python tools/generate_star_systems.py --seed 3311337 --clean --write-image-prompts
python tools/generate_star_systems.py --seed 3311340 --sectors 6 --clean
```

Outputs:

- `game/assets/generated/galaxy.json`: compact runtime sector index for F12 or a future galaxy map.
- `game/assets/generated/systems/orion_0001.json`: full runtime star, background, planet, orbit, and visual profile data.
- `game/assets/generated/star_sources_4k/*.png`: direct high-res imagegen star sources, used as source art/reference and processed into runtime star disks.
- `game/assets/generated/planet_sources_4k/*.png`: direct high-res imagegen equirectangular planet sources, ideally 2:1.
- `game/assets/generated/background_sources_4k/*.png`: direct high-res imagegen background sources for tile processing.
- `game/assets/generated/stars/*.png`: runtime star disk textures processed from direct high-res sources, or legacy sheet slices as fallback.
- `game/assets/generated/star_frames/<variant>/sun_00.png..sun_95.png`: runtime animated star frame sequences generated from direct high-res star sources. Generated systems can point `star.frameDirectory` here and still use `StabilizedSunView` plus `sun_stabilized.gdshader`.
- `game/assets/generated/star_frames_experimental/<variant>/sun_00.png..sun_95.png`: primary generated-star frame sequences that preserve more direct PNG character while keeping the same `StabilizedSunView`/shader path. Runtime prefers these automatically for generated stars when matching frames exist; use `--star-frames=stable` for fallback review.
- `game/assets/generated/planet_surfaces/*.png`: validated runtime planet surface maps, currently targeting 4096x2048 for direct high-res sources.
- `game/assets/generated/background_tiles/*.png`: validated 4096x4096 tileable space backdrops; current baseline preserves the accepted source art through the direct-source rectangular mosaic path, not Sol-style recoloring, square crop quilting, or fullscreen wallpaper stretching.
- `game/assets/generated/planets/*.png`: legacy 1024x512 sheet-sliced planet sources; do not use for new approved generated systems.
- `game/assets/generated/backgrounds/*.png`: legacy sliced imagegen backdrops; do not use directly as runtime space textures.
- `tools/generated/highres_generation_batch.jsonl`: exact-count high-res generation queue; current target is 14 star sources, 14 background sources, and 32 planet sources.
- `tools/generated/imagegen_prompts.jsonl`: prompt manifest for creating new high-res source art.
- `tools/generated/highres_asset_report.json`: validation report with dimensions, sharpness score, seam risk, and pass/fail status.
- `tools/generated/imagegen_asset_manifest.json`: legacy manifest of imported imagegen sheets and sliced assets.

Current accepted starter high-res pack for the two generated Orion systems:
2 star sources, 2 background sources, 7 planet sources; `tools/generated/highres_asset_report.json`
must show `failedCount=0` before regenerating systems from those assets.
The 2 accepted star sources also produce 96-frame runtime sequences:
`blue_white_star_01` and `orange_dwarf_01`.

Default generation creates the starting sector `Orion` with three star systems:
the hand-authored `Sol` preset plus two generated systems. `--count` controls only
the generated systems in that starting sector, and every sector is clamped to 2-5
systems so the map stays curated instead of becoming another giant dump.

The checked-in starmap test galaxy is generated with `--sectors 6 --clean`.
`galaxy.json` and each generated system JSON now carry `parsecX`/`parsecY`.
These coordinates are the source of truth for route distance: neighboring
systems inside a sector are laid out on a 10 parsec hex step, while systems in
different sectors are naturally farther apart. Sectors must still draw as a
single contiguous mosaic on the starmap; parsec spacing affects star placement
inside those mosaic cells and the route distance label, not a visual gap between
sector borders.

The starmap uses left click to select a destination and draw a thin cyan dashed
route with a parsec label. Pressing OK tunes the warp engine, closes the map,
and keeps the route locked in gold when the map is opened again. The right panel
Reset Drive button clears that tuned target; changing the current system also
clears it. Holding right mouse over a star opens the planet list popup.

Generated runtime systems use the same background contract as Sol through
`SpaceBackdropView`: a texture tile layer plus a procedural starfield layer.
Generated JSON pins the Sol baseline values (`TextureAlpha=1.0`,
`TextureParallax=0.08`, `StarParallax=0.32`) instead of rolling per-archetype
runtime parallax. The experimental generated nebula overlay is a separate layer
and is disabled in the baseline path. For new direct high-res imagegen
backgrounds, `tools/process_highres_imagegen_assets.py --background-mode direct-source`
preserves the source nebula/star art by quilting full rectangular source passes
at native pixel scale, not by stretching one image over the whole world and not
by cutting square fragments out of it. Only the final 4096px tile edges are
blended for repeat safety, and Sol is not used as a recolor/base texture.
`SpaceBackdropView` draws the primary high-res pass at exact tile size and uses
only very faint phase-offset secondary passes to soften visible repetition. The
direct-source processor keeps the unblurred rectangular source pass as the
dominant layer; any blurred broad pass is only a weak underlay. The backdrop
runtime layer uses linear filtering without mipmaps so high-res star dust and
nebula ridges do not smear during gameplay. The main texture pass uses the
source asset color directly (`Colors.White`
modulate, no runtime color grade or alpha dimming). Accepted PNGs keep their
original color/composition instead of being re-synthesized.
`ProceduralStarfieldLayer` still owns the runtime parallax
starfield, so the texture tile should add art direction and depth, not become a
fullscreen stretched wallpaper. `tools/create_background_tiles.py` remains the
legacy/fallback path for recolored 4096 px variants of `res://assets/backgrounds/space_nebula_tile.png`.

Prefer fixing mirror/kaleidoscope artifacts in `SpaceBackdropView` first. If an
already accepted tile itself must be edited, `tools/demirror_background_tiles.py`
backs up every touched source under `tools/generated/backups/<backup-id>/` and
can restore the exact previous files:

```powershell
python tools/demirror_background_tiles.py --target-set active --synthesize-asymmetric --strength 0.92 --backup-id background_tiles_YYYYMMDD_pre_asymmetric_synthesis
python tools/demirror_background_tiles.py --restore tools/generated/backups/background_tiles_YYYYMMDD_pre_asymmetric_synthesis
```

Generated planets should not use raw imagegen files directly at runtime.
For new content, `tools/process_highres_imagegen_assets.py` processes direct
high-res planet sources into 4096x2048 runtime maps, blends the horizontal seam,
sharpens them, validates blur/seam risk, and only then registers the assets in
the catalog. The older `tools/process_planet_surface_maps.py` remains a legacy
fallback for the old 1024x512 sheet-sliced planet sources.

Generated stars should not use direct high-res star PNGs as flat runtime sprites.
`tools/process_highres_imagegen_assets.py --update-catalog` registers the still
disk texture and also writes a 96-frame sequence under
`game/assets/generated/star_frames/<variant>`. `tools/generate_star_systems.py`
then emits `frameDirectory`, `framePrefix`, and `frameCount` into system JSON so
the game renders those stars through `StabilizedSunView` and the temporal
smoothing shader.

Experimental generated-star output is now the primary generated-star runtime path
and remains intentionally reversible. Run
`python tools/process_highres_imagegen_assets.py --experimental-star-frames-only`
to write `game/assets/generated/star_frames_experimental/<variant>` without
touching catalog JSON or stable frames. Matching experimental frames are used by
default for generated stars only; launch with `--star-frames=stable` or
`--stable-star-frames` to roll back instantly to the stable
`star_frames/<variant>` path.

Accepted generated-star solution, fixed on 2026-05-10:

- the direct high-res star PNG is source art/reference, not the primary animated
  runtime sprite;
- generated star frames use the Sol-like 96-frame plasma sequence as the
  animation carrier, then recolor and mix in the high-res source character and
  detail;
- the loop must be closed: `sun_95 -> sun_00` should have roughly the same
  pixel-diff magnitude as a normal adjacent step such as `sun_00 -> sun_01`;
- reject batches with visible restart, remap blur, flat/static PNG motion, or
  detached corona blobs that look like separate glowing spots;
- verify large stars in an actual Godot capture with `--stress-near star`, not
  only in an image browser.

Do not approve a new generated asset batch only because it looks good in the
image browser. Validate that the source and processed output do not become soft
or stretched in-game: check the report, then inspect a real Godot capture for at
least one large planet and one tile-heavy background.
