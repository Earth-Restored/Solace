#!/usr/bin/env pwsh

Push-Location ./../

try {
    $env:EF_PROVIDER = "Postgres"
    dotnet ef dbcontext optimize --project src/Solace.DB --startup-project src/Solace.Cdn --output-dir CompiledModels/Postgres --namespace Solace.DB.CompiledModels.Postgres --context EarthDbContext --precompile-queries --nativeaot
    # $env:EF_PROVIDER = "Sqlite"
    # dotnet ef dbcontext optimize --project src/Solace.DB --startup-project src/Solace.ApiServer --output-dir CompiledModels/Sqlite --namespace Solace.DB.CompiledModels.Sqlite --context EarthDbContext --precompile-queries --nativeaot
}
finally {
    Pop-Location
}