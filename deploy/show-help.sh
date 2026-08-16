#!/usr/bin/env bash
# Deploy help banner for MrShoofer Agency (enterprise-selling).
# Shown when a workspace terminal opens; also: ./deploy/show-help.sh
#
set +e

BOLD=$'\033[1m'
DIM=$'\033[2m'
CYAN=$'\033[36m'
GREEN=$'\033[32m'
YELLOW=$'\033[33m'
RESET=$'\033[0m'

cat <<BANNER

${CYAN}${BOLD}══════════════════════════════════════════════════════════${RESET}
${BOLD}  MrShoofer Agency — deploy help${RESET}
${CYAN}${BOLD}══════════════════════════════════════════════════════════${RESET}

  ${GREEN}${BOLD}./deploy.sh${RESET}              safe deploy (keeps :5005 for peer apps)
  ${GREEN}./deploy.sh --fast${RESET}       short same-port restart
  ${GREEN}./deploy.sh --full${RESET}       force full rebuild
  ${GREEN}./deploy.sh --dry-run${RESET}    plan only
  ${GREEN}./deploy.sh --docker${RESET}     Docker deploy (also stable :5005)

  ${DIM}One-time (zero-downtime blue/green behind :5005):${RESET}
  ${YELLOW}./deploy/scripts/install-stable-localhost-proxy.sh${RESET}

  ${DIM}Config:${RESET}  .env.deploy   (DEPLOY_HOST / DEPLOY_PASS — gitignored)
  ${DIM}Target:${RESET}  root@89.42.199.39  →  ${BOLD}/root/InterpirceORS/publish${RESET}
  ${DIM}Peer APIs:${RESET} always ${BOLD}http://127.0.0.1:5005/${RESET}  (backends 15005/15006)
  ${DIM}Public:${RESET}   agency.shoofer.taxi / agency.mrshoofer.ir

  ${DIM}Re-show this help anytime:${RESET}  ${GREEN}./deploy/show-help.sh${RESET}

${CYAN}${BOLD}══════════════════════════════════════════════════════════${RESET}

BANNER
