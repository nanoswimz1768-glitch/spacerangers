$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$root = Split-Path -Parent $PSScriptRoot
$downloads = Join-Path $root ".tools\downloads"
$godotDir = Join-Path $root ".tools\godot"
$godotZip = Join-Path $downloads "Godot_v4.6.2-stable_mono_win64.zip"
$templatesZip = Join-Path $downloads "Godot_v4.6.2-stable_mono_export_templates.tpz"
$dotnetInstall = Join-Path $downloads "dotnet-install.ps1"
$dotnetDir = Join-Path $root ".tools\dotnet"
$godotDotnetDir = "C:\CodexTools\dotnet-8.0.420"
$dotnet = Join-Path $dotnetDir "dotnet.exe"
$godotDotnet = Join-Path $godotDotnetDir "dotnet.exe"
$godot = Join-Path $godotDir "Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64_console.exe"
$game = Join-Path $root "game"
$buildDir = Join-Path $root "build\windows"
$exportExe = Join-Path $buildDir "SpaceRangersPrototype.exe"

New-Item -ItemType Directory -Force -Path $downloads, $godotDir, $dotnetDir, $buildDir | Out-Null

if (!(Test-Path $godotZip)) {
    Invoke-WebRequest -Uri "https://github.com/godotengine/godot/releases/download/4.6.2-stable/Godot_v4.6.2-stable_mono_win64.zip" -OutFile $godotZip
}

if (!(Test-Path $templatesZip)) {
    Invoke-WebRequest -Uri "https://github.com/godotengine/godot/releases/download/4.6.2-stable/Godot_v4.6.2-stable_mono_export_templates.tpz" -OutFile $templatesZip
}

if (!(Test-Path $dotnetInstall)) {
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $dotnetInstall
}

if (!(Test-Path $dotnet)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $dotnetInstall -Channel 8.0 -Quality GA -InstallDir $dotnetDir -Architecture x64 -NoPath
}

if (!(Test-Path $godotDotnet)) {
    New-Item -ItemType Directory -Force -Path $godotDotnetDir | Out-Null
    & powershell -NoProfile -ExecutionPolicy Bypass -File $dotnetInstall -Channel 8.0 -Quality GA -InstallDir $godotDotnetDir -Architecture x64 -NoPath
}

if (!(Test-Path $godot)) {
    Expand-Archive -LiteralPath $godotZip -DestinationPath $godotDir -Force
}

$templatesVersion = "4.6.2.stable.mono"
$templatesDir = Join-Path $env:APPDATA "Godot\export_templates\$templatesVersion"
$windowsTemplate = Join-Path $templatesDir "windows_release_x86_64.exe"
if (!(Test-Path $windowsTemplate)) {
    $tempTemplates = Join-Path $root ".tools\template_extract"
    if (Test-Path $tempTemplates) {
        Remove-Item -LiteralPath $tempTemplates -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $tempTemplates, $templatesDir | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($templatesZip, $tempTemplates)
    Get-ChildItem -LiteralPath (Join-Path $tempTemplates "templates") | Copy-Item -Destination $templatesDir -Recurse -Force
    Remove-Item -LiteralPath $tempTemplates -Recurse -Force
}

$env:DOTNET_ROOT = $godotDotnetDir
$env:PATH = "$godotDotnetDir;$dotnetDir;$env:PATH"

& $dotnet run --project (Join-Path $root "tests\SpaceRangers.Core.Tests\SpaceRangers.Core.Tests.csproj") --configuration Release
& $dotnet build (Join-Path $game "SpaceRangersPrototype.csproj") --configuration Release
& $godot --headless --path $game --import
if ($LASTEXITCODE -ne 0) {
    throw "Godot import failed with exit code $LASTEXITCODE."
}

& $godot --headless --path $game --export-release "Windows Desktop" $exportExe
if ($LASTEXITCODE -ne 0 -or !(Test-Path $exportExe)) {
    throw "Godot Windows export failed."
}

Write-Host "Exported $exportExe"
