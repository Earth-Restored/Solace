#!/usr/bin/env pwsh

Push-Location ./../

try {
    # need to wait for https://github.com/dotnet/efcore/pull/37734
    # dotnet ef dbcontext optimize --project src/Solace.DB --startup-project src/Solace.Cdn --output-dir CompiledModels --namespace Solace.DB.CompiledModels --context EarthDbContext
    # dotnet ef dbcontext optimize --project src/Solace.DB --startup-project src/Solace.Cdn --output-dir CompiledModels --namespace Solace.DB.CompiledModels --context EarthDbContext --precompile-queries --nativeaot
    # dotnet ef dbcontext optimize --project src/Solace.Cdn --startup-project src/Solace.Cdn --output-dir CompiledModels --namespace Solace.Cdn.CompiledModels --context EarthDbContext --precompile-queries --nativeaot
}
finally {
    Pop-Location
}