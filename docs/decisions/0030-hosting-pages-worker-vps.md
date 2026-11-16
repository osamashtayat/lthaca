# ADR-0030: Hosting: Cloudflare Pages for the site, a Worker for `/api` and `/hubs`, the API and database on one OVHcloud VPS

- **Status:** Proposed (OPS-06; the owner decided L1, L2 and the provider on 2026-11-13 and 2026-11-16, and accepts this ADR)
- **Date:** 2026-11-16
- **Decides:** L1 and L2 (backlog, Launch decisions) and the VPS provider. Provisioning is OPS-07.

## Context
Ithaca has to go live somewhere. What exists today:
- **One origin everywhere.** In Docker, nginx (`web`) serves the SPA and proxies `/api` and `/hubs` to `api`, which has
  no published port (`src/frontend/docker/nginx.conf`). In development, Vite's proxy does the same
  (`vite.config.ts`). The SPA calls relative URLs, so it has no CORS setup and needs none.
- **Cookie auth** (ADR-0012, ADR-0023): `ithaca_session`, `ithaca_af` and the readable `XSRF-TOKEN`, all SameSite=Lax
  with no `__Host-` prefix, Secure in Production only (`AuthSetup`: `CookieSecurePolicy.Always` when
  `IsProduction()`). Every unsafe request carries `X-XSRF-TOKEN`. SignalR uses the same session cookie.
- **Plain HTTP locally** (ADR-0009); TLS was always meant to terminate in front of the app.
- **One API instance** (ADR-0016): the nightly sync and SignalR groups live in process.
- **The stack is Docker Compose**: `db` (PostgreSQL 17 + pgvector), the one-shot `migrate`, `api`, `web`.
- **Sentry (EU) and an uptime monitor** (ADR-0028); **TMDB attribution** in the site footer (ADR-0017).

Constraints: Cloudflare Workers can't run ASP.NET Core and Cloudflare has no PostgreSQL with pgvector, so the API and
database need a server. The domain is registered at Cloudflare, and Cloudflare sits in front of the site. Phones are
≈ 95% of users, and the expected users are in Europe and the Middle East.

## Decision

### 1. Site on Cloudflare Pages; a Worker keeps one origin ("B1", owner 2026-11-13)
- **Cloudflare Pages** builds and serves `src/frontend` (`npm run build`, output `dist`, a preview per PR).
- **A Cloudflare Worker on the same domain** has the routes `<domain>/api/*` and `<domain>/hubs/*`. It forwards each
  request to the VPS origin unchanged: method, path, query, body, the `Cookie` header and `Set-Cookie` in replies,
  `X-XSRF-TOKEN`, and WebSocket upgrades on `/hubs/*` (passed through, not terminated in the Worker). It adds no logic of
  its own: no auth, caching or rewriting of API responses.
- **The VPS runs `db`, `migrate` and `api` only.** There's no `web` (nginx) container in production; Pages takes its
  place.
- The browser only ever sees `https://<domain>`, so the SPA, the cookies (host-only, Lax) and antiforgery work exactly
  as now. **No CORS, no app code changes.**

### 2. VPS: OVHcloud VPS-2, Frankfurt (owner 2026-11-16)
- **4 vCores, 8 GB RAM, 75 GB NVMe**, unlimited traffic, daily automatic backups kept 7 days, **billed monthly** (no
  long commitment). About **£6.29 + VAT a month** when checked (2026-11-16).
- **Why:** the most CPU and RAM per pound among the options, monthly billing, and a region near the users. 8 GB rather
  than the 2–4 GB first estimated, so PostgreSQL with pgvector, the API, the catalog sync and the Letterboxd import
  can run side by side without memory pressure.
- **Compared:**

  | Option | Verdict |
  |---|---|
  | **OVHcloud VPS-2** (Frankfurt) | **Chosen**: 4 vCores / 8 GB, monthly, cheapest per resource |
  | Hostinger KVM 2 | Runner-up: better-rated support, but 2 vCores and 24 months prepaid for its price |
  | Hetzner | 2026 price rises; new servers unavailable when checked |
  | DigitalOcean | ≈ $48 a month for 8 GB: several times the cost for the same size |

- **Running cost at launch:** the VPS (≈ £6.29 + VAT a month). Pages, the Worker, DNS, the proxy and the origin
  certificate fit Cloudflare's free plans at launch traffic; if the Worker outgrows the free daily request allowance,
  Workers Paid (about $5 a month) lifts it (OPS-07 checks the current limits). The domain renewal is billed yearly by
  Cloudflare. AI ($25 a month, L3), email (Resend) and Sentry are separate decisions.

### 3. TLS
- **Cloudflare proxied DNS** for the site and for the VPS origin hostname the Worker calls, SSL mode **"Full (strict)"**,
  WebSockets on.
