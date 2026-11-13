using Ithaca.Domain.Decks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>swipes</c>: one per deck item (unique), keyed by the client's <c>swipeId</c> so a retry finds it. Deleted with its item.
/// </summary>
internal sealed class SwipeConfiguration : IEntityTypeConfiguration<Swipe>
{
    public void Configure(EntityTypeBuilder<Swipe> builder)
    {
        builder.ToTable("swipes", t =>
            t.HasCheckConstraint("ck_swipes_seen_verdict", $"seen_verdict IS NULL OR direction = '{nameof(SwipeDirection.Up)}'"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(s => s.SeenVerdict).HasConversion<string>().HasMaxLength(10);
        builder.HasOne<DeckItem>().WithOne().HasForeignKey<Swipe>(s => s.DeckItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.DeckItemId).IsUnique().HasDatabaseName("ux_swipes_deck_item_id");
    }
}
