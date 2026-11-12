# ADR-0011: AGENTS.md as the only rule source; `.claude/commands` instead of rules/skills (deviation from FlowOps)

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
FlowOps has AGENTS.md plus `.claude/rules/*.md`, `.claude/skills/` and six agents. The brief asks for AGENTS.md as the single source of truth, CLAUDE.md that only imports it, `.claude/commands/` and one `architecture-reviewer` agent.

## Decision
- Rules live once in AGENTS.md (short) and docs/ (detail). `.claude/` contains only `settings.json` (permissions, secrets deny-list), `commands/` (`new-endpoint`, `new-feature`, `new-migration`, `report`), `agents/architecture-reviewer.md` and `launch.json`.
- CLAUDE.md is `@AGENTS.md` plus Claude-only pointers; it never restates a rule.

## Consequences
+ No drift between rule copies; any agent (Claude, Codex, Cursor) reads the same file.
- Claude loses path-scoped auto-loaded rules; the commands and the reviewer agent point to the docs instead.

## Alternatives rejected
FlowOps' rules folder (duplicates AGENTS.md content).
