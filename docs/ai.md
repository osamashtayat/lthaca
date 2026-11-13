# AI: LLM calls, prompts, embeddings, evals

The LLM is used **only at the edges**: (1) parse mood text into filters, (2) rank a candidate list we provide,
(3) write short explanations. It never chooses films on its own: candidates always come from our `films` table, and every
`tmdbId` it returns is checked against the list we sent.

> **Data sources (ADR-0017):** TMDB is for **display** (details, posters, trailers); **Wikidata** (CC0) feeds the **AI**
> (embeddings, ranking, explanations). Never send TMDB-derived text (overviews, taglines, TMDB keywords) to the LLM or the
> embedding model: the sync embeds `FilmFacts.EmbeddingText` (Wikidata) only. Mood parsing (user text + generic genre
> names) is unaffected.
> Our own user data (votes, likes, ratings, imports) may be used freely. AI-safe film data lives in `film_facts`
> (`FilmFacts`), filled by the Wikidata enricher; `films` holds the TMDB display data.

## Pieces
| Concern | Interface (Application) | Implementations (Infrastructure) | Config |
|---|---|---|---|
| Chat/completion | `ILlmClient.CompleteAsync(LlmRequest) → Result<LlmResponse>` | `FakeLlmClient`, `AnthropicLlmClient` (OpenAI: not built) | `Ai:LlmProvider`, `Ai:LlmModel`, `Ai:ModelOverrides`, `Ai:Effort`, `Ai:UseRefusalFallback`, `Ai:ApiKey` |
| Embeddings | `IEmbeddingClient.EmbedAsync(texts, Document\|Query) → Result<float[Film.EmbeddingDimensions][]>`, `ModelName` | `FakeEmbeddingClient`, `VoyageEmbeddingClient` (ADR-0013) | `Ai:EmbeddingProvider`, `Ai:EmbeddingModel`, `Ai:EmbeddingApiKey` |
| Prompts | `IPromptLibrary.RenderAsync(PromptId, variables)` → text + output schema | `FilePromptLibrary` (reads `prompts/`) | `Ai:PromptsPath` |
| Typed output | `StructuredOutput.Parse<T>(response) → Result<T>` | — | — |
| Movie data | `ITmdbClient` (returns `Result`) | `FakeTmdbClient`, `TmdbHttpClient` | `Tmdb:Client`, `Tmdb:Language`, `Tmdb:ApiKey` |
| AI-safe film facts | `IWikidataClient.GetFilmsAsync(tmdbIds)` (returns `Result`) | `FakeWikidataClient`, `WikidataHttpClient` (SPARQL) | `Wikidata:Client`, `Wikidata:Endpoint`, `Wikidata:UserAgent`, `Wikidata:BatchSize`, `Wikidata:RefreshDays` (no key) |

Provider selection happens once, in `Infrastructure/DependencyInjection.cs → AddExternalClients`. Callers never know which
provider runs. Selecting a provider that isn't built, or a real provider without its key, fails at startup with a clear message.

## Keys (never in the repo)
```bash
dotnet user-secrets --project src/backend/Ithaca.Api set "Ai:ApiKey" "<anthropic key>"
dotnet user-secrets --project src/backend/Ithaca.Api set "Tmdb:ApiKey" "<tmdb api key or read access token>"
dotnet user-secrets --project src/backend/Ithaca.Api set "Ai:EmbeddingApiKey" "<voyage key>"
dotnet user-secrets --project src/backend/Ithaca.Api set "Ai:EmbeddingProvider" "Voyage"
dotnet user-secrets --project src/backend/Ithaca.Api set "Ai:LlmProvider" "Anthropic"
dotnet user-secrets --project src/backend/Ithaca.Api set "Tmdb:Client" "Http"
```
For Docker, put the same values in `.env` (`Ai__ApiKey`, `Ai__EmbeddingApiKey`, `Tmdb__ApiKey`, `Ai__LlmProvider`,
`Ai__EmbeddingProvider`, `Tmdb__Client`); `.env` is git-ignored.
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

## Unclear, off-topic and injected text (ADR-0022)
- `mood-parse.v2` returns `intent`: `mood` | `unclear` | `offTopic`. The model only labels the text; instructions inside it
  ("ignore your rules", fake `SYSTEM:` lines, closing tags) are `offTopic` and never followed. Filters are empty unless
  `intent` is `mood`.
- The mood service decides: chips + non-mood text → chips only, `textIgnored: true`; no chips + non-mood text → 422
  `mood.unclear`; empty text → no LLM call. Real refusals stay `ai.refused`.
- Never repeat the user's text in a response, message or log; user-facing copy says "we", never "the AI".

## Embeddings and similarity (ADR-0002, ADR-0013)
- One vector per film in `films.embedding` (`vector(1024)`, HNSW cosine index) plus `films.embedding_model`.
- Filled by the catalog sync (`FilmEmbedder`, after the Wikidata step) from `FilmFacts.EmbeddingText` only; films without
  facts get no vector, so TMDB text never reaches the model (ADR-0017; `CatalogSync:EmbedOverviews` is gone).
