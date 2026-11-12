using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;
using Ithaca.Application.Films;

namespace Ithaca.Application.Library;

/// <summary><c>GET /api/library</c> query: one shelf, sorted, optionally one genre, paged.</summary>
public sealed record LibraryListRequest : PageRequest
{
    public LibraryShelf Shelf { get; init; } = LibraryShelf.Liked;

    public LibrarySort Sort { get; init; } = LibrarySort.Recent;

    [MaxLength(FilmBrowseRequest.GenreMaxLength)]
    public string? Genre { get; init; }
}

/// <summary>
/// <c>PUT /api/library/{tmdbId}</c>: the whole state, last write wins. All false + no rating deletes the row; a rating
/// implies seen (service rules, A8).
/// </summary>
public sealed record UpdateUserFilmRequest(bool Liked, bool OnWatchlist, bool Seen, [Range(1, 5)] int? Rating);

/// <summary>A <c>UserFilm</c>: the caller's flags and rating for one film (all false/null when untouched).</summary>
public sealed record UserFilmDto(bool Liked, bool OnWatchlist, bool Seen, int? Rating, FilmSummaryDto Film, DateTimeOffset UpdatedAt);

/// <summary>Shelf counts for My Cinema's header.</summary>
public sealed record LibrarySummaryDto(int Liked, int Watchlist, int Seen);
