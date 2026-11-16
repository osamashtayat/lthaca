using Ithaca.Domain.Films;
using Ithaca.Domain.Imports;
using Ithaca.Domain.Library;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>import_items</c>: an import's parsed films (title, year, Letterboxd URI, flags, rating; nothing else from the file).
/// Deleted with their job (cascade). Pending items are read in position order; the not-found list by title.
/// </summary>
internal sealed class ImportItemConfiguration : IEntityTypeConfiguration<ImportItem>
{
    public void Configure(EntityTypeBuilder<ImportItem> builder)
    {
        builder.ToTable("import_items", t =>
            t.HasCheckConstraint(
                "ck_import_items_rating_half_stars",
                $"rating_half_stars IS NULL OR rating_half_stars BETWEEN {UserFilm.MinHalfStars} AND {UserFilm.MaxHalfStars}"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Title).HasMaxLength(Film.TitleMaxLength);
        builder.Property(i => i.LetterboxdUri).HasMaxLength(ImportItem.LetterboxdUriMaxLength);
        builder.Property(i => i.NotFoundReason).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<ImportJob>().WithMany().HasForeignKey(i => i.ImportJobId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => new { i.ImportJobId, i.Position }).IsUnique().HasDatabaseName("ux_import_items_job_position");
    }
}
