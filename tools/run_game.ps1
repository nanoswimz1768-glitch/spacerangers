$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$godot = Join-Path $root ".tools\godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe"
$godotConsole = Join-Path $root ".tools\godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64_console.exe"
$dotnet = Join-Path $root ".tools\dotnet\dotnet.exe"
$godotDotnet = "C:\CodexTools\dotnet-8.0.420\dotnet.exe"

if (!(Test-Path $godot)) {
    throw "Godot was not found at $godot. Run tools\export_windows.ps1 to prepare portable tools."
}

$godotImport = $godot
if (Test-Path $godotConsole) {
    $godotImport = $godotConsole
}

if ((Test-Path $godotDotnet)) {
    $dotnet = $godotDotnet
}

if (!(Test-Path $dotnet)) {
    throw "Local .NET SDK was not found at $dotnet. Run tools\export_windows.ps1 to prepare portable tools."
}

$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"

$project = Join-Path $root "game"

function Test-ShipTextureImportStale {
    param([string]$ProjectRoot)

    function Get-FileMd5 {
        param([string]$Path)

        $md5 = [System.Security.Cryptography.MD5]::Create()
        $stream = [System.IO.File]::OpenRead((Resolve-Path $Path))
        try {
            return ([BitConverter]::ToString($md5.ComputeHash($stream))).Replace("-", "").ToLowerInvariant()
        }
        finally {
            $stream.Dispose()
            $md5.Dispose()
        }
    }

    function Get-ImportedSourceMd5 {
        param([string]$Path)

        foreach ($line in Get-Content -Path $Path -ErrorAction SilentlyContinue) {
            if ($line -match 'source_md5="([^"]+)"') {
                return $Matches[1].ToLowerInvariant()
            }
        }

        return ""
    }

    $shipDir = Join-Path $ProjectRoot "assets\ships"
    $importDir = Join-Path $ProjectRoot ".godot\imported"
    if (!(Test-Path $shipDir)) {
        return $false
    }

    if (!(Test-Path $importDir)) {
        return $true
    }

    foreach ($source in Get-ChildItem -Path $shipDir -Filter "*.png" -File) {
        $ctexImports = @(Get-ChildItem -Path $importDir -Filter "$($source.Name)-*.ctex" -File -ErrorAction SilentlyContinue)
        $md5Imports = @(Get-ChildItem -Path $importDir -Filter "$($source.Name)-*.md5" -File -ErrorAction SilentlyContinue)
        if ($ctexImports.Count -eq 0 -or $md5Imports.Count -eq 0) {
            return $true
        }

        $sourceMd5 = Get-FileMd5 -Path $source.FullName
        $importedMd5 = ""
        foreach ($md5Import in $md5Imports) {
            $candidateMd5 = Get-ImportedSourceMd5 -Path $md5Import.FullName
            if ($candidateMd5 -eq $sourceMd5) {
                $importedMd5 = $candidateMd5
                break
            }
        }

        if ($importedMd5 -ne $sourceMd5) {
            return $true
        }
    }

    return $false
}

function Test-DotnetBuildStale {
    param([string]$Root, [string]$ProjectRoot)

    $outputDll = Join-Path $ProjectRoot ".godot\mono\temp\bin\Debug\SpaceManagersPrototype.dll"
    if (!(Test-Path $outputDll)) {
        return $true
    }

    $outputTime = (Get-Item $outputDll).LastWriteTimeUtc
    $sourceRoots = @(
        (Join-Path $ProjectRoot "scripts"),
        (Join-Path $Root "src")
    )

    foreach ($sourceRoot in $sourceRoots) {
        if (!(Test-Path $sourceRoot)) {
            continue
        }

        foreach ($source in Get-ChildItem -Path $sourceRoot -Filter "*.cs" -File -Recurse) {
            if ($source.LastWriteTimeUtc -gt $outputTime) {
                return $true
            }
        }
    }

    foreach ($projectFile in @(
        (Join-Path $ProjectRoot "SpaceManagersPrototype.csproj"),
        (Join-Path $ProjectRoot "SpaceManagersPrototype.sln"),
        (Join-Path $Root "Directory.Build.props"),
        (Join-Path $Root "global.json")
    )) {
        if ((Test-Path $projectFile) -and (Get-Item $projectFile).LastWriteTimeUtc -gt $outputTime) {
            return $true
        }
    }

    return $false
}

if (Test-DotnetBuildStale -Root $root -ProjectRoot $project) {
    Write-Host "C# build output is stale; building Space Managers prototype..."
    & $dotnet build (Join-Path $project "SpaceManagersPrototype.sln")
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }
}

if (Test-ShipTextureImportStale -ProjectRoot $project) {
    Write-Host "Godot ship texture imports are stale; reimporting project resources..."
    & $godotImport --headless --import --path $project
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "Godot import failed with exit code $LASTEXITCODE."
    }
}

$gameArgs = @($args)
if ($gameArgs.Count -gt 0 -and $gameArgs[0] -eq "--") {
    $gameArgs = @($gameArgs | Select-Object -Skip 1)
}

if ($gameArgs.Count -gt 0) {
    & $godot --path $project -- @gameArgs
}
else {
    & $godot --path $project
}
