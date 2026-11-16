<#
DD309: install the probe package, drive FreeHarbor through it, record where everything landed, remove
the package, and record what was left. Writes out\dd309-<Mode>.md, the table the design asks for.

Refuses to start on a machine that already has a FreeHarbor root or a freeharbor distribution: an
existing %LOCALAPPDATA%\FreeHarbor folder is written to directly even by a virtualized package, which
would make the Virtualized run measure nothing. Back up and remove the installed FreeHarbor first,
and run Reset-Probe.ps1 between the two modes.

  powershell -File build\msix-probe\Measure-Probe.ps1 -Mode Virtualized [-WithTray]

-WithTray pauses once the engine is serving, so the tray can be opened from Start and its Engine
page checked by hand; press Enter to carry on.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Virtualized', 'Unvirtualized')][string]$Mode,
    [switch]$WithTray
)

. (Join-Path $PSScriptRoot 'Common.ps1')

# ---- refuse a machine that would spoil the measurement ------------------------------------------
$problems = @()
if (Get-ProbePackage) { $problems += 'the probe package is still installed (Reset-Probe.ps1)' }
if (Get-LxssEntry) { $problems += "a '$Distribution' distribution is registered (back it up, then wsl --unregister $Distribution)" }
if (Test-Path -LiteralPath $RealRoot) { $problems += "$RealRoot exists (uninstall FreeHarbor, or Reset-Probe.ps1)" }
if (Get-Process -Name FreeHarbor -ErrorAction SilentlyContinue) { $problems += 'a FreeHarbor process is running (quit the tray)' }
if ($problems) {
    $problems | ForEach-Object { Write-Host "refused: $_" -ForegroundColor Red }
    exit 2
}

$msix = Join-Path $OutDir "FreeHarbor-DD309-$Mode.msix"
if (-not (Test-Path $msix)) { throw "$msix is missing: run New-ProbePackage.ps1 -Mode $Mode first" }

$snapshots = New-Object System.Collections.Generic.List[object]
$verbs = New-Object System.Collections.Generic.List[object]
function Note([string]$Label) { $snapshots.Add((Get-Snapshot $Label)); Write-Host "-- $Label" }

Note 'before'

# ---- install -------------------------------------------------------------------------------------
Add-AppxPackage -Path $msix
Note 'installed'

# ---- provision, through the package --------------------------------------------------------------
$verbs.Add((Invoke-Packaged -Name "$Mode-provision" -Arguments '--provision' -Budget ([TimeSpan]::FromMinutes(20))))
Note 'provisioned'
Get-InsideView "$Mode-provisioned"

$verbs.Add((Invoke-Packaged -Name "$Mode-autostart" -Arguments @('--autostart', 'on')))
Note 'autostart on'

# ---- serve, and ask the engine from outside -------------------------------------------------------
$host_ = Invoke-Packaged -Name "$Mode-run" -Arguments '--run' -NoWait
$deadline = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $deadline -and -not (Test-Path '\\.\pipe\docker_engine')) { Start-Sleep -Seconds 2 }
Note 'serving'

$family = Get-ProbeFamilyName
$cli = @((Join-Path $RealRoot 'cli\docker.exe'),
    (Join-Path $env:LOCALAPPDATA "Packages\$family\LocalCache\Local\FreeHarbor\cli\docker.exe")) |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($cli) {
    $out = Join-Path $OutDir "$Mode-docker-version.out.txt"
    $env:DOCKER_CONFIG = Split-Path -Parent (Split-Path -Parent $cli)
    $docker = Start-Process -FilePath $cli -NoNewWindow -PassThru -RedirectStandardOutput $out `
        -RedirectStandardError (Join-Path $OutDir "$Mode-docker-version.err.txt") `
        -ArgumentList @('-H', 'npipe:////./pipe/docker_engine', 'version', '--format', '{{.Server.Version}}')
    # As in Invoke-Packaged: without an open handle, Windows PowerShell reports no ExitCode.
    $null = $docker.Handle
    $exit = if ($docker.WaitForExit(60000)) { $docker.ExitCode } else { $docker.Kill(); 'killed after 60s' }
    $verbs.Add([pscustomobject]@{ Name = "$Mode-docker-version ($cli)"; Exit = $exit; Pid = $docker.Id })
}
else {
    $verbs.Add([pscustomobject]@{ Name = "$Mode-docker-version"; Exit = 'no docker.exe in either root'; Pid = $null })
}

$verbs.Add((Invoke-Packaged -Name "$Mode-status" -Arguments '--status' -Budget ([TimeSpan]::FromMinutes(2))))

if ($WithTray) {
    Write-Host ''
    Write-Host "Open 'FreeHarbor DD309 probe ($Mode)' from Start, look at the Engine page, note what it says,"
    Write-Host 'then quit the tray from its menu.'
    Read-Host 'Press Enter to carry on' | Out-Null
}

# ---- stop, then the two verbs that have to find the disk ----------------------------------------
$verbs.Add((Invoke-Packaged -Name "$Mode-stop" -Arguments '--stop' -Budget ([TimeSpan]::FromMinutes(2))))
$running = Get-Process -Id $host_.Pid -ErrorAction SilentlyContinue
if ($running -and -not $running.WaitForExit(30000)) { $running.Kill() }
Note 'stopped'

$verbs.Add((Invoke-Packaged -Name "$Mode-fsck" -Arguments '--fsck' -Budget ([TimeSpan]::FromMinutes(10))))
$verbs.Add((Invoke-Packaged -Name "$Mode-compact" -Arguments '--compact' -Budget ([TimeSpan]::FromMinutes(10))))
Note 'checked and compacted'
Get-InsideView "$Mode-end"

# ---- remove, and see what stays ------------------------------------------------------------------
Get-ProbePackage | Remove-AppxPackage
Note 'package removed'

# ---- the table -----------------------------------------------------------------------------------
$fields = $snapshots[0].PSObject.Properties.Name | Where-Object { $_ -ne 'Step' }
$lines = @("# DD309, $Mode", '', "Measured $(Get-Date -Format 'yyyy-MM-dd HH:mm') on $([Environment]::OSVersion.VersionString).", '')
$lines += '| Reading | ' + (($snapshots | ForEach-Object { $_.Step }) -join ' | ') + ' |'
$lines += '|---|' + (($snapshots | ForEach-Object { '---' }) -join '|') + '|'
foreach ($field in $fields) {
    $cells = $snapshots | ForEach-Object { $value = $_.$field; if ($null -eq $value -or "$value" -eq '') { '-' } else { "$value" } }
    $lines += "| $field | " + ($cells -join ' | ') + ' |'
}
$lines += @('', '| Verb (run inside the package) | Exit |', '|---|---|')
$lines += $verbs | ForEach-Object { "| $($_.Name) | $($_.Exit) |" }
$lines += @('', "What each verb said is in out\$Mode-*.out.txt and .err.txt; the package's own view of HKCU",
    "and of %LOCALAPPDATA%\FreeHarbor is in out\inside-$Mode-*.txt.")
$report = Join-Path $OutDir "dd309-$Mode.md"
[IO.File]::WriteAllText($report, ($lines -join "`r`n") + "`r`n", (New-Object Text.UTF8Encoding($false)))

Write-Host ''
Write-Host "Wrote $report"
Write-Host 'Before the other mode: powershell -File build\msix-probe\Reset-Probe.ps1'
