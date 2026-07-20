#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Refreshes the vendored KTX-Software (libktx) native libraries under native/<rid>/.

.DESCRIPTION
    Obj2Tiles bundles the KTX-Software native library (libktx) for every supported
    runtime identifier so that KTX2 (KHR_texture_basisu) texture encoding works from
    the published single-file executable without any external 'ktx' tool.

    This script downloads the official KTX-Software release assets, verifies their
    SHA-256 digests (as published by the GitHub release API), extracts the libktx native
    library from each and copies it into native/<rid>/ with the file name the P/Invoke
    resolver expects (ktx.dll / libktx.so / libktx.dylib).

    Asset -> RID mapping (KTX-Software <version>, Apache-2.0):
        win-x64      ktx.dll      <- KTX-Software-<v>-Windows-x64.exe      (bin/ktx.dll)
        win-arm64    ktx.dll      <- KTX-Software-<v>-Windows-arm64.exe    (bin/ktx.dll)
        linux-x64    libktx.so    <- KTX-Software-<v>-Linux-x86_64.tar.bz2 (lib/libktx.so.<v>)
        linux-arm64  libktx.so    <- KTX-Software-<v>-Linux-arm64.tar.bz2  (lib/libktx.so.<v>)
        osx-x64      libktx.dylib <- KTX-Software-<v>-Darwin-x86_64.pkg    (lib/libktx.<v>.dylib)

    Tooling:
      * tar    - unpacks the Linux .tar.bz2 tarballs (and, where libarchive has xar
                 support, the macOS .pkg too). Bundled with Windows 10+/Linux/macOS.
      * 7-Zip  - required to unpack the Windows NSIS installers (.exe), and used as a
                 fallback for the macOS .pkg when tar cannot read xar archives.
                 Install: winget install 7zip.7zip | choco install 7zip |
                          sudo apt-get install p7zip-full | brew install p7zip

    Set $env:GITHUB_TOKEN to raise the anonymous GitHub API rate limit if needed.

.PARAMETER Version
    KTX-Software release version to vendor. Default: 4.4.2.

.PARAMETER Rid
    One or more runtime identifiers to refresh. Default: all five supported RIDs.

.PARAMETER SkipChecksum
    Skip SHA-256 verification of the downloaded assets (not recommended).

.EXAMPLE
    ./update-libktx.ps1
    Refreshes every vendored libktx to the default version.

.EXAMPLE
    ./update-libktx.ps1 -Version 4.4.2 -Rid linux-x64,linux-arm64
    Refreshes only the Linux libraries (no 7-Zip required).
