# Builds the Room Configurator scene (TV, chair, table) for WebGL using Imagine WebAR World Tracker.
# Close the Unity editor first (it locks the project), then run:  .\build-room.ps1
param(
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.3.12f1\Editor\Unity.exe",
    [switch]$SkipSetup
)
$ErrorActionPreference = "Stop"
$proj = $PSScriptRoot
$logDir = Join-Path $proj "Logs"
New-Item -ItemType Directory -Force $logDir | Out-Null

if (Test-Path (Join-Path $proj "Temp\UnityLockfile")) {
    Write-Host "Unity has this project open. Close the editor and run again." -ForegroundColor Yellow
    exit 1
}

if (-not $SkipSetup) {
    Write-Host "Step 1/2: configure project + generate room models and scene"
    & $UnityExe -batchmode -quit -nographics -projectPath $proj -buildTarget WebGL `
        -executeMethod RayonixAR.RoomSetup.RoomConfiguratorSetup.BatchSetup `
        -logFile (Join-Path $logDir "room-setup.log") | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host "Setup failed, see Logs\room-setup.log" -ForegroundColor Red; exit $LASTEXITCODE }
}

Write-Host "Step 2/2: build WebGL (this takes several minutes)"
& $UnityExe -batchmode -quit -nographics -projectPath $proj -buildTarget WebGL `
    -executeMethod RayonixAR.RoomSetup.RoomConfiguratorSetup.BatchBuild `
    -logFile (Join-Path $logDir "room-build.log") | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed, see Logs\room-build.log" -ForegroundColor Red; exit $LASTEXITCODE }

Write-Host "Done. Build is in Builds\RoomWebGL and mirrored to webar\room" -ForegroundColor Green
