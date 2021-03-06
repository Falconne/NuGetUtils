Set-StrictMode -Version Latest
$scriptDir = $PSScriptRoot

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
        [Parameter(Mandatory = $true, HelpMessage = "Build Output")] $output
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

function Save-FileFromNetwork
{
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $url,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $destination
    )

    [Net.ServicePointManager]::SecurityProtocol = "tls12, tls11, tls"
    $webclient = New-Object System.Net.WebClient
    # On some networks automatic proxy resolution causes several
    # seconds delay
    $webClient.Proxy = $null
    Write-Host "Downloading $url => $destination"
    $webClient.DownloadFile($url, $destination)
    Write-Host "Done"
}


# Fetch latest nuget.exe from the internet
function Get-NuGet
{
    $nuget = "$PSScriptRoot\nuget.exe"
    $retriesAllowed = 2
    while (!(Test-Path $nuget))
    {
        try
        {
            $url = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
            Write-Host "Fetching latest NuGet from $url into $nuget"
            Save-FileFromNetwork $url $nuget
            Write-Host "Done"
        }
        catch
        {
            if ($retriesAllowed -le 0)
            {
                throw
            }

            $retriesAllowed--
            Write-Host "Download failed. Will retry..."
            Start-Sleep -Seconds 10
        }
    }

    return $nuget
}


function build($sln)
{
    Write-Host "Restoring NuGet packages"
    & (Get-NuGet) restore $sln

    Write-Host "Building $sln"
    $vswhere = Join-Path $scriptDir "vswhere.exe"
    $installationPath = & $vswhere -latest -version 15 -requires Microsoft.Component.MSBuild -property installationPath
    if (!$installationPath)
    {
        Write-Error "VS 2017 with MSBuild not found"
        exit 1
    }

    $msbuild = "$installationPath\MSBuild\Current\Bin\MSBuild.exe"
    if (!(Test-Path $msbuild))
    {
        Write-Host "MSBuild not found at $msbuild."
        $msbuild = "$installationPath\MSBuild\15.0\Bin\MSBuild.exe"
        Write-Host "Will try $msbuild instead"
    }


    Write-Host "Running $msbuild"
    & $msbuild /m $sln /p:Configuration=Release /nologo | Tee -Variable output
    if ($LASTEXITCODE -ne 0)
    {
        checkForErrors $output
        Write-Error "Build Errors Detected"
        exit 1
    }
}

