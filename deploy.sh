#!/usr/bin/env bash
# One-command deploy for MrShoofer Agency (enterprise-selling).
#
# Usage:
#   ./deploy.sh                  # systemd, last commit (same-port unless stable proxy installed)
#   ./deploy.sh --docker         # Docker image deploy
#   ./deploy.sh --fast           # short restart (~1s blip)
#   ./deploy.sh --dry-run        # plan only
#   ./deploy.sh --full           # force full rebuild
#   ./deploy.sh --autostart-only # only enable reboot persistence
#
# Localhost API mesh (peer apps on :5005):
#   Never flip the port other apps call. Install once:
#     DEPLOY_PASS=... ./deploy/scripts/install-stable-localhost-proxy.sh
#   Then blue/green only swaps backends 15005/15006 behind stable :5005.
#
# Config (optional file: .env.deploy — not committed):
#   DEPLOY_HOST=89.42.199.39
#   DEPLOY_PASS=...
#   DEPLOY_USER=root
#   DEPLOY_SSH_KEY=~/.ssh/ors_deploy
#   USE_DOCKER=0
#   DOCKER_PLATFORM=linux/amd64   # set on Apple Silicon
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

# Load local secrets/defaults if present
if [[ -f "$ROOT/.env.deploy" ]]; then
  # shellcheck disable=SC1091
  set -a
  # shellcheck source=/dev/null
  source "$ROOT/.env.deploy"
  set +a
fi

DEPLOY_HOST="${DEPLOY_HOST:-89.42.199.39}"
DEPLOY_USER="${DEPLOY_USER:-root}"
USE_DOCKER="${USE_DOCKER:-0}"
ENSURE_AUTOSTART="${ENSURE_AUTOSTART:-1}"
SKIP_BLUE_GREEN="${SKIP_BLUE_GREEN:-0}"
FORCE_FULL="${FORCE_FULL:-0}"
DRY_RUN="${DRY_RUN:-0}"
AUTOSTART_ONLY=0

usage() {
  sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --docker|-d) USE_DOCKER=1 ;;
    --fast|-f) SKIP_BLUE_GREEN=1 ;;
    --dry-run) DRY_RUN=1 ;;
    --full) FORCE_FULL=1 ;;
    --autostart-only) AUTOSTART_ONLY=1 ;;
    --host) DEPLOY_HOST="$2"; shift ;;
    --help|-h) usage 0 ;;
    *)
      echo "Unknown option: $1" >&2
      usage 1
      ;;
  esac
  shift
done

export DEPLOY_HOST DEPLOY_USER USE_DOCKER ENSURE_AUTOSTART
export SKIP_BLUE_GREEN FORCE_FULL DRY_RUN
export DEPLOY_PASS="${DEPLOY_PASS:-}"
export DEPLOY_SSH_KEY="${DEPLOY_SSH_KEY:-}"
export DOCKER_PLATFORM="${DOCKER_PLATFORM:-}"
export AGENCY_IMAGE="${AGENCY_IMAGE:-mrshoofer-agency:latest}"

# Prompt for password once if not set, no SSH key, and not dry-run
if [[ -z "${DEPLOY_PASS}" && -z "${DEPLOY_SSH_KEY}" && "$DRY_RUN" != "1" ]]; then
  if [[ -t 0 ]]; then
    read -r -s -p "SSH password for ${DEPLOY_USER}@${DEPLOY_HOST}: " DEPLOY_PASS
    echo
    export DEPLOY_PASS
  else
    echo "Set DEPLOY_PASS, DEPLOY_SSH_KEY, or create .env.deploy" >&2
    exit 1
  fi
fi

if [[ "$AUTOSTART_ONLY" == "1" ]]; then
  exec bash "$ROOT/deploy/scripts/ensure-autostart.sh"
fi

# Sensible default for Apple Silicon when using Docker
if [[ "$USE_DOCKER" == "1" && -z "${DOCKER_PLATFORM}" ]]; then
  arch="$(uname -m 2>/dev/null || true)"
  if [[ "$arch" == "arm64" ]]; then
    export DOCKER_PLATFORM=linux/amd64
    echo "==> Apple Silicon detected — using DOCKER_PLATFORM=linux/amd64"
  fi
fi

echo "==> Deploy target: ${DEPLOY_USER}@${DEPLOY_HOST}"
echo "    mode: $([[ "$USE_DOCKER" == "1" ]] && echo docker || echo systemd)  fast=${SKIP_BLUE_GREEN}  full=${FORCE_FULL}  dry=${DRY_RUN}"

# Deploy only ships committed files (git diff / publish). Catch the jalali/trip-search
# class of 404s before cutover — untracked wwwroot assets never reach production.
REQUIRED_STATIC=(
  wwwroot/css/jalali-datepicker.css
  wwwroot/js/jalali-datepicker.js
  wwwroot/css/trip-search.css
  wwwroot/css/TaxiTripsIndex.css
  wwwroot/css/IndexPage.css
  wwwroot/js/searchpage.js
  wwwroot/js/IndexPage.js
)
missing=()
untracked=()
for f in "${REQUIRED_STATIC[@]}"; do
  if [[ ! -f "$ROOT/$f" ]]; then
    missing+=("$f")
  elif ! git -C "$ROOT" ls-files --error-unmatch "$f" >/dev/null 2>&1; then
    untracked+=("$f")
  fi
done
if ((${#missing[@]} + ${#untracked[@]} > 0)); then
  echo "==> Deploy blocked: required static assets are missing from git" >&2
  if ((${#missing[@]} > 0)); then
    printf '  MISSING:   %s\n' "${missing[@]}" >&2
  fi
  if ((${#untracked[@]} > 0)); then
    printf '  UNTRACKED: %s  (commit before deploy)\n' "${untracked[@]}" >&2
  fi
  exit 1
fi

exec bash "$ROOT/deploy/scripts/zero-downtime-deploy.sh"
