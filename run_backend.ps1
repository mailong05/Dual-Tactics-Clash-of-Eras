# ==============================================================================
# SCRIPT KHOI CHAY IO CLOUD & DEEP LEARNING AI BACKEND (DOCKER / LOCAL)
# Dual Tactics: Clash of Eras
# ==============================================================================
param (
    [switch]$Docker,
    [switch]$Local,
    [int]$Port = 8000
)

$projectPath = $PSScriptRoot
$backendDir = Join-Path $projectPath "backend"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "   IO CLOUD & DEEP LEARNING AI BACKEND LAUNCHER" -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan

# Kiem tra Docker Daemon neu duoc chon hoac mac dinh
$dockerAvailable = $false
try {
    $null = docker info 2>$null
    if ($LASTEXITCODE -eq 0) { $dockerAvailable = $true }
} catch {}

if ($Docker -or ($dockerAvailable -and -not $Local)) {
    Write-Host "[*] Phat hien Docker Engine dang hoat dong. Dang build va chay container..." -ForegroundColor Green
    Set-Location $projectPath
    docker compose up --build -d
    Write-Host ""
    Write-Host "[OK] Container 'dual_tactics_cloud_ai' da duoc khoi chay tren port $Port!" -ForegroundColor Green
    Write-Host "  -> Health Endpoint: http://localhost:$Port/health" -ForegroundColor Cyan
    Write-Host "  -> Swagger Docs:    http://localhost:$Port/docs" -ForegroundColor Cyan
    exit 0
}

Write-Host "[*] Dang khoi chay IO Cloud Backend qua Python truc tiep..." -ForegroundColor Yellow
$pythonExe = "C:\Users\VanLong\AppData\Local\Programs\Python\Python310\python.exe"
if (-not (Test-Path $pythonExe)) {
    $pythonExe = "python"
}

Set-Location $backendDir
Write-Host "[*] Executing: $pythonExe -m uvicorn app:app --host 0.0.0.0 --port $Port --reload" -ForegroundColor Gray
Start-Process -FilePath $pythonExe -ArgumentList "-m uvicorn app:app --host 0.0.0.0 --port $Port --reload"
Write-Host ""
Write-Host "[OK] IO Cloud Backend da khoi chay thanh cong tren port $Port!" -ForegroundColor Green
Write-Host "  -> Health Endpoint: http://localhost:$Port/health" -ForegroundColor Cyan
Write-Host "  -> Swagger Docs:    http://localhost:$Port/docs" -ForegroundColor Cyan
Write-Host "  -> Unity Client:    IOCloudManager se tu dong ket noi." -ForegroundColor Green
Set-Location $projectPath
