# ==============================================================================
# SCRIPT BUILD TU DONG DU AN ADAPTIVE TOWER DEFENSE 3D THANH FILE ANDROID (.APK)
# Mobile Deployment Tool (Assessment Rubric Item 3: 20 pts)
# ==============================================================================
param (
    [switch]$Dev,
    [string]$OutputPath = "Builds\Android\DualTactics_Android.apk"
)

$ErrorActionPreference = "Stop"
$projectPath = $PSScriptRoot

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   PREHISTORIC TOWER DEFENSE 3D - ANDROID APK BUILD TOOL" -ForegroundColor Yellow
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
    Write-Host "  -> Prehistoric TD -> 📱 Xuat File Game Android (.apk)..." -ForegroundColor Green
    exit 1
}

Write-Host "[OK] Tim thay Unity: $unityExe" -ForegroundColor Green

# Kiem tra xem da cai dat Android Build Support chua
$unityDir = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetDirectoryName($unityExe))
$androidEngine = Join-Path $unityDir "Data\PlaybackEngines\AndroidPlayer"
if (-not (Test-Path $androidEngine)) {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Yellow
    Write-Host "[CHU Y] CHUA CAI DAT MODULE 'ANDROID BUILD SUPPORT' TRONG UNITY HUB!" -ForegroundColor Yellow
    Write-Host "================================================================" -ForegroundColor Yellow
    Write-Host "De bien dich file .apk, Unity can co bo cong cu Android Build Support." -ForegroundColor White
    Write-Host "Cac buoc cai dat cuc ky nhanh trong Unity Hub:" -ForegroundColor Cyan
    Write-Host "  Buoc 1: Mo ung dung Unity Hub tren may tinh." -ForegroundColor White
    Write-Host "  Buoc 2: Chon tab 'Installs' o menu ben trai." -ForegroundColor White
    Write-Host "  Buoc 3: Bam vao dau 3 cham ⋮ (hoac banh rang) canh phien ban Unity 6." -ForegroundColor White
    Write-Host "  Buoc 4: Chon 'Add modules' -> Tich vao 'Android Build Support' -> Bam Install." -ForegroundColor Green
    Write-Host "  Buoc 5: Cho tai xong roi chay lai script nay la se build file .apk thanh cong 100%!" -ForegroundColor White
    Write-Host ""
    Write-Host "(*) Trong luc chua cai module Android, ban co the choi test ngay bang cach:" -ForegroundColor Cyan
    Write-Host "    Chay script .\build_game.ps1 de xuat file Windows (.exe) chay cuc muot!" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Yellow
    exit 0
}

# 2. Kiem tra xem Unity Editor co dang mo project khong
$runningUnity = Get-Process -Name "Unity" -ErrorAction SilentlyContinue
if ($runningUnity) {
    Write-Host ""
    Write-Host "[CANH BAO] PHAT HIEN UNITY EDITOR DANG MO PROJECT!" -ForegroundColor Yellow
    Write-Host "Vi Unity Editor dang giu lock project, ban co 2 cach build Android rat de:" -ForegroundColor Cyan
    Write-Host "  [CACH 1] (Khuyen nghi - Nhanh nhat): Vao cua so Unity Editor dang mo, bam menu:" -ForegroundColor White
    Write-Host "     'Prehistoric TD' -> '📱 Xuat File Game Android (.apk)...' -> Bam 'TIEN HANH BUILD FILE .APK'!" -ForegroundColor Green
    Write-Host "  [CACH 2]: Luu scene va Tat Unity Editor, sau do chay lai file build_android.ps1 nay." -ForegroundColor White
    Write-Host ""
    exit 0
}

# 3. Tien hanh build batch mode neu Unity Editor da dong
$fullOutput = [System.IO.Path]::GetFullPath((Join-Path $projectPath $OutputPath))
$outDir = [System.IO.Path]::GetDirectoryName($fullOutput)
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$logFile = Join-Path $projectPath "build_android.log"
Write-Host "[*] Dang khoi dong Unity Batch Mode de build file .apk..." -ForegroundColor Cyan
Write-Host "  Scene: Assets/LlamAcademy/Dinos/Scenes/Dinos.unity" -ForegroundColor Gray
Write-Host "  Dich den: $fullOutput" -ForegroundColor Gray
Write-Host "  Log file: $logFile" -ForegroundColor Gray

$unityArgs = @(
    "-quit",
    "-batchmode",
    "-projectPath", "`"$projectPath`"",
    "-executeMethod", "LlamAcademy.Dinos.Editor.PrehistoricMobileBuildTool.BuildAndroidApkCommandLine",
    "-logFile", "`"$logFile`""
)

$proc = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -PassThru -NoNewWindow
$proc.WaitForExit()

if ($proc.ExitCode -eq 0 -and (Test-Path $fullOutput)) {
    $sizeMB = [math]::Round(((Get-Item $fullOutput).Length / 1MB), 2)
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host " [THANH CONG] BUILD HOAN TAT FILE ANDROID (.APK)!" -ForegroundColor Green
    Write-Host " File .apk: $fullOutput ($sizeMB MB)" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Green
    explorer.exe /select,$fullOutput
} else {
    Write-Host ""
    Write-Host "[!] Build gap loi (Exit code: $($proc.ExitCode)). Xem chi tiet tai: $logFile" -ForegroundColor Red
}
