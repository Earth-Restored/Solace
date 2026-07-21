#!/usr/bin/env pwsh
param (
    [Parameter(Mandatory = $true, HelpMessage = "Enter the name of the migration")]
    [string]$MigrationName
)

dotnet ef migrations add $MigrationName --project ./../src/Solace.Db --startup-project ./../src/Solace.ApiServer --context EarthDbContext