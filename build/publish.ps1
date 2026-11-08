# Publishes Zwitcher as installer and single-file executable.
# Used by CI and the release workflow, and works the same locally.
#
# Signing: set SIGNING_PFX_PATH and SIGNING_PFX_PASSWORD to sign the package. Only signed
# builds get the uiAccess manifest, because Windows refuses to start an unsigned uiAccess exe.
# Without them the build is unsigned and keeps the elevated-window limitation.
param(
    [string] $Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'src/Zwitcher.App/Zwitcher.App.csproj'
$launcherProject = Join-Path $root 'src/Zwitcher.Launcher/Zwitcher.Launcher.csproj'
$artifacts = Join-Path $root 'artifacts'
$buildOutput = Join-Path $artifacts "publish/$Runtime"
$releaseOutput = Join-Path $artifacts "release"

# Directory.Build.props is the only place the version lives.
$version = (dotnet msbuild $project -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $version) { throw 'Could not read the version.' }

$pfx = $env:SIGNING_PFX_PATH
$sign = $pfx -and $env:SIGNING_PFX_PASSWORD
if ($sign -and -not (Test-Path $pfx)) { throw "Signing certificate not found: $pfx" }
if (-not $sign) { Write-Warning 'SIGNING_PFX_PATH/SIGNING_PFX_PASSWORD not set. Building unsigned, without uiAccess.' }

if (Test-Path $buildOutput) { Remove-Item $buildOutput -Recurse -Force }

dotnet publish $project `
    --configuration Release `
    --runtime $Runtime `
    --self-contained `
    --output $buildOutput `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:UiAccess=$($sign ? 'true' : 'false')
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# Zwitcher.exe is a small launcher (no WPF, so it can be trimmed) that Velopack starts instead
# of the uiAccess app. See src/Zwitcher.Launcher.
dotnet publish $launcherProject `
    --configuration Release `
    --runtime $Runtime `
    --self-contained `
    --output $buildOutput `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=true `
    -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish of the launcher failed.' }

$packArgs = @(
    'pack', '-u', 'Zwitcher', '-v', $version,
    '-o', $releaseOutput, '-p', $buildOutput, '--mainExe', 'Zwitcher.exe',
    '-f', 'net10-x64-desktop', '-r', $Runtime,
    '--msi', '--instLocation', 'PerMachine'
)
if ($sign) {
    # /tr timestamps the signature so it stays valid after the certificate expires.
    $packArgs += '--signParams', "/f `"$pfx`" /p `"$env:SIGNING_PFX_PASSWORD`" /fd sha256 /tr http://timestamp.digicert.com /td sha256"
}

dotnet vpk @packArgs
if ($LASTEXITCODE -ne 0) { throw 'dotnet vpk pack failed.' }

Write-Host "Created installer$($sign ? '' : ' (unsigned)')"
