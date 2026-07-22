param(
    [string]$ApiKey = $env:ROBOFLOW_API_KEY,
    [int]$Port = 8001
)

$ErrorActionPreference = 'Stop'
$serviceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$python = Join-Path $serviceDir '.venv\Scripts\python.exe'
$envFile = Join-Path $serviceDir '.env'

if ([string]::IsNullOrWhiteSpace($ApiKey) -and (Test-Path $envFile)) {
    $apiKeyLine = Get-Content $envFile |
        Where-Object { $_ -match '^\s*ROBOFLOW_API_KEY\s*=' } |
        Select-Object -First 1
    if ($apiKeyLine) {
        $ApiKey = ($apiKeyLine -replace '^\s*ROBOFLOW_API_KEY\s*=', '').Trim().Trim('"').Trim("'")
    }
}

if ([string]::IsNullOrWhiteSpace($ApiKey) -or
    $ApiKey -match 'YOUR|REPLACE|API_KEY_MỚI|API_KEY_MOI') {
    throw 'Thiếu ROBOFLOW_API_KEY hợp lệ. Tạo server/meter-ai/.env hoặc chạy: .\start.ps1 -ApiKey "your-real-key"'
}

$listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($listener) {
    try {
        $health = Invoke-RestMethod "http://127.0.0.1:$Port/health" -TimeoutSec 3
        if ($health.status -eq 'ok') {
            Write-Host "Meter AI đã chạy tại http://127.0.0.1:$Port (PID $($listener[0].OwningProcess))." -ForegroundColor Green
            exit 0
        }
    }
    catch {
        # The port belongs to another process; report it below.
    }

    throw "Cổng $Port đang được PID $($listener[0].OwningProcess) sử dụng. Hãy đóng tiến trình đó hoặc chọn -Port khác."
}

if (-not (Test-Path $python)) {
    # Reuse the already-installed inference SDK and isolate the web-service packages.
    python -m venv --system-site-packages (Join-Path $serviceDir '.venv')
    & $python -m pip install -r (Join-Path $serviceDir 'requirements.txt')
}

$env:ROBOFLOW_API_KEY = $ApiKey
Set-Location $serviceDir
& $python -m uvicorn app:app --host 127.0.0.1 --port $Port
