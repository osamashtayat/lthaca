namespace Ithaca.Application.Auth;

/// <summary>The caller: a user (<see cref="Email"/> set) or a guest (<see cref="RoomCode"/> set, the one room it is valid for).</summary>
public sealed record CurrentUserDto(PrincipalKind Kind, Guid Id, string DisplayName, string? Email, string? RoomCode);
