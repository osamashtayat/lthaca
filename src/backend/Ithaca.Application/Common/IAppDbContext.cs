using Ithaca.Application.Users;
using Ithaca.Domain.Films;
using Ithaca.Domain.Library;
using Ithaca.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Common;

/// <summary>
/// The Application layer's unit of work (ADR-0001). Only use-case services use it, and they call
/// <see cref="SaveChangesAsync"/> exactly once per use case. Entity sets are added as their features land;
/// there is deliberately no generic repository.
/// </summary>
public interface IAppDbContext
{
    DbSet<Film> Films { get; }

    /// <summary>Wikidata facts per film (ADR-0017): the only film data AI may see.</summary>
    DbSet<FilmFacts> FilmFacts { get; }

    /// <summary>The trailer cache (ADR-0003): TMDB data, at most <see cref="FilmTrailer.MaxAge"/> old (ADR-0017).</summary>
    DbSet<FilmTrailer> FilmTrailers { get; }

    /// <summary>Registered users, read-only: accounts are created and changed through <c>ISessionManager</c> (Identity).</summary>
    IQueryable<UserReadModel> Users { get; }

    DbSet<Guest> Guests { get; }

    /// <summary>My Cinema: a user's flags and rating per film. Referencing a film keeps it from being purged (ADR-0019).</summary>
    DbSet<UserFilm> UserFilms { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
