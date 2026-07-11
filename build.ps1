#!/usr/bin/env pwsh
$RuntimeInfo = [System.Runtime.InteropServices.RuntimeInformation]

$CurrentOS = "unknown"
if ($RuntimeInfo::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) { $CurrentOS = "win" }
elseif ($RuntimeInfo::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) { $CurrentOS = "linux" }
elseif ($RuntimeInfo::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) { $CurrentOS = "osx" }

$CurrentArch = $RuntimeInfo::ProcessArchitecture.ToString().ToLower()

$configuration = "Debug"

git submodule update --init --recursive

Write-Host "Building for $CurrentOS-$CurrentArch ($configuration)..." -ForegroundColor Cyan

if (Test-Path "build") { Remove-Item -Path "build" -Recurse -Force }

New-Item -ItemType Directory -Path "build/staticdata" -Force | Out-Null
Copy-Item -Path "staticdata/*" -Destination "build/staticdata" -Recurse -Force

function Publish-Project {
    param(
        [string]$Path,
        [string]$Output
    )
    dotnet publish $Path -c $configuration --arch $CurrentArch --os $CurrentOS -o $Output -p:UseSharedLibs=true
}

Publish-Project -Path "src/Solace.Launcher" -Output "build/launcher"
Publish-Project -Path "src/Solace.EventBus.Server" -Output "build/components/event-bus"
Publish-Project -Path "src/Solace.ObjectStore.Server" -Output "build/components/object-store"
Publish-Project -Path "src/Solace.Buildplate" -Output "build/components/buildplate-launcher"
Publish-Project -Path "src/Solace.ApiServer" -Output "build/components/api-server"
Publish-Project -Path "src/Solace.Locator" -Output "build/components/locator"
Publish-Project -Path "src/Solace.Cdn" -Output "build/components/cdn"
Publish-Project -Path "src/Solace.TappablesGenerator" -Output "build/components/tappable-generator"
Publish-Project -Path "src/Solace.TileRenderer" -Output "build/components/tile-renderer"
Publish-Project -Path "src/Solace.AdminPanel" -Output "build/components/admin-panel"

Write-Host "Build complete!" -ForegroundColor Green