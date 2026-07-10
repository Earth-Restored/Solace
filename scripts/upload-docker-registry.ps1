#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory = $true)][string]$Username,
    [string]$Registry = "ghcr.io"
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
    } else {
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
        [Parameter(Mandatory=$true)][string]$ProjectName,
        [Parameter(Mandatory=$true)][string]$PackageName,
        [string]$Username = $script:Username,
        [string]$Registry = $script:Registry
    )

    dotnet publish "src/$ProjectName/$ProjectName.csproj" -c Release /t:PublishContainer -p:ContainerRegistry=$Registry -p:"ContainerRepository=$Username/solace-$PackageName" -p:ContainerImageTag=latest
}

Write-Information "Checking existing Docker authentication for $Registry..."
$isLoggedIn = CheckDockerRegistryLogin -Registry $Registry

if ($isLoggedIn) {
    Write-Host "Already authenticated to $Registry. No action needed." -ForegroundColor Cyan
} else {
    Write-Information "Active session not found or invalid."
    DockerRegistryLogin -Registry $Registry -Username $Username
}

Push-Location ./../

$projects = @(
    [pscustomobject]@{ProjectName='Solace.EventBus.Server';PackageName='event-bus'}
    [pscustomobject]@{ProjectName='Solace.ObjectStore.Server';PackageName='object-store'}
    [pscustomobject]@{ProjectName='Solace.Buildplate';PackageName='buildplate-launcher'}
    [pscustomobject]@{ProjectName='Solace.ApiServer';PackageName='api-server'}
    [pscustomobject]@{ProjectName='Solace.Locator';PackageName='locator'}
    [pscustomobject]@{ProjectName='Solace.TappablesGenerator';PackageName='tappable-generator'}
    [pscustomobject]@{ProjectName='Solace.TileRenderer';PackageName='tile-renderer'}
    [pscustomobject]@{ProjectName='Solace.AdminPanel';PackageName='admin-panel'}
)

try {
    foreach ($project in $projects) {
        Push-Project -ProjectName $project.ProjectName -PackageName $project.PackageName
    }
}
finally {
    Pop-Location
}