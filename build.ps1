Set-StrictMode -Version Latest

$scriptDir = $PSScriptRoot

$ErrorActionPreference = "Stop"

. "$scriptDir\common.ps1"

$sln = Join-Path $scriptDir "NuGetUtils.sln"

$outdir = Join-Path $scriptDir "bin"
if (Test-Path $outdir) { Remove-Item -Recurse $outdir  }

build $sln

Write-Host "All Done"
