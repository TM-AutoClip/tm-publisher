# Publishes and packs the release files on Windows (artifacts/release). Usage: tools/publish.ps1 [-Rids win-x64,linux-x64]
#   [-Out <folder>] [-Work <folder>]: the packages and SHA256SUMS.txt go to -Out (default artifacts/release), the published programs
#   to -Work (default artifacts/publish); both relative to the repository root unless absolute.
#
# Only from a clean working tree: the programs carry the commit they were built from (in their version), so a release file is always
# that commit. The window program is trimmed single-file; the background scheduler is Native AOT for win-x64 (needs Visual Studio's C++
# tools), and the "jit" build (trimmed, ReadyToRun, single file) for a Linux target (Native AOT for Linux is built on Linux:
# tools/publish.sh).
param([string[]]$Rids = @('win-x64'), [string]$Out = 'artifacts/release', [string]$Work = 'artifacts/publish')
$ErrorActionPreference = 'Stop'
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

# Native AOT's linker search on Windows calls vswhere.exe, which is not on the PATH by default.
$installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if (Test-Path (Join-Path $installer 'vswhere.exe')) { $env:PATH = "$env:PATH;$installer" }

foreach ($rid in $Rids) {
    $window = Join-Path $Work "$rid/window"
    $scheduler = Join-Path $Work "$rid/scheduler"
    foreach ($folder in $window, $scheduler) { if (Test-Path $folder) { Remove-Item -Recurse -Force $folder } }
    & dotnet publish (Join-Path $root 'src/TMPublisher.Desktop/TMPublisher.Desktop.csproj') -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false `
        -m:1 -nodeReuse:false -o $window
    if ($LASTEXITCODE -ne 0) { throw "window publish failed for $rid" }
    # @(...) around the if: a one-element result would be a plain string, and splatting a string passes it letter by letter.
    $kind = @(if ($rid -notlike 'win-*') { '-p:SchedulerBuild=jit' })
    & dotnet publish (Join-Path $root 'src/TMPublisher.Background/TMPublisher.Background.csproj') -c Release -r $rid `
        -p:DebugType=none -p:DebugSymbols=false @kind -m:1 -nodeReuse:false -o $scheduler
    if ($LASTEXITCODE -ne 0) { throw "scheduler publish failed for $rid" }
    & python (Join-Path $PSScriptRoot 'package.py') $window $scheduler $rid $version $Out
    if ($LASTEXITCODE -ne 0) { throw "packing failed for $rid" }
}
