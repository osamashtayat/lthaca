using Ithaca.Domain.Films;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>film_trailers</c>: the trailer cache, one row per film (ADR-0003). Deleted with its film (cascade): it is cached TMDB
/// data, not user data. Indexed by <c>fetched_at</c> for the nightly purge past <see cref="FilmTrailer.MaxAge"/>.
/// </summary>
internal sealed class FilmTrailerConfiguration : IEntityTypeConfiguration<FilmTrailer>
{
    public void Configure(EntityTypeBuilder<FilmTrailer> builder)
    {
        builder.ToTable("film_trailers", t =>
            t.HasCheckConstraint("ck_film_trailers_key_and_name", "(key IS NULL) = (name IS NULL)"));
        builder.HasKey(x => x.FilmId);
        builder.Property(x => x.FilmId).ValueGeneratedNever();
        builder.HasOne<Film>().WithOne().HasForeignKey<FilmTrailer>(x => x.FilmId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Key).HasMaxLength(FilmTrailer.KeyMaxLength);
        builder.Property(x => x.Name).HasMaxLength(FilmTrailer.NameMaxLength);
        builder.HasIndex(x => x.FetchedAt).HasDatabaseName("ix_film_trailers_fetched_at");
    }
}
