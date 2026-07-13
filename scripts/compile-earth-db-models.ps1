#!/usr/bin/env pwsh

Push-Location ./../

try {
    $env:EF_PROVIDER = "Postgres"
    dotnet ef dbcontext optimize --project src/Solace.DB.Postgres --startup-project src/Solace.ApiServer --output-dir CompiledModels --namespace Solace.DB.CompiledModels.Postgres --context EarthDbContext
    $env:EF_PROVIDER = "Sqlite"
    dotnet ef dbcontext optimize --project src/Solace.DB.Sqlite --startup-project src/Solace.ApiServer --output-dir CompiledModels --namespace Solace.DB.CompiledModels.Sqlite --context EarthDbContext
}
finally {
    Pop-Location
}