using Ithaca.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary><c>rooms</c>: group sessions, found by their unique upper-case code (BE-10).</summary>
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
    }
}
