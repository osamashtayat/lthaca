using Ithaca.Domain.Rooms;
using Ithaca.Domain.Users;
using Ithaca.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>room_members</c>: a user or a guest in a room (exactly one), present while <c>left_at</c> is null. One row per user
/// and per guest in a room; a present member's seat (1–8) is unique in its room, so a room never holds more than 8 even
/// when people join at once. Deleted with the room, the user or the guest.
/// </summary>
internal sealed class RoomMemberConfiguration : IEntityTypeConfiguration<RoomMember>
{
    public void Configure(EntityTypeBuilder<RoomMember> builder)
    {
        builder.ToTable("room_members", t =>
        {
            t.HasCheckConstraint("ck_room_members_user_or_guest", "(user_id IS NULL) <> (guest_id IS NULL)");
            t.HasCheckConstraint("ck_room_members_seat", $"seat BETWEEN 1 AND {Room.MaxMembers}");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasOne<Room>().WithMany().HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Guest>().WithMany().HasForeignKey(m => m.GuestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => new { m.RoomId, m.UserId }).IsUnique().HasDatabaseName("ux_room_members_room_user");
        builder.HasIndex(m => new { m.RoomId, m.GuestId }).IsUnique().HasDatabaseName("ux_room_members_room_guest");
        builder.HasIndex(m => new { m.RoomId, m.Seat }).IsUnique().HasFilter("left_at IS NULL").HasDatabaseName("ux_room_members_present_seat");
        builder.HasIndex(m => m.UserId).HasDatabaseName("ix_room_members_user_id");
        builder.HasIndex(m => m.GuestId).HasDatabaseName("ix_room_members_guest_id");
        builder.Ignore(m => m.IsPresent);
    }
}
