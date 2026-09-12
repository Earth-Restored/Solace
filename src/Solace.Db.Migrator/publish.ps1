$outputDir = Join-Path $PSScriptRoot "bin", "Release" "publish"

dotnet publish -c Release --self-contained false -p:UseAppHost=false -o $outputDir `
    -p:EFCoreCompileQueries=false `
    -p:EFCorePrecompileQueries=false `
    -p:EFPrecompileQueriesStage=None `
    -p:EFScaffoldModelStage=None

$runtimesPath = Join-Path $outputDir "runtimes"

if (Test-Path $runtimesPath) {
    Get-ChildItem -Path $runtimesPath -Directory | 
        Where-Object { $_.Name -notmatch '^(win.*|osx.*|linux-x64|linux-arm64|linux-arm)$' } | 
        Remove-Item -Recurse -Force
}