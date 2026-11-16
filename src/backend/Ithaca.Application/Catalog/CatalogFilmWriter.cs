using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Domain.Films;

namespace Ithaca.Application.Catalog;

/// <summary>
/// The one way a film gets into (or refreshed in) <c>films</c> from TMDB details: display data only (ADR-0017), its
/// Wikidata facts and embedding follow in the next sync. Shared by <see cref="CatalogSyncService"/> and the Letterboxd
/// import (ADR-0024 decision 4), so neither calls the other. Adds to the context; the caller saves.
/// </summary>
public sealed class CatalogFilmWriter(IAppDbContext db)
{
    /// <summary>
    /// Updates <paramref name="existing"/> from <paramref name="details"/>, or creates and adds a new film when it's null.
    /// Returns null (and changes nothing) when TMDB served no usable title.
    /// </summary>
    public CatalogWrite? Write(Film? existing, TmdbFilmDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (!Film.IsValidTitle(details.Title))
        {
            return null;
        }

        var film = existing ?? Film.Create(details.TmdbId, details.Title, now);
        film.UpdateDetails(
            details.Title, details.ReleaseYear, details.Overview, details.PosterPath, details.RuntimeMinutes, [.. details.Genres],
            details.Popularity, details.BackdropPath, details.VoteAverage, details.VoteCount, details.OriginalLanguage, now);
        if (existing is null)
        {
            db.Films.Add(film);
        }

        return new CatalogWrite(film, Added: existing is null);
    }
}

/// <summary>The film written and whether it was new to the catalog.</summary>
public sealed record CatalogWrite(Film Film, bool Added);
