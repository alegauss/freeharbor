# =============================================================================
# update-winget.ps1 - rewrites the winget manifests in build\winget for one
# release, from the installer that release is built from (DD281).
#
# Rewrites, in every YAML there (the version, installer and both locales):
#   PackageVersion, InstallerUrl, ReleaseNotesUrl, DisplayName, DisplayVersion,
#   InstallerSha256 and ReleaseDate.
#
# The hash is the point. It is taken from the installer on disk, which is the
# file publish-release.ps1 uploads, so the manifest vouches for the bytes people
# download rather than for a second build of the same commit.
#
# Usage:  powershell -File build\update-winget.ps1
#         powershell -File build\update-winget.ps1 -Version 1.0.15 -Sha <SHA256>
#   -Version  defaults to <Version> in Directory.Build.props
#   -Sha      defaults to the SHA-256 of dist\FreeWilly-Setup.exe, or of
#             dist\FreeWilly-Setup-<version>.exe once publish-release renamed it
#   -Date     defaults to the date of the commit being packaged, not today
# =============================================================================
param(
    [string]$Version,
    [string]$Sha,
    [string]$Date
)
$ErrorActionPreference = 'Stop'

# This script lives in build\; Directory.Build.props and dist\ are in the folder above.
$root = Split-Path $PSScriptRoot -Parent

# --- The version, from the one place that holds it (DD14) --------------------
if (-not $Version) {
    $props = [System.IO.File]::ReadAllText((Join-Path $root 'Directory.Build.props'))
    if ($props -notmatch '<Version>\s*([0-9]+\.[0-9]+\.[0-9]+)\s*</Version>') {
        throw "no <Version> found in Directory.Build.props"
    }
    $Version = $Matches[1]
}
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "invalid version: '$Version'. Use x.y.z (e.g. 1.0.15)."
}

# --- The hash of the installer that will be uploaded --------------------------
# Before publish-release.ps1 the file is FreeWilly-Setup.exe; after it, the same
# bytes are FreeWilly-Setup-<version>.exe. Either is the release's installer.
if (-not $Sha) {
    $candidates = @(
        (Join-Path $root "dist\FreeWilly-Setup-$Version.exe"),
        (Join-Path $root 'dist\FreeWilly-Setup.exe')
    )
    $setup = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $setup) {
        throw "no installer in dist\ for v$Version - run build\build-installer.cmd first, or pass -Sha"
    }
    $Sha = (Get-FileHash $setup -Algorithm SHA256).Hash
}
$Sha = $Sha.ToUpperInvariant()
if ($Sha -notmatch '^[0-9A-F]{64}$') {
    throw "invalid SHA-256: '$Sha'"
}

# --- The date, from the commit rather than the clock --------------------------
# The clock would make this the one field a rerun for the same tag does not
# reproduce. The tag answers when regenerating an old release; HEAD answers when
# cutting a new one, where the tag does not exist yet. `rev-parse -q` prints
# nothing on a miss, and the preference is lowered because in Windows PowerShell
# 5.1 a native command's stderr under 'Stop' ends the script.
function Get-CommitDate([string]$rev) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $null = & git -C $root rev-parse -q --verify "$rev^{commit}" 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        $out = & git -C $root log -1 --format=%cs $rev 2>$null
        if ($LASTEXITCODE -eq 0 -and $out -match '^\d{4}-\d{2}-\d{2}$') { return $out }
        return $null
    }
    catch { return $null }
    finally { $ErrorActionPreference = $prev }
}

$dateFrom = '-Date'
if (-not $Date) { $Date = Get-CommitDate "v$Version"; $dateFrom = "the commit of v$Version" }
if (-not $Date) { $Date = Get-CommitDate 'HEAD'; $dateFrom = 'the commit at HEAD' }
if (-not $Date) { $Date = (Get-Date).ToString('yyyy-MM-dd'); $dateFrom = 'the clock (no git)' }

Write-Host "version : $Version"
Write-Host "sha256  : $Sha"
Write-Host "date    : $Date  ($dateFrom)"

# --- The rewrite ---------------------------------------------------------------
# UTF-8 without a BOM both ways: the pt-BR locale carries accents, and Windows
# PowerShell 5.1 would otherwise decode it with the ANSI codepage.
$utf8 = New-Object System.Text.UTF8Encoding($false)
$dir = Join-Path $PSScriptRoot 'winget'
$files = Get-ChildItem -Path $dir -Filter '*.yaml'
if ($files.Count -eq 0) { throw "no manifests in $dir" }

foreach ($file in $files) {
    $c = [System.IO.File]::ReadAllText($file.FullName, $utf8)

    # [ \t] and [^\r\n] rather than \s and .: \s crosses a line break, and . keeps
    # the \r of a CRLF file, so either would rewrite more than the one field.
    $c = $c -replace '(?m)^PackageVersion:[ \t]*[^\r\n]+', "PackageVersion: $Version"
    $c = $c -replace 'releases/download/v[0-9]+\.[0-9]+\.[0-9]+/FreeWilly-Setup-[0-9]+\.[0-9]+\.[0-9]+\.exe',
                     "releases/download/v$Version/FreeWilly-Setup-$Version.exe"
    $c = $c -replace 'releases/tag/v[0-9]+\.[0-9]+\.[0-9]+', "releases/tag/v$Version"

    # installer.iss sets UninstallDisplayName to "FreeWilly <version>", so the
    # Add/Remove Programs name moves with every release too.
    $c = $c -replace '(?m)^([ \t]*)- DisplayName:[ \t]*FreeWilly[ \t]+[0-9]+\.[0-9]+\.[0-9]+', "`${1}- DisplayName: FreeWilly $Version"
    $c = $c -replace '(?m)^([ \t]*)DisplayVersion:[ \t]*[^\r\n]+', "`${1}DisplayVersion: $Version"
    $c = $c -replace '(?m)^([ \t]*)InstallerSha256:[ \t]*[^\r\n]+', "`${1}InstallerSha256: $Sha"
    $c = $c -replace '(?m)^ReleaseDate:[ \t]*[^\r\n]+', "ReleaseDate: $Date"

    [System.IO.File]::WriteAllText($file.FullName, $c, $utf8)
    Write-Host "updated : build\winget\$($file.Name)"
}

Write-Host ''
Write-Host "winget manifests updated for v$Version."
