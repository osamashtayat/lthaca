# Taste

`TasteProfile` (taste vector) built from likes, ratings and imports; similarity search over `films.embedding`.

- Contract (BE-14a, api-contract.md §12): `TasteService.GetAsync` → `TasteDto` (readiness + taste receipt), `TasteErrors.NotReady`
  (`taste.not_ready`), and the `taste` values of `DeckSource` / `RoomMode`. All answer 501 until BE-14 builds them.
- Copy the shape of `../Films/` (the reference slice). See `docs/backend-conventions.md`.
