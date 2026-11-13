using Ithaca.Domain.Moods;
using Ithaca.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>mood_queries</c>: one per mood request. Moods are stored as their enum names (text[]); genres, keywords as text[].
/// Deleting the user deletes their mood queries (cascade), and with them the decks built from them.
/// </summary>
internal sealed class MoodQueryConfiguration : IEntityTypeConfiguration<MoodQuery>
{
    public void Configure(EntityTypeBuilder<MoodQuery> builder)
    {
        builder.ToTable("mood_queries");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => m.UserId).HasDatabaseName("ix_mood_queries_user_id");
        builder.PrimitiveCollection(m => m.Moods).ElementType(e => e.HasConversion<string>()).IsRequired();
        builder.Property(m => m.MaxRuntime).HasConversion<string>().HasMaxLength(10);
        builder.Property(m => m.Language).HasMaxLength(MoodQuery.LanguageLength);
        builder.Property(m => m.Text).HasMaxLength(MoodQuery.TextMaxLength);
        builder.Property(m => m.IncludeGenres).IsRequired();
        builder.Property(m => m.ExcludeGenres).IsRequired();
        builder.Property(m => m.Keywords).IsRequired();
    }
}
