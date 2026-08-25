#!/usr/bin/env bash
# Runs every test in the solution.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

exec dotnet test "$@"
