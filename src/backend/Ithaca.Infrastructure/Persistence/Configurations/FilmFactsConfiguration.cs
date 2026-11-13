using Ithaca.Domain.Films;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>film_facts</c>: one row per film Wikidata knows (ADR-0017), keyed by the film. Deleted with its film (cascade): the
/// facts are derived data, unlike user data, which restricts deletes (ADR-0019).
/// </summary>
internal sealed class FilmFactsConfiguration : IEntityTypeConfiguration<FilmFacts>
{
    public void Configure(EntityTypeBuilder<FilmFacts> builder)
    {
        builder.ToTable("film_facts");
        builder.HasKey(x => x.FilmId);
        builder.Property(x => x.FilmId).ValueGeneratedNever();
        builder.HasOne<Film>().WithOne().HasForeignKey<FilmFacts>(x => x.FilmId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.WikidataId).HasMaxLength(FilmFacts.WikidataIdMaxLength).IsRequired();
        builder.Property(x => x.Genres).IsRequired();
        builder.Property(x => x.MainSubjects).IsRequired();
        builder.Property(x => x.Directors).IsRequired();
        builder.Property(x => x.Cast).IsRequired();
        builder.Property(x => x.Countries).IsRequired();
        builder.Property(x => x.Languages).IsRequired();
        builder.Property(x => x.Awards).IsRequired();
        builder.Property(x => x.EmbeddingText).IsRequired();
        builder.HasIndex(x => x.FetchedAt).HasDatabaseName("ix_film_facts_fetched_at");
    }
}
