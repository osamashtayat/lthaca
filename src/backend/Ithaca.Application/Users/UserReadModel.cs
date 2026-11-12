namespace Ithaca.Application.Users;

/// <summary>
/// A registered <c>User</c> as services read it. The account itself (credentials, lockout) is ASP.NET Core Identity's and
/// lives in Infrastructure, so Domain and Application never see Identity types; <c>IAppDbContext.Users</c> projects to this.
/// Init properties, not a constructor: EF can translate filters on a member-initialised projection, not on a constructed one.
/// </summary>
public sealed record UserReadModel
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required string DisplayName { get; init; }
}
