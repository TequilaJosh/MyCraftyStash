#requires -Version 5.1
<#
    publish.ps1 - one-shot publish of My Crafty Stash.

    1. Bump <FileVersion> + <AssemblyVersion> in the csproj (revision by default).
       This is what the in-app updater compares against, so it has to bump or
       no one sees the new build.
    2. dotnet publish (Release, win-x64, self-contained, single-file).
    3. Run Inno Setup compiler against Installer\MyCraftyStash.iss
       - emits  Installer\output\MyCraftyStash_Setup_X.Y.Z.W.exe
    4. Create a GitHub release (tag vX.Y.Z.W) with the installer attached.
       The in-app updater (App.xaml.cs + Services\UpdateService.cs) polls
       https://api.github.com/repos/TequilaJosh/MyCraftyStash/releases/latest
       on startup, offers to download, and launches the installer. Release
       notes come from the matching section of Installer\changelog.txt and
       feed the in-app "What's New" popup, so keep that file current.
    NOTE: the \\Win-u5iq2hisnh3 network share belongs to J and H Inventory's
    updater. MCS must never publish there: an MCS build on that share (or
    vice versa) makes one app offer the other app's installer as an "update".

    Usage (from repo root or anywhere):
        .\Installer\publish.ps1
        .\Installer\publish.ps1 -BumpPart Patch          # bump 1.0.X.0 instead of revision
        .\Installer\publish.ps1 -BumpPart Minor          # bump 1.X.0.0
        .\Installer\publish.ps1 -BumpPart Major          # bump X.0.0.0
        .\Installer\publish.ps1 -SetVersion "1.0.3.0"    # explicit version
        .\Installer\publish.ps1 -SkipBump                # republish current version
        .\Installer\publish.ps1 -SkipPublish             # iscc + release only
        .\Installer\publish.ps1 -SkipGitHub              # build locally, no release
        .\Installer\publish.ps1 -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
#>

