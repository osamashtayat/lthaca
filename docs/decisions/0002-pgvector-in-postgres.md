# ADR-0002: Embeddings stored in PostgreSQL with pgvector

- **Status:** Accepted
- **Date:** 2026-11-12

## Context
Decks adapt using a taste vector and film similarity. We need one embedding per catalog film and nearest-neighbour queries, joined with normal filters (genre, runtime, already seen).

## Decision
- Store `films.embedding` as `vector(1536)` (`Film.EmbeddingDimensions`) in the same PostgreSQL database, with an HNSW index using `vector_cosine_ops`.
- Docker image `pgvector/pgvector:pg17`; the initial migration runs `CREATE EXTENSION vector` (`HasPostgresExtension("vector")`).
- Packages `Pgvector` + `Pgvector.EntityFrameworkCore` in Infrastructure only; Domain keeps `float[]`, converted in `FilmConfiguration`.

## Consequences
+ One database, transactional with the rest of the data; similarity and filters in one SQL query.
+ No extra service to run locally or in production.
- 1536 dimensions is an assumption (OpenAI `text-embedding-3-small`-sized). Another model size needs a migration and a re-embed.
- At very large scale a dedicated vector store may be faster; not needed for a curated catalog.

## Alternatives rejected
A separate vector database (Pinecone, Qdrant): another service, cross-store consistency; in-memory similarity in the API: doesn't scale past a small catalog.
