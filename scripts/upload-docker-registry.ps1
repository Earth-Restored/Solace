#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory = $true)][string]$Username,
    [string]$Registry = "ghcr.io",
    [string[]]$Projects = @("*"),
    [string[]]$Architectures = @("x64", "arm64", "arm32")
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
        [string[]]$Architectures = @("x64", "arm64", "arm32"),
        [string]$Username = $script:Username,
        [string]$Registry = $script:Registry,
        [int]$MaxRetries = 3,
        [int]$WaitSeconds = 10
    )

    $rids = $Architectures | ForEach-Object {
        $arch = $_ -replace '^linux-', ''
        if ($arch -eq "arm32") { "linux-arm" } else { "linux-$arch" }
    }

    $csprojPath = Join-Path "src" $ProjectName "$ProjectName.csproj"
    $executableName = $ProjectName

    if (Test-Path $csprojPath) {
        [xml]$csprojXml = Get-Content -Path $csprojPath
        $assemblyNameNode = $csprojXml.SelectSingleNode("//AssemblyName")
        if ($assemblyNameNode -and -not [string]::IsNullOrWhiteSpace($assemblyNameNode.InnerText)) {
            $executableName = $assemblyNameNode.InnerText.Trim()
        }
    }

    $imageTag = if ($Registry) { "$Registry/$Username/solace-${PackageName}:latest" } else { "$Username/solace-${PackageName}:latest" }
    $dockerfilePath = $null

    if ($AOT) {
        $platforms = ($rids | ForEach-Object {
            switch ($_) {
                "linux-x64"   { "linux/amd64" }
                "linux-arm64" { "linux/arm64" }
                "linux-arm"   { "linux/arm/v7" }
                default       { $_ -replace '^linux-', 'linux/' }
            }
        }) -join ","

        $csprojCopyCommands = (Get-ChildItem -Path . -Filter "*.csproj" -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/]tests[\\/]' } |
        ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($PWD.Path, $_.FullName).Replace('\', '/')
            "COPY `"$relativePath`" `"$relativePath`""
        }) -join "`n"

        $dockerfileContent = @"
FROM --platform=`$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
ARG BUILDARCH

RUN --mount=type=cache,id=apt-cache-`$BUILDARCH,target=/var/cache/apt \
    --mount=type=cache,id=apt-lists-`$BUILDARCH,target=/var/lib/apt/lists \
    --mount=type=cache,id=downloads-`$BUILDARCH,target=/var/cache/downloads \
    apt-get update && apt-get install -y --no-install-recommends \
    curl \
    xz-utils \
    llvm \
    && ZIG_ARCH=`$(uname -m) \
    && ZIG_FILE="zig-`$ZIG_ARCH-linux-0.16.0.tar.xz" \
    && ZIG_PATH="/var/cache/downloads/`$ZIG_FILE" \
    && if ! xz -t "`$ZIG_PATH" >/dev/null 2>&1; then \
           rm -f "`$ZIG_PATH" "`$ZIG_PATH.tmp" \
           && curl -fsSL "https://ziglang.org/download/0.16.0/`$ZIG_FILE" -o "`$ZIG_PATH.tmp" \
           && mv "`$ZIG_PATH.tmp" "`$ZIG_PATH"; \
       fi \
    && tar -xJ -f "`$ZIG_PATH" -C /usr/local \
    && ln -sf /usr/local/zig-*/zig /usr/local/bin/zig

ENV NUGET_PACKAGES=/root/.nuget/packages

WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
$csprojCopyCommands

ARG TARGETARCH

RUN --mount=type=cache,id=nuget-global-packages,target=/root/.nuget/packages \
    --mount=type=cache,id=nuget-global-v3-cache,target=/root/.local/share/NuGet/v3-cache \
    case "`$TARGETARCH" in \
        "amd64") RID="linux-x64" ;; \
        "arm64") RID="linux-arm64" ;; \
        "arm")   RID="linux-arm" ;; \
        *)       RID="linux-`$TARGETARCH" ;; \
    esac && \
    dotnet restore "src/$ProjectName/$ProjectName.csproj" \
        -r `$RID \
        /p:PublishAot=true

COPY . .

RUN --mount=type=cache,id=nuget-global-packages,target=/root/.nuget/packages \
    --mount=type=cache,id=nuget-global-v3-cache,target=/root/.local/share/NuGet/v3-cache \
    case "`$TARGETARCH" in \
        "amd64") ZIG_TARGET="x86_64-linux-gnu.2.34"    RID="linux-x64" ;; \
        "arm64") ZIG_TARGET="aarch64-linux-gnu.2.34"   RID="linux-arm64" ;; \
        "arm")   ZIG_TARGET="arm-linux-gnueabihf.2.34" RID="linux-arm" ;; \
        *)       ZIG_TARGET="`$TARGETARCH-linux-gnu.2.34" RID="linux-`$TARGETARCH" ;; \
    esac && \
    printf '#!/bin/sh\nfor arg do\n  shift\n  case "`$arg" in\n    -pie|-Wl,-pie|*-pie|-fuse-ld=*|-Wl,-fuse-ld=*|*--discard-all*|*--gc-sections*|*--icf*|--target=*)\n      ;;\n    *)\n      set -- "`$@" "`$arg"\n      ;;\n  esac\ndone\nexec zig cc -target %s "`$@"\n' "`$ZIG_TARGET" > /tmp/zig-cc && \
    chmod +x /tmp/zig-cc && \
    dotnet publish "src/$ProjectName/$ProjectName.csproj" -c Release -r `$RID --no-restore \
        /p:PublishAot=true \
        /p:CppCompilerAndLinker=/tmp/zig-cc \
        /p:LinkerFlavor=lld \
        /p:ObjCopyName=llvm-objcopy \
        /p:PublishTrimmed=true \
        /p:EnableTrimAnalyzer=true \
        /p:TrimmerRemoveSymbols=true \
        /p:DebuggerSupport=false \
        /p:EnableUnsafeBinaryFormatterSerialization=false \
        /p:EnableUnsafeUTF7Encoding=false \
        /p:EventSourceSupport=false \
        /p:HttpActivityPropagationSupport=false \
        /p:MetadataUpdaterSupport=false \
        /p:EFCoreCompileQueries=false \
        /p:EFCorePrecompileQueries=false \
        /p:EFPrecompileQueriesStage=None \
        /p:EFScaffoldModelStage=None \
        -o /app/publish

