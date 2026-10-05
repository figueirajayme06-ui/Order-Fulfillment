[CmdletBinding()]
param(
    [ValidateSet("auto", "localdb", "docker")]
    [string]$DatabaseMode = "auto",
    [string]$KeyVaultUri = "https://kv-ofdev.vault.azure.net/",
    [string]$ServiceBusConnectionOverride,
    [switch]$SkipDbPublish,
    [switch]$NoInstall
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\.." )).Path
$srcRoot = Join-Path $repoRoot "src"
$stateDir = Join-Path $PSScriptRoot ".state"

New-Item -ItemType Directory -Path $stateDir -Force | Out-Null

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Test-RequiredCommand {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Save-StateValue {
    param(
        [string]$Name,
        [string]$Value
    )

    Set-Content -Path (Join-Path $stateDir $Name) -Value $Value -Encoding ASCII
}

function Get-MsBuildPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "vswhere not found. Install Visual Studio Build Tools with SSDT workload."
    }

    # SSMS 22 is also registered with vswhere and bundles MSBuild, but it does
    # not include the SSDT targets required by this SQL project.  Select an
    # MSBuild installation that actually contains those targets instead of
    # assuming the most recently installed VS-family product is suitable.
    $msbuild = & $vswhere -products * -find "MSBuild\**\Bin\MSBuild.exe" |
        Where-Object {
            $msbuildRoot = Resolve-Path (Join-Path (Split-Path $_ -Parent) "..\..")
            Test-Path (Join-Path $msbuildRoot "Microsoft\VisualStudio\v*\SSDT\Microsoft.Data.Tools.Schema.SqlTasks.targets")
        } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($msbuild)) {
        throw "MSBuild.exe was not found via vswhere."
    }

    return $msbuild
}

function Wait-ForPort {
    param(
        [int]$Port,
        [int]$TimeoutSeconds = 45
    )

    $end = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $end) {
        $listener = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
        if ($listener) {
            return $true
        }
        Start-Sleep -Milliseconds 500
    }

    return $false
}

function Test-PortListening {
    param([int]$Port)
    return $null -ne (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)
}

function Start-TrackedProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [hashtable]$EnvironmentVariables = @{}
    )

    $logPath = Join-Path $stateDir "$Name.log"
    $errorLogPath = Join-Path $stateDir "$Name.err.log"

    $argumentLine = [string]::Join(" ", ($Arguments | ForEach-Object {
        if ($_ -match "\s") { '"{0}"' -f $_ } else { $_ }
    }))

    # Inject per-process environment by temporarily setting variables before launch.
    $previousEnvironment = @{}
    foreach ($key in $EnvironmentVariables.Keys) {
        $envPath = "Env:$key"
        if (Test-Path $envPath) {
            $previousEnvironment[$key] = (Get-Item $envPath).Value
        }
        else {
            $previousEnvironment[$key] = $null
        }

        Set-Item -Path $envPath -Value $EnvironmentVariables[$key]
    }

    try {
        if (Test-Path $logPath) {
            Remove-Item -Path $logPath -Force
        }

        if (Test-Path $errorLogPath) {
            Remove-Item -Path $errorLogPath -Force
        }

        $proc = Start-Process -FilePath $FilePath `
            -ArgumentList $argumentLine `
            -WorkingDirectory $WorkingDirectory `
            -NoNewWindow `
            -PassThru `
            -RedirectStandardOutput $logPath `
            -RedirectStandardError $errorLogPath
    }
    finally {
        foreach ($key in $EnvironmentVariables.Keys) {
            $envPath = "Env:$key"
            if ($null -eq $previousEnvironment[$key]) {
                Remove-Item -Path $envPath -ErrorAction SilentlyContinue
            }
            else {
                Set-Item -Path $envPath -Value $previousEnvironment[$key]
            }
        }
    }

    Save-StateValue -Name "$Name.pid" -Value $proc.Id
    return $proc
}

Write-Step "Running prerequisite check"
& (Join-Path $PSScriptRoot "check.ps1")

Write-Step "Resolving repository paths"
if (-not (Test-Path (Join-Path $srcRoot "OF.WebApp\OF.WebApp.csproj"))) {
    throw "Expected project not found at $srcRoot\OF.WebApp\OF.WebApp.csproj"
}

Test-RequiredCommand -Name "dotnet"
Test-RequiredCommand -Name "npm"
Test-RequiredCommand -Name "sqllocaldb"
Test-RequiredCommand -Name "sqlpackage"

$effectiveMode = $DatabaseMode
if ($DatabaseMode -eq "auto") {
    $effectiveMode = if (Get-Command docker -ErrorAction SilentlyContinue) { "docker" } else { "localdb" }
}

Write-Step "Using database mode: $effectiveMode"

if ($effectiveMode -eq "docker") {
    $containerName = "of-sql-dev"
    $saPassword = "yourStrong!Password"

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "Docker mode requested but docker command is unavailable."
    }

    $existing = docker ps -a --format "{{.Names}}" | Where-Object { $_ -eq $containerName }
    if ($existing) {
        docker start $containerName | Out-Null
    }
    else {
        docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=$saPassword" -p 1433:1433 --name $containerName -d mcr.microsoft.com/mssql/server:2019-latest | Out-Null
    }

    Save-StateValue -Name "db-mode.txt" -Value "docker"
    Save-StateValue -Name "db-container.txt" -Value $containerName
    $sqlConnection = "Server=localhost,1433;Database=db-ofdev;User ID=sa;Password=$saPassword;Encrypt=false;TrustServerCertificate=true;MultipleActiveResultSets=true"
    $dacpacConnection = "Server=localhost,1433;Database=db-ofdev;User ID=sa;Password=$saPassword;Encrypt=false;TrustServerCertificate=true;"
}
else {
    sqllocaldb start MSSQLLocalDB | Out-Null
    Save-StateValue -Name "db-mode.txt" -Value "localdb"
    $sqlConnection = "Server=(localdb)\MSSQLLocalDB;Database=db-ofdev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true;"
    $dacpacConnection = "Server=(localdb)\MSSQLLocalDB;Database=db-ofdev;Integrated Security=true;TrustServerCertificate=true;"
}

