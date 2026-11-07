# Publishes Zwitcher as one self-contained exe and zips it:
#   artifacts/Zwitcher-<version>-<runtime>.zip
# Used by CI and the release workflow, and works the same locally.
param(
    [string] $Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'src/Zwitcher.App/Zwitcher.App.csproj'
$artifacts = Join-Path $root 'artifacts'
$output = Join-Path $artifacts "publish/$Runtime"

# Directory.Build.props is the only place the version lives.
$version = (dotnet msbuild $project -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $version) { throw 'Could not read the version.' }

if (Test-Path $output) { Remove-Item $output -Recurse -Force }

# IncludeNativeLibrariesForSelfExtract: WPF's native DLLs go into the exe too, so the zip
# holds a single file. No trimming: WPF and WinForms don't support it. No compression: the
# zip compresses already, and decompressing would slow every start.
dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained `
    --output $output `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$zip = Join-Path $artifacts "Zwitcher-$version-$Runtime.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip

Write-Host "Created $zip"
$zip
