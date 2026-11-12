# Ithaca

Pick a movie tonight — alone or with friends. Mobile-first web app: swipe a deck, start it by mood or taste, or open a room
and vote with friends in real time.

**Agents and developers: start with [AGENTS.md](AGENTS.md).** Docs are in [docs/](docs/architecture.md).

```bash
cp .env.example .env
docker compose up --build        # http://localhost:8080  ·  plumbing check: /dev/status  ·  reference slice: /films/550
```

Status: architecture skeleton. One reference slice (`GET /api/films/{tmdbId}` → `/films/:tmdbId`) and the plumbing
(pgvector catalog table, RoomHub, Fake TMDB/LLM/embedding clients, evals). Features are not built yet.
