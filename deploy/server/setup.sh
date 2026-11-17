#!/usr/bin/env bash
# One-time setup of the production VPS (ADR-0030; docs/deployment.md, step 2). Ubuntu 24.04 LTS, run with sudo by the
# admin user you log in as (on Hostinger, the `ithaca` user from docs/deployment.md step 2, with your SSH key):
#
#   sudo ./setup.sh /path/to/ci-deploy-key.pub
#
# The argument is the PUBLIC half of the key GitHub Actions uses to deploy. Safe to re-run.
# It does: updates, automatic security updates, Docker, a non-root `deploy` user (docker group, that key only),
# SSH keys only (no passwords, no root login), ufw (SSH in, nothing else), the Cloudflare-only rule for port 443,
# /opt/ithaca for the stack and /etc/ithaca/certs for the origin certificate.
set -euo pipefail

DEPLOY_KEY="${1:?usage: sudo ./setup.sh /path/to/ci-deploy-key.pub}"
HERE="$(cd "$(dirname "$0")" && pwd)"
ADMIN="${SUDO_USER:?run with sudo from your admin user, not as root directly}"
API_UID=1654 # $APP_UID in the .NET runtime image: the user the api container runs as

[ "$(id -u)" -eq 0 ] || { echo "run with sudo" >&2; exit 1; }
grep -q '^ssh-' "$DEPLOY_KEY" || { echo "$DEPLOY_KEY is not an SSH public key" >&2; exit 1; }
# Lock-out guard: password login is about to be switched off.
[ -s "/home/$ADMIN/.ssh/authorized_keys" ] || { echo "$ADMIN has no ~/.ssh/authorized_keys; add your key first" >&2; exit 1; }

export DEBIAN_FRONTEND=noninteractive
apt-get update -q
apt-get upgrade -yq
apt-get install -yq ca-certificates curl ufw unattended-upgrades iptables iproute2

echo "== automatic security updates"
cat > /etc/apt/apt.conf.d/20auto-upgrades <<'CONF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
CONF
systemctl enable --now unattended-upgrades

echo "== Docker (Docker's apt repository)"
if ! command -v docker >/dev/null; then
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod a+r /etc/apt/keyrings/docker.asc
  . /etc/os-release
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $VERSION_CODENAME stable" \
    > /etc/apt/sources.list.d/docker.list
  apt-get update -q
  apt-get install -yq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi
systemctl enable --now docker

echo "== deploy user (CI logs in as this user; docker group membership is root-equivalent, keep the key safe)"
id deploy >/dev/null 2>&1 || useradd --create-home --shell /bin/bash deploy
usermod -aG docker deploy
install -d -m 700 -o deploy -g deploy /home/deploy/.ssh
install -m 600 -o deploy -g deploy "$DEPLOY_KEY" /home/deploy/.ssh/authorized_keys
passwd -l deploy >/dev/null

echo "== SSH: keys only, no root login"
cat > /etc/ssh/sshd_config.d/10-ithaca.conf <<'CONF'
PasswordAuthentication no
KbdInteractiveAuthentication no
PermitRootLogin no
CONF
sshd -t
systemctl reload ssh

echo "== ufw: SSH in, nothing else (Docker's published 443 is handled by the Cloudflare rule below)"
ufw default deny incoming
ufw default allow outgoing
ufw allow OpenSSH
ufw --force enable

echo "== Cloudflare-only rule for the API port"
install -m 0755 "$HERE/cloudflare-firewall.sh" /usr/local/sbin/ithaca-cloudflare-firewall
install -m 0644 "$HERE/ithaca-cloudflare-firewall.service" "$HERE/ithaca-cloudflare-firewall.timer" /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now ithaca-cloudflare-firewall.service ithaca-cloudflare-firewall.timer

echo "== folders"
install -d -m 750 -o deploy -g deploy /opt/ithaca
install -d -m 755 /etc/ithaca/certs
for f in /etc/ithaca/certs/origin.pem /etc/ithaca/certs/origin.key; do
  [ -f "$f" ] && chown "$API_UID" "$f" && chmod 400 "$f"
done

echo
echo "Done. Next (docs/deployment.md): put the Cloudflare origin certificate in /etc/ithaca/certs (origin.pem, origin.key,"
echo "owned by uid $API_UID, mode 400; re-running this script fixes the owner), and /opt/ithaca/.env (owner deploy, mode 600)."
