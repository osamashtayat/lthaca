# Deployment (production)

How Ithaca runs in production, how to set it up once, how every merge is deployed, and how to roll back. The decision
is [ADR-0030](decisions/0030-hosting-pages-worker-vps.md); the backlog task is OPS-07. Backups are OPS-08, monitoring
OPS-09.

```
phone ──https──▶ Cloudflare ──┬── ithacamovies.com/*          ▶ Cloudflare Pages (the SPA, deploy/pages/_headers)
                              └── ithacamovies.com/api/*, /hubs/* ▶ Worker (deploy/cloudflare-worker)
                                         │ https, Full (strict)
                                         ▼
                              origin.ithacamovies.com = OVHcloud VPS-2, Frankfurt, port 443 open to Cloudflare only
                              docker-compose.prod.yml: db (pgvector) → migrate (one-shot) → api (Kestrel, origin cert)
```

| Piece | Where it is defined | Deployed by |
|---|---|---|
| SPA | `src/frontend`, headers in `deploy/pages/_headers` | `deploy.yml` → `wrangler pages deploy` |
| Worker | `deploy/cloudflare-worker` (`node --test` there) | `deploy.yml` → `wrangler deploy` |
| db, migrate, api | `docker-compose.prod.yml`, images on ghcr.io (private) | `deploy.yml` → SSH as `deploy`, `docker compose pull/up` |
| Server | `deploy/server/setup.sh`, the Cloudflare-only firewall unit | once, by hand (step 2) |
| Secrets | `/opt/ithaca/.env` on the server (template `deploy/server/production.env.example`) | by hand, never in git or CI |

## One-time setup (the owner)

Every step that needs a login, a key or a payment is done by the owner in that provider's dashboard. Agents never enter
keys or passwords.

### 1. Cloudflare: zone settings and the origin certificate
1. `ithacamovies.com` → **SSL/TLS → Overview: Full (strict)**. Edge Certificates: **Always Use HTTPS** on, minimum
   TLS 1.2, and **HSTS** on (max-age 6 months, include subdomains) once the site works over HTTPS.
2. **Network: WebSockets** on.
3. **SSL/TLS → Origin Server → Create Certificate**: RSA (2048), hostnames `ithacamovies.com` and `*.ithacamovies.com`,
   15 years. Save the certificate as `origin.pem` and the private key as `origin.key` (the key is shown only once).

### 2. The VPS
1. In the OVH control panel install **Ubuntu 24.04 LTS** with your own SSH public key. Log in as `ubuntu`.
2. On your computer, create the key GitHub Actions deploys with (no passphrase; keep the private half only in GitHub):
   ```bash
   ssh-keygen -t ed25519 -C ithaca-deploy -N "" -f ithaca-deploy
   ```
3. Copy `deploy/server/` and `ithaca-deploy.pub` to the server and run the setup (updates, automatic security updates,
   Docker, the `deploy` user, SSH keys only, ufw, the Cloudflare-only rule for port 443, folders):
   ```bash
   scp -r deploy/server ithaca-deploy.pub ubuntu@<vps-ip>:~/
   ```
   ```bash
   ssh ubuntu@<vps-ip> 'cd ~/server && sudo ./setup.sh ~/ithaca-deploy.pub'
   ```
   Before closing that session, check from a second terminal that `ssh ubuntu@<vps-ip>` still works.

### 3. Certificate and secrets on the server
1. Copy `origin.pem` and `origin.key` to `/etc/ithaca/certs/`, then run `sudo ./setup.sh ~/ithaca-deploy.pub` again: it
   gives both files to the container's user (uid 1654) with mode 400.
2. Create `/opt/ithaca/.env` from `deploy/server/production.env.example` with the real values
   (`sudo -u deploy nano /opt/ithaca/.env`, then `sudo chmod 600 /opt/ithaca/.env`).

### 4. DNS and the Pages project
1. DNS: an **A record `origin` → the VPS IPv4, Proxied** (orange cloud). This is the Worker's `ORIGIN`
   (`deploy/cloudflare-worker/wrangler.toml`). Users never see it.
