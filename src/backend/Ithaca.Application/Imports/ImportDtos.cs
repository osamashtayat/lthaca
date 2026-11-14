using Ithaca.Domain.Imports;

namespace Ithaca.Application.Imports;

/// <summary>
/// The uploaded file as the service sees it (api-contract.md §13 "Upload types"). The Api turns its <c>IFormFile</c> into
/// this, so Application never references ASP.NET Core. The file itself is never stored (ADR-0024).
/// </summary>
public sealed record ImportUpload(Stream Content, string FileName, long Length);

/// <summary>One Letterboxd import and its progress (§13).</summary>
/// <param name="FilmCount">Distinct films in the file, after merging the four files.</param>
/// <param name="ProcessedCount">0…<paramref name="FilmCount"/>: progress.</param>
/// <param name="MatchedCount">Found in our catalog or on TMDB.</param>
/// <param name="AddedToCatalogCount">Of <paramref name="MatchedCount"/>: films the job added from TMDB.</param>
/// <param name="NotFoundCount">Films not found; kept even after the list itself is emptied (job rule 6).</param>
/// <param name="NotFoundListKept">False for older imports: their not-found list returns an empty page.</param>
/// <param name="Changes">What it changed in My Cinema; grows while running.</param>
public sealed record ImportJobDto(
    Guid Id,
    ImportStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt,
    int FilmCount,
    int ProcessedCount,
    int MatchedCount,
    int AddedToCatalogCount,
    int NotFoundCount,
    bool NotFoundListKept,
    ImportChangesDto Changes);

/// <summary>What an import changed in My Cinema. <paramref name="RatingsKept"/>: films whose Ithaca rating won over the file's (job rule 3).</summary>
/// <param name="NewFilms"><c>UserFilm</c> rows created.</param>
public sealed record ImportChangesDto(int NewFilms, int Seen, int Rated, int Liked, int Watchlist, int RatingsKept);

/// <summary>A film from the export that wasn't imported. Its title and year are the user's file data, never logged.</summary>
public sealed record ImportNotFoundDto(string Title, int? Year, string? LetterboxdUri, ImportNotFoundReason Reason);
