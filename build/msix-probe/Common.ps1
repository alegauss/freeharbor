<#
DD309: names and readings shared by the probe scripts. Dot-source it; it does nothing on its own.

What the probe answers: whether `wsl --import`, run by FreeHarbor from inside an MSIX package,
registers a disk that both the app and WslService see. WslService runs outside the package and sees
the real filesystem; the app may see a virtualized one. Every reading below is taken from OUTSIDE the
package (this script's own view, which is the real one) unless its name says Inside.
#>

$ErrorActionPreference = 'Stop'

# The certificate subject and the manifest Publisher must be the same string, byte for byte.
$Subject = 'CN=FreeHarbor DD309 Probe'
$PackageName = 'FreeHarbor.Dd309Probe'
$AppId = 'FreeHarbor'
$Alias = 'fh309'
$Distribution = 'freeharbor'

$ProbeDir = $PSScriptRoot
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$OutDir = Join-Path $ProbeDir 'out'
$RealRoot = Join-Path $env:LOCALAPPDATA 'FreeHarbor'
$AliasPath = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\$Alias.exe"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Get-ProbePackage {
    Get-AppxPackage -Name $PackageName -ErrorAction SilentlyContinue
}

# The package family name, from the installed package or from what a previous install left behind.
function Get-ProbeFamilyName {
    $package = Get-ProbePackage
    if ($package) { return $package.PackageFamilyName }
    $left = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Packages') -Directory -Filter "$PackageName*" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($left) { return $left.Name }
    return $null
}

# WSL's own record of the distribution, which is what WslService will open.
function Get-LxssEntry {
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss'
    if (-not (Test-Path $key)) { return $null }
    Get-ChildItem $key | ForEach-Object { Get-ItemProperty $_.PSPath } |
        Where-Object { $_.DistributionName -eq $Distribution } | Select-Object -First 1
}

function Get-RunValues {
    $run = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue
    if (-not $run) { return @() }
    $run.PSObject.Properties |
        Where-Object { $_.Name -notlike 'PS*' -and ("$($_.Name) $($_.Value)" -match 'FreeHarbor|fh309') } |
        ForEach-Object { "$($_.Name) = $($_.Value)" }
}

function Get-FileSize([string]$Path) {
    if (Test-Path -LiteralPath $Path) { '{0:N0} MB' -f ((Get-Item -LiteralPath $Path).Length / 1MB) } else { $null }
}

# Everything the table needs, as seen from outside the package.
function Get-Snapshot([string]$Label) {
    $family = Get-ProbeFamilyName
    $packageRoot = if ($family) { Join-Path $env:LOCALAPPDATA "Packages\$family" } else { $null }
    $cacheRoot = if ($packageRoot) { Join-Path $packageRoot 'LocalCache\Local\FreeHarbor' } else { $null }
    $lxss = Get-LxssEntry
    $basePath = if ($lxss) { $lxss.BasePath -replace '^\\\\\?\\', '' } else { $null }

    [pscustomobject][ordered]@{
        Step                 = $Label
        PackageInstalled     = [bool](Get-ProbePackage)
        LxssBasePath         = $basePath
        VhdxAtBasePath       = if ($basePath) { Get-FileSize (Join-Path $basePath 'ext4.vhdx') } else { $null }
        RealRootExists       = Test-Path -LiteralPath $RealRoot
        VhdxUnderRealRoot    = Get-FileSize (Join-Path $RealRoot 'distro\ext4.vhdx')
        CacheRoot            = $cacheRoot
        CacheRootExists      = if ($cacheRoot) { Test-Path -LiteralPath $cacheRoot } else { $false }
        VhdxUnderCacheRoot   = if ($cacheRoot) { Get-FileSize (Join-Path $cacheRoot 'distro\ext4.vhdx') } else { $null }
        PrivateHiveExists    = if ($packageRoot) { Test-Path -LiteralPath (Join-Path $packageRoot 'SystemAppData\Helium\User.dat') } else { $false }
        RunValues            = (Get-RunValues) -join '; '
        DockerEnginePipe     = Test-Path '\\.\pipe\docker_engine'
    }
}

# Run FreeHarbor inside the package, through the execution alias, and keep what it said.
function Invoke-Packaged {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string[]]$Arguments,
        [TimeSpan]$Budget = [TimeSpan]::FromMinutes(5),
        [switch]$NoWait
    )
    $stdout = Join-Path $OutDir "$Name.out.txt"
    $stderr = Join-Path $OutDir "$Name.err.txt"
    Write-Host "> $Alias $($Arguments -join ' ')"
    $process = Start-Process -FilePath $AliasPath -ArgumentList $Arguments -NoNewWindow -PassThru `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    # Windows PowerShell keeps ExitCode only if the handle was opened while the process was alive.
    $null = $process.Handle
    if ($NoWait) { return [pscustomobject]@{ Name = $Name; Exit = 'running'; Pid = $process.Id } }

    if (-not $process.WaitForExit([int]$Budget.TotalMilliseconds)) {
        try { $process.Kill() } catch { }
        return [pscustomobject]@{ Name = $Name; Exit = "killed after $([int]$Budget.TotalMinutes) min"; Pid = $process.Id }
    }
    [pscustomobject]@{ Name = $Name; Exit = $process.ExitCode; Pid = $process.Id }
}

# The package's own view: a command run with the package's identity and virtualization, writing
# into OutDir, which is outside AppData and therefore never redirected.
function Get-InsideView([string]$Label) {
    $family = Get-ProbeFamilyName
    if (-not $family -or -not (Get-ProbePackage)) { return }
    $file = Join-Path $OutDir "inside-$Label.txt"
    Remove-Item -LiteralPath $file -ErrorAction SilentlyContinue
    $script = "(echo === Lxss & reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Lxss /s " +
        "& echo === Run & reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run " +
        "& echo === %LOCALAPPDATA%\FreeHarbor & dir /s /b `"%LOCALAPPDATA%\FreeHarbor`") > `"$file`" 2>&1"
    try {
        Invoke-CommandInDesktopPackage -PackageFamilyName $family -AppId $AppId -Command 'cmd.exe' -Args "/c $script"
    }
    catch {
        Set-Content -LiteralPath $file -Value "Invoke-CommandInDesktopPackage failed: $($_.Exception.Message)"
        return
    }
    # It returns before the command has run, so wait for the file to settle.
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $file)) { Start-Sleep -Milliseconds 500 }
    Start-Sleep -Seconds 2
}
