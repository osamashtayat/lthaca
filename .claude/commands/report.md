---
description: Write the mandatory end-of-task report (AGENTS.md §5) for the work just done
---

Produce the end-of-task report for the work done in this session, exactly in the format of AGENTS.md §5.

Gather facts first, don't guess:
- `git status --short` and `git diff --stat` (plus `git log` for commits made in this task) for created/changed/deleted files.
- The commands you actually ran (build, test, migrations, lint, evals) and their real results. If something wasn't run, say "not run".
- Re-check the rules in AGENTS.md §2 against the diff: controllers/hubs thin, logic in services, no duplicated helpers or
  messages, no secrets, no fetch/toast/signalr imports in components. List any exception with its file and line.

Then output, with every field present:

Summary: what was done, in 2–4 sentences
Files created: list of paths
Files changed: list of paths, with one line each on what changed
Files deleted: list of paths (or "none")
Commands run: build, test and migration commands, with results
Decisions made: anything that wasn't specified and why
Rule check: confirm controllers are thin, logic is in services, nothing is duplicated, or list exceptions
Open questions / TODOs: anything left for a human
