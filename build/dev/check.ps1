[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

function Test-CommandExists {
    param(
        [Parameter(Mandatory = $true)][string]$Name
    )

    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

$results = @()

$required = @(
    @{ Name = "dotnet"; Reason = "Build and run backend" },
    @{ Name = "npm"; Reason = "Run frontend" },
    @{ Name = "sqllocaldb"; Reason = "Local SQL Server fallback" },
    @{ Name = "sqlpackage"; Reason = "Publish DACPAC schema" }
)

$optional = @(
    @{ Name = "docker"; Reason = "Optional SQL container mode" },
    @{ Name = "azurite-blob.cmd"; Reason = "Local Blob emulator" },
    @{ Name = "azurite-table.cmd"; Reason = "Local Table emulator" }
)

foreach ($item in $required) {
    $exists = Test-CommandExists -Name $item.Name
    $results += [PSCustomObject]@{
        Tool = $item.Name
        Required = $true
        Available = $exists
        Reason = $item.Reason
    }
}

foreach ($item in $optional) {
    $exists = Test-CommandExists -Name $item.Name
    $results += [PSCustomObject]@{
        Tool = $item.Name
        Required = $false
        Available = $exists
        Reason = $item.Reason
    }
}

$results | Sort-Object Required, Tool -Descending | Format-Table -AutoSize

$missingRequired = $results | Where-Object { -not $_.Available -and $_.Required }
if ($missingRequired.Count -gt 0) {
    Write-Host "`nMissing required tools:" -ForegroundColor Red
    $missingRequired | ForEach-Object { Write-Host "- $($_.Tool): $($_.Reason)" -ForegroundColor Red }
    exit 1
}

Write-Host "`nAll required local dev tools are available." -ForegroundColor Green
