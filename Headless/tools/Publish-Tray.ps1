<#
.SYNOPSIS
  Publish the standalone tray app (MPBackend.Tray) as a self-contained Windows build and zip it.

.DESCRIPTION
  Output: Headless/publish/MPBackend.Tray/ (single-file exe plus wwwroot) and
  Headless/publish/MPBackend.Tray-<runtime>.zip. No .NET runtime is needed on the target machine.

.EXAMPLE
  ./Publish-Tray.ps1                 # win-x64, Release, single file
  ./Publish-Tray.ps1 -NoSingleFile   # folder publish (if the single-file bundle gives trouble)
#>
param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [switch]$NoSingleFile
)

$ErrorActionPreference = 'Stop'
$headless = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $headless 'MPBackend.Tray'
$out = Join-Path $headless 'publish\MPBackend.Tray'
$zip = Join-Path $headless "publish\MPBackend.Tray-$Runtime.zip"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }

$dotnetArgs = @('publish', $project, '-c', $Configuration, '-r', $Runtime, '--self-contained', 'true', '-o', $out)
if ($NoSingleFile) { $dotnetArgs += '-p:PublishSingleFile=false' }
& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
Write-Host "published to $out"
Write-Host "zip: $zip"
