#!/usr/bin/env pwsh
# Convenience wrapper for the CLI, so you can type:
#   .\scripts\shipping.ps1 quote --length 200 --breadth 300 --height 150 --weight 5
#   .\scripts\shipping.ps1 packages list
# The CLI's exit code is passed straight through (0 ok, 1 usage, 2 API error,
# 3 no packaging solution), so this stays usable in scripts.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Arguments
)

$ErrorActionPreference = 'Stop'

Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet run --project src/Shipping.Cli --no-launch-profile -- @Arguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
