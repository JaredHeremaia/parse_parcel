#!/usr/bin/env bash
# Convenience wrapper for the CLI, so you can type:
#   ./scripts/shipping.sh quote --length 200 --breadth 300 --height 150 --weight 5
#   ./scripts/shipping.sh packages list
# The CLI's exit code is passed straight through (0 ok, 1 usage, 2 API error,
# 3 no packaging solution), so this stays usable in scripts.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

exec dotnet run --project src/Shipping.Cli --no-launch-profile -- "$@"
