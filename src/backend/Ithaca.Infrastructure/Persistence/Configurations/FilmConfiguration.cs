using Ithaca.Domain.Films;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>films</c>: our catalog (ADR-0003). The embedding is a <c>vector(Film.EmbeddingDimensions)</c> column with an HNSW
/// cosine index; Domain keeps a <c>float[]</c>, converted here so Pgvector never leaks out of Infrastructure.
/// </summary>
internal sealed class FilmConfiguration : IEntityTypeConfiguration<Film>
{
    public const string TmdbIdIndex = "ux_films_tmdb_id";
    public const string EmbeddingIndex = "ix_films_embedding_hnsw";

    private static readonly ValueComparer<float[]?> EmbeddingComparer = new(
        (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
        v => v == null ? 0 : v.Length,
        v => v == null ? null : v.ToArray());

    public void Configure(EntityTypeBuilder<Film> builder)
    {
        builder.ToTable("films");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.TmdbId).IsRequired();
        builder.HasIndex(f => f.TmdbId).IsUnique().HasDatabaseName(TmdbIdIndex);
        builder.Property(f => f.Title).HasMaxLength(Film.TitleMaxLength).IsRequired();
        builder.Property(f => f.Genres).IsRequired();
        builder.Property(f => f.OriginalLanguage).HasMaxLength(Film.OriginalLanguageMaxLength);

        builder.Property(f => f.Embedding)
            .HasColumnType($"vector({Film.EmbeddingDimensions})")
            .HasConversion(v => v == null ? null : new Vector(v), v => v == null ? null : v.ToArray())
            .Metadata.SetValueComparer(EmbeddingComparer);
        builder.Property(f => f.EmbeddingModel).HasMaxLength(Film.EmbeddingModelMaxLength);
        builder.Property(f => f.EmbeddingSourceHash).HasMaxLength(Film.EmbeddingSourceHashLength);
        builder.HasIndex(f => f.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasDatabaseName(EmbeddingIndex);
    }
}
