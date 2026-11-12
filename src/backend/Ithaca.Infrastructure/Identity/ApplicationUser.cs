using Microsoft.AspNetCore.Identity;

namespace Ithaca.Infrastructure.Identity;

/// <summary>
/// A registered <c>User</c>: the Identity account plus our profile fields. Lives in Infrastructure so Domain and
/// Application never see Identity types; they read it as <c>UserReadModel</c> through <c>IAppDbContext.Users</c>.
/// <see cref="IdentityUser{TKey}.UserName"/> is the id (sign-in is by email), so any valid email can register.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}
