#!/usr/bin/env bash
# Zero-downtime incremental deploy for MrShoofer enterprise-selling.
#
# Deploys only artifacts produced from files changed in the last commit
# (or a custom git range / working tree).
#
# Usage:
#   cp deploy/deploy.env.example deploy/deploy.env   # once
#   ./deploy/zero-downtime-deploy.sh                 # last commit
#   ./deploy/zero-downtime-deploy.sh --docker         # force Docker runtime
#   ./deploy/zero-downtime-deploy.sh --systemd        # force systemd runtime
#   ./deploy/zero-downtime-deploy.sh --dry-run
#   ./deploy/zero-downtime-deploy.sh --working-tree  # uncommitted changes
#   ./deploy/zero-downtime-deploy.sh --range HEAD~3..HEAD
#   ./deploy/zero-downtime-deploy.sh --ensure-autostart-only
#
# Modes (auto-detected from changed files):
#   hot        – only wwwroot/static assets → rsync into live tree, no restart
#   bluegreen  – code/views/csproj → publish, stage next release, cut over
#
# Runtimes (deploy.env DEPLOY_RUNTIME, or --docker / --systemd):
#   docker   – blue/green containers, restart: unless-stopped (survives VPS reboot)
#   systemd  – blue/green dirs + systemctl (enabled on boot)

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

ENV_FILE="${ROOT}/deploy/deploy.env"
DRY_RUN=0
RANGE="HEAD~1..HEAD"
USE_WORKING_TREE=0
FORCE_FULL=0
RUNTIME_OVERRIDE=""
ENSURE_AUTOSTART_ONLY=0

die() { echo "ERROR: $*" >&2; exit 1; }
log() { echo "==> $*"; }
warn() { echo "WARN: $*" >&2; }

usage() {
  cat <<'USAGE'
Zero-downtime incremental deploy for MrShoofer enterprise-selling.

Usage:
  cp deploy/deploy.env.example deploy/deploy.env   # once
  ./deploy/zero-downtime-deploy.sh                 # last commit (uses DEPLOY_RUNTIME)
  ./deploy/zero-downtime-deploy.sh --docker         # force Docker runtime
  ./deploy/zero-downtime-deploy.sh --systemd        # force systemd runtime
  ./deploy/zero-downtime-deploy.sh --dry-run
  ./deploy/zero-downtime-deploy.sh --working-tree
  ./deploy/zero-downtime-deploy.sh --range HEAD~3..HEAD
  ./deploy/zero-downtime-deploy.sh --ensure-autostart-only

Modes: hot (wwwroot only) | bluegreen (code/views → publish + cutover)
Runtimes: docker (default) | systemd
Autostart: containers use restart:unless-stopped; systemd unit is enabled on boot.
USAGE
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run) DRY_RUN=1; shift ;;
    --working-tree) USE_WORKING_TREE=1; shift ;;
    --range) RANGE="${2:-}"; [[ -n "$RANGE" ]] || die "--range needs a value"; shift 2 ;;
    --full) FORCE_FULL=1; shift ;;
    --docker) RUNTIME_OVERRIDE=docker; shift ;;
    --systemd) RUNTIME_OVERRIDE=systemd; shift ;;
    --ensure-autostart-only) ENSURE_AUTOSTART_ONLY=1; shift ;;
    -h|--help) usage ;;
    *) die "unknown arg: $1 (try --help)" ;;
  esac
done

[[ -f "$ENV_FILE" ]] || die "missing $ENV_FILE — copy deploy/deploy.env.example and fill it in"

# shellcheck disable=SC1090
source "$ENV_FILE"

: "${DEPLOY_HOST:?set DEPLOY_HOST in deploy.env}"
: "${DEPLOY_USER:?set DEPLOY_USER in deploy.env}"
: "${REMOTE_BASE:?set REMOTE_BASE in deploy.env}"
: "${REMOTE_LINK:=${REMOTE_BASE}/current}"
: "${SERVICE_NAME:=application}"
: "${DEPLOY_RUNTIME:=docker}"
: "${DOCKER_CONTAINER_PREFIX:=mrshoofer-enterprise}"
: "${DOCKER_RUNTIME_IMAGE:=mcr.microsoft.com/dotnet/aspnet:8.0}"
: "${DOCKER_SHARED_DIR:=${REMOTE_BASE}/shared}"
: "${APP_PORT:=5000}"
: "${ALT_PORT:=5001}"
: "${HEALTH_PATH:=/}"
: "${HEALTH_TIMEOUT_SEC:=45}"
: "${PRESERVE_REMOTE_FILES:=appsettings.json appsettings.Production.json}"
: "${NGINX_SITE:=}"
: "${DEPLOY_SSH_PORT:=22}"
: "${ENSURE_AUTOSTART:=1}"

