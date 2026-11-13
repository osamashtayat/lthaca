# Moods

`MoodQuery` (Domain/Moods): mood chips, runtime, language and free text → filters. Built for solo decks (BE-09); the room
mood step (BE-11) reuses the same pieces.

- `MoodInterpreter`: the ADR-0022 rules (chips via `MoodChips`, text via `mood-parse.v2`, genres checked against our
  catalog, `mood.unclear`, `textIgnored`). A collaborator, not a use-case service; it doesn't save.
- `MoodQueryDtos.From`: the one `MoodQuery` → `MoodQueryDto` mapping.
- `MoodErrors`: `mood.unclear`. Never put the person's text in a message or a log.
- Full flow: docs/ai.md → Mood flow.
