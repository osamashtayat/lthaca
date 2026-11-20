using Ithaca.Application.Auth;
using Ithaca.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity for users (ADR-0012, ADR-0023): email + password, lockout after 5 failures for 15 minutes,
/// no roles. Identity owns the cookie scheme; how the cookie behaves (flags, lifetime, 401s) is the Api's (AuthSetup).
/// Also persists the ASP.NET Core Data Protection key ring to PostgreSQL (issue #64, ADR-0030) so session cookies,
/// antiforgery tokens and password-reset links survive container restarts and deploys.
/// </summary>
internal static class IdentitySetup
{
    public const string DataProtectionApplicationName = "Ithaca";
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static IServiceCollection AddIthacaIdentity(this IServiceCollection services)
    {
        services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToDbContext<AppDbContext>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // Length is the rule (8-128, api-contract.md §4); no composition rules (NIST SP 800-63B).
                options.Password.RequiredLength = RegisterRequest.PasswordMinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.Lockout.MaxFailedAccessAttempts = MaxFailedSignIns;
                options.Lockout.DefaultLockoutTimeSpan = LockoutDuration;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddSingleton<PasswordResetTokens>();
        services.AddScoped<ISessionManager, IdentitySessionManager>();
        return services;
    }
}
