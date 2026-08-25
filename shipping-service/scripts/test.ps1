#!/usr/bin/env pwsh
# Runs every test in the solution.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Arguments
)

$ErrorActionPreference = 'Stop'

Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet test @Arguments
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