if (-not $SkipDbPublish) {
    Write-Step "Building SQL project and publishing DACPAC"
    $msbuild = Get-MsBuildPath
    Push-Location $srcRoot
    try {
        & $msbuild ".\OF.Data.Design\OF.Data.Design.sqlproj" /t:Build /p:Configuration=Debug
    }
    finally {
        Pop-Location
    }

    $dacpac = Join-Path $srcRoot "OF.Data.Design\bin\Debug\OF.Data.Design.dacpac"
    if (-not (Test-Path $dacpac)) {
        throw "DACPAC not found at $dacpac"
    }

    & sqlpackage /Action:Publish /SourceFile:"$dacpac" /TargetConnectionString:"$dacpacConnection" /p:BlockOnPossibleDataLoss=false /p:DropObjectsNotInSource=false
}
else {
    Write-Step "Skipping DACPAC publish"
}

Write-Step "Starting Azurite blob/table services"
$azuriteLocation = Join-Path $srcRoot ".azurite"
New-Item -ItemType Directory -Path $azuriteLocation -Force | Out-Null

if (-not (Test-PortListening -Port 10000)) {
    # Use the npm Windows command shims explicitly. A bare `azurite-blob` can
    # resolve to a non-Windows shim earlier on PATH, which Start-Process cannot run.
    $null = Start-TrackedProcess -Name "azurite-blob" -FilePath "azurite-blob.cmd" -Arguments @("--location", $azuriteLocation, "--blobPort", "10000", "--silent") -WorkingDirectory $repoRoot
    if (-not (Wait-ForPort -Port 10000 -TimeoutSeconds 20)) {
        throw "Azurite blob service failed to bind on port 10000."
    }
}

if (-not (Test-PortListening -Port 10002)) {
    $null = Start-TrackedProcess -Name "azurite-table" -FilePath "azurite-table.cmd" -Arguments @("--location", $azuriteLocation, "--tablePort", "10002", "--silent") -WorkingDirectory $repoRoot
    if (-not (Wait-ForPort -Port 10002 -TimeoutSeconds 20)) {
        throw "Azurite table service failed to bind on port 10002."
    }
}

if (-not $NoInstall) {
    Write-Step "Ensuring frontend dependencies"
    Push-Location (Join-Path $srcRoot "OF.Frontend")
    try {
        npm install
    }
    finally {
        Pop-Location
    }
}

Write-Step "Starting backend"
if (-not (Test-PortListening -Port 7200)) {
    $backendEnvironmentVariables = @{
        "ASPNETCORE_ENVIRONMENT" = "Development"
        "ASPNETCORE_URLS" = "https://localhost:7200;http://localhost:5200"
        "KeyVaultUri" = $KeyVaultUri
        "ServiceBusNamespace" = ""
        "SqlConnection" = $sqlConnection
        "StorageConnection" = "UseDevelopmentStorage=true"
        "Data__LogLevel" = "Warning"
    }

    if (-not [string]::IsNullOrWhiteSpace($ServiceBusConnectionOverride)) {
        $backendEnvironmentVariables["ServiceBusConnection"] = $ServiceBusConnectionOverride
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:NOF_SERVICEBUS_CONNECTION)) {
        $backendEnvironmentVariables["ServiceBusConnection"] = $env:NOF_SERVICEBUS_CONNECTION
    }

    $null = Start-TrackedProcess -Name "backend" -FilePath "dotnet" -Arguments @(
        "run",
        "--no-build",
        "--no-launch-profile",
        "--project",
        (Join-Path $srcRoot "OF.WebApp\OF.WebApp.csproj")
    ) -WorkingDirectory $srcRoot -EnvironmentVariables $backendEnvironmentVariables
}
else {
    Write-Host "Backend already listening on port 7200; skipping launch." -ForegroundColor Yellow
}

if (-not (Wait-ForPort -Port 7200 -TimeoutSeconds 45)) {
    throw "Backend did not start listening on port 7200. Check $stateDir\backend.log"
}

Write-Step "Starting frontend"
if (-not (Test-PortListening -Port 5174)) {
    $null = Start-TrackedProcess -Name "frontend" -FilePath "npm.cmd" -Arguments @("run", "dev") -WorkingDirectory (Join-Path $srcRoot "OF.Frontend")
}
else {
    Write-Host "Frontend already listening on port 5174; skipping launch." -ForegroundColor Yellow
}

if (-not (Wait-ForPort -Port 5174 -TimeoutSeconds 45)) {
    throw "Frontend did not start listening on port 5174. Check $stateDir\frontend.log"
}

Write-Host "`nLocal dev environment is ready:" -ForegroundColor Green
Write-Host "- Frontend: http://localhost:5174"
Write-Host "- Backend:  https://localhost:7200"
Write-Host "- State dir: $stateDir"
Write-Host "- Stop all:  .\build\dev\stop.ps1"