if [[ -n "$RUNTIME_OVERRIDE" ]]; then
  DEPLOY_RUNTIME="$RUNTIME_OVERRIDE"
fi
[[ "$DEPLOY_RUNTIME" == "docker" || "$DEPLOY_RUNTIME" == "systemd" ]] \
  || die "DEPLOY_RUNTIME must be docker or systemd (got: $DEPLOY_RUNTIME)"

SSH_OPTS=(-o BatchMode=yes -o StrictHostKeyChecking=accept-new -p "$DEPLOY_SSH_PORT")
if [[ -n "${DEPLOY_SSH_KEY:-}" ]]; then
  SSH_OPTS+=(-i "$DEPLOY_SSH_KEY")
fi

ssh_cmd() { ssh "${SSH_OPTS[@]}" "${DEPLOY_USER}@${DEPLOY_HOST}" "$@"; }

# ─── Autostart (survives VPS reboot) ─────────────────────────────────────────

ensure_autostart() {
  [[ "$ENSURE_AUTOSTART" == "1" || "$ENSURE_AUTOSTART_ONLY" -eq 1 ]] || return 0
  log "Ensuring auto-start after VPS reboot (runtime=$DEPLOY_RUNTIME)…"

  if [[ "$DRY_RUN" -eq 1 ]]; then
    log "[dry-run] would enable docker/systemd autostart on ${DEPLOY_HOST}"
    return 0
  fi

  if [[ "$DEPLOY_RUNTIME" == "docker" ]]; then
    ssh_cmd bash -s <<EOF
set -euo pipefail
if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: docker is not installed on the server" >&2
  exit 1
fi
# Docker daemon itself must start on boot
sudo systemctl enable docker >/dev/null 2>&1 || true
sudo systemctl start docker >/dev/null 2>&1 || true
# Active app container keeps restart policy (set at create time)
PREFIX="$DOCKER_CONTAINER_PREFIX"
for c in blue green current; do
  name="\${PREFIX}-\${c}"
  if docker ps -a --format '{{.Names}}' | grep -qx "\$name"; then
    docker update --restart unless-stopped "\$name" >/dev/null || true
  fi
done
# Prefer a single "current" alias container if present
if docker ps -a --format '{{.Names}}' | grep -qx "\${PREFIX}-live"; then
  docker update --restart unless-stopped "\${PREFIX}-live" >/dev/null || true
fi
echo "docker autostart ready"
EOF
  else
    ssh_cmd bash -s <<EOF
set -euo pipefail
SERVICE="$SERVICE_NAME"
UNIT_SRC="/tmp/${SERVICE_NAME}.service"
# Install unit from repo copy if we uploaded it; otherwise just enable existing
if [[ -f "\$UNIT_SRC" ]]; then
  sudo cp "\$UNIT_SRC" "/etc/systemd/system/\${SERVICE}.service"
  sudo systemctl daemon-reload
fi
sudo systemctl enable "\$SERVICE"
echo "systemd autostart ready for \$SERVICE"
EOF
  fi
}

if [[ "$ENSURE_AUTOSTART_ONLY" -eq 1 ]]; then
  # Upload systemd unit so enable works even on first setup
  if [[ "$DEPLOY_RUNTIME" == "systemd" && "$DRY_RUN" -eq 0 ]]; then
    scp -P "$DEPLOY_SSH_PORT" ${DEPLOY_SSH_KEY:+-i "$DEPLOY_SSH_KEY"} \
      -o BatchMode=yes -o StrictHostKeyChecking=accept-new \
      "$ROOT/deploy/systemd/application.service" \
      "${DEPLOY_USER}@${DEPLOY_HOST}:/tmp/${SERVICE_NAME}.service"
  fi
  ensure_autostart
  log "Autostart configured."
  exit 0