[CmdletBinding()]
param(
    [ValidateSet('Major','Minor','Patch','Revision')]
    [string]$BumpPart = 'Revision',
    [string]$SetVersion,
    [switch]$SkipBump,
    [switch]$SkipPublish,
    [switch]$SkipGitHub,
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'

$repoRoot      = Split-Path -Parent $PSScriptRoot
$csproj        = Join-Path $repoRoot 'MyCraftyStash.csproj'
$issPath       = Join-Path $PSScriptRoot 'MyCraftyStash.iss'
$publishDir    = Join-Path $repoRoot 'bin\Release\net8.0-windows10.0.19041.0\win-x64\publish'
$exePath       = Join-Path $publishDir 'MyCraftyStash.exe'
$installerOut  = Join-Path $PSScriptRoot 'output'
$localChangelog  = Join-Path $PSScriptRoot 'changelog.txt'

if (-not (Test-Path $csproj)) {
    throw "csproj not found at $csproj - make sure publish.ps1 lives in the Installer\ folder of the repo."
}

# ── 1. Bump the version in the csproj ────────────────────────────────────────
function Bump-Csproj {
    param(
        [string]$CsprojPath,
        [string]$BumpPart,
        [string]$ExplicitVersion
    )

    # Preserve file exactly - no encoding surprises, no auto-trimmed newlines.
    $content = [System.IO.File]::ReadAllText($CsprojPath)

    if ($content -notmatch '<FileVersion>(\d+)\.(\d+)\.(\d+)\.(\d+)</FileVersion>') {
        throw "Could not find <FileVersion>X.Y.Z.W</FileVersion> in $CsprojPath"
    }
    $oldFv = "$($Matches[1]).$($Matches[2]).$($Matches[3]).$($Matches[4])"

    if ($ExplicitVersion) {
        if ($ExplicitVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
            throw "-SetVersion must be in MAJOR.MINOR.PATCH.REVISION format (got '$ExplicitVersion')."
        }
        $newFv = $ExplicitVersion
    }
    else {
        $major = [int]$Matches[1]
        $minor = [int]$Matches[2]
        $patch = [int]$Matches[3]
        $rev   = [int]$Matches[4]

        switch ($BumpPart) {
            'Major'    { $major++; $minor = 0; $patch = 0; $rev = 0 }
            'Minor'    { $minor++; $patch = 0; $rev = 0 }
            'Patch'    { $patch++; $rev = 0 }
            'Revision' { $rev++ }
        }
        $newFv = "$major.$minor.$patch.$rev"
    }

    # Strip trailing .0 for SemVer-flavoured fields so 1.0.2.0 -> 1.0.2 but
    # 1.0.2.5 stays 1.0.2.5.
    $semVer = $newFv -replace '\.0$', ''

    $content = $content -replace '<FileVersion>\d+\.\d+\.\d+\.\d+</FileVersion>',         "<FileVersion>$newFv</FileVersion>"
    $content = $content -replace '<AssemblyVersion>\d+\.\d+\.\d+\.\d+</AssemblyVersion>', "<AssemblyVersion>$newFv</AssemblyVersion>"
    $content = $content -replace '<Version>\d+(\.\d+){2,3}</Version>',                    "<Version>$semVer</Version>"
    $content = $content -replace '<InformationalVersion>\d+(\.\d+){2,3}</InformationalVersion>', "<InformationalVersion>$semVer</InformationalVersion>"

    [System.IO.File]::WriteAllText($CsprojPath, $content)

    return [PSCustomObject]@{ Old = $oldFv; New = $newFv }
}

if ($SkipBump) {
    $oldFv = if ((Get-Content -Raw $csproj) -match '<FileVersion>(\d+\.\d+\.\d+\.\d+)</FileVersion>') { $Matches[1] } else { '?' }
    Write-Host "==> Skipping version bump (current $oldFv)" -ForegroundColor DarkYellow
}
else {
    $bump = Bump-Csproj -CsprojPath $csproj -BumpPart $BumpPart -ExplicitVersion $SetVersion
    Write-Host "==> Version: $($bump.Old) -> $($bump.New)" -ForegroundColor Cyan
}

# ── 2. Publish (single-file, self-contained, win-x64) ────────────────────────
if (-not $SkipPublish) {
    # Explicit RID-aware restore first. A normal Debug build leaves
    # obj\project.assets.json restored WITHOUT win-x64, and publish then dies
    # with NETSDK1047 ("assets file doesn't have a target for ...win-x64")
    # instead of re-restoring. Forcing the restore makes publish reliable no
    # matter what built last.
    Write-Host "==> dotnet restore (win-x64) ..." -ForegroundColor Cyan
    & dotnet restore $csproj -r win-x64
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed (exit $LASTEXITCODE)" }

    Write-Host "==> dotnet publish ..." -ForegroundColor Cyan
    & dotnet publish $csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=true `
        -p:IncludeNativeLibrariesForSelfExtract=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
}

if (-not (Test-Path $exePath)) {
    throw "Published exe not found at $exePath - did publish succeed? Check that the project targets net8.0-windows10.0.19041.0 and was published with -r win-x64."
}

# ── 3. Locate iscc ───────────────────────────────────────────────────────────
if (-not $IsccPath) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $IsccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path $IsccPath)) {
    throw "Inno Setup compiler (ISCC.exe) not found. Install from https://jrsoftware.org/isdl.php or pass -IsccPath."
}

# ── 4. Compile installer ─────────────────────────────────────────────────────
Write-Host "==> Inno Setup compile ..." -ForegroundColor Cyan
& $IsccPath $issPath
if ($LASTEXITCODE -ne 0) { throw "iscc failed (exit $LASTEXITCODE)" }

# Read the version we just baked in (FileVersion of the published exe) - used
# both for the share's setup.exe + version.txt and for locating the versioned
# installer iscc just produced.
$fileVersion = (Get-Item $exePath).VersionInfo.FileVersion
if (-not $fileVersion) {
    throw "Could not read FileVersion from $exePath."
}

$versionedSetup = Join-Path $installerOut "MyCraftyStash_Setup_$fileVersion.exe"
if (-not (Test-Path $versionedSetup)) {
    throw "Expected installer not found at $versionedSetup. Did iscc emit a different filename?"
}
Write-Host "==> Built installer: $versionedSetup" -ForegroundColor Green

# ── 5. Create the GitHub release the in-app updater looks for ───────────────
function Get-ChangelogSection {
    param([string]$ChangelogPath, [string]$Version)
    # Returns the bullet block under the "## $Version" header, or $null.
    if (-not (Test-Path $ChangelogPath)) { return $null }
    $lines = Get-Content $ChangelogPath
    $collecting = $false
    $section = @()
    foreach ($line in $lines) {
        if ($line -match '^\s*(?:#{1,6}\s*|=+\s*)?v?(\d+\.\d+\.\d+(?:\.\d+)?)\s*=*\s*$') {
            if ($collecting) { break }                  # hit the next version header
            $collecting = ($Matches[1] -eq $Version)
            continue
        }
        if ($collecting -and $line.Trim().Length -gt 0) { $section += $line }
    }
    if ($section.Count -eq 0) { return $null }
    return ($section -join "`n")
}

if ($SkipGitHub) {
    Write-Host "==> Skipping GitHub release (-SkipGitHub)" -ForegroundColor DarkYellow
}
else {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw "GitHub CLI (gh) not found. Install from https://cli.github.com or re-run with -SkipGitHub."
    }

    $tag = "v$fileVersion"
    $notes = Get-ChangelogSection -ChangelogPath $localChangelog -Version $fileVersion
    if (-not $notes) {
        Write-Warning "No '## $fileVersion' section in Installer\changelog.txt - the release (and the in-app What's New popup) will have generic notes. Add a section and re-run with -SkipBump -SkipPublish to fix."
        $notes = "My Crafty Stash $fileVersion"
    }

    # gh release create fails if the tag already exists - that's the guard
    # against accidentally re-publishing over a shipped version. Use
    # 'gh release delete' + re-run if you really mean to replace one.
    Write-Host "==> Creating GitHub release $tag ..." -ForegroundColor Cyan
    $notesFile = Join-Path $env:TEMP "mcs_release_notes_$fileVersion.md"
    [System.IO.File]::WriteAllText($notesFile, $notes, (New-Object System.Text.UTF8Encoding($false)))
    try {
        & gh release create $tag $versionedSetup `
            --repo TequilaJosh/MyCraftyStash `
            --title "My Crafty Stash $fileVersion" `
            --notes-file $notesFile
        if ($LASTEXITCODE -ne 0) { throw "gh release create failed (exit $LASTEXITCODE)" }
    }
    finally {
        Remove-Item $notesFile -ErrorAction SilentlyContinue
    }
    Write-Host "==> Release published: https://github.com/TequilaJosh/MyCraftyStash/releases/tag/$tag" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Local installer:  $versionedSetup"
if (-not $SkipGitHub) {
    Write-Host "  GitHub release:   https://github.com/TequilaJosh/MyCraftyStash/releases/tag/v$fileVersion"
}
