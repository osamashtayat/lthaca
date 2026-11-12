@AGENTS.md

# Claude Code specifics

The import above is the shared instruction set and applies in full; nothing here repeats or overrides it.

- **Commands:** `/new-endpoint <feature> <METHOD /path>`, `/new-feature <feature> <page>`, `/new-migration <Name>`, and `/report` (writes the mandatory end-of-task report).
- **Agent:** run `architecture-reviewer` (read-only) on a finished change before you report; fix its "Must fix" items.
- **Running the app:** `.claude/launch.json` has `web` (Vite on :5273, needs the API on :5280) and `stack` (Docker on :8080). Check plumbing at `/dev/status`.
- `.env` and secrets are deny-listed in `.claude/settings.json`; use `.env.example` to learn the keys.
