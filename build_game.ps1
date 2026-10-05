# ==============================================================================
# SCRIPT BUILD TU DONG DU AN ADAPTIVE TOWER DEFENSE 3D THANH FILE .EXE
# ==============================================================================
param (
    [switch]$Run,
    [switch]$Dev,
    [string]$OutputPath = "Builds\Windows\AdaptiveTowerDefense3D.exe"
)

$ErrorActionPreference = "Stop"
$projectPath = $PSScriptRoot

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   PREHISTORIC TOWER DEFENSE 3D - WINDOWS EXE BUILD TOOL" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan

# 1. Tim duong dan Unity.exe
$unityExe = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"
if (-not (Test-Path $unityExe)) {
    $hubPaths = Get-ChildItem "C:\Program Files\Unity\Hub\Editor" -Recurse -Filter "Unity.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($hubPaths) {
        $unityExe = $hubPaths.FullName
    }
}

if (-not (Test-Path $unityExe)) {
    Write-Host "[!] Khong tim thay Unity.exe tren he thong!" -ForegroundColor Red
    Write-Host "Vui long mo Unity Editor va chon menu:" -ForegroundColor Yellow
    Write-Host "  -> Prehistoric TD -> Xuat File Game (.exe)..." -ForegroundColor Green
    exit 1
}

Write-Host "[OK] Tim thay Unity: $unityExe" -ForegroundColor Green

# 2. Kiem tra xem Unity Editor co dang mo project khong
$runningUnity = Get-Process -Name "Unity" -ErrorAction SilentlyContinue
if ($runningUnity) {
    Write-Host ""
    Write-Host "[CANH BAO] PHAT HIEN UNITY EDITOR DANG MO PROJECT!" -ForegroundColor Yellow
    Write-Host "Vi Unity Editor dang giu lock project, ban co 2 cach build rat de:" -ForegroundColor Cyan
    Write-Host "  [CACH 1] (Khuyen nghi - Nhanh nhat): Vao cua so Unity Editor dang mo, bam menu:" -ForegroundColor White
    Write-Host "     'Prehistoric TD' -> 'Xuat File Game (.exe)...' -> Bam 'TIEN HANH BUILD FILE .EXE'!" -ForegroundColor Green
    Write-Host "  [CACH 2]: Luu scene va Tat Unity Editor, sau do chay lai file build_game.ps1 nay." -ForegroundColor White
    Write-Host ""

    $fullExe = Join-Path $projectPath $OutputPath
    if ($Run -and (Test-Path $fullExe)) {
        Write-Host "Dang khoi chay ban .exe san co: $fullExe" -ForegroundColor Green
        Start-Process $fullExe
        exit 0
    }
    exit 0
}

# 3. Tien hanh build batch mode neu Unity Editor da dong
$fullOutput = [System.IO.Path]::GetFullPath((Join-Path $projectPath $OutputPath))
$outDir = [System.IO.Path]::GetDirectoryName($fullOutput)
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$logFile = Join-Path $projectPath "Logs\build.log"
if (-not (Test-Path (Join-Path $projectPath "Logs"))) {
    New-Item -ItemType Directory -Path (Join-Path $projectPath "Logs") -Force | Out-Null
}

Write-Host "Dang tien hanh bien dich game sang: $fullOutput ..." -ForegroundColor Cyan
Write-Host "Qua trinh nay mat khoang 1-3 phut..." -ForegroundColor Gray

$buildArgs = @(
    "-quit",
    "-batchmode",
    "-projectPath", "`"$projectPath`"",
    "-executeMethod", "LlamAcademy.Dinos.Editor.PrehistoricBuildTool.CommandLineBuild",
    "-outputPath", "`"$fullOutput`"",
    "-logFile", "`"$logFile`""
)

$proc = Start-Process -FilePath $unityExe -ArgumentList $buildArgs -Wait -PassThru -NoNewWindow

if ($proc.ExitCode -eq 0 -and (Test-Path $fullOutput)) {
    $fileInfo = Get-Item $fullOutput
    $sizeMb = [math]::Round($fileInfo.Length / 1MB, 1)
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "[THANH CONG] XUAT FILE GAME .EXE HOAN TAT!" -ForegroundColor Green
    Write-Host "Vi tri: $fullOutput" -ForegroundColor White
    Write-Host "Dung luong: $sizeMb MB" -ForegroundColor White
    Write-Host "================================================================" -ForegroundColor Green

    if ($Run) {
        Write-Host "Dang khoi chay game..." -ForegroundColor Cyan
        Start-Process $fullOutput
    } else {
        explorer.exe "/select,$fullOutput"
    }
} else {
    Write-Host ""
    Write-Host "[LOI] Qua trinh build khong thanh cong (Exit code: $($proc.ExitCode))!" -ForegroundColor Red
    Write-Host "Kiem tra log tai: $logFile" -ForegroundColor Yellow
    if (Test-Path $logFile) {
        Get-Content $logFile -Tail 25
    }
}
