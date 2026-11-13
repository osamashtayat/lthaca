using Ithaca.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary><c>guests</c>: people in a room without an account (ADR-0012). Created by room join (BE-10).</summary>
internal sealed class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.ToTable("guests");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.DisplayName).HasMaxLength(Guest.DisplayNameMaxLength).IsRequired();
        builder.Property(g => g.CreatedAt).IsRequired();
    }
}
