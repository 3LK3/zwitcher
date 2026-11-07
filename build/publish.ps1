# Publishes Zwitcher as installer and single-file executable.
# Used by CI and the release workflow, and works the same locally.
param(
    [string] $Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'src/Zwitcher.App/Zwitcher.App.csproj'
$artifacts = Join-Path $root 'artifacts'
$buildOutput = Join-Path $artifacts "publish/$Runtime"
$releaseOutput = Join-Path $artifacts "release/$Runtime"

# Directory.Build.props is the only place the version lives.
$version = (dotnet msbuild $project -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $version) { throw 'Could not read the version.' }

if (Test-Path $buildOutput) { Remove-Item $buildOutput -Recurse -Force }

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained `
    --output $buildOutput `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

dotnet vpk pack -u Zwitcher -v $version -o $releaseOutput -p $buildOutput -f net10-x64-desktop

Write-Host "Created installer"