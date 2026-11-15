using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;
using Ithaca.Domain.Films;
using Ithaca.Domain.Library;

namespace Ithaca.Domain.Imports;

/// <summary>
/// One film of an <see cref="ImportJob"/>: the four export files' rows for it, merged (§13 job rule 1). Only these fields
/// are kept from the upload: title, year, Letterboxd URI, the flags and the rating (ADR-0024 decision 3). Pending while
/// <see cref="NotFoundReason"/> is null; deleted once matched; a not-found item stays as the "not found" list of the
/// user's latest import. Titles are the user's data: never logged.
/// </summary>
public sealed class ImportItem
{
    /// <summary>The longest Letterboxd URI kept; a longer one is dropped and the film is identified by title and year.</summary>
    public const int LetterboxdUriMaxLength = 300;

    private ImportItem(Guid importJobId, int position, string title, int? year, string? letterboxdUri, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        ImportJobId = importJobId;
        Position = position;
        Title = title;
        Year = year;
        LetterboxdUri = letterboxdUri;
    }

    /// <summary>For persistence materialization only.</summary>
    private ImportItem()
    {
        Title = "";
    }

    public Guid Id { get; private set; }

    public Guid ImportJobId { get; private set; }

    /// <summary>Processing order within the job (0-based, file order): batches resume from the lowest pending one.</summary>
    public int Position { get; private set; }

    public string Title { get; private set; }

    public int? Year { get; private set; }

    public string? LetterboxdUri { get; private set; }

    /// <summary>In <c>watched.csv</c>.</summary>
    public bool Watched { get; private set; }

    /// <summary>In <c>likes/films.csv</c>.</summary>
    public bool Liked { get; private set; }

    /// <summary>In <c>watchlist.csv</c>.</summary>
    public bool OnWatchlist { get; private set; }

    /// <summary>From <c>ratings.csv</c>, as half stars (1–10, like <see cref="UserFilm.RatingHalfStars"/>).</summary>
    public short? RatingHalfStars { get; private set; }

    /// <summary>Null while pending; set when the film couldn't be imported.</summary>
    public ImportNotFoundReason? NotFoundReason { get; private set; }

    public static ImportItem Create(
        Guid importJobId, int position, string title, int? year, string? letterboxdUri,
        bool watched, bool liked, bool onWatchlist, decimal? rating, DateTimeOffset now)
    {
        if (!Film.IsValidTitle(title))
        {
            throw new DomainException(ErrorCodes.Validation, $"title must be 1-{Film.TitleMaxLength} characters.", "title");
        }

        if (rating is { } r && !UserFilm.IsValidRating(r))
        {
            throw new DomainException(ErrorCodes.Validation, UserFilm.RatingRule, "rating");
        }

        return new ImportItem(importJobId, position, title.Trim(), year, letterboxdUri is { Length: > 0 and <= LetterboxdUriMaxLength } ? letterboxdUri : null, now)
        {
            Watched = watched,
            Liked = liked,
            OnWatchlist = onWatchlist,
            RatingHalfStars = rating is { } value ? (short)(value * 2) : null,
        };
    }

    public void MarkNotFound(ImportNotFoundReason reason) => NotFoundReason = reason;
}
