using Ithaca.Domain.Decks;
using Ithaca.Domain.Rooms;
using Ithaca.Domain.Moods;
using Ithaca.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>decks</c>: a user's solo decks and rooms' shared decks (exactly one owner; one deck per room). Deleting the user or the
/// room deletes them (cascade), and with them their items, swipes and votes. Only a solo deck by mood has a mood query.
/// </summary>
internal sealed class DeckConfiguration : IEntityTypeConfiguration<Deck>
{
    public void Configure(EntityTypeBuilder<Deck> builder)
    {
        builder.ToTable("decks", t =>
        {
            t.HasCheckConstraint("ck_decks_user_or_room", "(user_id IS NULL) <> (room_id IS NULL)");
            t.HasCheckConstraint(
                "ck_decks_mood_query",
                $"CASE WHEN room_id IS NULL THEN (source = '{nameof(DeckSource.Mood)}') = (mood_query_id IS NOT NULL) ELSE mood_query_id IS NULL END");
        });
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Source).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.UserId).HasDatabaseName("ix_decks_user_id");
        builder.HasOne<Room>().WithMany().HasForeignKey(d => d.RoomId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.RoomId).IsUnique().HasDatabaseName("ux_decks_room_id");
        builder.HasOne<MoodQuery>().WithMany().HasForeignKey(d => d.MoodQueryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.MoodQueryId).HasDatabaseName("ix_decks_mood_query_id");
    }
}
