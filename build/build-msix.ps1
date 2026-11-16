<#
build-msix.ps1 - packs the published FreeHarbor as an MSIX (DD310). Called by build-msix.cmd
after build.cmd has published both executables. Ported from claude-tray's, which passed Store
certification.

  1) version: <Version> from Directory.Build.props, as X.Y.Z.0 (the Store requires the fourth
     part to be 0)
  2) layout in dist\msix\layout: FreeHarbor.exe, bin\docker.exe (the forwarder the installer
     puts on PATH), agent\, the manifest with the version filled in, and the committed logos
  3) makepri, then makeappx pack -> dist\FreeHarbor.msix, unsigned: the Store signs it
  4) with -Sideload: the same package signed with a self-signed certificate whose subject is the
     manifest's Publisher, as dist\FreeHarbor-sideload.msix, for a test machine
#>
[CmdletBinding()]
param(
    [switch]$Sideload
)
$ErrorActionPreference = 'Stop'

# This script lives in build\; Directory.Build.props, src\ and dist\ are in the folder above.
$root = Split-Path -Parent $PSScriptRoot

# --- 1) Version -------------------------------------------------------------------------------
$props = [IO.File]::ReadAllText((Join-Path $root 'Directory.Build.props'))
if ($props -notmatch '<Version>\s*([0-9]+\.[0-9]+\.[0-9]+)\s*</Version>') {
    throw 'no <Version> in Directory.Build.props'
}
$version = "$($Matches[1]).0"

# --- 2) Layout --------------------------------------------------------------------------------
$exe = Join-Path $root 'src\FreeHarbor.Tray\bin\Release\net10.0-windows\win-x64\publish\FreeHarbor.exe'
$shim = Join-Path $root 'src\FreeHarbor.Shim\bin\Release\net10.0\win-x64\publish\docker.exe'
foreach ($binary in $exe, $shim) {
    if (-not (Test-Path $binary)) { throw "not published: $binary (run build.cmd)" }
}

$dist = Join-Path $root 'dist'
$layout = Join-Path $dist 'msix\layout'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $layout 'bin') | Out-Null

Copy-Item $exe $layout
Copy-Item $shim (Join-Path $layout 'bin')
Copy-Item (Join-Path $PSScriptRoot 'agent') $layout -Recurse
Copy-Item (Join-Path $PSScriptRoot 'msix\Assets') $layout -Recurse

$manifestText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'msix\AppxManifest.xml'))
$manifestText = $manifestText.Replace('{VERSION}', $version)
$manifestPath = Join-Path $layout 'AppxManifest.xml'
[IO.File]::WriteAllText($manifestPath, $manifestText, (New-Object Text.UTF8Encoding $false))
Write-Host "layout: $layout  (version $version)"

# --- 3) makepri and makeappx ------------------------------------------------------------------
$kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$makeappx = Get-ChildItem $kits -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like '*\x64\*' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw 'makeappx.exe not found: install the Windows SDK' }
$tools = $makeappx.DirectoryName

# resources.pri is what lets the shell find the logo's scaled variants; without it the taskbar
# draws the icon on a square of the accent colour (claude-tray T416).
$priconfig = Join-Path $dist 'msix\priconfig.xml'
& (Join-Path $tools 'makepri.exe') createconfig /cf $priconfig /dq en-US /pv 10.0.0 /o | Out-Host
if ($LASTEXITCODE -ne 0) { throw "makepri createconfig exited $LASTEXITCODE" }
& (Join-Path $tools 'makepri.exe') new /pr $layout /cf $priconfig /mn $manifestPath `
    /of (Join-Path $layout 'resources.pri') /o | Out-Host
if ($LASTEXITCODE -ne 0) { throw "makepri new exited $LASTEXITCODE" }

$msix = Join-Path $dist 'FreeHarbor.msix'
& $makeappx.FullName pack /o /d $layout /p $msix | Out-Host
if ($LASTEXITCODE -ne 0) { throw "makeappx exited $LASTEXITCODE" }
Write-Host "package: $msix (unsigned, for the Store)"

# --- 4) Sideload ------------------------------------------------------------------------------
if ($Sideload) {
    $publisher = ([xml]$manifestText).Package.Identity.Publisher
    $certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $publisher } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
    if (-not $certificate) {
        $certificate = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature `
            -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(1) `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    }
    $cer = Join-Path $dist 'FreeHarbor-sideload.cer'
    Export-Certificate -Cert $certificate -FilePath $cer | Out-Null

    $signed = Join-Path $dist 'FreeHarbor-sideload.msix'
    Copy-Item $msix $signed -Force
    & (Join-Path $tools 'signtool.exe') sign /fd SHA256 /sha1 $certificate.Thumbprint $signed | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "signtool exited $LASTEXITCODE" }

    Write-Host "sideload: $signed"
    Write-Host 'Trust the certificate once, from an administrator PowerShell, before installing it:'
    Write-Host "  Import-Certificate -FilePath '$cer' -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
}
