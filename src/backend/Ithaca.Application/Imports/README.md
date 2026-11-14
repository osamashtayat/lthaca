# Imports

`ImportJob`: a Letterboxd export (ZIP or one CSV) merged into `UserFilm` in the background (api-contract.md §13, ADR-0024).

- Contract (BE-15a): `ImportService` (stub until BE-15), `ImportUpload` (the file as Application sees it; the Api's
  `CreateImportRequest` holds the `IFormFile`), DTOs, `ImportErrors`, `ImportLimits.MaxUploadBytes`.
- BE-15: parsing, `ImportOptions` limits, the job, `LetterboxdFilmLink`.
