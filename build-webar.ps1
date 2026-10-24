# Builds the Rayonix AR test scene for WebGL using Imagine WebAR World Tracker.
# Close the Unity editor first (it locks the project), then run:  .\build-webar.ps1
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
    Write-Host "Step 1/2: configure project + create AR test scene"
    & $UnityExe -batchmode -quit -nographics -projectPath $proj -buildTarget WebGL `
        -executeMethod RayonixAR.Editor.ARTestSetup.BatchConfigureAndScene `
        -logFile (Join-Path $logDir "webar-setup.log") | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host "Setup failed, see Logs\webar-setup.log" -ForegroundColor Red; exit $LASTEXITCODE }
}

Write-Host "Step 2/2: build WebGL (this takes several minutes)"
& $UnityExe -batchmode -quit -nographics -projectPath $proj -buildTarget WebGL `
    -executeMethod RayonixAR.Editor.ARTestSetup.BatchBuild `
    -logFile (Join-Path $logDir "webar-build.log") | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed, see Logs\webar-build.log" -ForegroundColor Red; exit $LASTEXITCODE }

Write-Host "Done. Build is in Builds\WebGL and mirrored to webar\unity" -ForegroundColor Green
