param(
    [switch]$SkipIntegrationTests,
    [switch]$SkipFrontendTests,
    [switch]$SkipFrontendBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action

    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Name"
    }
}

function Assert-Prerequisite {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not (Test-Path $Path)) {
        throw $Message
    }
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Push-Location $repoRoot
try {
    Assert-Prerequisite `
        -Path (Join-Path $repoRoot 'server\src\SmartRentalPlatform.Api\obj\project.assets.json') `
        -Message "Missing backend restore artifacts. Run 'dotnet restore server/SmartRentalPlatform.slnx' once in the current shell before running quality gate."

    Assert-Prerequisite `
        -Path (Join-Path $repoRoot 'server\tests\SmartRentalPlatform.UnitTests\obj\project.assets.json') `
        -Message "Missing unit test restore artifacts. Run 'dotnet restore server/SmartRentalPlatform.slnx' once in the current shell before running quality gate."

    if (-not $SkipIntegrationTests) {
        Assert-Prerequisite `
            -Path (Join-Path $repoRoot 'server\tests\SmartRentalPlatform.IntegrationTests\obj\project.assets.json') `
            -Message "Missing integration test restore artifacts. Run 'dotnet restore server/SmartRentalPlatform.slnx' once in the current shell before running quality gate."
    }

    Invoke-Step "Build backend solution" {
        dotnet build server/SmartRentalPlatform.slnx --no-restore
    }

    Invoke-Step "Run backend unit tests" {
        dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --no-build --no-restore
    }

    if (-not $SkipIntegrationTests) {
        Invoke-Step "Run backend integration tests" {
            dotnet test server/tests/SmartRentalPlatform.IntegrationTests/SmartRentalPlatform.IntegrationTests.csproj --no-build --no-restore
        }
    }

    Push-Location (Join-Path $repoRoot 'client')
    try {
        if ((-not $SkipFrontendTests) -or (-not $SkipFrontendBuild)) {
            Assert-Prerequisite `
                -Path (Join-Path $repoRoot 'client\node_modules') `
                -Message "Missing frontend dependencies. Run 'npm install' in client before running quality gate."
        }

        if (-not $SkipFrontendTests) {
            Invoke-Step "Run frontend Vitest suite" {
                npm.cmd run test:run
            }
        }

        if (-not $SkipFrontendBuild) {
            Invoke-Step "Build frontend" {
                npm.cmd run build
            }
        }
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    Write-Host "Quality gate passed." -ForegroundColor Green
}
finally {
    Pop-Location
}
