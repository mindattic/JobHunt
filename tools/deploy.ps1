#Requires -Version 5.1
<#
.SYNOPSIS
    Shuts down any running JobHunt, clears the build cache, rebuilds, and publishes a standalone
    Release copy to C:\Apps\JobHunt\ — the same process Automata (tools\deploy.ps1) and KdpPublish
    use, so a double-click always runs current source rather than a stale build.

.DESCRIPTION
    1. Stops any running JobHunt.App.exe.
    2. Removes bin/obj for the App, Core and Tests projects — a clean rebuild, not an incremental
       one. BUILD cache only: the data under %LocalAppData%\MindAttic\JobHunt\ (the database, logs
       and each applicant's LinkedIn sign-in profile) and the documents under
       Documents\JobHunt\ are never touched, so nothing a user entered is lost by redeploying.
    3. dotnet publish (Release, win-x64, framework-dependent single file) -> C:\Apps\JobHunt\.
       wwwroot (the panel) publishes as loose files beside the exe; the app reads them from disk.
    4. Writes C:\Apps\JobHunt\launch.bat, which calls this deploy.ps1 (by its baked-in source path)
       before every launch. If the source repo has moved, it warns and launches what is deployed.

.PARAMETER Launch
    After publishing, start the deployed JobHunt.App.exe.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 -Launch
#>
param([switch]$Launch)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir = Split-Path $PSScriptRoot   # tools\ -> JobHunt\
$proj    = Join-Path $repoDir 'JobHunt.App\JobHunt.App.csproj'
$out     = 'C:\Apps\JobHunt'
$exeName = 'JobHunt.App.exe'

# ── Stop running instance ──────────────────────────────────────────────────
Write-Host ''
Write-Host '  Stopping running instance...' -ForegroundColor Yellow
$procs = Get-Process 'JobHunt.App' -ErrorAction SilentlyContinue
if ($procs) {
    $procs | Stop-Process -Force
    Write-Host "    Stopped ($(@($procs).Count) process(es))" -ForegroundColor DarkYellow
    Start-Sleep -Milliseconds 800
} else {
    Write-Host '    Nothing running.' -ForegroundColor DarkGray
}

# ── Clear build cache ──────────────────────────────────────────────────────
# bin/obj only — never %LocalAppData%\MindAttic\JobHunt\ (database, logs, LinkedIn sign-ins) or
# Documents\JobHunt\ (tailored résumés and cover letters).
Write-Host ''
Write-Host '  Clearing build cache (bin/obj)...' -ForegroundColor Cyan
foreach ($rel in @('JobHunt.App', 'JobHunt.Core', 'JobHunt.Tests')) {
    $projRoot = Join-Path $repoDir $rel
    if (-not (Test-Path $projRoot)) { continue }
    foreach ($d in @((Join-Path $projRoot 'bin'), (Join-Path $projRoot 'obj'))) {
        if (Test-Path $d) {
            try { Remove-Item -Recurse -Force $d -ErrorAction Stop }
            catch { Write-Host "    (could not fully remove $d : $($_.Exception.Message))" -ForegroundColor DarkYellow }
        }
    }
}

# ── Publish ─────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host "  Publishing $proj" -ForegroundColor Cyan
Write-Host "    -> $out\$exeName"

dotnet publish $proj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    --output $out | Out-Host

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed (exit $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

$exe = Join-Path $out $exeName
if (-not (Test-Path $exe)) {
    Write-Host "Publish completed but $exeName not found at $exe." -ForegroundColor Red
    exit 1
}

# ── Write self-redeploying launcher ─────────────────────────────────────────
# Always redeploys from source before launching, so a double-click can never run a stale build.
# $PSScriptRoot is baked in as an absolute path so it resolves wherever launch.bat is started from.
$deployPs1Path = Join-Path $PSScriptRoot 'deploy.ps1'
$launchBatPath = Join-Path $out 'launch.bat'
@(
    '@echo off',
    'title JobHunt',
    "set `"DEPLOY_PS1=$deployPs1Path`"",
    'if exist "%DEPLOY_PS1%" (',
    '    echo Redeploying latest build from source...',
    '    powershell -NoProfile -ExecutionPolicy Bypass -File "%DEPLOY_PS1%"',
    '    if errorlevel 1 echo Redeploy failed - launching whatever is already in this folder instead.',
    ') else (',
    '    echo Source repo not found at "%DEPLOY_PS1%" - launching existing build without redeploying.',
    ')',
    "start `"`" `"%~dp0$exeName`""
) | Set-Content $launchBatPath -Encoding ascii

# ── Summary ────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '  Published successfully.' -ForegroundColor Green
Write-Host "    Exe    : $exe ($([math]::Round(((Get-Item $exe).Length / 1MB), 1)) MB)" -ForegroundColor Gray
Write-Host "    Launch : $launchBatPath" -ForegroundColor Gray
Write-Host ''

if ($Launch) {
    Write-Host '  Launching...' -ForegroundColor Cyan
    Start-Process $exe
}
