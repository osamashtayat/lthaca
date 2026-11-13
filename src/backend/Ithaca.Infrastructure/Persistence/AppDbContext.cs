using Ithaca.Application.Common;
using Ithaca.Application.Users;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Films;
using Ithaca.Domain.Library;
using Ithaca.Domain.Moods;
using Ithaca.Domain.Rooms;
using Ithaca.Domain.Users;
using Ithaca.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context, including ASP.NET Core Identity's user tables (no roles: <c>IdentityUserContext</c>).
/// Entity sets and configurations are added per feature.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IAppDbContext
{
    /// <summary>The pgvector extension; created by the initial migration (ADR-0002).</summary>
    public const string VectorExtension = "vector";

    public DbSet<Film> Films => Set<Film>();

    public DbSet<FilmFacts> FilmFacts => Set<FilmFacts>();

    public DbSet<FilmTrailer> FilmTrailers => Set<FilmTrailer>();

    public DbSet<Guest> Guests => Set<Guest>();

    public DbSet<UserFilm> UserFilms => Set<UserFilm>();

    public DbSet<MoodQuery> MoodQueries => Set<MoodQuery>();

    public DbSet<Deck> Decks => Set<Deck>();

    public DbSet<DeckItem> DeckItems => Set<DeckItem>();

    public DbSet<Swipe> Swipes => Set<Swipe>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();

    IQueryable<UserReadModel> IAppDbContext.Users =>
        Users.AsNoTracking().Select(u => new UserReadModel { Id = u.Id, Email = u.Email!, DisplayName = u.DisplayName });

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension(VectorExtension);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