- Re-embedded only when needed: no vector yet, `films.embedding_model` ≠ the configured model, or the facts' text changed
  (`films.embedding_source_hash` ≠ `film_facts.embedding_text_hash`, SHA-256 of the text). `CatalogSync:EmbeddingBatchSize`
  (100) films per request and per save; a failure keeps finished batches.
- **Voyage** (`Ai:EmbeddingProvider = Voyage`, needs `Ai:EmbeddingApiKey`; startup fails clearly without it): typed
  `HttpClient` (no .NET SDK) → `POST https://api.voyageai.com/v1/embeddings`, model `Ai:EmbeddingModel` (`voyage-4`),
  `output_dimension` 1024, `input_type` `document` for films and `query` for mood/taste text, ≤ 1,000 texts per request.
  429 and 5xx are retried up to 6 attempts, waiting `Retry-After` when sent, else 1, 2, 4… s (capped at 120 s); other
  failures are `ai.unavailable`; the wrong number or size of vectors is `ai.invalid_output`. Texts are never logged.
  Without a payment method Voyage's rate limits are low (not published); keep the batch size modest. First 200M tokens free.
- `TasteProfile` (taste vector) lives in the same space: build it from embeddings of films the user liked.
- Never compare vectors from different models (`embedding_model` tells them apart).

## Catalog sync (ADR-0003)
- `CatalogSyncService`: TMDB discover pages (most popular first) → upsert by `TmdbId` (display data) → delete films not
  refreshed within `CatalogSync:MaxAgeDays` (≤ 180: TMDB's 6-month caching limit) → Wikidata enrichment (below) →
  embeddings (above). Saves once per page and once per Wikidata or embedding batch. A Wikidata or embedding failure fails
  the run (`wikidata.unavailable`, `ai.*`); the work before it is already saved.
- Run once: `dotnet run --project src/backend/Ithaca.Api -- --sync-catalog`. Nightly: `CatalogSync:Enabled = true`.
- Attribution: the footer notice is required wherever TMDB data is shown (notice + small TMDB logo in `AppFooter`, ADR-0017).

## Wikidata enricher (ADR-0017)
- `WikidataEnricher` (a collaborator of `CatalogSyncService`, not a use-case service), the last step of the sync: films without `FilmFacts`, or with facts older than
  `Wikidata:RefreshDays` (30), in TMDB-id order, `Wikidata:BatchSize` (100) per SPARQL query, one save per batch, so a
  failed run keeps its finished batches and the next run continues. Films Wikidata doesn't link are logged (TMDB ids
  only) and skipped, and retried next run.
- Linking: Wikidata's "TMDB movie ID" (P4947) only, never title matching. Two items for one id → the oldest (lowest Q).
- Facts (English labels): genres (P136), main subjects (P921), directors (P57), cast (P161, best-known first by sitelink
  count, since Wikidata keeps no billing order), countries of origin (P495), original languages (P364), earliest
  publication year (P577), shortest duration (P2047, in minutes), awards (P166). Lists are de-duplicated and capped
  (`FilmFacts.Max*`).
- `FilmFacts.EmbeddingText` is built by `FilmFacts.BuildEmbeddingText(WikidataFacts)`: one labelled line per fact in a fixed
  order, deterministic. Its only input is the Wikidata record, so TMDB text can't reach it (a test checks this). The sync
  embeds it; changing the format means re-embedding the catalog.
- `Wikidata:Client`: `Fake` (default; knows films 550 and 27205, not 496243) or `Http`. The HTTP client POSTs to
  `query.wikidata.org/sparql` with the descriptive User-Agent from `Wikidata:UserAgent` (a project URL, never a personal
  email), 70 s timeout; any failure status (incl. 429), timeout or unreadable body is `wikidata.unavailable`.
- Wikidata is CC0: no attribution or age limit; `film_facts` rows go when their film is purged (cascade).

## Adding a provider (e.g. Voyage embeddings, OpenAI chat)
1. Implement the interface in `Infrastructure/Ai/` (official SDK if one exists for .NET; otherwise a typed `HttpClient`).
2. Add the case to the switch in `AddExternalClients` and extend the startup validation (provider allowed + key required).
3. Keys via user-secrets/env only. 4. Run `dotnet run --project evals/Ithaca.Evals -- --live` with the provider set.

## Evals (`evals/`)
- `evals/cases/*.json`, one case per file:
  `id`, `description`, `prompt` (`name.vN`), `variables`, `input` or `candidates` (TMDB ids), `recordedResponse`, `checks[]`.
- Check types (`EvalChecks.cs`): `validJson`, `excludesGenre`, `doesNotIncludeGenre`, `idsFromCandidates`, `excludesTmdbId`;
  BE-09 adds `intentIs` and `mustNotContain` (text that must not appear anywhere in the reply).
- Each case has a `kind`: `representative`, `edge` or `adversarial` (BE-09); the runner reports counts per kind. A
  prompt's suite covers all three, e.g. mood-parse: ~10 / ~5 / ~5 (ADR-0022).
- **Replay mode (default):** the recorded reply is checked. No network, safe for CI.
- **Live mode (`-- --live`):** the configured `ILlmClient` answers and the reply is printed. With `Fake` the cases are expected
  to fail (no intelligence); with Anthropic this is the quality gate before shipping a prompt version. Live runs cost money.
- Every new AI behaviour ships with at least one case, including a "must not" case.
