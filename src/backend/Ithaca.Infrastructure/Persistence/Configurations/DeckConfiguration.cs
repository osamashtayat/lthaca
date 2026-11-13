using Ithaca.Domain.Decks;
using Ithaca.Domain.Moods;
using Ithaca.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary><c>decks</c>: a user's solo decks. Deleting the user deletes them (cascade), and with them their items and swipes.</summary>
internal sealed class DeckConfiguration : IEntityTypeConfiguration<Deck>
{
    public void Configure(EntityTypeBuilder<Deck> builder)
    {
        builder.ToTable("decks", t =>
            t.HasCheckConstraint("ck_decks_mood_query", $"(source = '{nameof(DeckSource.Mood)}') = (mood_query_id IS NOT NULL)"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Source).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.UserId).HasDatabaseName("ix_decks_user_id");
        builder.HasOne<MoodQuery>().WithMany().HasForeignKey(d => d.MoodQueryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.MoodQueryId).HasDatabaseName("ix_decks_mood_query_id");
    }
}
