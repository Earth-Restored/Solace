#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory = $true)][string]$Username,
    [string]$Registry = "ghcr.io",
    [string[]]$Projects = @("*")
)

$InformationPreference = 'Continue'

function CheckDockerRegistryLogin {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)][string]$Registry
    )

    Write-Verbose "Validating credentials for $Registry against the server..."

    $loginOutput = "EOF" | docker login $Registry 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Verbose "Successfully authenticated to $Registry."
        return $true
    }
    else {
        Write-Verbose "Registry rejected the stored token (or none was found). Reason:`n$loginOutput"
        return $false
    }
}

function DockerRegistryLogin {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)][string]$Registry,
        [Parameter(Mandatory = $true)][string]$Username
    )

    Write-Information "Initiating login for user '$Username' to $Registry..."
    
    $credential = Get-Credential -UserName $Username -Message "Enter your credentials for ${Registry}:"
    $token = $credential.GetNetworkCredential().Password

    Write-Verbose "Sending credentials to docker login..."
    $token | docker login $Registry -u $Username --password-stdin

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Docker login failed for $Registry. Exiting script."
        exit 1
    }
    
    Write-Host "Successfully authenticated to $Registry!" -ForegroundColor Green
}

function Push-Project {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectName,
        [Parameter(Mandatory = $true)][string]$PackageName,
        [Parameter(Mandatory = $true)][bool]$AOT,
        [string]$Username = $script:Username,
        [string]$Registry = $script:Registry
    )

    $arguments = @(
        "publish", "src/$ProjectName/$ProjectName.csproj",
        "-c", "Release",
        "/p:DebuggerSupport=false",
        "/p:EnableUnsafeBinaryFormatterSerialization=false",
        "/p:EnableUnsafeUTF7Encoding=false",
        "/p:EventSourceSupport=false",
        "/p:HttpActivityPropagationSupport=false",
        "/p:InvariantGlobalization=true",
        "/p:MetadataUpdaterSupport=false",
        "/t:PublishContainer",
        "-p:ContainerRegistry=$Registry",
        "-p:ContainerRepository=$Username/solace-$PackageName",
        "-p:ContainerImageTag=latest"
    )

    if ($AOT) {
        $arguments += @(
            "/p:PublishAot=true",
            "/p:PublishTrimmed=true",
            "/p:EnableTrimAnalyzer=true",
            "/p:TrimmerRemoveSymbols=true"
        )
    }

    dotnet @arguments
}

Write-Information "Checking existing Docker authentication for $Registry..."
$isLoggedIn = CheckDockerRegistryLogin -Registry $Registry

if ($isLoggedIn) {
    Write-Host "Already authenticated to $Registry. No action needed." -ForegroundColor Cyan
}
else {
    Write-Information "Active session not found or invalid."
    DockerRegistryLogin -Registry $Registry -Username $Username
}

Push-Location ./../

$projectList = @(
    [pscustomobject]@{ProjectName = 'Solace.EventBus.Server'; PackageName = 'event-bus'; AOT = $true }
    [pscustomobject]@{ProjectName = 'Solace.ObjectStore.Server'; PackageName = 'object-store'; AOT = $true }
    [pscustomobject]@{ProjectName = 'Solace.Buildplate.Launcher'; PackageName = 'buildplate-launcher'; AOT = $false }
    [pscustomobject]@{ProjectName = 'Solace.Buildplate.Updater'; PackageName = 'buildplate-updater'; AOT = $true }
    [pscustomobject]@{ProjectName = 'Solace.ApiServer'; PackageName = 'api-server'; AOT = $false }
    [pscustomobject]@{ProjectName = 'Solace.Cdn'; PackageName = 'cdn'; AOT = $false }
    [pscustomobject]@{ProjectName = 'Solace.AuthServer'; PackageName = 'auth-server'; AOT = $false }
    [pscustomobject]@{ProjectName = 'Solace.Locator'; PackageName = 'locator'; AOT = $true }
    [pscustomobject]@{ProjectName = 'Solace.TappablesGenerator'; PackageName = 'tappable-generator'; AOT = $true }
    [pscustomobject]@{ProjectName = 'Solace.TileRenderer'; PackageName = 'tile-renderer'; AOT = $true }
    [pscustomobject]@{ProjectName = 'Solace.WebPortal'; PackageName = 'web-portal'; AOT = $false }
)

$selectedProjects = $projectList | Where-Object {
    $item = $_
    $matched = $false
    foreach ($pattern in $Projects) {
        if ($item.ProjectName -like $pattern -or $item.PackageName -like $pattern) {
            $matched = $true
            break
        }
    }
    $matched
}

if ($selectedProjects.Count -eq 0) {
    Write-Warning "No projects matched your filter: $Projects"
    Pop-Location
    exit 0
}

try {
    foreach ($project in $selectedProjects) {
        Push-Project -ProjectName $project.ProjectName -PackageName $project.PackageName -AOT $project.AOT
    }
}
finally {
    Pop-Location
}