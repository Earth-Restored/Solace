#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory = $true)]
    [string[]]$Profiles
)

$ErrorActionPreference = "Stop"

git submodule update --init --recursive

foreach ($PublishProfile in $Profiles) {
    if ($PublishProfile -notmatch '^(win|linux|osx)-(x64|x86|arm64|arm)$') {
        Write-Warning "Skipping invalid profile: $PublishProfile. Format should be os-arch (e.g., win-x64)."
        continue
    }

    $os, $arch = $PublishProfile.Split('-')
    $outDir = "publish/$PublishProfile"

    Write-Host "--- Publishing to $outDir ---" -ForegroundColor Cyan

    New-Item -ItemType Directory -Path "$outDir/staticdata" -Force | Out-Null
    Copy-Item -Path "staticdata/*" -Destination "$outDir/staticdata" -Recurse -Force

    Write-Host "Publishing Launcher (Native AOT)..." -ForegroundColor Yellow
    dotnet publish src/Solace.Launcher `
        -c Release `
        --os $os --arch $arch `
        -o "$outDir/launcher" `
        -p:PublishAot=true `
        -p:StripSymbols=true `
        -p:PublishTrimmed=true `
        -p:TrimmerRemoveSymbols=true `
        -p:DebuggerSupport=false `
        -p:EnableUnsafeBinaryFormatterSerialization=false `
        -p:EnableUnsafeUTF7Encoding=false `
        -p:EventSourceSupport=false `
        -p:Http3Support=false `
        -p:InvariantGlobalization=true `
        -p:DebugType=none `
        -p:DebugSymbols=false
        
    Get-ChildItem -Path "$outDir/launcher" -Include *.dbg, *.pdb -Recurse | Remove-Item -Force

    Copy-Item -Path "$outDir/launcher/appsettings.json" -Destination "$outDir/launcher/DO_NOT_MODIFY_default_apps_settings.json" -Recurse -Force

    $projects = @{
        "src/Solace.EventBus.Server"    = "components/event-bus"
        "src/Solace.ObjectStore.Server" = "components/object-store"
        "src/Solace.Buildplate"         = "components/buildplate-launcher"
        "src/Solace.ApiServer"          = "components/api-server"
        "src/Solace.Locator"            = "components/locator"
        "src/Solace.Cdn"                = "components/cdn"
        "src/Solace.TappablesGenerator" = "components/tappable-generator"
        "src/Solace.TileRenderer"       = "components/tile-renderer"
        "src/Solace.AdminPanel"         = "components/admin-panel"
    }

    foreach ($projectPath in $projects.Keys) {
        $outputFolder = "$outDir/$($projects[$projectPath])"
        Write-Host "Publishing $projectPath..."
        dotnet publish $projectPath -c Release --os $os --arch $arch -o $outputFolder -p:UseSharedLibs=true
    }
}

Write-Host "All publish tasks complete!" -ForegroundColor Green