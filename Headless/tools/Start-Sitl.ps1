<#
.SYNOPSIS
  Run the ArduPilot SITL binary that Mission Planner's Simulation screen downloads, for backend development.

.DESCRIPTION
  Listens on tcp:5760 (+10 per instance). SITL exits when its TCP client disconnects, so -Restart relaunches it.

.EXAMPLE
  ./Start-Sitl.ps1 -Restart
  ./Start-Sitl.ps1 -Instance 1 -Location "-35.3632,149.1652,584,353"
#>
param(
    [string]$SitlDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mission Planner\sitl'),
    [string]$Model = '+',
    [string]$Exe = 'ArduCopter.exe',
    [string]$Defaults = 'default_params\copter.parm',
    [string]$Location = '-35.363261,149.165230,584,353',
    [int]$Instance = 0,
    [switch]$Wipe,
    [switch]$Restart
)

$exePath = Join-Path $SitlDir $Exe
if (-not (Test-Path $exePath)) {
    throw "SITL binary not found at $exePath. Start a simulation once from Mission Planner's Simulation screen to download it."
}

$work = Join-Path $SitlDir $Model
New-Item -ItemType Directory -Force $work | Out-Null
$env:PATH = "$SitlDir;$env:PATH"
$env:HOME = $work

$sitlArgs = @("-M$Model", "-O$Location", '-s1', '--serial0', 'tcp:0', '-I', "$Instance",
              '--defaults', (Join-Path '..' $Defaults))  # relative: Start-Process does not quote paths with spaces
if ($Wipe) { $sitlArgs += '--wipe' }

do {
    Write-Host "starting SITL on tcp:$(5760 + 10 * $Instance)"
    $p = Start-Process -FilePath $exePath -ArgumentList $sitlArgs -WorkingDirectory $work -NoNewWindow -PassThru
    $p.WaitForExit()
    Write-Host "SITL exited ($($p.ExitCode))"
} while ($Restart)
