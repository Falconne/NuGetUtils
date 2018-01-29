Set-StrictMode -Version Latest

$scriptDir = $PSScriptRoot

$ErrorActionPreference = "Stop"

. "$scriptDir\common.ps1"

$sln = Join-Path $scriptDir "NuGetUtils.sln"

Write-Host "Restoring nuget packages"
$nuget = Join-Path $scriptDir "nuget.exe"
& $nuget restore $sln
if ($LASTEXITCODE -ne 0)
{
    Stop-WithError "Error during nuget restore"
}


$outdir = Join-Path $scriptDir "bin"
if (Test-Path $outdir) { Remove-Item -Recurse $outdir  }

build $sln

Write-Host "All Done"
