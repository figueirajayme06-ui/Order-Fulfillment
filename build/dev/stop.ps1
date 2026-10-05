[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$stateDir = Join-Path $PSScriptRoot ".state"

function Stop-Tracked {
    param([string]$Name)

    $pidFile = Join-Path $stateDir "$Name.pid"
    if (-not (Test-Path $pidFile)) {
        return
    }

    $pidValue = Get-Content $pidFile -ErrorAction SilentlyContinue | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($pidValue)) {
        Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
        return
    }

    $proc = Get-Process -Id ([int]$pidValue) -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "Stopping $Name (PID $pidValue)"
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }

    Remove-Item $pidFile -Force -ErrorAction SilentlyContinue
}

Stop-Tracked -Name "frontend"
Stop-Tracked -Name "backend"
Stop-Tracked -Name "azurite-blob"
Stop-Tracked -Name "azurite-table"

$dbModeFile = Join-Path $stateDir "db-mode.txt"
$dbContainerFile = Join-Path $stateDir "db-container.txt"

if ((Test-Path $dbModeFile) -and (Test-Path $dbContainerFile)) {
    $mode = (Get-Content $dbModeFile | Select-Object -First 1).Trim()
    $containerName = (Get-Content $dbContainerFile | Select-Object -First 1).Trim()

    if ($mode -eq "docker" -and -not [string]::IsNullOrWhiteSpace($containerName) -and (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Host "Stopping docker container $containerName"
        docker stop $containerName | Out-Null
    }
}

Write-Host "Stopped local dev processes." -ForegroundColor Green
