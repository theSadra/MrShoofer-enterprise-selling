#!/usr/bin/env bash
# Hot-reload local Agency app (Razor/CSS/JS + browser refresh)
set -euo pipefail
cd "$(dirname "$0")"
export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=0
exec dotnet watch run --launch-profile http --non-interactive
