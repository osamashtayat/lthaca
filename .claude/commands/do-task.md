---
description: Work one backlog task end to end (read → worktree → implement → verify → review → PR → report)
argument-hint: <task ID, e.g. BE-05 or FE-04>
---

Do backlog task `$ARGUMENTS` following AGENTS.md §4 exactly. Stop and ask the owner (or open an issue, AGENTS.md §4.4)
instead of guessing when something below can't be done.

1. **Read the task.** Find `$ARGUMENTS` in `docs/backlog.md`. Check: its status is `todo`; every task in "Depends on" is
   `done` (frontend tasks depend on contract tasks such as BE-01 or BE-14a, never on backend logic); it's in your lane;
   it's not a `P-` proposal; nobody claimed it (`gh pr list --state open --search "[$ARGUMENTS] in:title"`,
   `git ls-remote origin 'refs/heads/task/$ARGUMENTS-*'` both empty). If any check fails, stop and say why.
2. **Read the context.** The contract sections it names in `docs/api-contract.md`, the docs table at the top of AGENTS.md
   for your side, the ADRs it mentions, and the files you will touch. Note anything ambiguous; ask before deciding.
3. **Branch + worktree** (AGENTS.md §4.2), from the latest `origin/main`:
   `git fetch origin && git worktree add -b task/$ARGUMENTS-<short-name> .claude/worktrees/$ARGUMENTS origin/main`,
   then work only inside `.claude/worktrees/$ARGUMENTS`. Frontend: `cd src/frontend && npm ci`.
   First commit: set the task to `in progress` in `docs/backlog.md`; push with `-u`; open a **draft PR** titled
   `[$ARGUMENTS] <title>` right away (that is the claim other agents see).
4. **Implement**, within the task's "Scope in" only, following AGENTS.md §2–3:
   - Backend: `/new-endpoint`, `/new-migration` as needed; contract changes update `docs/api-contract.md`,
     `./scripts/export-openapi.sh` and `npm run api:types` in the same branch. Tests stay offline (Fake providers).
   - Frontend: `/new-feature`; add MSW handlers for every endpoint you consume (docs/frontend-conventions.md → Mock API);
     match the design board named in the task (docs/frontend-conventions.md → Design reference). Never edit the contract.
5. **Verify** with the commands from AGENTS.md §1 for your side, checking exit codes:
   backend `dotnet build Ithaca.slnx -warnaserror && dotnet test tests/backend/Ithaca.Tests` (+ evals if AI changed);
   frontend `cd src/frontend && npm run typecheck && npm run lint && npm test && npm run build`. UI work: open the page at
   375px with mocks on, and with `VITE_API_MOCKS=off` when the backend part is `done`. Every acceptance criterion must be
   shown, not assumed.
6. **Review:** run the `architecture-reviewer` agent on the branch diff; fix every "Must fix".
7. **Finish:** set the task to `done` in `docs/backlog.md`; merge/rebase `origin/main` again; push; mark the draft PR
   ready, its body ending with the §5 report and the Claude Code attribution line. When CI is green,
   squash-merge, delete the branch, `git worktree remove .claude/worktrees/$ARGUMENTS`. Red CI → never merge: leave the PR
   open, open an issue, set the task to `blocked (#n)`.
8. **Report:** end with `/report` (AGENTS.md §5), listing every issue you opened or updated.
