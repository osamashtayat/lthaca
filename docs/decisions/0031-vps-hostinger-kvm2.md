# ADR-0031: VPS provider: Hostinger KVM 2 in Europe (OVHcloud VPS-2 not available)

- **Status:** Proposed (owner chose Hostinger KVM 2 on 2026-11-17; Accepted once the data centre, term and price below
  are filled in from the order). Supersedes ADR-0030 §2 (the VPS provider) and the OVH-specific lines of its §4 and
  Consequences; the rest of ADR-0030 stands.
- **Date:** 2026-11-17
- **Decides:** the L1 provider (backlog, Launch decisions). Provisioning is OPS-07.

## Context
ADR-0030 chose OVHcloud VPS-2 in Frankfurt. On 2026-11-17, when the owner went to order it, OVH offered no VPS in any
European location: VPS-2 only in North America and Asia, and otherwise VPS-1 only. A server outside Europe would add
roughly 100 ms or more to every API call and live room update for the expected users (Europe and the Middle East) and
move user data out of the EU, so it was not considered. Waiting for OVH stock was the other option; the owner chose
to go ahead with the runner-up from the OPS-06 comparison.

Nothing in the app or the deployment depends on the provider: the Worker reaches the server through the `origin` DNS
record, the stack is `docker-compose.prod.yml`, and `deploy/server/setup.sh` targets any Ubuntu 24.04 VPS.

## Decision
- **Hostinger KVM 2**, in a **European data centre** (Germany preferred, so the latency assumptions of ADR-0030 hold;
  otherwise the nearest EU location offered).
- As compared in OPS-06: **2 vCores**, 8 GB RAM, NVMe storage; better-rated support than OVH; its advertised price
  needed a long prepaid term (24 months).
- **Filled in from the order** (owner, before this ADR is Accepted):

  | Item | Value |
  |---|---|
  | Data centre | _to fill_ |
  | vCores / RAM / disk as ordered | _to fill_ |
  | Billing term and price (incl. VAT) | _to fill_ |
  | Automatic backups included | _to fill_ (a second layer only; OPS-08 does the real backups) |

- **Operating system: plain Ubuntu 24.04** (no Docker, panel or app template). `setup.sh` installs Docker itself.
- **Hostinger's panel firewall stays off**: the server's own ufw and the Cloudflare-only rule (`setup.sh`,
  `cloudflare-firewall.sh`) are the one source of truth; two rule sets would disagree silently.
- **Login:** Hostinger servers start with `root`. The runbook (docs/deployment.md step 2) first creates an admin user
  (`ithaca`) with your SSH key and sudo, then `setup.sh` switches root and password logins off, as before.

## Consequences
+ The launch isn't blocked on OVH stock; the setup, deploy and firewall work unchanged.
+ Better-rated support than OVH, so the "support is slow" risk in ADR-0030 §4 is smaller. The move path stays the same
  (restore the off-server dump, change the `origin` record and `DEPLOY_HOST`), so the provider can change again later.
- **2 vCores instead of 4:** fine at launch traffic, but the catalog sync, Letterboxd imports and request handling share
  less CPU. Watch it in Sentry traces and the server's load (OPS-09); OPS-08 schedules the nightly sync for a quiet hour.
- **A long prepaid term** (if chosen for the price) is money committed even if we move; the monthly price, if offered,
  avoids that at a higher cost. Recorded in the table above.
- One extra one-time step at setup (the admin user), because Hostinger starts with root.

## Alternatives rejected
- **OVHcloud VPS-2 outside Europe** (North America, Asia): latency for the expected users and data outside the EU.
- **OVHcloud VPS-1:** not checked against our needs while VPS-2 was the plan; the owner preferred a known option.
- **Waiting for OVH VPS-2 in Europe:** no date for restock; the owner chose not to wait.
- Hetzner and DigitalOcean: as in ADR-0030 §2 (unavailable / several times the cost).
