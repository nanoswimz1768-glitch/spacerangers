# Space Rangers Prototype

Windows-first 2D real-time space prototype inspired by the feel of classic Space Rangers, built on Godot 4 .NET and C#.

## Run

```powershell
.\tools\run_game.ps1
```

Controls:

- `W` forward thrust
- `S` slow reverse thrust
- `A/D` turn
- `Q/E` strafe
- Mouse aim
- `R` toggle Navigation/Combat mode
- Left mouse button fire in Combat mode
- `Left Shift` afterburner in Navigation mode
- `Tab` switch player ship sprite
- `F3` toggle ship hitbox debug overlay

## Prepare Ship Assets

```powershell
python .\tools\build_approved_ship_assets.py
```

This builds the approved top-down ship catalog from the review sheets and writes Godot-ready sprites into `game/assets/ships`.

## Regenerate Space Texture

```powershell
python .\tools\generate_space_textures.py
```

## Test Core Simulation

```powershell
.\tools\test_core.ps1
```

## Export Windows EXE

```powershell
.\tools\export_windows.ps1
```

The exported build is written to `build/windows/SpaceRangersPrototype.exe`.

Godot .NET currently receives its SDK from `C:\CodexTools\dotnet-8.0.420` during export/run. The project also keeps a local SDK under `.tools\dotnet`, but the ASCII path avoids Godot/MSBuild path issues when the workspace lives under Cyrillic directories.