- **On the VPS, TLS uses a Cloudflare origin certificate** (trusted only by Cloudflare, long-lived, no renewal job).
  OPS-07 decides whether Kestrel or a small proxy in front of `api` presents it.
- **The VPS firewall opens the HTTPS port to Cloudflare's IP ranges only**, plus SSH (keys only). The database port is
  never public (`127.0.0.1` binding as today, or none). Direct requests to the server's IP are refused.
- **Local development stays plain HTTP** (ADR-0009 unchanged): Vite on :5273, Docker on :8080, LAN phone testing as now.

### 4. What this means for the existing ADRs
- **ADR-0009:** confirmed. TLS terminates at Cloudflare (browser side) and on the VPS (origin side, Full strict);
  locally nothing changes. In production nginx is replaced by Pages + the Worker; `nginx.conf` stays for the local
  Docker stack and the CI smoke test.
- **ADR-0012 / ADR-0023:** unchanged. Cookies stay Lax and host-only, **Secure in Production**
  (`ASPNETCORE_ENVIRONMENT=Production` on the VPS). Same origin, so a shared room link opened from a chat app still
  arrives with the session, and antiforgery needs no exemption.
- **ADR-0016:** unchanged. One VPS runs one `api` container, so the nightly sync and SignalR groups need no lock and no
  backplane.
- **ADR-0017:** the footer attribution ships with the SPA, so Pages serves it as before.
- **ADR-0028:** the uptime monitor (OPS-09) checks `https://<domain>/api/health/ready`, so it covers Pages, the Worker
  and the VPS in one probe. Sentry needs the production DSN on the VPS and at the Pages build (OPS-07).
- **Data Protection keys must persist before go-live (issue #64).** Today a restart or redeploy creates a new key ring
  and signs everyone out and voids reset links. Every production deploy recreates the `api` container, so #64 must be
  fixed (keys in PostgreSQL, or a volume) before OPS-07 goes live.
- **Backups are off the server (OPS-08).** OVH's daily snapshots stay as a second layer only; the nightly `pg_dump`
  goes to storage that isn't at OVH (e.g. Cloudflare R2), with a tested restore.
- **Risk: OVH support is slow.** The app must not depend on it. Everything that runs on the VPS is the Compose stack,
  secrets in `.env` and the off-server dump, so **moving to another VPS is: provision it (OPS-07 steps), restore the
  latest dump, point the Worker's origin and the origin DNS record at the new server.** Pages, the Worker, the domain
  and the users' URLs don't change, and users stay signed in once #64 keeps the keys in the database.

### 5. Open for the owner
- **The production domain.** `ithacamovies.com` is proposed; the owner confirms it (or names another) before OPS-07
  configures Pages, the Worker routes, `Email:SiteUrl` and the email sending subdomain (L4).

## Consequences
+ One origin: no CORS, no preflights, no cookie or antiforgery changes, no frontend changes.
+ Static files from Cloudflare's edge, close to phones; the VPS only serves the API and the hub.
+ Low fixed cost (one small monthly VPS); no long contract; a move to another VPS is a restore plus a DNS/Worker change.
+ The VPS can't be reached except through Cloudflare.
- One more moving piece than nginx-on-the-VPS: the Worker lives in the repo (`deploy/cloudflare-worker/`) with its own
  test, and a broken Worker breaks the API for everyone (OPS-07: rollback for it as for the site).
- What nginx does today has to be redone for Pages in OPS-07: the Content-Security-Policy and `nosniff` headers and
  long caching for `/assets/` (a Pages `_headers` file), and the SPA fallback to `index.html`.
- **Client IPs for rate limits (OPS-07):** requests reach the API from Cloudflare, not from nginx on a private network.
  The API trusts `X-Forwarded-For` only from `RateLimits:TrustedProxyNetworks` (private ranges today), so production
  must trust Cloudflare's ranges there (or a proxy on the VPS must set the header from `CF-Connecting-IP`); otherwise
  every user shares the limit of a Cloudflare address.
- A single VPS is a single point of failure for the API (accepted at launch; ADR-0016 lists what scaling out needs).
- OVH support is slow; mitigated only by the backups and the move path above.

## Alternatives rejected
- **Option A: everything on the VPS** (nginx `web` too, cached by Cloudflare): simpler, one piece less, but the owner
  prefers Pages for the site.
- **Option B2: the API on `api.<domain>`**: cross-origin cookies, CORS and preflights, antiforgery-cookie and client
  changes, and ADR-0012/0023 reopened.
- **Managed container host + managed PostgreSQL with pgvector:** less upkeep, much higher monthly cost.
- **Backend on Cloudflare:** not possible (no ASP.NET Core runtime in Workers, no Postgres with pgvector).
- **Other VPS providers:** see the comparison in §2.
