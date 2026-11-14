using Ithaca.Domain.Decks;
using Ithaca.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>votes</c>: a room member's vote on a card of the room's deck (ADR-0015). One per (deck item, member), unique, and keyed
/// by the client's <c>voteId</c> so a retry finds it. Deleted with its card or its member.
/// </summary>
internal sealed class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("votes", t =>
            t.HasCheckConstraint("ck_votes_seen_verdict", $"seen_verdict IS NULL OR direction = '{nameof(SwipeDirection.Up)}'"));
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(v => v.SeenVerdict).HasConversion<string>().HasMaxLength(10);
        builder.HasOne<DeckItem>().WithMany().HasForeignKey(v => v.DeckItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RoomMember>().WithMany().HasForeignKey(v => v.RoomMemberId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => new { v.DeckItemId, v.RoomMemberId }).IsUnique().HasDatabaseName("ux_votes_deck_item_id_room_member_id");
        builder.HasIndex(v => v.RoomMemberId).HasDatabaseName("ix_votes_room_member_id");
    }
}
