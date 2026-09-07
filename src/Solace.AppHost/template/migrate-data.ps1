$ErrorActionPreference = 'Stop'

$TEMP_OVERRIDE = "docker-compose.import-temp-ports.yaml"

function Cleanup {
    Write-Host "Cleaning up temporary override file..."
    if (Test-Path $TEMP_OVERRIDE) {
        Remove-Item -Path $TEMP_OVERRIDE -Force -ErrorAction SilentlyContinue
    }
    
    if (Test-Path ".\down.ps1") {
        & .\down.ps1
    } else {
        & bash .\down.sh
    }
}

try {
    if (Test-Path ".\down.ps1") {
        & .\down.ps1
    } else {
        & bash .\down.sh
    }

    if (Test-Path ".env") {
        Get-Content .env | ForEach-Object {
            $line = $_.Trim()
            if ($line -and -not $line.StartsWith('#')) {
                $key, $value = $line -split '=', 2
                if ($key -and $value) {
                    $value = $value.Trim('"').Trim("'")
                    [System.Environment]::SetEnvironmentVariable($key.Trim(), $value.Trim(), "Process")
                }
            }
        }
    }

    $POSTGRES_HOST     = if ($env:POSTGRES_HOST)     { $env:POSTGRES_HOST }     else { "localhost" }
    $POSTGRES_PORT     = if ($env:POSTGRES_PORT)     { $env:POSTGRES_PORT }     else { "5432" }
    $POSTGRES_USER     = if ($env:POSTGRES_USER)     { $env:POSTGRES_USER }     else { "postgres" }
    $POSTGRES_PASSWORD = if ($env:POSTGRES_PASSWORD) { $env:POSTGRES_PASSWORD } else { "postgres" }
    $OBJECT_STORE_PORT = if ($env:OBJECT_STORE_PORT) { $env:OBJECT_STORE_PORT } else { "8080" }
    $OBJECT_STORE_ENDPOINT = "http://localhost:${OBJECT_STORE_PORT}/"

    $OLD_PATH = Read-Host -Prompt "Enter path to old installation (folder with components, data, launcher, staticdata)"

    if (-not (Test-Path -Path $OLD_PATH -PathType Container)) {
        Write-Error "Error: Path '$OLD_PATH' does not exist."
        exit 1
    }

    $overrideContent = @"
services:
  postgres:
    ports:
      - "${POSTGRES_PORT}:${POSTGRES_PORT}"
  object-store:
    ports:
      - "${OBJECT_STORE_PORT}:${OBJECT_STORE_PORT}"
"@
    Set-Content -Path $TEMP_OVERRIDE -Value $overrideContent -Encoding utf8

    $COMPOSE_FILES = @("-f", "docker-compose.yaml")
    if (Test-Path "docker-compose.override.yaml") {
        $COMPOSE_FILES += @("-f", "docker-compose.override.yaml")
    }
    $COMPOSE_FILES += @("-f", $TEMP_OVERRIDE)

    Write-Host "Starting postgres and object-store containers..."
    & docker compose @COMPOSE_FILES up -d postgres object-store

    Start-Sleep -Seconds 3

    & dotnet ./migrator/Solace.Db.Migrator.dll `
      --skip-intro `
      --old-path "$OLD_PATH" `
      --host "$POSTGRES_HOST" `
      --port "$POSTGRES_PORT" `
      --user "$POSTGRES_USER" `
      --password "$POSTGRES_PASSWORD" `
      --endpoint "$OBJECT_STORE_ENDPOINT" `
      -y
}
finally {
    Cleanup
}