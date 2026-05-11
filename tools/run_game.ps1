$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Join-Path $root ".tools\godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe"
$dotnet = Join-Path $root ".tools\dotnet\dotnet.exe"
$godotDotnet = "C:\CodexTools\dotnet-8.0.420\dotnet.exe"

if (!(Test-Path $godot)) {
    throw "Godot was not found at $godot. Run tools\export_windows.ps1 to prepare portable tools."
}

if ((Test-Path $godotDotnet)) {
    $dotnet = $godotDotnet
}

if (!(Test-Path $dotnet)) {
    throw "Local .NET SDK was not found at $dotnet. Run tools\export_windows.ps1 to prepare portable tools."
}

$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"

$gameArgs = @($args)
if ($gameArgs.Count -gt 0 -and $gameArgs[0] -eq "--") {
    $gameArgs = @($gameArgs | Select-Object -Skip 1)
}

if ($gameArgs.Count -gt 0) {
    & $godot --path (Join-Path $root "game") -- @gameArgs
}
else {
    & $godot --path (Join-Path $root "game")
}
