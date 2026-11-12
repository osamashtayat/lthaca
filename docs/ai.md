# AI: LLM calls, prompts, embeddings, evals

The LLM is used **only at the edges**: (1) parse mood text into filters, (2) rank a candidate list we provide,
(3) write short explanations. It never chooses films on its own: candidates always come from our `films` table, and every
`tmdbId` it returns is checked against the list we sent.

> **TMDB terms (ADR-0017, decision pending):** TMDB's API terms restrict using TMDB content with ML/AI applications.
> Until the owner decides, do **not** send TMDB-derived text (overviews, titles in bulk) to the LLM or the embedding model,
> and keep `CatalogSync:EmbedOverviews = false`. Mood parsing (user text + generic genre names) is unaffected.

## Pieces
| Concern | Interface (Application) | Implementations (Infrastructure) | Config |
|---|---|---|---|
| Chat/completion | `ILlmClient.CompleteAsync(LlmRequest) → Result<LlmResponse>` | `FakeLlmClient`, `AnthropicLlmClient` (OpenAI: not built) | `Ai:LlmProvider`, `Ai:LlmModel`, `Ai:ModelOverrides`, `Ai:Effort`, `Ai:UseRefusalFallback`, `Ai:ApiKey` |
| Embeddings | `IEmbeddingClient.EmbedAsync(text) → float[Film.EmbeddingDimensions]`, `ModelName` | `FakeEmbeddingClient` (Voyage: not built, ADR-0013) | `Ai:EmbeddingProvider`, `Ai:EmbeddingApiKey` |
| Prompts | `IPromptLibrary.RenderAsync(PromptId, variables)` → text + output schema | `FilePromptLibrary` (reads `prompts/`) | `Ai:PromptsPath` |
| Typed output | `StructuredOutput.Parse<T>(response) → Result<T>` | — | — |
| Movie data | `ITmdbClient` (returns `Result`) | `FakeTmdbClient`, `TmdbHttpClient` | `Tmdb:Client`, `Tmdb:Language`, `Tmdb:ApiKey` |

Provider selection happens once, in `Infrastructure/DependencyInjection.cs → AddExternalClients`. Callers never know which
provider runs. Selecting a provider that isn't built, or a real provider without its key, fails at startup with a clear message.

## Keys (never in the repo)
```bash
dotnet user-secrets --project src/backend/Ithaca.Api set "Ai:ApiKey" "<anthropic key>"
dotnet user-secrets --project src/backend/Ithaca.Api set "Tmdb:ApiKey" "<tmdb api key or read access token>"
dotnet user-secrets --project src/backend/Ithaca.Api set "Ai:LlmProvider" "Anthropic"
dotnet user-secrets --project src/backend/Ithaca.Api set "Tmdb:Client" "Http"
```
For Docker, put the same values in `.env` (`Ai__ApiKey`, `Tmdb__ApiKey`, `Ai__LlmProvider`, `Tmdb__Client`); `.env` is git-ignored.
For the evals runner, export them as environment variables (`Ai__ApiKey=… Ai__LlmProvider=Anthropic`).

## Anthropic client (ADR-0018)
- Official `Anthropic` SDK, beta Messages endpoint. The prompt file is the system prompt; `LlmRequest.Input` is the single
  user message; the prompt's schema file is sent as `output_config.format`, so the API returns JSON matching it.
- Model: `Ai:LlmModel` (default `claude-opus-5-5`). Per prompt: `Ai:ModelOverrides`, e.g.
  `"Ai": { "ModelOverrides": { "mood-parse": "claude-haiku-5-5" } }` — the main cost lever. `Ai:Effort` optional.
- Refusals: `Ai:UseRefusalFallback` (default on) asks the API to re-serve a policy decline on a fallback model; a refusal that
  still stands is `ai.refused`. Provider/network failures are `ai.unavailable`; truncated or invalid replies `ai.invalid_output`.

## Prompts
- Files: `prompts/<name>.v<N>.md` + `prompts/<name>.v<N>.schema.json`; ids in `Application/Ai/IPromptLibrary.cs → Prompts`.
- Schemas are closed (`additionalProperties: false`, every property in `required`; optional values as `anyOf [type, null]`)
  and match the C# output record (`MoodParseOutput`, `RankCandidatesOutput`).
- `{{variable}}` placeholders are filled by the caller. **User text (mood words) goes in `LlmRequest.Input`, never into the
  prompt template.**
- Never edit a shipped version: add `v{N+1}` (both files), point `Prompts.X` at it, run the evals for both.
- Prompt files ship with the API (linked in `Ithaca.Api.csproj`, copied in the Dockerfile).

## Structured outputs
- Parse with `StructuredOutput.Parse<T>`; invalid JSON → `ai.invalid_output` (HTTP 502), never an exception to the user.
- After parsing, the feature service validates meaning: genres from our list only, `tmdbId ∈ candidates`, hard no's removed.
  The model's output is advice, never trusted data.

## Embeddings and similarity (ADR-0002, ADR-0013)
- One vector per film in `films.embedding` (`vector(1024)`, HNSW cosine index) plus `films.embedding_model`.
- Filled by the catalog sync when `CatalogSync:EmbedOverviews` is on (off: ADR-0017). The sync re-embeds a film when its
  text changed or its stored model differs from the configured one.
- `TasteProfile` (taste vector) lives in the same space: build it from embeddings of films the user liked.
- Never compare vectors from different models (`embedding_model` tells them apart).

## Catalog sync (ADR-0003)
- `CatalogSyncService`: TMDB discover pages (most popular first) → upsert by `TmdbId` → optional embedding → delete films not
  refreshed within `CatalogSync:MaxAgeDays` (≤ 180: TMDB's 6-month caching limit). Saves once per page.
- Run once: `dotnet run --project src/backend/Ithaca.Api -- --sync-catalog`. Nightly: `CatalogSync:Enabled = true`.
- Attribution: the footer notice is required wherever TMDB data is shown (logo still TODO, ADR-0017).

## Adding a provider (e.g. Voyage embeddings, OpenAI chat)
1. Implement the interface in `Infrastructure/Ai/` (official SDK if one exists for .NET; otherwise a typed `HttpClient`).
2. Add the case to the switch in `AddExternalClients` and extend the startup validation (provider allowed + key required).
3. Keys via user-secrets/env only. 4. Run `dotnet run --project evals/Ithaca.Evals -- --live` with the provider set.

## Evals (`evals/`)
- `evals/cases/*.json`, one case per file:
  `id`, `description`, `prompt` (`name.vN`), `variables`, `input` or `candidates` (TMDB ids), `recordedResponse`, `checks[]`.
- Check types (`EvalChecks.cs`): `validJson`, `excludesGenre`, `doesNotIncludeGenre`, `idsFromCandidates`, `excludesTmdbId`.
- **Replay mode (default):** the recorded reply is checked. No network, safe for CI.
- **Live mode (`-- --live`):** the configured `ILlmClient` answers and the reply is printed. With `Fake` the cases are expected
  to fail (no intelligence); with Anthropic this is the quality gate before shipping a prompt version. Live runs cost money.
- Every new AI behaviour ships with at least one case, including a "must not" case.
