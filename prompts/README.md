# Prompts

Versioned prompt files, loaded by `IPromptLibrary` (`FilePromptLibrary`). Rules (full version in `docs/ai.md`):

- File name `{name}.v{version}.md`; the id lives in `Ithaca.Application/Ai/IPromptLibrary.cs → Prompts`.
- Never edit a version that has shipped: copy it to `v{n+1}`, switch `Prompts.X` to the new version, run the evals.
- Variables are `{{name}}` and are filled by the caller; the user's own text goes in the request input, never pasted into the prompt.
- Every prompt names its JSON output shape, which matches a C# record in `Ithaca.Application/Ai/`.
- The user message of `rank-candidates.v2` and `explain-pick.v2` is a typed record too (`PickPrompts.cs`): Wikidata facts
  and our own data only, never TMDB text (ADR-0017).

| Prompt (live version) | Used by | Output |
|---|---|---|
| `mood-parse.v2` | `MoodInterpreter` (solo and room moods) | `MoodParseOutput` |
| `rank-candidates.v2` | `PickAdvisor.RankAsync` (solo top 3) | `RankCandidatesOutput` |
| `explain-pick.v2` | `PickAdvisor.ExplainAsync` (a room's picks) | `ExplainPicksOutput` |
