#!/usr/bin/env bash
# Runs every test suite in this repo, the same way CI does.
set -euo pipefail
cd "$(dirname "$0")/.."
echo "==> dotnet test Hestia.sln -c Release --nologo"
dotnet test Hestia.sln -c Release --nologo
echo 'All tests passed.'
