#!/usr/bin/env pwsh
param (
    [Parameter(Mandatory = $true, HelpMessage = "Enter the name of the migration")]
    [string]$MigrationName
)

Push-Location ./../src/Solace.ApiServer
try {
    dotnet ef migrations add $MigrationName --project ../Solace.DB --context EarthDbContext
}
finally {
    Pop-Location
}