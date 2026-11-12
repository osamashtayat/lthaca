namespace Ithaca.Application.Auth;

/// <summary>Who is calling: a signed-in <c>User</c> or a room-scoped <c>Guest</c> (glossary, ADR-0012).</summary>
public enum PrincipalKind
{
    User,
    Guest,
}
