#!/usr/bin/env pwsh
param (
    [Parameter(Mandatory = $true, HelpMessage = "Enter the name of the migration")]
    [string]$MigrationName
)

$apiServerPath = "src/Solace.ApiServer"

if (Test-Path $apiServerPath) {
    Write-Host "Changing directory to $apiServerPath..." -ForegroundColor Cyan
    Push-Location $apiServerPath
} else {
    Write-Error "Error: The directory '$apiServerPath' could not be found. Please run this script from the project root."
    exit 1
}

try {
    Write-Host "Creating SQLite migration: '$MigrationName'..." -ForegroundColor Green
    $env:EF_PROVIDER = "Sqlite"
    dotnet ef migrations add $MigrationName --project ../Solace.DB.Sqlite --context EarthDbContext

    Write-Host "Creating Postgres migration: '$MigrationName'..." -ForegroundColor Green
    $env:EF_PROVIDER = "Postgres"
    dotnet ef migrations add $MigrationName --project ../Solace.DB.Postgres --context EarthDbContext

    Write-Host "Both migrations created successfully!" -ForegroundColor Cyan
}
catch {
    Write-Error "An error occurred while generating the migrations: $_"
}
finally {
    Pop-Location
}