# .net11 not available yet, todo: update to .net11
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS final
WORKDIR /app
COPY --chown=`$APP_UID:`$APP_UID --from=build /app/publish .
ENTRYPOINT ["./$executableName"]
"@

        $dockerfilePath = [System.IO.Path]::GetTempFileName()
        Set-Content -Path $dockerfilePath -Value $dockerfileContent -Encoding UTF8
    }

    try {
        for ($attempt = 1; $attempt -le $MaxRetries; $attempt++) {
            if ($attempt -eq 1) {
                Write-Information "Publishing $ProjectName..."
            }
            else {
                Write-Information "Publishing $ProjectName (Attempt $attempt of $MaxRetries)..."
            }

            if ($AOT) {
                docker buildx build --platform $platforms --provenance=false --sbom=false -f $dockerfilePath -t $imageTag --push .
            }
            else {
                $ridsJoined = $rids -join ';'
                $arguments = @(
                    "publish", "src/$ProjectName/$ProjectName.csproj",
                    "-c", "Release",
                    "/p:RuntimeIdentifiers=`"$ridsJoined`"",
                    "/p:DebuggerSupport=false",
                    "/p:EnableUnsafeBinaryFormatterSerialization=false",
                    "/p:EnableUnsafeUTF7Encoding=false",
                    "/p:EventSourceSupport=false",
                    "/p:HttpActivityPropagationSupport=false",
                    "/p:MetadataUpdaterSupport=false",
                    "/p:EFCoreCompileQueries=false", # pretty broken, does not respect lang version for some reason (does not recognize [with(...)]), todo: enabled when it's fixed, same for the three bellow
                    "/p:EFCorePrecompileQueries=false",
                    "/p:EFPrecompileQueriesStage=None",
                    "/p:EFScaffoldModelStage=None",
                    "/t:PublishContainer",
                    "-p:ContainerRegistry=$Registry",
                    "-p:ContainerRepository=$Username/solace-$PackageName",
                    "-p:ContainerImageTag=latest",
                    "-p:ContainerRuntimeIdentifiers=`"$ridsJoined`""
                )
                dotnet @arguments
            }

            if ($LASTEXITCODE -eq 0) {
                Write-Host "Successfully published $ProjectName!" -ForegroundColor Green
                return
            }

            if ($attempt -lt $MaxRetries) {
                Write-Warning "Publish failed for $ProjectName. Waiting $WaitSeconds seconds before retry..."
                Start-Sleep -Seconds $WaitSeconds
            }
        }

        Write-Error "Failed to publish $ProjectName after $MaxRetries attempts."
        exit 1
    }
    finally {
        if ($dockerfilePath -and (Test-Path $dockerfilePath)) {
            Remove-Item $dockerfilePath -ErrorAction SilentlyContinue
        }
    }
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

$projectList = @(
    [pscustomobject]@{ProjectName = 'Solace.EventBus.Server'; PackageName = 'event-bus'; AOT = $true }
    [pscustomobject]@{ProjectName = 'Solace.ObjectStore.Server'; PackageName = 'object-store'; AOT = $true }
    [pscustomobject]@{ProjectName = 'Solace.Buildplate.ServerSetup'; PackageName = 'buildplate-server-setup'; AOT = $true }
    [pscustomobject]@{ProjectName = 'Solace.Buildplate.Updater'; PackageName = 'buildplate-updater'; AOT = $true }
    [pscustomobject]@{ProjectName = 'Solace.Buildplate.Launcher'; PackageName = 'buildplate-launcher'; AOT = $false }
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

Push-Location ./../

try {
    foreach ($project in $selectedProjects) {
        Push-Project -ProjectName $project.ProjectName -PackageName $project.PackageName -AOT $project.AOT -Architectures $Architectures
    }
}
finally {
    Pop-Location
}