2. Create the Pages project once (Direct Upload; GitHub Actions uploads each build):
   ```bash
   npx --yes wrangler@4.156.0 pages project create ithaca --production-branch main
   ```
3. Pages → `ithaca` → **Custom domains → add `ithacamovies.com`** (Cloudflare adds the DNS record). Optional: redirect
   `www` to the apex with a Redirect Rule.

### 5. GitHub: approval, secrets, variables
1. **Settings → Environments → New `production`**: Required reviewers = the owner; Deployment branches = `main` only.
   This is the one manual approval before every production deploy (a single job deploys API, Worker and site).
2. **Settings → Environments → New `preview`**: no reviewers; it holds only the Pages token for PR previews.
3. **Secrets** (never echoed by the workflow). The production ones are environment secrets, so only an approved run can
   use them; a workflow changed on a branch can't reach them.

   | Name | Where | Value |
   |---|---|---|
   | `DEPLOY_SSH_KEY` | environment `production` | the private half of `ithaca-deploy` |
   | `CLOUDFLARE_API_TOKEN` | environment `production` | API token with Account › Cloudflare Pages: Edit, Account › Workers Scripts: Edit, Zone `ithacamovies.com` › Workers Routes: Edit |
   | `SENTRY_AUTH_TOKEN` | environment `production` | a Sentry organization token (`osama-vz`) for source-map uploads |
   | `CLOUDFLARE_PAGES_TOKEN` | environment `preview` | a second API token with Account › Cloudflare Pages: Edit only (it could still publish to Pages' production branch, so PR authors must be trusted collaborators) |

4. **Variables** (repository variables, not secret; the deploy is skipped until they exist):

   | Name | Value |
   |---|---|
   | `PAGES_PROJECT` | `ithaca` |
   | `CLOUDFLARE_ACCOUNT_ID` | from the Cloudflare dashboard |
   | `DEPLOY_HOST` | the VPS IPv4 (SSH goes straight to the server, not through Cloudflare) |
   | `DEPLOY_SSH_KNOWN_HOSTS` | the output of `ssh-keyscan -t ed25519 <vps-ip>`, checked against `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub` on the server |
   | `SITE_URL` | `https://ithacamovies.com` |
   | `VITE_SENTRY_DSN` | the `ithaca-frontend` DSN (public by design) |

### 6. Before real users: blockers
- **BE-22 (issue #64):** Data Protection keys persisted. Without it every deploy signs everyone out and voids reset links.
- **BE-23 (issue #74):** the API trusts Cloudflare's ranges for `X-Forwarded-For`. Without it all users share one rate
  limit.

## Every deploy

1. A merge to `main` starts **Deploy** (`.github/workflows/deploy.yml`): it builds the site (with hidden source maps)
   and the two images (`ithaca-api`, `ithaca-migrate`, tagged with the commit).
2. The owner approves the `production` environment in the run, once.
3. One job then deploys, in order:
   - **API:** the compose file is copied to `/opt/ithaca`, the images are pulled, `migrate` runs, then the new `api`
     starts; the job waits for its health check and then for `/api/health/ready` at the origin
     (`origin.ithacamovies.com`, no Worker or Pages involved). The server keeps the running commit in
     `/opt/ithaca/DEPLOYED` and the one before in `PREVIOUS`.
   - **Worker** (`wrangler deploy`).
   - **Site:** the source maps go to Sentry and are deleted, so they are never served; then Pages (production branch
     `main`) and a smoke check through the domain.

If the API's health check fails after `up`, the new `api` container is left running and the run stops before the Worker
and site: roll back (below).

Pull requests get a Pages preview of the SPA (UI only: previews on `*.pages.dev` have no API, ADR-0030 §1).

Migrations must stay compatible with the API version before them (add columns and tables; remove in a later release):
the old API keeps serving for the seconds between `migrate` and the new `api`, and rollbacks rely on it.

## Rollback

| What | How | Time |
|---|---|---|
| **Site** | Cloudflare → Pages → `ithaca` → Deployments → an earlier production deployment → **Rollback to this deployment** | seconds |
| **API** (and everything) | Actions → **Deploy** → Run workflow, `ref` = the commit to go back to (`cat /opt/ithaca/PREVIOUS` on the server), approve. Works for commits from OPS-07 on (they contain `docker-compose.prod.yml` and the Worker) | a few minutes |
| **Worker** | `npx --yes wrangler@4.156.0 rollback` in `deploy/cloudflare-worker`, or the Run workflow above | seconds |

**A rollback across a migration:** the old API runs against the newer schema, which works when the migration only added
things (the rule above). If it can't, revert the schema first with the **newer** image (its bundle knows the
migration), then roll back:
```bash
ssh deploy@<vps-ip> 'cd /opt/ithaca && ITHACA_REGISTRY=ghcr.io/osamashtayat ITHACA_TAG=$(cat DEPLOYED) \
  docker compose -f docker-compose.prod.yml run --rm migrate <LastGoodMigrationName>'
```
A destructive migration that can't be reverted means restoring the database (OPS-08).

## Moving to another VPS (ADR-0030 §4)
Steps 2–3 on the new server, restore the latest dump (OPS-08), change the `origin` A record and `DEPLOY_HOST` /
`DEPLOY_SSH_KNOWN_HOSTS`, run Deploy. Pages, the Worker and the users' URLs don't change.

## OPS-07 acceptance checks

| Check | How |
|---|---|
| The domain serves the app from Pages over HTTPS | `curl -sI https://ithacamovies.com/` shows `server: cloudflare` and the `_headers` CSP; the app loads on a phone |
| `/api` goes through the Worker, not to Pages | `curl -s https://ithacamovies.com/api/health` returns `{"status":"ok"}` (JSON, not `index.html`) |
| Sign-in works (Secure cookies, antiforgery) | sign up and sign in on a phone; in desktop devtools the `ithaca_session` and `XSRF-TOKEN` cookies are `Secure`, `SameSite=Lax` |
| A room works across two phones | open a room on one phone, join from the link on another (mobile data, not the same Wi-Fi), vote to a match; both update live |
| Per-IP limits see real clients (after BE-23) | two networks each get their own limit (e.g. failed sign-ins from one phone don't lock the other out) |
| The VPS refuses direct traffic | from any machine: `curl -k --max-time 10 https://<vps-ip>/api/health` times out; `nc -vz <vps-ip> 5432` fails |
| Site and API rollbacks rehearsed once | do one site rollback and one API rollback (Run workflow with the previous commit), then deploy `main` again |
| Frontend errors show readable source lines in Sentry | in the browser console on the site run `setTimeout(() => { throw new Error('ithaca sentry check') })`; the event in `ithaca-frontend` shows the original `.tsx` lines |

## Troubleshooting
- **`/api/...` returns the SPA's HTML:** the Worker routes aren't matching. Check Workers → `ithaca-api-proxy` →
  Settings → Domains & Routes shows both routes on the `ithacamovies.com` zone.
- **Cloudflare 525/526:** the origin certificate or SSL mode. The server must present the Cloudflare origin certificate
  on 443 (`/etc/ithaca/certs`, readable by uid 1654) and the zone must be Full (strict).
- **Cloudflare 522 / timeouts:** the firewall. `sudo iptables -S ITHACA-CLOUDFLARE` lists Cloudflare's ranges and
  `sudo iptables -S DOCKER-USER` shows the jump (inbound on the public interface only, so the containers' own outbound
  HTTPS isn't affected); `sudo systemctl restart ithaca-cloudflare-firewall` re-applies them. Docker's published ports
  bypass ufw by design.
- **The deploy stops with "required variable … is missing":** `/opt/ithaca/.env` lacks a provider line; production
  never falls back to the Fake providers.
- **Everyone signed out after a deploy:** BE-22 (#64) isn't deployed yet.
