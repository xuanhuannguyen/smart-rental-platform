# reset-demo.ps1
# Reset the local database back to the demo scenario from a completely clean schema.

param(
    [string]$TargetMigration = "20260715181000_NormalizeDemoLandlordKycNames",
    [int]$ApiPort = 5294,
    [switch]$ClearUploads,
    [switch]$SkipApiStart
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjPath = Join-Path $ScriptDir "src\SmartRentalPlatform.Infrastructure"
$StartProjPath = Join-Path $ScriptDir "src\SmartRentalPlatform.Api"
$ApiProjectFile = Join-Path $StartProjPath "SmartRentalPlatform.Api.csproj"
$UploadsDir = Join-Path $StartProjPath "wwwroot\uploads"

function Invoke-Step {
    param(
        [string]$Title,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host $Title -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Title"
    }
}

function Stop-ApiProcesses {
    Write-Host "Stopping SmartRentalPlatform API processes..." -ForegroundColor Yellow

    Get-Process -Name SmartRentalPlatform.Api -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue

    $escapedProject = [Regex]::Escape($ApiProjectFile)
    $dotnetApiProcesses = Get-CimInstance Win32_Process -Filter "name = 'dotnet.exe'" |
        Where-Object {
            $_.CommandLine -match $escapedProject -or
            $_.CommandLine -match "SmartRentalPlatform.Api.csproj" -or
            $_.CommandLine -match "--urls\s+http://localhost:$ApiPort"
        }

    foreach ($process in $dotnetApiProcesses) {
        Write-Host "Stopping dotnet API process $($process.ProcessId)..." -ForegroundColor Yellow
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Clear-UploadedFiles {
    if (-not $ClearUploads) {
        Write-Host "Skipping uploads cleanup. Pass -ClearUploads to delete user-uploaded files." -ForegroundColor DarkYellow
        return
    }

    if (-not (Test-Path $UploadsDir)) {
        Write-Host "Uploads directory does not exist: $UploadsDir" -ForegroundColor DarkYellow
        return
    }

    Write-Host "Clearing uploaded files under $UploadsDir, keeping uploads\demo..." -ForegroundColor Cyan

    Get-ChildItem -LiteralPath $UploadsDir -Force |
        Where-Object { $_.Name -ne "demo" } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Recurse -Force
        }
}

Stop-ApiProcesses

Invoke-Step "Reverting database to migration 0 by dropping all tables and constraints." {
    # Check if smart_rental_postgres container is running
    $DockerRunning = (docker ps --filter "name=smart_rental_postgres" --format "{{.Names}}") -eq "smart_rental_postgres"
    if ($DockerRunning) {
        Write-Host "Dropping public schema inside smart_rental_postgres container..." -ForegroundColor Cyan
        docker exec -i smart_rental_postgres psql -U postgres -d smart_rental_platform -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public; GRANT ALL ON SCHEMA public TO postgres; GRANT ALL ON SCHEMA public TO public;"
    } else {
        Write-Host "smart_rental_postgres container not running. Falling back to dotnet ef database update 0..." -ForegroundColor Yellow
        dotnet ef database update 0 --project $ProjPath --startup-project $StartProjPath
    }
}


Clear-UploadedFiles

Invoke-Step "Applying migrations back to $TargetMigration." {
    dotnet ef database update $TargetMigration --project $ProjPath --startup-project $StartProjPath
}

if ($SkipApiStart) {
    Write-Host ""
    Write-Host "Reset complete. API restart skipped by -SkipApiStart." -ForegroundColor Green
    return
}

Write-Host ""
Write-Host "Starting API Server on http://localhost:$ApiPort ..." -ForegroundColor Green
dotnet run --project $StartProjPath --urls "http://localhost:$ApiPort"
