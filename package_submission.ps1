# ==============================================================================
# SCRIPT DONG GOI TOAN BO PACKAGES & TAI LIEU NOP BAI TAP LON
# Mon hoc: New Technology and Application Development in IT
# De tai: Dual Tactics: Clash of Eras - Adaptive Prehistoric Tower Defense 3D
# ==============================================================================
param (
    [string]$GroupName = "NHOM_ADAPTIVE_TD_3D",
    [string]$OutputFolder = "Packages_Submission"
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   CONG CU DONG GOI NOP BAI TAP LON - MON CONG NGHE MOI" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan

$destDir = Join-Path $projectRoot $OutputFolder
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
}

$tempPackageDir = Join-Path $destDir "$GroupName"
if (Test-Path $tempPackageDir) {
    Remove-Item -Recurse -Force $tempPackageDir
}
New-Item -ItemType Directory -Path $tempPackageDir -Force | Out-Null

Write-Host "[1/4] Dang tong hop cac tai lieu bao cao va slide..." -ForegroundColor Green
$docFiles = @(
    "BAO_CAO_BAI_TAP_LON_ADAPTIVE_TOWER_DEFENSE_3D.md",
    "SLIDE_THUYET_TRINH_BAO_CAO.md",
    "KICH_BAN_VIDEO_DEMO_YOUTUBE.md",
    "ACADEMIC_SUBMISSION_REPORT.md",
    "TESTING_REPORT.md",
    "EVIDENCE_OF_INTEGRATION.md",
    "DOCKER_AND_CLOUD_GUIDE.md",
    "README.md"
)

$docDest = Join-Path $tempPackageDir "1_Documents_and_Reports"
New-Item -ItemType Directory -Path $docDest -Force | Out-Null
foreach ($file in $docFiles) {
    $src = Join-Path $projectRoot $file
    if (Test-Path $src) {
        Copy-Item $src -Destination $docDest -Force
        Write-Host "  -> Da sao chep: $file" -ForegroundColor Gray
    }
}

Write-Host "[2/4] Dang dong goi Backend & Docker Engine..." -ForegroundColor Green
$backendDest = Join-Path $tempPackageDir "2_Backend_Docker_Cloud"
New-Item -ItemType Directory -Path $backendDest -Force | Out-Null
Copy-Item (Join-Path $projectRoot "backend") -Destination $backendDest -Recurse -Force
Copy-Item (Join-Path $projectRoot "docker-compose.yml") -Destination $backendDest -Force
Copy-Item (Join-Path $projectRoot "run_backend.ps1") -Destination $backendDest -Force
Write-Host "  -> Da dong goi Backend, Dockerfile, Compose va Model Weights." -ForegroundColor Gray

Write-Host "[3/4] Dang kiem tra va tong hop ban Build Standalone..." -ForegroundColor Green
$buildsDest = Join-Path $tempPackageDir "3_Builds_Windows_and_Android"
New-Item -ItemType Directory -Path $buildsDest -Force | Out-Null

$windowsBuild = Join-Path $projectRoot "Builds\Windows"
if (Test-Path $windowsBuild) {
    Copy-Item $windowsBuild -Destination $buildsDest -Recurse -Force
    Write-Host "  -> Da sao chep ban Windows (.exe)" -ForegroundColor Gray
} else {
    Write-Host "  [!] Chua thay ban Windows trong Builds\Windows (Chay .\build_game.ps1 de tao)." -ForegroundColor Yellow
}

$androidBuild = Join-Path $projectRoot "Builds\Android\DualTactics_Android.apk"
if (Test-Path $androidBuild) {
    Copy-Item $androidBuild -Destination $buildsDest -Force
    Write-Host "  -> Da sao chep ban Android (.apk)" -ForegroundColor Gray
}

Write-Host "[4/4] Dang tao file thong tin Link Youtube va phan cong thanh vien..." -ForegroundColor Green
$infoContent = @"
================================================================================
THONG TIN NOP BAI TAP LON - CONG NGHE MOI TRONG CNTT
De tai: Dual Tactics: Clash of Eras - Adaptive Prehistoric Tower Defense 3D
================================================================================

1. LINK VIDEO DEMO YOUTUBE:
- Link Youtube: https://youtu.be/DEMO_DUAL_TACTICS_2026 (Thay bang link video thuc te cua nhom)
- Thoi luong: 7 - 10 phut
- Chat luong: Full HD 1080p 60fps

2. DANH SACH THANH VIEN & PHAN CONG NHIEM VU:
- Mai Van Long (Nhom truong - MSSV: 23730631) - Ty le dong gop: 100%
  Nhiem vu: Tong the kien truc, Game AI Threat-Aware A*, AR Tabletop 6-DOF, On-Device Neural Network.
- Thanh vien 2: Do hoa 3D, URP Shaders, Level Design 3 Maps & Dot bien Nui lua.
- Thanh vien 3: Backend Python FastAPI, Docker Compose, Model Pytorch & Testing Suite.
- Thanh vien 4: Mobile Touch Controller, Toi uu hoa URP Mobile, UI Canvas & Am thanh.
- Thanh vien 5: Kiem thu QA, Lap bao cao, Soan thao Slide thuyet trinh & Dung video Demo.

3. CAU TRUC GOI PACKAGES NOP BAI:
- 1_Documents_and_Reports: File Bao cao BTL (Word/PDF/MD), Slide thuyet trinh, Kich ban Youtube.
- 2_Backend_Docker_Cloud: Toan bo server FastAPI, PyTorch model, Dockerfile, docker-compose.
- 3_Builds_Windows_and_Android: Ban chay truc tiep Windows .exe va Android .apk.
- AdaptiveTowerDefense3D_CompletePackage.unitypackage: Package source code asset trong Unity.
"@
$infoPath = Join-Path $tempPackageDir "THONG_TIN_NOP_BAI_VA_LINK_YOUTUBE.txt"
Set-Content -Path $infoPath -Value $infoContent -Encoding UTF8

# Nen thanh file ZIP tong the
$finalZip = Join-Path $destDir "$GroupName`_SUBMISSION.zip"
Write-Host ""
Write-Host "Dang nén toàn bộ gói nộp bài thành file ZIP: $finalZip ..." -ForegroundColor Cyan
if (Test-Path $finalZip) {
    Remove-Item $finalZip -Force
}
Compress-Archive -Path "$tempPackageDir\*" -DestinationPath $finalZip -CompressionLevel Optimal

Write-Host "================================================================" -ForegroundColor Green
Write-Host "[THANH CONG] DA TAO HOAN TAT GOI PACKAGES NOP BAI TAP LON!" -ForegroundColor Green
Write-Host "Vi tri file: $finalZip" -ForegroundColor White
$zipSize = [math]::Round((Get-Item $finalZip).Length / 1MB, 2)
Write-Host "Dung luong: $zipSize MB" -ForegroundColor White
Write-Host "================================================================" -ForegroundColor Green

# Mo thu muc ket qua
explorer.exe "/select,$finalZip"
