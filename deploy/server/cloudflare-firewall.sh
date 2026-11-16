#!/usr/bin/env bash
# Lets only Cloudflare reach the API's public HTTPS port (ADR-0030 §3). Run as root; the systemd unit
# ithaca-cloudflare-firewall.service runs it after Docker starts and on every boot.
#
# Why not ufw: Docker publishes ports with its own iptables rules, which skip ufw's INPUT chain. Docker's DOCKER-USER
# chain is the supported place for such filters. Published IPv6 goes through docker-proxy (INPUT), which ufw covers.
#
# Fails closed: if Cloudflare's list can't be fetched it uses the last saved copy; with neither, port 443 is dropped.
set -euo pipefail

PORT="${ITHACA_HTTPS_PORT:-443}"
CACHE="/etc/ithaca/cloudflare-ips-v4.txt"
CHAIN="ITHACA-CLOUDFLARE"

mkdir -p "$(dirname "$CACHE")"
if fresh="$(curl -fsS --max-time 15 https://www.cloudflare.com/ips-v4)" && [ -n "$fresh" ]; then
  printf '%s\n' "$fresh" > "$CACHE.new" && mv "$CACHE.new" "$CACHE"
else
  echo "warning: could not fetch Cloudflare's IP list; using $CACHE if present" >&2
fi

iptables -N "$CHAIN" 2>/dev/null || iptables -F "$CHAIN"
if [ -s "$CACHE" ]; then
  while read -r range; do
    [[ "$range" =~ ^[0-9./]+$ ]] || continue
    iptables -A "$CHAIN" -s "$range" -j RETURN
  done < "$CACHE"
fi
iptables -A "$CHAIN" -j DROP

# Match the original destination port (before Docker's DNAT to the container's 8443); add the jump once.
rule=(-p tcp -m conntrack --ctorigdstport "$PORT" --ctdir ORIGINAL -j "$CHAIN")
iptables -N DOCKER-USER 2>/dev/null || true
iptables -C DOCKER-USER "${rule[@]}" 2>/dev/null || iptables -I DOCKER-USER "${rule[@]}"

echo "Port $PORT: allowed from $(grep -c . "$CACHE" 2>/dev/null || echo 0) Cloudflare ranges, dropped for everyone else."
