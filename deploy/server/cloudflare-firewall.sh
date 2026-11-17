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

# Only connections arriving on the public interface. Without `-i`, the rule would also catch the containers' own
# outbound HTTPS (api → Anthropic, Voyage, TMDB, Resend, Sentry), which DOCKER-USER sees too, and drop it.
EXT_IF="$(ip route show default | awk '{print $5; exit}')"
[ -n "$EXT_IF" ] || { echo "error: no default route, can't tell the public interface" >&2; exit 1; }

# Build the new list in a separate chain, switch the jump to it, then swap the names: there is never a moment
# without the final DROP (re-runs on Docker restarts and the weekly refresh stay closed).
NEW="$CHAIN-NEW"
iptables -N "$NEW" 2>/dev/null || iptables -F "$NEW"
if [ -s "$CACHE" ]; then
  while read -r range; do
    [[ "$range" =~ ^[0-9./]+$ ]] || continue
    iptables -A "$NEW" -s "$range" -j RETURN
  done < "$CACHE"
fi
iptables -A "$NEW" -j DROP

# Match the original destination port (before Docker's DNAT to the container's 8443).
match=(-p tcp -m conntrack --ctorigdstport "$PORT" --ctdir ORIGINAL)
iptables -N DOCKER-USER 2>/dev/null || true
iptables -I DOCKER-USER -i "$EXT_IF" "${match[@]}" -j "$NEW"
# Remove every jump to the old chain, including the interface-less one an earlier version of this script added.
mapfile -t old_rules < <(iptables -S DOCKER-USER | grep -- "-j $CHAIN\$" || true)
for old in "${old_rules[@]}"; do
  read -r -a old_rule <<< "${old#-A DOCKER-USER }"
  iptables -D DOCKER-USER "${old_rule[@]}"
done
if iptables -S "$CHAIN" >/dev/null 2>&1; then
  iptables -F "$CHAIN"
  iptables -X "$CHAIN"
fi
iptables -E "$NEW" "$CHAIN" # renaming keeps the jump pointing at it

echo "Port $PORT: allowed from $(grep -c . "$CACHE" 2>/dev/null || echo 0) Cloudflare ranges, dropped for everyone else."
