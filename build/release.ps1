# Prepares a release for a tag: checks that the tag matches the version, packs the Velopack
# release and writes the release notes from CHANGELOG.md. The workflow then uploads both.
#   ./build/release.ps1 -Tag v1.0.0
param(
    [Parameter(Mandatory)] [string] $Tag
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'src/Zwitcher.App/Zwitcher.App.csproj'
$artifacts = Join-Path $root 'artifacts'

$version = (dotnet msbuild $project -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $version) { throw 'Could not read the version.' }
if ($Tag -ne "v$version") {
    throw "Tag '$Tag' doesn't match the version in Directory.Build.props ('v$version')."
}

# The section from "## [<version>]" up to the next "## [" heading.
$changelog = Get-Content (Join-Path $root 'CHANGELOG.md')
$start = [Array]::FindIndex($changelog, [Predicate[string]] { param($l) $l -match "^## \[$([regex]::Escape($version))\]" })
if ($start -lt 0) { throw "CHANGELOG.md has no section for $version." }
$end = [Array]::FindIndex($changelog, $start + 1, [Predicate[string]] { param($l) $l -match '^## \[' })
if ($end -lt 0) { $end = $changelog.Length }
$notes = ($changelog[($start + 1)..($end - 1)] -join "`n").Trim()
if (-not $notes) { throw "The CHANGELOG.md section for $version is empty." }

New-Item -ItemType Directory -Force $artifacts | Out-Null
$notesFile = Join-Path $artifacts 'release-notes.md'
Set-Content -Path $notesFile -Value $notes -Encoding utf8NoBOM

$releaseDir = Join-Path $artifacts 'release/win-x64'
if (Test-Path $releaseDir) { Remove-Item $releaseDir -Recurse -Force }

# Fetch the previous release so vpk pack can build a delta package. The first release has
# nothing to download, and a missing delta only means users get the full package.
if ($env:GH_TOKEN) {
    dotnet vpk download github --repoUrl https://github.com/3LK3/zwitcher --token $env:GH_TOKEN -o $releaseDir
    if ($LASTEXITCODE -ne 0) { Write-Warning 'No previous release to build a delta from.' }
}

& (Join-Path $PSScriptRoot 'publish.ps1') -Runtime win-x64
if ($LASTEXITCODE -ne 0) { throw 'publish.ps1 failed.' }

Write-Host "Release $Tag ready: $releaseDir, notes in $notesFile"
