# Ithaca

Pick a movie tonight — alone or with friends. Mobile-first web app: swipe a deck, start it by mood or taste, or open a room
and vote with friends in real time.

**Agents and developers: start with [AGENTS.md](AGENTS.md).** Docs are in [docs/](docs/architecture.md).

```bash
cp .env.example .env
docker compose up --build        # http://localhost:8080  ·  reference slice: /films/550 (plumbing check /dev/status: Vite dev server only)
```

Status: architecture skeleton. One reference slice (`GET /api/films/{tmdbId}` → `/films/:tmdbId`) and the plumbing
(pgvector catalog, TMDB sync, Anthropic client with schema-enforced JSON, RoomHub, evals). Features are not built yet.
This website uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB.
