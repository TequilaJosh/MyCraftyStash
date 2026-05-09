#requires -Version 5.1
<#
    publish.ps1 - one-shot publish of My Crafty Stash.

    1. Bump <FileVersion> + <AssemblyVersion> in the csproj (revision by default).
       This is what the in-app updater compares against, so it has to bump or
       no one sees the new build.
    2. dotnet publish (Release, win-x64, self-contained, single-file).
    3. Run Inno Setup compiler against Installer\MyCraftyStash.iss
       - emits  Installer\output\MyCraftyStash_Setup_X.Y.Z.W.exe
    4. Copy that versioned setup to \\Win-u5iq2hisnh3\e\Installation\setup.exe
       and write version.txt next to it. The in-app updater (App.xaml.cs +
       Services\UpdateService.cs) reads version.txt on startup, compares to
       the running assembly version, and offers to launch setup.exe when a
       newer build is available.

    Usage (from repo root or anywhere):
        .\Installer\publish.ps1
        .\Installer\publish.ps1 -BumpPart Patch          # bump 1.0.X.0 instead of revision
        .\Installer\publish.ps1 -BumpPart Minor          # bump 1.X.0.0
        .\Installer\publish.ps1 -BumpPart Major          # bump X.0.0.0
        .\Installer\publish.ps1 -SetVersion "1.0.3.0"    # explicit version
        .\Installer\publish.ps1 -SkipBump                # republish current version
        .\Installer\publish.ps1 -SkipPublish             # iscc + share copy only
        .\Installer\publish.ps1 -SkipShare               # build locally, don't touch the share
        .\Installer\publish.ps1 -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
#>

[CmdletBinding()]
param(
    [ValidateSet('Major','Minor','Patch','Revision')]
    [string]$BumpPart = 'Revision',
    [string]$SetVersion,
    [switch]$SkipBump,
    [switch]$SkipPublish,
    [switch]$SkipShare,
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'

$repoRoot      = Split-Path -Parent $PSScriptRoot
$csproj        = Join-Path $repoRoot 'MyCraftyStash.csproj'
$issPath       = Join-Path $PSScriptRoot 'MyCraftyStash.iss'
$publishDir    = Join-Path $repoRoot 'bin\Release\net8.0-windows10.0.19041.0\win-x64\publish'
$exePath       = Join-Path $publishDir 'MyCraftyStash.exe'
$installerOut  = Join-Path $PSScriptRoot 'output'
$installShare    = '\\Win-u5iq2hisnh3\e\Installation'
$shareSetup      = Join-Path $installShare 'setup.exe'
$shareVersion    = Join-Path $installShare 'version.txt'
$shareChangelog  = Join-Path $installShare 'changelog.txt'
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

# ── 5. Push setup.exe + version.txt to the share for the in-app updater ─────
if ($SkipShare) {
    Write-Host "==> Skipping share publish (-SkipShare)" -ForegroundColor DarkYellow
}
elseif (-not (Test-Path $installShare)) {
    Write-Warning "Install share $installShare is not reachable - skipping share publish. Connect to the network and re-run with -SkipPublish -SkipBump to push the existing build."
}
else {
    Write-Host "==> Copying setup.exe to share ..." -ForegroundColor Cyan
    Copy-Item -Path $versionedSetup -Destination $shareSetup -Force

    Write-Host "==> Writing version.txt = $fileVersion" -ForegroundColor Cyan
    # Plain ASCII, no BOM - UpdateService trims and Version.TryParse's,
    # which would choke on a UTF-8 BOM.
    [System.IO.File]::WriteAllText($shareVersion, $fileVersion, [System.Text.Encoding]::ASCII)

    # Push the release-notes file too so the in-app "What's New" popup has
    # something to read on next launch. Skipped silently when there's no
    # local changelog.txt (you can still publish without one).
    if (Test-Path $localChangelog) {
        Write-Host "==> Copying changelog.txt to share ..." -ForegroundColor Cyan
        Copy-Item -Path $localChangelog -Destination $shareChangelog -Force
    }
    else {
        Write-Warning "No Installer\changelog.txt found - skipping notes upload. Add one before publishing if you want users to see release notes."
    }
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Local installer:  $versionedSetup"
if (-not $SkipShare -and (Test-Path $shareSetup)) {
    Write-Host "  Share setup.exe:  $shareSetup"
    Write-Host "  Share version:    $fileVersion"
    if (Test-Path $shareChangelog) {
        Write-Host "  Share changelog:  $shareChangelog"
    }
}
