<#
.SYNOPSIS
    Builds the Workspace Session Restorer installer (.exe).
.DESCRIPTION
    1. Detects and handles any running instances of WorkspaceRestorer.
    2. dotnet publish  (single-file win-x64 exe -> publish\)
    3. ISCC.exe        (compiles installer\WorkspaceRestorer.iss -> artifacts\)
.PARAMETER Configuration
    Build configuration: 'Release' (default) or 'Debug'.
.PARAMETER Version
    Custom version string (e.g. 1.0.1). If omitted, automatically read from WorkspaceRestorer.csproj.
.PARAMETER KillRunning
    Automatically terminates running WorkspaceRestorer processes to prevent file lock errors during publish.
.PARAMETER SkipPublish
    Skips the dotnet publish step and only builds the installer from existing publish\ files.
.PARAMETER Clean
    Cleans publish and build artifacts prior to building.
.EXAMPLE
    .\installer\build-installer.ps1
.EXAMPLE
    .\installer\build-installer.ps1 -KillRunning
.EXAMPLE
    .\installer\build-installer.ps1 -Configuration Release -Version 1.0.1
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Version,

    [switch]$KillRunning,

    [switch]$SkipPublish,

    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$root = Split-Path -Parent $PSScriptRoot   # repo root (installer\ -> repo)
$exeName = 'WorkspaceRestorer.exe'
$publishDir = Join-Path $root 'publish'
$artifactsDir = Join-Path $root 'artifacts'
$csprojPath = Join-Path $root 'src\WorkspaceRestorer\WorkspaceRestorer.csproj'

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Workspace Session Restorer — Installer Build Pipeline" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# ---- 1. Extract / Validate Version ----
if (-not $Version) {
    if (Test-Path $csprojPath) {
        [xml]$csprojXml = Get-Content $csprojPath
        $detectedVersion = $csprojXml.Project.PropertyGroup.Version | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
        if ($detectedVersion) {
            $Version = $detectedVersion.Trim()
        }
    }
}
if (-not $Version) {
    $Version = '1.0.0'
}
Write-Host "Target Version : $Version" -ForegroundColor Gray
Write-Host "Configuration  : $Configuration" -ForegroundColor Gray

# ---- 2. Locate ISCC.exe ----
Write-Host "`n[1/4] Locating Inno Setup compiler (ISCC.exe)..." -ForegroundColor Yellow

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe"
)

# Also check registry keys if not found
$regPaths = @(
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1',
    'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1',
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 7_is1'
)
foreach ($reg in $regPaths) {
    if (Test-Path $reg) {
        $installLocation = (Get-ItemProperty -Path $reg -Name 'InstallLocation' -ErrorAction SilentlyContinue).InstallLocation
        if ($installLocation) {
            $isccCandidates += Join-Path $installLocation 'ISCC.exe'
        }
    }
}

$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}
if (-not $iscc) {
    throw "ISCC.exe not found. Please install Inno Setup 6 or 7:`n  winget install JRSoftware.InnoSetup"
}
Write-Host "  Found ISCC: $iscc" -ForegroundColor Green

# ---- 3. Handle Running Instances & Clean ----
Write-Host "`n[2/4] Checking running instances and preparation..." -ForegroundColor Yellow

