using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;
using Ithaca.Application.Moods;

namespace Ithaca.Application.Films;

/// <summary>Browse order: TMDB popularity at the last sync ("Trending this week"), release date, or TMDB score.</summary>
public enum FilmSort
{
    Popular,
    Newest,
    Rating,
}

/// <summary><c>GET /api/films</c> query (api-contract.md §9). <see cref="HideSeen"/> is ignored for anonymous callers.</summary>
public sealed record FilmBrowseRequest : PageRequest
{
    public const int SearchMaxLength = 100;
    public const int GenreMaxLength = 50;

    /// <summary>Case-insensitive title substring.</summary>
    [MaxLength(SearchMaxLength)]
    public string? Search { get; init; }

    [MaxLength(GenreMaxLength)]
    public string? Genre { get; init; }

    public RuntimeLimit? MaxRuntime { get; init; }

    [Range(0d, 10d)]
    public double? MinVoteAverage { get; init; }

    public bool HideSeen { get; init; }

    public FilmSort Sort { get; init; } = FilmSort.Popular;
}
