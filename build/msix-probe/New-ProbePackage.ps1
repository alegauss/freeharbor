<#
DD309: build a minimal, self-signed MSIX holding the current FreeHarbor.exe, in one of the two modes
the design asks about.

  Virtualized    the default for a full-trust package: new folders under AppData are redirected to
                 the package's LocalCache and HKCU writes go to a private hive.
  Unvirtualized  desktop6:FileSystemWriteVirtualization and RegistryWriteVirtualization disabled,
                 under the rescap:unvirtualizedResources capability.

The package exposes the execution alias fh309.exe, so `fh309 --provision` runs with the package's
identity. Needs the Windows 10/11 SDK (makeappx, signtool). Needs no administrator: trusting the
certificate does, and that is one command, printed at the end.

  powershell -File build\msix-probe\New-ProbePackage.ps1 -Mode Virtualized
  powershell -File build\msix-probe\New-ProbePackage.ps1 -Mode Unvirtualized -SkipPublish
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Virtualized', 'Unvirtualized')][string]$Mode,
    # Reuse the last Release publish instead of running dotnet publish again.
    [switch]$SkipPublish,
    # Pack only: check the manifest and layout without creating or using a certificate.
    [switch]$NoSign
)

. (Join-Path $PSScriptRoot 'Common.ps1')
Add-Type -AssemblyName System.Drawing

function Find-SdkTool([string]$Name) {
    $found = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\$Name" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $found) { throw "$Name not found: install the Windows SDK (it ships makeappx and signtool)." }
    $found.FullName
}

$makeappx = Find-SdkTool 'makeappx.exe'
$signtool = Find-SdkTool 'signtool.exe'

# ---- the executable ------------------------------------------------------------------------------
$publish = Join-Path $RepoRoot 'src\FreeHarbor.Tray\bin\Release\net10.0-windows\win-x64\publish\FreeHarbor.exe'
if (-not $SkipPublish) {
    & dotnet publish (Join-Path $RepoRoot 'src\FreeHarbor.Tray\FreeHarbor.Tray.csproj') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }
}
if (-not (Test-Path $publish)) { throw "$publish is missing: run without -SkipPublish" }

# ---- the layout ----------------------------------------------------------------------------------
$stage = Join-Path $OutDir "stage-$Mode"
Remove-Item -Recurse -Force -LiteralPath $stage -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'Assets') | Out-Null
Copy-Item $publish (Join-Path $stage 'FreeHarbor.exe')

# The logos a manifest requires, drawn from the product's own icon. Windows PowerShell's
# System.Drawing cannot decode the PNG-compressed 256 px frame, so the largest frame it can is used.
$source = $null
foreach ($size in 256, 128, 64, 48, 32) {
    try {
        $icon = New-Object System.Drawing.Icon (Join-Path $RepoRoot 'src\FreeHarbor.Tray\FreeHarbor.ico'), $size, $size
        $source = $icon.ToBitmap()
        $icon.Dispose()
        break
    }
    catch { $source = $null }
}
if (-not $source) { throw 'no frame of FreeHarbor.ico could be read' }
foreach ($logo in @(@{ Name = 'Square150x150Logo.png'; Size = 150 }, @{ Name = 'Square44x44Logo.png'; Size = 44 },
        @{ Name = 'StoreLogo.png'; Size = 50 })) {
    $bitmap = New-Object System.Drawing.Bitmap $logo.Size, $logo.Size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($source, 0, 0, $logo.Size, $logo.Size)
    $bitmap.Save((Join-Path $stage "Assets\$($logo.Name)"), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
}
$source.Dispose()

# ---- the manifest --------------------------------------------------------------------------------
$unvirtualized = $Mode -eq 'Unvirtualized'
$properties = if ($unvirtualized) {
    @'
    <desktop6:FileSystemWriteVirtualization>disabled</desktop6:FileSystemWriteVirtualization>
    <desktop6:RegistryWriteVirtualization>disabled</desktop6:RegistryWriteVirtualization>
'@
} else { '' }
$capability = if ($unvirtualized) { '    <rescap:Capability Name="unvirtualizedResources" />' } else { '' }

$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
  xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
  xmlns:desktop6="http://schemas.microsoft.com/appx/manifest/desktop/windows10/6"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap uap3 desktop desktop6 rescap">
  <Identity Name="$PackageName" Publisher="$Subject" Version="1.0.0.0" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>FreeHarbor DD309 probe ($Mode)</DisplayName>
    <PublisherDisplayName>FreeHarbor DD309 Probe</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
$properties  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Resources>
    <Resource Language="en-us" />
  </Resources>
  <Applications>
    <Application Id="$AppId" Executable="FreeHarbor.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements DisplayName="FreeHarbor DD309 probe ($Mode)" Description="DD309 measurement package"
        BackgroundColor="transparent" Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png" />
      <Extensions>
        <uap3:Extension Category="windows.appExecutionAlias" Executable="FreeHarbor.exe" EntryPoint="Windows.FullTrustApplication">
          <uap3:AppExecutionAlias>
            <desktop:ExecutionAlias Alias="$Alias.exe" />
          </uap3:AppExecutionAlias>
        </uap3:Extension>
      </Extensions>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
$capability
  </Capabilities>
</Package>
"@
[IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding($false)))

# ---- pack and sign -------------------------------------------------------------------------------
$msix = Join-Path $OutDir "FreeHarbor-DD309-$Mode.msix"
& $makeappx pack /o /d $stage /p $msix
if ($LASTEXITCODE -ne 0) { throw "makeappx failed ($LASTEXITCODE)" }
if ($NoSign) {
    Write-Host "Packed $msix, unsigned (-NoSign): it will not install."
    return
}

# One certificate for both modes, kept in the current user's store and reused.
$certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject } |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $certificate) {
    $certificate = New-SelfSignedCertificate -Type Custom -Subject $Subject -KeyUsage DigitalSignature `
        -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddMonths(6) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}
$cer = Join-Path $OutDir 'FreeHarbor-DD309.cer'
Export-Certificate -Cert $certificate -FilePath $cer | Out-Null

& $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint $msix
if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)" }

Write-Host ''
Write-Host "Built $msix"
$trusted = Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint }
if (-not $trusted) {
    Write-Host 'Trust the certificate once, from an administrator PowerShell:'
    Write-Host "  Import-Certificate -FilePath '$cer' -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
}
Write-Host "Then: powershell -File build\msix-probe\Measure-Probe.ps1 -Mode $Mode"