#>
[CmdletBinding()]
param(
    [string]$Version = '4.4.2',

    [ValidateSet('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64')]
    [string[]]$Rid = @('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64'),

    [switch]$SkipChecksum
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$NativeRoot = $PSScriptRoot
$Repo = 'KhronosGroup/KTX-Software'
$UserAgent = 'Obj2Tiles-update-libktx'

# RID -> { release asset, extracted-file name to locate, vendored output name }
$Targets = [ordered]@{
    'win-x64'     = @{ Asset = "KTX-Software-$Version-Windows-x64.exe";      Find = 'ktx.dll';               Out = 'ktx.dll' }
    'win-arm64'   = @{ Asset = "KTX-Software-$Version-Windows-arm64.exe";    Find = 'ktx.dll';               Out = 'ktx.dll' }
    'linux-x64'   = @{ Asset = "KTX-Software-$Version-Linux-x86_64.tar.bz2"; Find = "libktx.so.$Version";    Out = 'libktx.so' }
    'linux-arm64' = @{ Asset = "KTX-Software-$Version-Linux-arm64.tar.bz2";  Find = "libktx.so.$Version";    Out = 'libktx.so' }
    'osx-x64'     = @{ Asset = "KTX-Software-$Version-Darwin-x86_64.pkg";    Find = "libktx.$Version.dylib"; Out = 'libktx.dylib' }
}

function Get-SevenZip {
    foreach ($name in '7z', '7za', '7zz') {
        $cmd = Get-Command $name -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    return $null
}

# Fetches the release metadata (asset names, download URLs and SHA-256 digests) via the
# GitHub API. Honours $env:GITHUB_TOKEN to lift the anonymous rate limit.
function Get-Release {
    param([string]$Version)

    $headers = @{ 'User-Agent' = $UserAgent; 'Accept' = 'application/vnd.github+json' }
    if ($env:GITHUB_TOKEN) { $headers['Authorization'] = "Bearer $env:GITHUB_TOKEN" }
    $api = "https://api.github.com/repos/$Repo/releases/tags/v$Version"
    return Invoke-RestMethod -Uri $api -Headers $headers -MaximumRetryCount 5 -RetryIntervalSec 5
}

function Get-Asset {
    param($Asset, [string]$Destination)

    Write-Host "  downloading $($Asset.name)"
    Invoke-WebRequest -Uri $Asset.browser_download_url -OutFile $Destination `
        -Headers @{ 'User-Agent' = $UserAgent } -MaximumRetryCount 5 -RetryIntervalSec 5

    if ($SkipChecksum) { return }

    # The GitHub API publishes a "<algo>:<hex>" digest for every asset (unlike the .sha1
    # sidecars, which only exist for the archive assets - not the .exe/.pkg installers).
    $digestProp = $Asset.PSObject.Properties['digest']
    $digest = if ($digestProp) { [string]$digestProp.Value } else { $null }
    if (-not $digest) {
        Write-Warning "  no digest published for $($Asset.name); skipping checksum"
        return
    }
    $algo, $expected = $digest -split ':', 2
    $expected = $expected.Trim().ToLowerInvariant()
    $actual = (Get-FileHash -Algorithm $algo.ToUpperInvariant() -LiteralPath $Destination).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "Checksum mismatch for $($Asset.name)`n  expected $expected`n  actual   $actual"
    }
    Write-Host "  $algo ok"
}

# Extracts an archive into $Dest, preferring tar (libarchive: tar.bz2 / gzip / cpio / xar)
# and falling back to 7-Zip (NSIS .exe, and .pkg where tar lacks xar support).
function Expand-Container {
    param([string]$Archive, [string]$Dest)

    New-Item -ItemType Directory -Path $Dest -Force | Out-Null

    & tar -xf $Archive -C $Dest 2>$null
    # tar can exit non-zero on Windows purely because it cannot create symlink entries
    # (e.g. libktx.dylib -> libktx.4.4.2.dylib); the real target files are still extracted.
    # Treat "at least one file was extracted" as success rather than trusting the exit code.
    if (Get-ChildItem -LiteralPath $Dest -Force -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1) {
        return $true
    }

    # Nothing came out (e.g. an NSIS .exe tar cannot read): fall back to 7-Zip.
    Get-ChildItem -LiteralPath $Dest -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    if ($script:SevenZip) {
        & $script:SevenZip x $Archive "-o$Dest" -y -bso0 -bsp0 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { return $true }
    }
    return $false
}

function Get-ExtractedFile {
    param([string]$Root, [string]$Name)
    return Get-ChildItem -LiteralPath $Root -Recurse -File -Filter $Name -ErrorAction SilentlyContinue |
        Select-Object -First 1
}

# Extracts the target libktx binary from a downloaded asset and returns its full path.
function Expand-NativeLib {
    param([hashtable]$Spec, [string]$Archive, [string]$WorkDir)

    $asset = $Spec.Asset

    if ($asset.EndsWith('.tar.bz2')) {
        # Extract only the real versioned .so member, so the .so/.so.4 symlink entries
        # (which fail to materialise on Windows) are never touched.
        $members = & tar -tjf $Archive
        if ($LASTEXITCODE -ne 0) { throw "tar failed to list $asset" }
        $rx = [regex]::Escape("lib/$($Spec.Find)") + '$'
        $member = $members | Where-Object { $_ -match $rx } | Select-Object -First 1
        if (-not $member) { throw "Could not find lib/$($Spec.Find) in $asset" }
        & tar -xjf $Archive -C $WorkDir $member
        if ($LASTEXITCODE -ne 0) { throw "tar failed to extract $member" }
        return (Join-Path $WorkDir ($member -replace '/', [IO.Path]::DirectorySeparatorChar))
    }

    # NSIS .exe (7-Zip only) or macOS .pkg (xar -> gzip Payload -> cpio). Unpack, then
    # keep unpacking nested containers (Payload / Payload~) until the library surfaces.
    $dest = Join-Path $WorkDir 'x'
    if (-not (Expand-Container $Archive $dest)) {
        $hint = if (-not $script:SevenZip) { ' Install 7-Zip (see this script''s help) and re-run.' } else { '' }
        throw "Could not unpack $asset.$hint"
    }

    for ($i = 0; $i -lt 8; $i++) {
        $hit = Get-ExtractedFile $dest $Spec.Find
        if ($hit) { return $hit.FullName }

        $nested = Get-ChildItem -LiteralPath $dest -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -in @('Payload', 'Payload~') -or $_.Extension -in @('.cpio', '.gz', '.xar', '.tar') } |
            Select-Object -First 1
        if (-not $nested) { break }

        $sub = Join-Path $dest "nested$i"
        if (-not (Expand-Container $nested.FullName $sub)) { break }
        Remove-Item -LiteralPath $nested.FullName -Force -ErrorAction SilentlyContinue
    }

    throw "Could not locate '$($Spec.Find)' inside $asset after extraction."
}

# --- main ---------------------------------------------------------------------

Write-Host "KTX-Software libktx updater - version $Version" -ForegroundColor Cyan

$selected = $Rid | Select-Object -Unique
$script:SevenZip = Get-SevenZip

# The Windows NSIS installers can only be opened by 7-Zip; fail fast if it is missing.
$winSelected = $selected | Where-Object { $_ -like 'win-*' }
if ($winSelected -and -not $script:SevenZip) {
    Write-Error @"
7-Zip is required to refresh the Windows RIDs ($($winSelected -join ', ')) but was not found on PATH.
Install it and re-run, or select only non-Windows RIDs with -Rid.
  Windows : winget install 7zip.7zip     (or: choco install 7zip)
  Linux   : sudo apt-get install p7zip-full
  macOS   : brew install p7zip
"@
    exit 1
}
if ($script:SevenZip) { Write-Host "Using 7-Zip: $script:SevenZip" -ForegroundColor DarkGray }

Write-Host "Querying release v$Version metadata..." -ForegroundColor DarkGray
$release = Get-Release -Version $Version
$assetMap = @{}
foreach ($a in $release.assets) { $assetMap[$a.name] = $a }

# Fail fast if any requested asset is missing from the release.
$missing = foreach ($r in $selected) { if (-not $assetMap.ContainsKey($Targets[$r].Asset)) { $Targets[$r].Asset } }
if ($missing) {
    Write-Error "Release v$Version is missing expected asset(s): $($missing -join ', ')"
    exit 1
}

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("ktx-update-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

$summary = [System.Collections.Generic.List[object]]::new()
try {
    foreach ($r in $selected) {
        $spec = $Targets[$r]
        Write-Host "[$r]" -ForegroundColor Yellow

        $archive = Join-Path $tmp $spec.Asset
        Get-Asset -Asset $assetMap[$spec.Asset] -Destination $archive

        $work = Join-Path $tmp $r
        New-Item -ItemType Directory -Path $work -Force | Out-Null
        $libPath = Expand-NativeLib -Spec $spec -Archive $archive -WorkDir $work

        $outDir = Join-Path $NativeRoot $r
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
        $outFile = Join-Path $outDir $spec.Out
        Copy-Item -LiteralPath $libPath -Destination $outFile -Force

        $item = Get-Item -LiteralPath $outFile
        $sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $outFile).Hash.ToLowerInvariant()
        Write-Host ("  -> {0}/{1}  ({2:N2} MB)" -f $r, $spec.Out, ($item.Length / 1MB)) -ForegroundColor Green

        $summary.Add([pscustomobject]@{
            RID    = $r
            File   = "$r/$($spec.Out)"
            MB     = [math]::Round($item.Length / 1MB, 2)
            SHA256 = $sha256.Substring(0, 16) + '...'
        })
    }
}
finally {
    Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "`nDone. Vendored libktx $Version" -ForegroundColor Cyan
$summary | Format-Table -AutoSize
Write-Host "If the version changed, update the source-mapping comment in Obj2Tiles.csproj." -ForegroundColor DarkGray
