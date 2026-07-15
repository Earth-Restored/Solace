#!/usr/bin/env pwsh

Push-Location ./../

try {
    dotnet ef dbcontext optimize --project src/Solace.DB --startup-project src/Solace.Cdn --output-dir CompiledModels --namespace Solace.DB.CompiledModels --context EarthDbContext
    # dotnet ef dbcontext optimize --project src/Solace.DB --startup-project src/Solace.Cdn --output-dir CompiledModels --namespace Solace.DB.CompiledModels --context EarthDbContext --precompile-queries --nativeaot
}
finally {
    Pop-Location
}