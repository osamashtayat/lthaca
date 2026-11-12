# ADR-0017: TMDB for display, Wikidata for AI (TMDB API terms)

- **Status:** Accepted (owner chose option 2, 2026-11-12)
- **Date:** 2026-11-12

## Context
TMDB's API Terms of Use (last updated 20 Oct 2023) say, in summary:
- §1.C: you may not use the TMDB APIs or TMDB content in connection with a machine learning or AI application, including training one.
- §2.A: LLM/AI-based uses and training or validating ML/AI systems count as *commercial use*, allowed only under a separate written agreement with TMDB (possibly with fees). Any revenue-generating use is commercial too.
- §1.C: nothing obtained from TMDB may be cached longer than 6 months.
- Attribution: the TMDB logo (less prominent than ours) and the notice "This … uses TMDB and the TMDB APIs but is not endorsed, certified, or otherwise approved by TMDB."

Ithaca's design embeds TMDB overviews (pgvector) and gives TMDB film data to Claude for ranking and explanations. That is squarely the restricted use. This is not legal advice; the owner decides.

## Decision
Already done regardless of the outcome:
- Attribution notice in the site footer (logo still TODO).
- The sync deletes films not refreshed within 180 days (`CatalogSync:MaxAgeDays`).
- Embedding TMDB-derived text is OFF (`CatalogSync:EmbedOverviews = false`). The LLM receives only user text and generic genre names until this is decided; rank/explain features must not be built on TMDB text before then.

Options for the owner:
1. **Ask TMDB for a written agreement** covering AI/LLM use (and commercial use if Ithaca will earn money). Keeps the current design. Recommended first step.
2. **Use a different source for AI inputs**: e.g. Wikidata (CC0) for genres, keywords, cast and plot-free metadata; keep TMDB only for display (posters, overviews, trailers) without sending its content to AI. More work, weaker taste signals.
3. **Switch catalog provider** to one whose license allows AI use (licensing cost varies).

## Outcome
The owner chose **option 2**: Wikidata feeds the AI, TMDB provides the full details and posters for display.
- **AI inputs** (embeddings, LLM ranking/explanations): Wikidata only (CC0) — genres, subgenres, keywords ("main subject"),
  directors, cast, countries, languages, year, runtime, awards. Films are linked by Wikidata's TMDB-id property, so no
  title matching is needed. Our own user data (votes, likes, ratings, imports) is ours and can be used freely.
- **Display** (title, overview, poster, trailers): TMDB, with the footer attribution, refreshed within 6 months.
- **Never** send TMDB-derived text (overviews, taglines, TMDB keyword lists) to the LLM or the embedding model.
  `CatalogSync:EmbedOverviews` (which embeds TMDB text) stays false and will be replaced by a Wikidata-based embedding text.
- Wikipedia plot text (CC BY-SA) is a possible later addition for richer embeddings; it needs an attribution decision first.
- Not built yet: the Wikidata enricher (next feature).

## Consequences
Until decided: deck features can use our own data (votes, likes, genres) and mood parsing; ranking/explanations and embeddings over TMDB text wait.

## Alternatives rejected
Silently proceeding with the original design.
