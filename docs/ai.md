# AI: LLM calls, prompts, embeddings, evals

The LLM is used **only at the edges**: (1) parse mood text into filters, (2) rank a candidate list we provide,
(3) write short explanations. It never chooses films on its own: candidates always come from our `films` table, and every
`tmdbId` it returns is checked against the list we sent.

## Pieces
| Concern | Interface (Application) | Implementations (Infrastructure) | Config |
|---|---|---|---|
| Chat/completion | `ILlmClient.CompleteAsync(LlmRequest) → LlmResponse(Json)` | `FakeLlmClient` (Anthropic, OpenAI: not built) | `Ai:LlmProvider`, `Ai:LlmModel`, `Ai:ApiKey` |
| Embeddings | `IEmbeddingClient.EmbedAsync(text) → float[Film.EmbeddingDimensions]` | `FakeEmbeddingClient` (deterministic) | `Ai:EmbeddingProvider` |
| Prompts | `IPromptLibrary.RenderAsync(PromptId, variables)` | `FilePromptLibrary` (reads `prompts/`) | `Ai:PromptsPath` |
| Typed output | `StructuredOutput.Parse<T>(response) → Result<T>` | — | — |
| Movie data | `ITmdbClient` | `FakeTmdbClient` (Http: not built) | `Tmdb:Client`, `Tmdb:BaseUrl`, `Tmdb:ApiKey` |

Provider selection happens once, in `Infrastructure/DependencyInjection.cs → AddExternalClients`. Callers never know which
provider runs. Selecting a provider that isn't built fails at startup with a clear message.

## Prompts
- Files: `prompts/<name>.v<N>.md`, ids in `Application/Ai/IPromptLibrary.cs → Prompts` (`Prompts.MoodParse` = `mood-parse.v1`).
- Each file starts with a comment naming its output record, and spells out the JSON shape.
- `{{variable}}` placeholders are filled by the caller. **User text (mood words) goes in `LlmRequest.Input`, never into the
  prompt template.**
- Never edit a shipped version: add `v{N+1}`, point `Prompts.X` at it, run the evals for both, keep the old file until removed
  on purpose.
- Prompt files ship with the API (linked in `Ithaca.Api.csproj`, copied in the Dockerfile).

## Structured outputs
- Every prompt has a C# record (`MoodParseOutput`, `RankCandidatesOutput`) in `Application/Ai/`. Parse with
  `StructuredOutput.Parse<T>`; invalid JSON → `Error(ai.invalid_output)` (HTTP 502), never an exception to the user.
- After parsing, the feature service validates meaning: genres from our list only, `tmdbId ∈ candidates`, hard no's removed.
  The model's output is advice, never trusted data.

## Embeddings and similarity
- One vector per film in `films.embedding` (`vector(1536)`, HNSW cosine index), filled by the nightly sync (ADR-0002/0003).
- `TasteProfile` (taste vector) lives in the same space: build it from embeddings of films the user liked.
- Changing the model or dimension = new constant + migration + re-embed; never mix vectors from two models.

## Adding a provider (e.g. Anthropic)
1. `Infrastructure/Ai/AnthropicLlmClient.cs` implementing `ILlmClient` (HTTP via `IHttpClientFactory`; add the SDK package
   only with a reason in the PR); map `LlmRequest.Prompt` → system, `Input` → user; ask for JSON only.
2. Add the case to the switch in `AddExternalClients` and relax the "only Fake" startup validation for it.
3. Keys via env (`Ai__ApiKey`), never committed. 4. Run `dotnet run --project evals/Ithaca.Evals -- --live` with the provider set.

## Evals (`evals/`)
- `evals/cases/*.json`, one case per file:
  `id`, `description`, `prompt` (`name.vN`), `variables`, `input` or `candidates` (TMDB ids), `recordedResponse`, `checks[]`.
- Check types (`EvalChecks.cs`): `validJson`, `excludesGenre`, `doesNotIncludeGenre`, `idsFromCandidates`, `excludesTmdbId`.
  Add a type there when a feature needs it.
- **Replay mode (default):** the recorded reply is checked. Proves the prompt renders and the checks hold for a known-good
  reply; no network, safe for CI.
- **Live mode (`-- --live`):** the configured `ILlmClient` answers. With the `Fake` provider the cases are expected to fail
  (it has no intelligence); with a real provider this is the quality gate before shipping a prompt version.
- Every new AI behaviour ships with at least one case, including a "must not" case (no horror for "not scary"; group hard no's
  respected; ids only from candidates).