$runningProcesses = Get-Process -Name 'WorkspaceRestorer' -ErrorAction SilentlyContinue
if ($runningProcesses) {
    $pids = ($runningProcesses | ForEach-Object { $_.Id }) -join ', '
    if ($KillRunning) {
        Write-Host "  Terminating running WorkspaceRestorer instance(s) (PID: $pids)..." -ForegroundColor Magenta
        $runningProcesses | Stop-Process -Force
        Start-Sleep -Milliseconds 600
    } else {
        # Check if running process locks the published exe
        $isPublishedLocked = $false
        foreach ($proc in $runningProcesses) {
            try {
                if ($proc.MainModule.FileName -like "$publishDir*") {
                    $isPublishedLocked = $true
                    break
                }
            } catch { }
        }

        if ($isPublishedLocked) {
            if ([Environment]::UserInteractive -and -not [Console]::IsInputRedirected) {
                Write-Warning "WorkspaceRestorer is currently running (PID: $pids) from '$publishDir'."
                $response = Read-Host "  Close process automatically to allow publishing? (Y/N)"
                if ($response -match '^(y|yes)$') {
                    $runningProcesses | Stop-Process -Force
                    Start-Sleep -Milliseconds 600
                } else {
                    throw "Build aborted: WorkspaceRestorer.exe is in use. Please close it and rerun."
                }
            } else {
                throw "WorkspaceRestorer is currently running (PID: $pids) and locking '$publishDir\$exeName'. Re-run with -KillRunning or exit the application first."
            }
        } else {
            Write-Host "  Notice: WorkspaceRestorer is running elsewhere (PID: $pids)." -ForegroundColor Gray
        }
    }
}

if ($Clean) {
    Write-Host "  Cleaning publish and artifacts folders..." -ForegroundColor Gray
    if (Test-Path $publishDir) { Remove-Item -Path "$publishDir\*" -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $artifactsDir) { Remove-Item -Path "$artifactsDir\WorkspaceRestorer-Setup-*.exe" -Force -ErrorAction SilentlyContinue }
}

if (-not (Test-Path $artifactsDir)) {
    New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
}
if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
}

# ---- 4. Publish the app ----
if (-not $SkipPublish) {
    Write-Host "`n[3/4] Publishing application ($($Configuration) - win-x64, single-file, self-contained)..." -ForegroundColor Yellow
    $publishArgs = @(
        'publish', $csprojPath,
        '-c', $Configuration,
        '-r', 'win-x64',
        '-p:PublishSingleFile=true',
        '--self-contained', 'true',
        "-p:Version=$Version",
        '-o', $publishDir
    )
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
} else {
    Write-Host "`n[3/4] Skipping publish as requested (-SkipPublish)..." -ForegroundColor Gray
}

$publishedExe = Join-Path $publishDir $exeName
if (-not (Test-Path $publishedExe)) {
    throw "Expected published executable missing: $publishedExe"
}
$exeSizeMB = [math]::Round(((Get-Item $publishedExe).Length / 1MB), 2)
Write-Host "  Published: $publishedExe ($($exeSizeMB) MB)" -ForegroundColor Green

# ---- 5. Compile installer with Inno Setup ----
Write-Host "`n[4/4] Compiling installer with ISCC..." -ForegroundColor Yellow
$issFile = Join-Path $root 'installer\WorkspaceRestorer.iss'
if (-not (Test-Path $issFile)) {
    throw "Inno script not found: $issFile"
}

Push-Location (Join-Path $root 'installer')
try {
    # Pass version override dynamically to Inno Setup
    & $iscc "/DMyAppVersion=$Version" $issFile
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}

# ---- 6. Summary Report ----
$stopwatch.Stop()
$installerName = "WorkspaceRestorer-Setup-$Version.exe"
$installerFile = Join-Path $artifactsDir $installerName

if (-not (Test-Path $installerFile)) {
    # Fallback search if version name was modified
    $installerFile = (Get-ChildItem (Join-Path $artifactsDir 'WorkspaceRestorer-Setup-*.exe') |
                      Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

if ($installerFile -and (Test-Path $installerFile)) {
    $installerItem = Get-Item $installerFile
    $sizeMB = [math]::Round(($installerItem.Length / 1MB), 2)
    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host " BUILD SUCCESSFUL!" -ForegroundColor Green
    Write-Host " Output Installer : $($installerItem.FullName)" -ForegroundColor Green
    Write-Host " File Size        : $sizeMB MB" -ForegroundColor Green
    Write-Host " Target Version   : $Version" -ForegroundColor Green
    Write-Host " Elapsed Time     : $([math]::Round($stopwatch.Elapsed.TotalSeconds, 1))s" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
} else {
    throw "Installer executable not found in $artifactsDir"
}
