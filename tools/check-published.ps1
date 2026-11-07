# Runs the published (trimmed, single-file) program against a copy of the 0.1.5 test database: tests/.../Background/PublishedProgramTests.cs.
# Usage: tools/check-published.ps1 [-Program <TMPublisher.exe>]; default: the win-x64 program tools/publish.ps1 leaves in
# artifacts/publish/win-x64/program. Windows only (the copied database's key is sealed with DPAPI). Sends nothing; the Task Scheduler task the
# run sets has a test name and is removed again.
param([string]$Program = 'artifacts/publish/win-x64/program/TMPublisher.exe')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not [IO.Path]::IsPathRooted($Program)) { $Program = Join-Path $root $Program }
if (-not (Test-Path $Program -PathType Leaf)) { throw "No published program at $Program; run tools/publish.ps1 first or pass -Program" }
$env:TMPUBLISHER_PUBLISHED_PROGRAM = (Resolve-Path $Program).Path
try {
    & dotnet test (Join-Path $root 'tests/TMPublisher.Tests/TMPublisher.Tests.csproj') -m:1 -nodeReuse:false `
        --filter 'FullyQualifiedName~TMPublisher.Tests.Background.PublishedProgramTests'
    if ($LASTEXITCODE -ne 0) { throw 'the published program check failed' }
}
finally {
    Remove-Item Env:TMPUBLISHER_PUBLISHED_PROGRAM -ErrorAction SilentlyContinue
}
