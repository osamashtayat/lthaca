# Taste

`TasteProfile` (taste vector) built from likes, ratings and imports; similarity search over `films.embedding`.

- `TasteService.GetAsync` → `TasteDto` (readiness + taste receipt; api-contract.md §12). `TasteErrors.NotReady` (`taste.not_ready`).
- `TasteProfileReader`: collaborator for readiness (`Taste:SignalsNeeded`, `TasteOptions`) and profiles, used by
  `DeckService` (decks by taste), `RoomService` (host readiness) and `RoomDeckService` (the blended room deck).
- `TasteReceipt`: pure receipt rules (`topGenres`, `notForYou`), unit-tested.
- The vector math (`WeightOf`, `Build`, `Blend`) is the domain's `Ithaca.Domain.Taste.TasteProfile`; `DeckSampler.Closeness`
  turns it into a ranking boost.
