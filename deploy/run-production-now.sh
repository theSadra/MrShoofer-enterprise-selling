#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
echo "==> Deploying Agency (enterprise-selling) to production…"
./deploy.sh --full
echo "==> Agency deploy done."
