<#
DD350 — notice an upstream release of something the engine manifest pins, and pin it.

Dependabot cannot read src\FreeHarbor.Core\Engine\engine-manifest.json: its pins are URLs with a
version in them and a SHA-256 of a file on download.docker.com, a GitHub release or Alpine's CDN.
So this does what Dependabot would, for these five artefacts, and the weekly workflow
(.github\workflows\engine-pins.yml) turns what it changed into a pull request.

Policy, per artefact:
  - Moby and the CLI move together: the highest release present for BOTH the Linux tarball and the
    Windows zip, so the client and the daemon are never pinned a version apart.
  - Within the current major only. Docker 30, Compose 6 or a new Alpine branch can change
    behaviour, so they are reported in -Majors for a person to decide and never pinned here.
  - Every file is downloaded and hashed here; the digest recorded is the one measured. Where upstream
    publishes one it must agree or the run fails: Alpine's .sha256 sibling, and the per-asset digest
    GitHub's release API reports for Compose and Buildx. download.docker.com publishes none, which
    the manifest's own comment already says.

The manifest is rewritten in place by text, entry by entry, so its comment block and layout survive.
Runs the same on a workstation (Windows PowerShell 5.1 or pwsh) as on the runner:

  powershell -File build\update-pins.ps1
#>
[CmdletBinding()]
param(
    # Where to write the pull request's body; nothing is written when nothing changed.
    [string]$Body,
    # Where to write one line per new major found, for the workflow to file as issues.
    [string]$Majors,
    # Report what would change and touch nothing.
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'src\FreeHarbor.Core\Engine\engine-manifest.json'
$manifestText = [IO.File]::ReadAllText($manifestPath)
$manifest = $manifestText | ConvertFrom-Json
$work = Join-Path ([IO.Path]::GetTempPath()) ('freeharbor-pins-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

$headers = @{ 'User-Agent' = 'freeharbor-pin-check'; 'Accept' = 'application/vnd.github+json' }
if ($env:GH_TOKEN) { $headers['Authorization'] = "Bearer $($env:GH_TOKEN)" }

function Get-Text([string]$Url) {
    $content = (Invoke-WebRequest -Uri $Url -UseBasicParsing -Headers @{ 'User-Agent' = 'freeharbor-pin-check' }).Content
    # Windows PowerShell hands back bytes for a type it does not call text, which Alpine's .yaml and
    # .sha256 are; pwsh hands back a string. Both are read as UTF-8 here.
    if ($content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($content) } else { [string]$content }
}

function Get-Versions([string]$Text, [string]$Pattern) {
    [regex]::Matches($Text, $Pattern) | ForEach-Object { [version]$_.Groups[1].Value } | Sort-Object -Unique
}

# The GitHub releases of a repository that are neither drafts nor prereleases, newest first.
function Get-GitHubReleases([string]$Repo) {
    $releases = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases?per_page=50" -Headers $headers
    $releases | Where-Object { -not $_.draft -and -not $_.prerelease -and $_.tag_name -match '^v(\d+\.\d+\.\d+)$' } |
        ForEach-Object { [pscustomobject]@{ Version = [version]$Matches[1]; Release = $_ } } |
        Sort-Object Version -Descending
}

function Get-Sha256([string]$Path) { (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant() }

$changes = New-Object System.Collections.Generic.List[object]
$found = New-Object System.Collections.Generic.List[string]

# One candidate: download it, measure it, hold it to the published digest if there is one.
function Add-Change([string]$Id, [string]$Name, [version]$New, [string]$Url, [string]$Published, [string]$Notes) {
    $entry = $manifest.$Id
    $file = Join-Path $work ([IO.Path]::GetFileName(([uri]$Url).AbsolutePath))
    Write-Host "downloading $Url"
    Invoke-WebRequest -Uri $Url -OutFile $file -UseBasicParsing -Headers @{ 'User-Agent' = 'freeharbor-pin-check' }
    $measured = Get-Sha256 $file
    if ($Published -and $Published -ne $measured) {
        throw "$Name $New`: measured $measured, upstream publishes $Published. Not pinning a file its own source disagrees with."
    }
    $script:changes.Add([pscustomobject]@{
        Id = $Id; Name = $Name; Old = $entry.version; New = "$New"; Url = $Url; Sha256 = $measured
        Corroborated = [bool]$Published; Notes = $Notes
    })
}

# ── Moby and the CLI ─────────────────────────────────────────────────────────────────────────────
$current = [version]$manifest.engine.version
$linux = Get-Versions (Get-Text 'https://download.docker.com/linux/static/stable/x86_64/') 'docker-(\d+\.\d+\.\d+)\.tgz'
$windows = Get-Versions (Get-Text 'https://download.docker.com/win/static/stable/x86_64/') 'docker-(\d+\.\d+\.\d+)\.zip'
$both = $linux | Where-Object { $windows -contains $_ }
$sameMajor = $both | Where-Object { $_.Major -eq $current.Major -and $_ -gt $current } | Select-Object -Last 1
$newMajor = $both | Where-Object { $_.Major -gt $current.Major } | Select-Object -Last 1
if ($newMajor) { $found.Add("Docker Engine and CLI $newMajor (pinned: $current)") }
if ($sameMajor) {
    $notes = "https://docs.docker.com/engine/release-notes/$($sameMajor.Major)/"
    Add-Change 'engine' 'Moby (Docker Engine)' $sameMajor "https://download.docker.com/linux/static/stable/x86_64/docker-$sameMajor.tgz" $null $notes
    Add-Change 'cli' 'Docker CLI' $sameMajor "https://download.docker.com/win/static/stable/x86_64/docker-$sameMajor.zip" $null $notes
}

# ── Compose and Buildx ───────────────────────────────────────────────────────────────────────────
foreach ($plugin in @(
        @{ Id = 'compose'; Name = 'Compose plugin'; Repo = 'docker/compose'; Asset = { param($v) 'docker-compose-windows-x86_64.exe' } },
        @{ Id = 'buildx'; Name = 'Buildx plugin'; Repo = 'docker/buildx'; Asset = { param($v) "buildx-v$v.windows-amd64.exe" } })) {
    $current = [version]$manifest.($plugin.Id).version
    $releases = Get-GitHubReleases $plugin.Repo
    $newMajor = $releases | Where-Object { $_.Version.Major -gt $current.Major } | Select-Object -First 1
    if ($newMajor) { $found.Add("$($plugin.Name) $($newMajor.Version) (pinned: $current)") }
    $candidate = $releases | Where-Object { $_.Version.Major -eq $current.Major -and $_.Version -gt $current } | Select-Object -First 1
    if (-not $candidate) { continue }

    $assetName = & $plugin.Asset $candidate.Version
    $asset = $candidate.Release.assets | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) { throw "$($plugin.Name) $($candidate.Version) has no asset named $assetName" }
    $published = if ($asset.digest -match '^sha256:([0-9a-f]{64})$') { $Matches[1] } else { $null }
    Add-Change $plugin.Id $plugin.Name $candidate.Version $asset.browser_download_url $published $candidate.Release.html_url
}

# ── Alpine ───────────────────────────────────────────────────────────────────────────────────────
# Within the branch the distribution was built on: a reinstall upgrades a kept distribution inside
# that branch (DD348), so a new branch is a decision about every existing machine, not a patch.
$current = [version]$manifest.rootfs.version
$branch = "v$($current.Major).$($current.Minor)"
function Get-AlpineRelease([string]$Path) {
    $yaml = Get-Text "https://dl-cdn.alpinelinux.org/alpine/$Path/releases/x86_64/latest-releases.yaml"
    # One entry per "-" line, and the fields in no promised order: version comes before flavor.
    $block = [regex]::Split($yaml, '(?m)^-\s*$') |
        Where-Object { $_ -match '(?m)^\s*flavor:\s*alpine-minirootfs\s*$' } | Select-Object -First 1
    if (-not $block) { throw "no alpine-minirootfs entry in $Path/latest-releases.yaml" }
    [pscustomobject]@{
        Version = [version]([regex]::Match($block, 'version:\s*(\S+)').Groups[1].Value)
        File = [regex]::Match($block, 'file:\s*(\S+)').Groups[1].Value
    }
}
$stable = Get-AlpineRelease 'latest-stable'
if ($stable.Version.Major -gt $current.Major -or $stable.Version.Minor -gt $current.Minor) {
    $found.Add("Alpine $($stable.Version.Major).$($stable.Version.Minor) branch (pinned: $current)")
}
$inBranch = Get-AlpineRelease $branch
if ($inBranch.Version -gt $current) {
    $url = "https://dl-cdn.alpinelinux.org/alpine/$branch/releases/x86_64/$($inBranch.File)"
    $published = ((Get-Text "$url.sha256") -split '\s+')[0].ToLowerInvariant()
    Add-Change 'rootfs' 'Alpine minirootfs' $inBranch.Version $url $published "https://alpinelinux.org/posts/"
}

# ── Rewrite ──────────────────────────────────────────────────────────────────────────────────────
foreach ($change in $changes) {
    $entry = $manifest.($change.Id)
    $blockMatch = [regex]::Match($manifestText, '"' + $change.Id + '"\s*:\s*\{[^}]*\}')
    if (-not $blockMatch.Success) { throw "no $($change.Id) entry in the manifest" }
    $fileName = $entry.fileName.Replace($change.Old, $change.New)
    $block = $blockMatch.Value
    $block = $block.Replace('"' + $entry.version + '"', '"' + $change.New + '"')
    $block = $block.Replace('"' + $entry.url + '"', '"' + $change.Url + '"')
    $block = $block.Replace('"' + $entry.fileName + '"', '"' + $fileName + '"')
    $block = $block.Replace('"' + $entry.sha256 + '"', '"' + $change.Sha256 + '"')
    $manifestText = $manifestText.Substring(0, $blockMatch.Index) + $block +
        $manifestText.Substring($blockMatch.Index + $blockMatch.Length)
}

foreach ($change in $changes) {
    $how = if ($change.Corroborated) { 'matches upstream' } else { 'measured; upstream publishes none' }
    Write-Host ("{0}: {1} -> {2}  sha256 {3} ({4})" -f $change.Name, $change.Old, $change.New, $change.Sha256, $how)
}
foreach ($major in $found) { Write-Host "new major, not pinned: $major" }
if ($changes.Count -eq 0) { Write-Host 'every pin is the latest within its major' }

if (-not $DryRun -and $changes.Count -gt 0) {
    [IO.File]::WriteAllText($manifestPath, $manifestText, (New-Object Text.UTF8Encoding($false)))
}

if ($Body -and $changes.Count -gt 0) {
    $lines = @(
        'The weekly pin check (DD350) found newer releases within the majors the engine manifest pins.',
        '',
        '| Artefact | Pinned | New | SHA-256 | Release notes |',
        '|---|---|---|---|---|'
    )
    foreach ($change in $changes) {
        $how = if ($change.Corroborated) { 'matches upstream' } else { 'measured here, none published' }
        $lines += "| $($change.Name) | $($change.Old) | **$($change.New)** | ``$($change.Sha256.Substring(0, 12))…`` $how | $($change.Notes) |"
    }
    $lines += @(
        '',
        'NOTICE and site/src/lib/product.generated.ts are regenerated in the same commit, and the test suite ran on this branch before it was opened.',
        '',
        '**Owed before merge:** one real provision. Hosted runners have no WSL2, so nothing here started the new engine. Install the branch''s build over an existing FreeHarbor and check `docker version`, `docker compose version`, `docker buildx version` and `/etc/alpine-release` in the distribution.'
    )
    [IO.File]::WriteAllText($Body, ($lines -join "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
}

if ($Majors) {
    [IO.File]::WriteAllText($Majors, ($found -join "`n"), (New-Object Text.UTF8Encoding($false)))
}

if ($env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "changed=$(([bool]($changes.Count -gt 0)).ToString().ToLowerInvariant())"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "summary=$(($changes | ForEach-Object { "$($_.Name) $($_.New)" }) -join ', ')"
}

Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
