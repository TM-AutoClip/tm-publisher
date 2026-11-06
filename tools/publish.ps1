# Publishes and packs the release files on Windows. Usage: tools/publish.ps1 [-Rids win-x64,linux-x64,linux-arm64]
#   [-Out <folder>] [-Work <folder>]: the packages and SHA256SUMS.txt go to -Out (default artifacts/release), the published program to
#   -Work (default artifacts/publish); both relative to the repository root unless absolute.
#
# Only from a clean working tree: the program carries the commit it was built from (in its version), so a release file is always that
# commit. One program file per platform: self-contained, single file, trimmed, the native libraries (SQLite3 Multiple Ciphers, Skia,
# HarfBuzz) inside it. No native compiler is needed, so every target can be built here.
param([string[]]$Rids = @('win-x64'), [string]$Out = 'artifacts/release', [string]$Work = 'artifacts/publish')
$ErrorActionPreference = 'Stop'
# 'pwsh -File' passes -Rids a,b,c as one string: split it, so both ways of calling work.
$Rids = @($Rids | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$root = Split-Path $PSScriptRoot -Parent
if (-not [IO.Path]::IsPathRooted($Out)) { $Out = Join-Path $root $Out }
if (-not [IO.Path]::IsPathRooted($Work)) { $Work = Join-Path $root $Work }
$dirty = & git -C $root status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'git status failed' }
if ($dirty) { throw "The working tree is not clean; commit first (release files must match a commit):`n$($dirty -join "`n")" }
$commit = (& git -C $root rev-parse --short HEAD).Trim()
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
Write-Host "TM Publisher $version from commit $commit"

foreach ($rid in $Rids) {
    $program = Join-Path $Work "$rid/program"
    if (Test-Path $program) { Remove-Item -Recurse -Force $program }
    & dotnet publish (Join-Path $root 'src/TMPublisher.Desktop/TMPublisher.Desktop.csproj') -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false `
        -m:1 -nodeReuse:false -o $program
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid" }
    & python (Join-Path $PSScriptRoot 'package.py') $program $rid $version $Out
    if ($LASTEXITCODE -ne 0) { throw "packing failed for $rid" }
}
