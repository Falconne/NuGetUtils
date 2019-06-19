Set-StrictMode -Version Latest
$scriptDir = $PSScriptRoot

if (!(Test-Path Env:\BUILD_VERSION))
{
    Write-Host "WARNING: BUILD_VERSION not set, assuming 1.0.0.1"
    $Env:BUILD_VERSION = "1.0.0.1"
}
$buildVersion = $Env:BUILD_VERSION

function _TeamCityFormatMessage
{
    param
    (
     [Parameter(Mandatory = $true)]
     [ValidateNotNullOrEmpty()]
     [string] $Message
    )

    $formatted = $Message
    $formatted = $formatted -replace "'", "|'"
    $formatted = $formatted -replace "\r", "|r"
    $formatted = $formatted -replace "\n", "|n"
    $formatted = $formatted -replace "[\[]", "|["
    $formatted = $formatted -replace "]", "|]"
    return $formatted
}

function Write-TeamCityMessage
{
    param
    (
     [Parameter(Mandatory = $true, Position = 0)]
     [ValidateNotNullOrEmpty()]
     [string] $Text,

     [Parameter(Mandatory = $true)]
     [ValidateSet("NORMAL","WARNING","FAILURE", "ERROR")]
     [string] $Status = "NORMAL",

     [string] $ErrorDetails
    )

    $formattedErrorDetails = ""
    $formattedText = _TeamCityFormatMessage $Text
    if ($ErrorDetails) { $formattedErrorDetails = _TeamCityFormatMessage $ErrorDetails }
    Write-Host "##teamcity[message text='$formattedText' errorDetails='$formattedErrorDetails' status='$Status']"
}

function checkForErrors
{
    param
    (
     [Parameter(Mandatory=$true, HelpMessage="Build Output")] $output
    )

    $errorPatterns = @(
            ": error",
            "Could not find file",
            " fatal error",
            "Cannot delete directory",
            "Error E",
            "could not be found with source path",
            "The process cannot access the file",
            "Error: ",
            "SOLUTION FAILED:",
            "Cannot delete directory",
            "Error \d+"
            )

    $foundErrors = $output | Select-String -pattern $errorPatterns |
    Out-String | % { $_.Split("`r`n") } | % { $_.Trim() } | Where { $_ }

    if ($foundErrors)
    {
        Write-Host "--------------------------------------------------------------------------------"
        Write-Host "START: Error Pattern Summary`r`n"

        $foundErrors | % { Write-TeamCityMessage -Text $_ -Status "ERROR" }

        Write-Host "`r`nEND: Error Pattern Summary"
        Write-Host "--------------------------------------------------------------------------------"
    }
}

function build($sln)
{
    Write-Host "Restoring NuGet packages"
    & dotnet restore $sln

    Write-Host "Building $sln"
    $vswhere = Join-Path $scriptDir "vswhere.exe"
    $installationPath = & $vswhere -version 15 -requires Microsoft.Component.MSBuild -property installationPath
    if (!$installationPath)
    {
        Write-Error "VS 2017 with MSBuild not found"
        exit 1
    }
    $msbuild = Join-Path $installationPath "MSBuild\15.0\Bin\MSBuild.exe"
    Write-Host "Running $msbuild"
    & $msbuild /m $sln /p:Configuration=Release /nologo | Tee -Variable output
    if ($LASTEXITCODE -ne 0)
    {
        checkForErrors $output
        Write-Error "Build Errors Detected"
        exit 1
    }
}

