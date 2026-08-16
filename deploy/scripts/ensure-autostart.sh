#!/usr/bin/env bash
# One-shot: make sure Agency comes back after a VPS reboot.
#
# Systemd mode (current live setup):
#   ./deploy/scripts/ensure-autostart.sh
#
# Docker mode:
#   USE_DOCKER=1 ./deploy/scripts/ensure-autostart.sh
#
set -euo pipefail

DEPLOY_HOST="${DEPLOY_HOST:-89.42.199.39}"
DEPLOY_USER="${DEPLOY_USER:-root}"
SERVICE="${SERVICE:-agency.service}"
DOCKER_NAME="${DOCKER_NAME:-mrshoofer-agency}"
USE_DOCKER="${USE_DOCKER:-0}"
SSH_OPTS="-o StrictHostKeyChecking=no -o PreferredAuthentications=password -o PubkeyAuthentication=no -o ConnectTimeout=30"

remote() {
  if [[ -n "${DEPLOY_PASS:-}" ]]; then
    SSHPASS="$DEPLOY_PASS" sshpass -e ssh -T $SSH_OPTS "${DEPLOY_USER}@${DEPLOY_HOST}" "$@"
  else
    ssh -T -o StrictHostKeyChecking=no -o ConnectTimeout=30 "${DEPLOY_USER}@${DEPLOY_HOST}" "$@"
  fi
}

if [[ "$USE_DOCKER" == "1" ]]; then
  remote "set -e
    systemctl enable docker
    systemctl enable containerd 2>/dev/null || true
    if docker inspect ${DOCKER_NAME} >/dev/null 2>&1; then
      docker update --restart always ${DOCKER_NAME}
      echo \"Docker container ${DOCKER_NAME}: restart=always\"
    else
      echo \"WARN: container ${DOCKER_NAME} not found — deploy with USE_DOCKER=1 first\" >&2
    fi
    systemctl is-enabled docker
    docker inspect -f '{{.HostConfig.RestartPolicy.Name}}' ${DOCKER_NAME} 2>/dev/null || true
  "
else
  remote "set -e
    systemctl enable ${SERVICE}
    systemctl is-enabled ${SERVICE}
    systemctl is-active ${SERVICE} || systemctl start ${SERVICE}
    echo \"systemd ${SERVICE} enabled for reboot\"
  "
fi
