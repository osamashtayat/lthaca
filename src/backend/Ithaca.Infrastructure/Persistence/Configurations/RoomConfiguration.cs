using Ithaca.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary><c>rooms</c>: group sessions, found by their unique upper-case code (BE-10), and their result once decided (BE-12).</summary>
internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public const string CodeIndex = "ux_rooms_code";

    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Code).HasMaxLength(RoomCode.Length).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique().HasDatabaseName(CodeIndex);
        builder.Property(r => r.Name).HasMaxLength(Room.NameMaxLength).IsRequired();
        builder.Property(r => r.Mode).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.ResultKind).HasConversion<string>().HasMaxLength(20);
        builder.PrimitiveCollection(r => r.ResultPicks).IsRequired();
        builder.PrimitiveCollection(r => r.ResultMembers).IsRequired();
        builder.PrimitiveCollection(r => r.ResultVotes).IsRequired();
        builder.PrimitiveCollection(r => r.ResultReasons); // null until the AI explained the picks (BE-13)

        // Postgres' xmin as an optimistic concurrency token (no column of ours): a start that races a close (or the last
        // member leaving) can't overwrite the other's status; the loser sees a write conflict and decides again (BE-11).
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
    }
}
