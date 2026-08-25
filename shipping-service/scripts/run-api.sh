#!/usr/bin/env bash
# Runs the API on http://localhost:5080.
#   ./scripts/run-api.sh              in-memory catalogue (no database needed)
#   ./scripts/run-api.sh --postgres   Postgres catalogue (docker compose up -d first)
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

launch_profile="http"
if [[ "${1:-}" == "--postgres" ]]; then
  launch_profile="postgres"
  shift
fi

exec dotnet run --project src/Shipping.Api --launch-profile "$launch_profile" "$@"
