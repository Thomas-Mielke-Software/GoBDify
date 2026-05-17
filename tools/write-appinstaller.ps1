param(
    [Parameter(Mandatory=$true)] [string]$Architecture,
    [Parameter(Mandatory=$true)] [string]$Version,
    [Parameter(Mandatory=$true)] [string]$IdentityName,
    [Parameter(Mandatory=$true)] [string]$Publisher,
    [Parameter(Mandatory=$true)] [string]$BaseUri,
    [Parameter(Mandatory=$true)] [string]$MsixFileName,
    [Parameter(Mandatory=$true)] [string]$OutPath
)

$selfUri = "$BaseUri/GoBDify_$Architecture.appinstaller"
$msixUri = "$BaseUri/$MsixFileName"

$xml = @"
<?xml version="1.0" encoding="utf-8"?>
<AppInstaller xmlns="http://schemas.microsoft.com/appx/appinstaller/2018" Uri="$selfUri" Version="$Version">
  <MainPackage Name="$IdentityName" Publisher="$Publisher" Version="$Version" ProcessorArchitecture="$Architecture" Uri="$msixUri" />
  <UpdateSettings>
    <OnLaunch HoursBetweenUpdateChecks="24" />
    <AutomaticBackgroundTask />
  </UpdateSettings>
</AppInstaller>
"@

# UTF-8 ohne BOM (Windows-AppInstaller akzeptiert BOM, aber sauberer ohne)
$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutPath, $xml, $enc)
Write-Host "  -> $OutPath"
