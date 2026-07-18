param(
    [string]$SnapshotPath = "database/smart_rental_demo_snapshot.dump",
    [string]$ComposeService = "postgres",
    [string]$ContainerName = "smart_rental_postgres",
    [string]$DatabaseName = "smart_rental_platform",
    [string]$DatabaseUser = "postgres"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ResolvedSnapshotPath = Resolve-Path (Join-Path $RepoRoot $SnapshotPath)
$ContainerSnapshotPath = "/tmp/smart_rental_demo_snapshot.dump"

Write-Host "Starting postgres container..."
docker compose up -d $ComposeService | Out-Host

Write-Host "Copying demo snapshot to container..."
docker cp $ResolvedSnapshotPath "${ContainerName}:${ContainerSnapshotPath}" | Out-Host

Write-Host "Terminating active database connections..."
docker compose exec -T $ComposeService psql -U $DatabaseUser -d postgres -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DatabaseName' AND pid <> pg_backend_pid();" | Out-Host

Write-Host "Dropping and recreating database '$DatabaseName'..."
docker compose exec -T $ComposeService dropdb -U $DatabaseUser --if-exists --force $DatabaseName | Out-Host
docker compose exec -T $ComposeService createdb -U $DatabaseUser $DatabaseName | Out-Host

Write-Host "Restoring demo snapshot..."
docker compose exec -T $ComposeService pg_restore -U $DatabaseUser -d $DatabaseName --no-owner --no-privileges $ContainerSnapshotPath | Out-Host

Write-Host "Demo database reset complete."
