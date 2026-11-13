using Ithaca.Domain.Films;
using Ithaca.Domain.Library;
using Ithaca.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>user_films</c>: My Cinema, one row per user and film. The film FK is <c>ON DELETE RESTRICT</c> (ADR-0019): a purge bug
/// fails loudly instead of deleting someone's library. Deleting a user deletes their rows (cascade).
/// </summary>
internal sealed class UserFilmConfiguration : IEntityTypeConfiguration<UserFilm>
{
    public void Configure(EntityTypeBuilder<UserFilm> builder)
    {
        builder.ToTable("user_films", t =>
            t.HasCheckConstraint("ck_user_films_rating", $"rating IS NULL OR rating BETWEEN {UserFilm.MinRating} AND {UserFilm.MaxRating}"));
        builder.HasKey(u => new { u.UserId, u.FilmId });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Film>().WithMany().HasForeignKey(u => u.FilmId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(u => u.IsEmpty);

        // Film id first: "is this film referenced?" (sync refresh and purge) and the PK's user-first lookups both stay indexed.
        builder.HasIndex(u => u.FilmId).HasDatabaseName("ix_user_films_film_id");
        builder.HasIndex(u => new { u.UserId, u.UpdatedAt }).HasDatabaseName("ix_user_films_user_id_updated_at");
    }
}
