# Decks

Solo decks (`Deck`, `DeckItem`, `Swipe`; api-contract.md §5). Built: "Just swipe" (BE-08). By mood: BE-09. By taste: BE-14.

- `DeckService`: the use cases (create, get, next batch, swipe, top 3). Reads and writes through `IAppDbContext` only.
- `DeckSampler`: pure picking logic (sampler variety, fit with signals, top-3 ranking), unit-tested without a database.
- `DeckSignals`: how much each swipe or library row counts.
- Films come from our own `films` table (ADR-0003). Deck items reference films, so the sync never purges them (ADR-0019).
