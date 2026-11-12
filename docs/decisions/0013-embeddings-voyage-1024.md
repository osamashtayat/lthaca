# ADR-0013: Embeddings from Voyage AI at 1024 dimensions, model recorded per vector

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
ADR-0002 assumed 1536-dimension vectors. Anthropic has no embeddings API and points to Voyage AI. Voyage's current 4-series models (`voyage-4`, `-large`, `-lite`) default to 1024 dimensions (256/512/2048 also supported) and are mutually compatible.

## Decision
- `Film.EmbeddingDimensions = 1024`; `films.embedding` is `vector(1024)` (migration `VoyageEmbeddingsAndPopularity`, which clears old vectors).
- Each vector stores the model that made it (`films.embedding_model`). The sync re-embeds a film when its text changed or its model differs from the configured one, so a model switch converges by itself.
- `Ai:EmbeddingProvider = Voyage` is the target; the client is built when a Voyage key exists. Until then `Fake` (deterministic, `embedding_model = "fake"`).
- Embedding TMDB-derived text is OFF until ADR-0017 is decided (`CatalogSync:EmbedOverviews = false`).

## Consequences
+ Smaller vectors (≈⅔ storage), faster HNSW search, quality fine for short film texts.
+ Mixed-model vectors are detectable.
- A second vendor key (Voyage) besides Anthropic.

## Alternatives rejected
OpenAI `text-embedding-3-small` at 1536 (another vendor anyway, larger vectors); self-hosted `voyage-4-nano` (operational cost now).
