using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>data_protection_keys</c>: ASP.NET Core Data Protection key ring (issue #64, ADR-0030). Persisted in PostgreSQL so
/// session cookies and password-reset tokens survive container restarts, deploys and VPS moves, and are covered by database
/// backups (OPS-08).
/// </summary>
internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder) =>
        builder.ToTable("data_protection_keys");
}
