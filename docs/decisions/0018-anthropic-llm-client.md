# ADR-0018: Anthropic client: official SDK, schema-enforced JSON, Result errors, server-side fallback

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
The LLM provider must be swappable, outputs must be typed, and the model must never invent films. The Anthropic Messages API supports structured outputs (a JSON schema enforced by the API) and server-side refusal fallback.

## Decision
- `AnthropicLlmClient` (Infrastructure) uses the official `Anthropic` NuGet SDK (13.8.0) on the beta Messages endpoint.
- Each prompt has `prompts/<name>.vN.schema.json` (closed schema: `additionalProperties: false`, all fields required) sent as `output_config.format`; the reply is validated again by `StructuredOutput.Parse<T>`.
- `ILlmClient.CompleteAsync` returns `Result<LlmResponse>`: provider/network failure → `ai.unavailable` (503), refusal that survives fallback → `ai.refused` (422), truncated/empty/invalid → `ai.invalid_output` (502). The SDK retries 408/409/429/5xx twice.
- Model by config: `Ai:LlmModel` (default `claude-opus-5-5`) with per-prompt `Ai:ModelOverrides` (e.g. `"mood-parse": "claude-haiku-5-5"` to cut cost). Optional `Ai:Effort`.
- Refusal fallback `"default"` (beta `server-side-fallback-2026-07-01`) on by default (`Ai:UseRefusalFallback`); on models without fallback support the request just runs as is.
- Keys only from user-secrets/env (`Ai:ApiKey`); startup fails if `Anthropic` is selected without one.

## Consequences
+ JSON shape guaranteed by the API, typed in C#, checked in evals.
+ Cost tunable per prompt without code changes.
- Uses beta API features (fallbacks); the SDK pin must be upgraded deliberately.

## Alternatives rejected
Raw HttpClient (the SDK exists and is official); JSON by prompt instruction only (no schema guarantee).
