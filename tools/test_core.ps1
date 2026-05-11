$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".tools\dotnet\dotnet.exe"
if (!(Test-Path $dotnet)) {
    throw "Local .NET SDK was not found at $dotnet. Run tools\export_windows.ps1 once to install tools."
}

$env:DOTNET_ROOT = Split-Path -Parent $dotnet
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& $dotnet run --project (Join-Path $root "tests\SpaceManagers.Core.Tests\SpaceManagers.Core.Tests.csproj") --configuration Release

