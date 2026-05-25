#!/usr/bin/env pwsh
Param (
    [string] $configuration = 'Release',
    [string[]] $profiles = @('framework-dependent-win-x64', 'framework-dependent-linux-x64') # 'framework-dependent-osx-arm64'
)

function Invoke-ProjectPublish {
    param (
        [Parameter(Mandatory = $true)] [string]$ProjectPath,
        [Parameter(Mandatory = $true)] [string]$OutDir,
        [Parameter(Mandatory = $true)] [string]$Configuration,
        [Parameter(Mandatory = $true)] [string]$BuildProfile
    )

    Write-Host "Publishing project $(Split-Path $ProjectPath -Leaf) for profile: $BuildProfile" -ForegroundColor Gray

    if ($BuildProfile -eq 'framework-dependent') {
        dotnet publish $ProjectPath -o $OutDir --no-self-contained -c $Configuration /p:PublishSingleFile=false
    }
    elseif ($BuildProfile -like 'framework-dependent-*') {
        $rid = $BuildProfile.Replace('framework-dependent-', '')
        dotnet publish $ProjectPath -o $OutDir --no-self-contained -c $Configuration -r $rid /p:PublishSingleFile=false
    }
    else {
        dotnet publish $ProjectPath -o $OutDir --sc -c $Configuration -r $BuildProfile
    }
}

git submodule update --init --remote --merge --recursive

$projects = "Solace.ApiServer", "Solace.Buildplate", "Solace.EventBus.Server", "Solace.ObjectStore.Server", "Solace.TappablesGenerator", "Solace.TileRenderer"

foreach ($buildProfile in $profiles) {
    $publishDir = "./build/$configuration/$buildProfile"

    Write-Host "Publishing profile $buildProfile"
    foreach ($name in $projects) {
        $projectPath = "./src/$name/$name.csproj"
        $projectDest = "$publishDir/components"

        Invoke-ProjectPublish `
            -ProjectPath $projectPath `
            -OutDir $projectDest `
            -Configuration $configuration `
            -BuildProfile $buildProfile
    }

    Invoke-ProjectPublish `
        -ProjectPath "./src/Solace.LauncherUI/Solace.LauncherUI.csproj" `
        -OutDir "$publishDir/launcher" `
        -Configuration $configuration `
        -BuildProfile $buildProfile

    if ($buildProfile -like "*win*") {
        Invoke-ProjectPublish `
            -ProjectPath "./src/Solace.KillHelper/Solace.KillHelper.csproj" `
            -OutDir "$publishDir/components" `
            -Configuration $configuration `
            -BuildProfile $buildProfile
        Invoke-ProjectPublish `
            -ProjectPath "./src/Solace.KillHelper/Solace.KillHelper.csproj" `
            -OutDir "$publishDir/launcher" `
            -Configuration $configuration `
            -BuildProfile $buildProfile
    }

    Copy-Item -Path "staticdata" -Destination "$publishDir/staticdata" -Recurse -Force

    $startScriptContent = @'
#!/usr/bin/env pwsh
$originalPath = Get-Location
$launcherDir = Join-Path $PSScriptRoot "launcher"

$isWin = if ($null -ne $IsWindows) { $IsWindows } else { [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT }
$isMac = if ($null -ne $IsMacOS) { $IsMacOS } else { [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX) }
$isLin = if ($null -ne $IsLinux) { $IsLinux } else { [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Unix -and -not $isMac }

try {
    Set-Location -Path $launcherDir
    
    if ($isWin) {
        $originalTitle = $Host.UI.RawUI.WindowTitle
        $Host.UI.RawUI.WindowTitle = "Solace Launcher"

        $fullPath = Join-Path $launcherDir "Launcher.exe"
        $launcher = Start-Process -FilePath $fullPath -ArgumentList $args -PassThru
        Wait-Process -Id $launcher.Id
    } elseif ($isLin -or $isMac) {
        $originalTitle = $null
        Write-Host "`e]0;Solace Launcher`a"

        $fullPath = Join-Path $launcherDir "Launcher"
        if (Test-Path $fullPath) {
            chmod +x $fullPath
        }
        
        $launcher = Start-Process -FilePath $fullPath -ArgumentList $args -PassThru
        Wait-Process -Id $launcher.Id
    } else {
        Write-Host "Unsupported platform"
    }
}
catch {
    Write-Error "Failed to launch: $($_.Exception.Message)"
}
finally {
    Set-Location -Path $originalPath
    
    if ($isWin) {
        $Host.UI.RawUI.WindowTitle = $originalTitle
    } elseif ($isLin -or $isMac) {
        Write-Host "`e]0;$originalTitle`a"
    } else {
        Write-Host "Unsupported platform"
    }
}
'@
    $startScriptContent | Out-File -FilePath "$publishDir/run_launcher.ps1" -Encoding utf8
    
    if (!$IsWindows) {
        chmod +x "$publishDir/run_launcher.ps1"
    }
}