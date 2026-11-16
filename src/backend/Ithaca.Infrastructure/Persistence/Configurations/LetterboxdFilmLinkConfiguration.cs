using Ithaca.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary><c>letterboxd_film_links</c>: Letterboxd URI → TMDB id, ids only, shared by all users; no FK (the film may be purged).</summary>
internal sealed class LetterboxdFilmLinkConfiguration : IEntityTypeConfiguration<LetterboxdFilmLink>
{
    public void Configure(EntityTypeBuilder<LetterboxdFilmLink> builder)
    {
        builder.ToTable("letterboxd_film_links");
        builder.HasKey(l => l.LetterboxdUri);
        builder.Property(l => l.LetterboxdUri).HasMaxLength(ImportItem.LetterboxdUriMaxLength);
    }
}
