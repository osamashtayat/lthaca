using Ithaca.Application.Auth;
using Ithaca.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ithaca.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>users</c>: Identity accounts plus profile (ADR-0012). Identity's own model is configured by
/// <c>IdentityUserContext</c>; this renames its tables and adds our columns. Emails are unique (normalized).
/// </summary>
internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public const string NormalizedEmailIndex = "ux_users_normalized_email";

    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Email).HasMaxLength(RegisterRequest.EmailMaxLength).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(RegisterRequest.EmailMaxLength).IsRequired();
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName(NormalizedEmailIndex);
        builder.Property(u => u.DisplayName).HasMaxLength(RegisterRequest.DisplayNameMaxLength).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
    }
}

/// <summary><c>user_claims</c> (Identity, unused for now).</summary>
internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("user_claims");
}

/// <summary><c>user_logins</c> (Identity; external sign-in, P-02).</summary>
internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("user_logins");
}

/// <summary><c>user_tokens</c> (Identity).</summary>
internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("user_tokens");
}

