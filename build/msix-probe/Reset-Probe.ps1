<#
DD309: put the machine back to where Measure-Probe.ps1 can start, between the two modes or at the end.

Removes, after asking: the probe package, the 'freeharbor' WSL distribution and its disk, the
%LOCALAPPDATA%\FreeHarbor folder, the package's leftover data folder, and the Run values that name
FreeHarbor or fh309. The distribution is the probe's only if the installed FreeHarbor was backed up
and removed before the first run, which is what Measure-Probe.ps1 insists on.

  powershell -File build\msix-probe\Reset-Probe.ps1
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param()

. (Join-Path $PSScriptRoot 'Common.ps1')

# The Inno installer installs into this same folder, and its distribution has this same name. Where
# it is still installed, both are the user's engine and not the probe's leftovers.
if ((Test-Path -LiteralPath (Join-Path $RealRoot 'unins000.exe')) -or
    (Test-Path -LiteralPath (Join-Path $RealRoot 'FreeHarbor.exe'))) {
    Write-Host "refused: $RealRoot holds an installed FreeHarbor. Back up its engine " -ForegroundColor Red
    Write-Host "(wsl --export $Distribution <file>.tar) and uninstall it first." -ForegroundColor Red
    exit 2
}

$family = Get-ProbeFamilyName
$package = Get-ProbePackage
if ($package -and $PSCmdlet.ShouldProcess($package.PackageFullName, 'Remove-AppxPackage')) {
    $package | Remove-AppxPackage
}

$lxss = Get-LxssEntry
if ($lxss -and $PSCmdlet.ShouldProcess("$Distribution at $($lxss.BasePath)", 'wsl --unregister (deletes its disk)')) {
    & wsl.exe --terminate $Distribution | Out-Null
    & wsl.exe --unregister $Distribution
}

foreach ($folder in @($RealRoot, $(if ($family) { Join-Path $env:LOCALAPPDATA "Packages\$family" }))) {
    if ($folder -and (Test-Path -LiteralPath $folder) -and $PSCmdlet.ShouldProcess($folder, 'Remove-Item -Recurse')) {
        Remove-Item -Recurse -Force -LiteralPath $folder
    }
}

$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$values = Get-ItemProperty $run -ErrorAction SilentlyContinue
if ($values) {
    foreach ($property in $values.PSObject.Properties | Where-Object {
            $_.Name -notlike 'PS*' -and ("$($_.Name) $($_.Value)" -match 'FreeHarbor|fh309') }) {
        if ($PSCmdlet.ShouldProcess("$run\$($property.Name)", 'Remove-ItemProperty')) {
            Remove-ItemProperty -Path $run -Name $property.Name
        }
    }
}

Get-Snapshot 'after reset' | Format-List
