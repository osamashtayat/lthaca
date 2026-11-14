namespace Ithaca.Application.Users;

/// <summary>The signed-in user's own profile (api-contract.md §14): the header of the "Profile &amp; settings" board.</summary>
public sealed record ProfileDto(Guid Id, string DisplayName, string Email, DateTimeOffset CreatedAt);
