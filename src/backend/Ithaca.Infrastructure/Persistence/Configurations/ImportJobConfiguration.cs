using Ithaca.Domain.Imports;
using Ithaca.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>import_jobs</c>: a user's Letterboxd imports. Deleted with the user (cascade), so deleting an account needs no import
/// code. A partial unique index allows one queued or running import per user (409 <c>import.already_running</c> even when
/// two uploads race).
/// </summary>
internal sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("import_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(j => j.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(j => j.IsActive);
        builder.HasIndex(j => j.UserId)
            .IsUnique()
            .HasFilter($"status IN ('{nameof(ImportStatus.Queued)}', '{nameof(ImportStatus.Running)}')")
            .HasDatabaseName("ux_import_jobs_user_active");
        builder.HasIndex(j => new { j.UserId, j.CreatedAt }).HasDatabaseName("ix_import_jobs_user_id_created_at");
        builder.HasIndex(j => new { j.Status, j.CreatedAt }).HasDatabaseName("ix_import_jobs_status_created_at");
    }
}
