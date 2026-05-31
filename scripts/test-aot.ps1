#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Native-AOT-publishes the smoke test and runs the resulting native binary.

.DESCRIPTION
    Builds the solution (so the libraries' assemblies exist for the smoke test's
    Reference + AssemblySearchPaths resolution), then `dotnet publish` with Native
    AOT and runs the produced self-contained native executable. The smoke test
    exercises the JSON/date/string parsing and process-spawning paths of all three
    libraries under AOT; it prints "AOT smoke OK" and exits 0 on success.

    Native AOT publish requires the platform C toolchain:
      - Windows: Visual Studio C++ build tools (run from a Developer prompt, or
        ensure the MSVC linker + vswhere are on PATH).
      - Linux:   clang and zlib1g-dev.
      - macOS:   Xcode command line tools.
    If the local toolchain is unavailable, rely on the `aot-smoke` CI job instead.

    This script is an optional convenience helper. Delete it if not needed.

.PARAMETER Configuration
    MSBuild configuration. Debug or Release. Defaults to Release.

.PARAMETER Rid
    Runtime identifier to publish for. Defaults to the host RID.

.EXAMPLE
    pwsh ./scripts/test-aot.ps1

.EXAMPLE
    pwsh ./scripts/test-aot.ps1 -Rid linux-x64
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Rid
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Project = Join-Path $RepoRoot 'tests/Vcs.Aot.SmokeTest/Vcs.Aot.SmokeTest.csproj'
$OutDir = Join-Path $RepoRoot 'aot-out'

Write-Host "==> Building solution ($Configuration)" -ForegroundColor DarkGray
& dotnet build (Join-Path $RepoRoot 'Vcs.slnx') -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$publishArgs = @('publish', $Project, '-c', $Configuration, '-o', $OutDir)
if ($Rid) { $publishArgs += @('-r', $Rid) }

Write-Host "==> Native AOT publish" -ForegroundColor DarkGray
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = if ($IsWindows) { 'Vcs.Aot.SmokeTest.exe' } else { 'Vcs.Aot.SmokeTest' }
$binary = Join-Path $OutDir $exe

Write-Host "==> Running native binary: $binary" -ForegroundColor DarkGray
& $binary
exit $LASTEXITCODE
