#!/usr/bin/env pwsh
param (
    [Parameter(Mandatory = $true, HelpMessage = "Enter the name of the migration")]
    [string]$MigrationName
)

dotnet ef migrations add $MigrationName --project ./../src/Solace.WebPortal --startup-project ./../src/Solace.WebPortal -o Data/Migrations --context ApplicationDbContext
