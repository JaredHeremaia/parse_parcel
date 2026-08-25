#!/usr/bin/env pwsh
# Runs the API on http://localhost:5080.
#   .\scripts\run-api.ps1             in-memory catalogue (no database needed)
#   .\scripts\run-api.ps1 -Postgres   Postgres catalogue (docker compose up -d first)
param(
    [switch] $Postgres
)

$ErrorActionPreference = 'Stop'

Push-Location (Join-Path $PSScriptRoot '..')
try {
    # $profile is a PowerShell automatic variable, so this one needs a different name.
    $launchProfile = if ($Postgres) { 'postgres' } else { 'http' }

    dotnet run --project src/Shipping.Api --launch-profile $launchProfile
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