fi

# ─── 1) Collect changed source files ─────────────────────────────────────────

collect_changed_sources() {
  local files
  if [[ "$USE_WORKING_TREE" -eq 1 ]]; then
    files="$(git status --porcelain -u | awk '{print substr($0,4)}')"
  else
    files="$(git diff --name-only --diff-filter=ACMR "$RANGE")"
  fi
  echo "$files" | sed '/^$/d' | sort -u
}

CHANGED_SOURCES="$(collect_changed_sources)"
if [[ -z "$CHANGED_SOURCES" && "$FORCE_FULL" -eq 0 ]]; then
  die "no changed files in range (use --working-tree, --range, or --full)"
fi

log "Runtime: $DEPLOY_RUNTIME"
log "Changed sources:"
echo "$CHANGED_SOURCES" | sed 's/^/  - /'

# ─── 2) Classify: hot (static only) vs bluegreen (needs publish) ─────────────

needs_rebuild=0
static_files=()

while IFS= read -r f; do
  [[ -z "$f" ]] && continue
  case "$f" in
    wwwroot/*)
      static_files+=("$f")
      ;;
    *.cshtml|*.cs|*.csproj|Program.cs|appsettings*.json|*.razor|Migrations/*)
      needs_rebuild=1
      ;;
    deploy/*|*.md|.github/*|Dockerfile|docker-compose.yml|liara.json|.gitignore|.editorconfig)
      warn "skipping non-runtime path: $f"
      ;;
    *)
      needs_rebuild=1
      ;;
  esac
done <<< "$CHANGED_SOURCES"

if [[ "$FORCE_FULL" -eq 1 ]]; then
  needs_rebuild=1
fi

MODE="hot"
if [[ "$needs_rebuild" -eq 1 ]]; then
  MODE="bluegreen"
fi

log "Deploy mode: $MODE"

# ─── 3) Map to publish artifacts to sync ─────────────────────────────────────

PUBLISH_DIR="${ROOT}/.deploy-publish"
ARTIFACTS_LIST="${ROOT}/.deploy-artifacts.txt"
rm -f "$ARTIFACTS_LIST"
: > "$ARTIFACTS_LIST"

add_artifact() {
  local rel="$1"
  [[ -n "$rel" ]] || return 0
  echo "$rel" >> "$ARTIFACTS_LIST"
}

if [[ "$MODE" == "hot" ]]; then
  for f in "${static_files[@]+"${static_files[@]}"}"; do
    add_artifact "$f"
  done
else
  log "Publishing Release build…"
  if [[ "$DRY_RUN" -eq 1 ]]; then
    log "[dry-run] would run: dotnet publish -c Release -o $PUBLISH_DIR"
  else
    rm -rf "$PUBLISH_DIR"
    dotnet publish "$ROOT/Application.csproj" -c Release -o "$PUBLISH_DIR" --nologo -v q
  fi

  add_artifact "Application.dll"
  add_artifact "Application.pdb"
  add_artifact "Application.deps.json"
  add_artifact "Application.runtimeconfig.json"
  add_artifact "Application.staticwebassets.endpoints.json"

  for f in "${static_files[@]+"${static_files[@]}"}"; do
    add_artifact "$f"
  done
fi

sort -u "$ARTIFACTS_LIST" -o "$ARTIFACTS_LIST"
FILTERED="$(mktemp)"
while IFS= read -r rel; do
  skip=0
  for keep in $PRESERVE_REMOTE_FILES; do
    if [[ "$rel" == "$keep" ]]; then
      skip=1
      break
    fi
  done
  [[ "$skip" -eq 1 ]] && continue
  echo "$rel"
done < "$ARTIFACTS_LIST" > "$FILTERED"
mv "$FILTERED" "$ARTIFACTS_LIST"

log "Artifacts to sync:"
if [[ ! -s "$ARTIFACTS_LIST" ]]; then
  warn "artifact list empty after filters — nothing to deploy"
  exit 0
fi
sed 's/^/  - /' "$ARTIFACTS_LIST"

if [[ "$DRY_RUN" -eq 1 ]]; then
  log "[dry-run] stopping before remote changes"
  ensure_autostart
  exit 0
fi

# ─── 4) Ensure remote layout ─────────────────────────────────────────────────

log "Ensuring remote release layout…"
ssh_cmd bash -s <<EOF
set -euo pipefail
BASE="$REMOTE_BASE"
LINK="$REMOTE_LINK"
SHARED="$DOCKER_SHARED_DIR"
mkdir -p "\$BASE/releases" "\$SHARED"
if [[ ! -e "\$LINK" ]]; then
  mkdir -p "\$BASE/releases/blue"
  ln -sfn "\$BASE/releases/blue" "\$LINK"
fi
CUR=\$(readlink -f "\$LINK")
if [[ "\$CUR" == *"/blue" ]]; then
  NEXT_COLOR=green
  CUR_COLOR=blue
else
  NEXT_COLOR=blue
  CUR_COLOR=green
fi
mkdir -p "\$BASE/releases/\$NEXT_COLOR"
rsync -a --delete \
  --exclude appsettings.json --exclude appsettings.Production.json --exclude appsettings.Development.json \
  "\$BASE/releases/\$CUR_COLOR/" "\$BASE/releases/\$NEXT_COLOR/" 2>/dev/null || \
  rsync -a "\$CUR/" "\$BASE/releases/\$NEXT_COLOR/" || true
echo "\$CUR_COLOR" > "\$BASE/.current-color"
echo "\$NEXT_COLOR" > "\$BASE/.next-color"
echo "current=\$CUR_COLOR next=\$NEXT_COLOR"
EOF

NEXT_COLOR="$(ssh_cmd "cat ${REMOTE_BASE}/.next-color")"
CUR_COLOR="$(ssh_cmd "cat ${REMOTE_BASE}/.current-color")"
NEXT_DIR="${REMOTE_BASE}/releases/${NEXT_COLOR}"
log "Overlaying into remote next slot: $NEXT_COLOR ($NEXT_DIR)"

# ─── 5) Rsync only listed artifacts into next (or live for hot) ──────────────

TARGET_DIR="$NEXT_DIR"
if [[ "$MODE" == "hot" ]]; then
  TARGET_DIR="$(ssh_cmd "readlink -f ${REMOTE_LINK}")"
  log "Hot path: writing static files into live dir $TARGET_DIR (no restart)"
fi

SOURCE_ROOT="$ROOT"
if [[ "$MODE" == "bluegreen" ]]; then
  SOURCE_ROOT="$PUBLISH_DIR"
fi

UPLOAD_LIST="$(mktemp)"
while IFS= read -r rel; do
  if [[ -e "${SOURCE_ROOT}/${rel}" ]]; then
    echo "$rel" >> "$UPLOAD_LIST"
  else
    warn "missing in publish/source (skipped): $rel"
  fi
done < "$ARTIFACTS_LIST"

[[ -s "$UPLOAD_LIST" ]] || die "no existing artifacts to upload"

RSYNC_RSH="ssh ${SSH_OPTS[*]}"
log "Uploading $(wc -l < "$UPLOAD_LIST" | tr -d ' ') file(s)…"
rsync -az --files-from="$UPLOAD_LIST" -e "$RSYNC_RSH" \
  "${SOURCE_ROOT}/" \
  "${DEPLOY_USER}@${DEPLOY_HOST}:${TARGET_DIR}/"

rm -f "$UPLOAD_LIST"

if [[ "$MODE" == "hot" ]]; then
  log "Hot deploy complete — live static assets updated, zero downtime."
  ensure_autostart
  exit 0
fi

# ─── 6) Blue/green cutover ───────────────────────────────────────────────────

switch_nginx_remote() {
  # args embedded in heredoc below
  true
}

wait_health() {
  local port="$1"
  local deadline=$((SECONDS + HEALTH_TIMEOUT_SEC))
  while (( SECONDS < deadline )); do
    if ssh_cmd "curl -fsS -o /dev/null -m 3 http://127.0.0.1:${port}${HEALTH_PATH}"; then
      return 0
    fi
    sleep 2
  done
  return 1
}

if [[ "$DEPLOY_RUNTIME" == "docker" ]]; then
  CANDIDATE_NAME="${DOCKER_CONTAINER_PREFIX}-${NEXT_COLOR}"
  LIVE_NAME="${DOCKER_CONTAINER_PREFIX}-live"
  OLD_NAME="${DOCKER_CONTAINER_PREFIX}-${CUR_COLOR}"

  log "Pulling runtime image (if needed) and starting candidate container $CANDIDATE_NAME on :${ALT_PORT}…"
  ssh_cmd bash -s <<EOF
set -euo pipefail
IMAGE="$DOCKER_RUNTIME_IMAGE"
NEXT="$NEXT_DIR"
SHARED="$DOCKER_SHARED_DIR"
ALT_PORT="$ALT_PORT"
CANDIDATE="$CANDIDATE_NAME"
PREFIX="$DOCKER_CONTAINER_PREFIX"

docker pull "\$IMAGE" >/dev/null || true

# Stop/remove leftover candidate with same name
docker rm -f "\$CANDIDATE" >/dev/null 2>&1 || true

RUN_ARGS=(
  run -d
  --name "\$CANDIDATE"
  --restart unless-stopped
  -p "127.0.0.1:\${ALT_PORT}:5000"
  -e ASPNETCORE_ENVIRONMENT=Production
  -e ASPNETCORE_URLS=http://0.0.0.0:5000
  -v "\$NEXT:/app"
)
if [[ -f "\$SHARED/appsettings.json" ]]; then
  RUN_ARGS+=(-v "\$SHARED/appsettings.json:/app/appsettings.json:ro")
fi
RUN_ARGS+=("\$IMAGE" dotnet Application.dll)
docker "\${RUN_ARGS[@]}"

echo "candidate started"
EOF

  log "Waiting for health on :${ALT_PORT}${HEALTH_PATH}…"
  if ! wait_health "$ALT_PORT"; then
    warn "candidate failed health check — leaving live container untouched"
    ssh_cmd "docker logs --tail 80 ${CANDIDATE_NAME} || true; docker rm -f ${CANDIDATE_NAME} || true"
    die "aborting cutover"
  fi

  log "Candidate healthy — cutting over Docker traffic → $NEXT_COLOR"
  ssh_cmd bash -s <<EOF
set -euo pipefail
BASE="$REMOTE_BASE"
LINK="$REMOTE_LINK"
NEXT_COLOR="$NEXT_COLOR"
NEXT="$NEXT_DIR"
APP_PORT="$APP_PORT"
ALT_PORT="$ALT_PORT"
NGINX_SITE="${NGINX_SITE}"
CANDIDATE="$CANDIDATE_NAME"
LIVE="$LIVE_NAME"
OLD="$OLD_NAME"
IMAGE="$DOCKER_RUNTIME_IMAGE"
SHARED="$DOCKER_SHARED_DIR"
PREFIX="$DOCKER_CONTAINER_PREFIX"

switch_nginx_port() {
  local port="\$1"
  [[ -n "\$NGINX_SITE" && -f "\$NGINX_SITE" ]] || return 0
  sed -i.bak -E "s#(127\\.0\\.0\\.1:)[0-9]+#\\1\${port}#g" "\$NGINX_SITE"
  nginx -t
  systemctl reload nginx
}

# 1) Send traffic to healthy candidate (zero downtime when nginx is set)
switch_nginx_port "\$ALT_PORT"

# 2) Flip symlink to new release
ln -sfn "\$NEXT" "\$LINK"
echo "\$NEXT_COLOR" > "\$BASE/.current-color"

# 3) Recreate live container on APP_PORT from the new release (autostart on reboot)
docker rm -f "\$LIVE" >/dev/null 2>&1 || true

RUN_ARGS=(
  run -d
  --name "\$LIVE"
  --restart unless-stopped
  -p "127.0.0.1:\${APP_PORT}:5000"
  -e ASPNETCORE_ENVIRONMENT=Production
  -e ASPNETCORE_URLS=http://0.0.0.0:5000
  -v "\$LINK:/app"
)
if [[ -f "\$SHARED/appsettings.json" ]]; then
  RUN_ARGS+=(-v "\$SHARED/appsettings.json:/app/appsettings.json:ro")
fi
RUN_ARGS+=("\$IMAGE" dotnet Application.dll)
docker "\${RUN_ARGS[@]}"

# 4) Wait for live, point nginx back, remove old color + candidate
for i in \$(seq 1 30); do
  if curl -fsS -o /dev/null -m 2 "http://127.0.0.1:\${APP_PORT}${HEALTH_PATH}"; then
    break
  fi
  sleep 1
done

switch_nginx_port "\$APP_PORT"

docker rm -f "\$CANDIDATE" >/dev/null 2>&1 || true
docker rm -f "\$OLD" >/dev/null 2>&1 || true
EOF

else
  # ─── systemd cutover ───────────────────────────────────────────────────────
  log "Starting candidate on :${ALT_PORT} from $NEXT_DIR (systemd runtime)…"
  ssh_cmd bash -s <<EOF
set -euo pipefail
NEXT="$NEXT_DIR"
ALT_PORT="$ALT_PORT"
if [[ -f /tmp/mrshoofer-candidate.pid ]]; then
  kill "\$(cat /tmp/mrshoofer-candidate.pid)" 2>/dev/null || true
  rm -f /tmp/mrshoofer-candidate.pid
fi
cd "\$NEXT"
nohup env ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_URLS="http://127.0.0.1:\${ALT_PORT}" \\
  /usr/bin/dotnet "\$NEXT/Application.dll" > /tmp/mrshoofer-candidate.log 2>&1 &
echo \$! > /tmp/mrshoofer-candidate.pid
EOF

  log "Waiting for health on :${ALT_PORT}${HEALTH_PATH}…"
  if ! wait_health "$ALT_PORT"; then
    warn "candidate failed health check — leaving live slot untouched"
    ssh_cmd 'kill $(cat /tmp/mrshoofer-candidate.pid) 2>/dev/null || true; rm -f /tmp/mrshoofer-candidate.pid; tail -n 80 /tmp/mrshoofer-candidate.log || true'
    die "aborting cutover"
  fi

  # Upload unit so enable/restart uses the symlink layout
  scp -P "$DEPLOY_SSH_PORT" ${DEPLOY_SSH_KEY:+-i "$DEPLOY_SSH_KEY"} \
    -o BatchMode=yes -o StrictHostKeyChecking=accept-new \
    "$ROOT/deploy/systemd/application.service" \
    "${DEPLOY_USER}@${DEPLOY_HOST}:/tmp/${SERVICE_NAME}.service"

  log "Candidate healthy — flipping current → $NEXT_COLOR"
  ssh_cmd bash -s <<EOF
set -euo pipefail
BASE="$REMOTE_BASE"
LINK="$REMOTE_LINK"
NEXT_COLOR="$NEXT_COLOR"
NEXT="$NEXT_DIR"
SERVICE="$SERVICE_NAME"
APP_PORT="$APP_PORT"
ALT_PORT="$ALT_PORT"
NGINX_SITE="${NGINX_SITE}"

switch_nginx_port() {
  local port="\$1"
  [[ -n "\$NGINX_SITE" && -f "\$NGINX_SITE" ]] || return 0
  sed -i.bak -E "s#(127\\.0\\.0\\.1:)[0-9]+#\\1\${port}#g" "\$NGINX_SITE"
  nginx -t
  systemctl reload nginx
}

switch_nginx_port "\$ALT_PORT"

ln -sfn "\$NEXT" "\$LINK"
echo "\$NEXT_COLOR" > "\$BASE/.current-color"

sudo cp "/tmp/\${SERVICE}.service" "/etc/systemd/system/\${SERVICE}.service"
sudo systemctl daemon-reload
sudo systemctl enable "\$SERVICE"
sudo systemctl restart "\$SERVICE"
sleep 2
sudo systemctl is-active --quiet "\$SERVICE"

for i in \$(seq 1 30); do
  if curl -fsS -o /dev/null -m 2 "http://127.0.0.1:\${APP_PORT}${HEALTH_PATH}"; then
    break
  fi
  sleep 1
done

switch_nginx_port "\$APP_PORT"

if [[ -f /tmp/mrshoofer-candidate.pid ]]; then
  kill "\$(cat /tmp/mrshoofer-candidate.pid)" 2>/dev/null || true
  rm -f /tmp/mrshoofer-candidate.pid
fi
EOF
fi

log "Verifying live :${APP_PORT}${HEALTH_PATH}…"
wait_health "$APP_PORT" || die "live health check failed after cutover"

ensure_autostart

log "Blue/green deploy complete (runtime=$DEPLOY_RUNTIME, slot=$NEXT_COLOR)."
log "App will restart automatically after a VPS reboot."
