using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Imports;

/// <summary>
/// A Letterboxd film URI matched to a TMDB id by an earlier import, of any user (§13 job rule 2): ids only, no titles or
/// user data, so re-imports skip the search. Not a foreign key to <c>films</c>: the film may since have been purged, and
/// the next import that needs it adds it back from TMDB by id.
/// </summary>
public sealed class LetterboxdFilmLink
{
    private LetterboxdFilmLink(string letterboxdUri, int tmdbId, DateTimeOffset now)
    {
        LetterboxdUri = letterboxdUri;
        TmdbId = tmdbId;
        CreatedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private LetterboxdFilmLink()
    {
        LetterboxdUri = "";
    }

    public string LetterboxdUri { get; private set; }

    public int TmdbId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static LetterboxdFilmLink Create(string letterboxdUri, int tmdbId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(letterboxdUri) || letterboxdUri.Length > ImportItem.LetterboxdUriMaxLength)
        {
            throw new DomainException(ErrorCodes.Validation, "letterboxdUri is required.", "letterboxdUri");
        }

        if (tmdbId <= 0)
        {
            throw new DomainException(ErrorCodes.Validation, "tmdbId must be positive.", "tmdbId");
        }

        return new LetterboxdFilmLink(letterboxdUri, tmdbId, now);
    }
}
