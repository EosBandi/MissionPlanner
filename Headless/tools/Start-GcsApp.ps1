<#
.SYNOPSIS
  Launch the GCS app (Windows build) against a SITL with the development aids enabled.

.EXAMPLE
  ./Start-GcsApp.ps1                       # connect to tcp:127.0.0.1:5760 and fly the dev mission
  ./Start-GcsApp.ps1 -GpsLoss              # also drop the simulated GPS for 45 s after take-off
  ./Start-GcsApp.ps1 -NoFlight             # connect only
#>
param(
    [string]$Connection = 'tcp:127.0.0.1:5760',
    [int]$TakeoffAltitude = 30,
    [switch]$GpsLoss,
    [switch]$NoFlight,
    [string]$MapCache = (Join-Path $PSScriptRoot '..\.devdata\gmapcache'),
    [string]$Exe = (Join-Path $PSScriptRoot '..\MPBackend.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\MPBackend.App.exe')
)

if (-not (Test-Path $Exe)) { throw "build first: dotnet build Headless/MPBackend.App -f net10.0-windows10.0.19041.0 ($Exe not found)" }
$env:MPGCS_AUTOCONNECT = $Connection
$env:MPGCS_DEV_FLY = if ($NoFlight) { '' } else { "$TakeoffAltitude" }
$env:MPGCS_DEV_GPSLOSS = if ($GpsLoss) { '1' } else { '' }
$env:MPGCS_MAPCACHE = if (Test-Path $MapCache) { (Resolve-Path $MapCache).Path } else { '' }
Start-Process -FilePath $Exe
