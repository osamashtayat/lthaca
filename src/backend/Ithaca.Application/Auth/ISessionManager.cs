using Ithaca.Application.Common;

namespace Ithaca.Application.Auth;

/// <summary>
/// Port to the account and session-cookie machinery (ASP.NET Core Identity, ADR-0012), implemented in Infrastructure.
/// Every method that changes the caller also switches the current request's principal, so a token issued later in the
/// same request (antiforgery) belongs to the new caller.
/// </summary>
public interface ISessionManager
{
    /// <summary>Creates the account and signs it in. <c>auth.email_taken</c> or a <c>validation</c> error on failure.</summary>
    Task<Result<Guid>> RegisterAsync(string email, string password, string displayName, CancellationToken ct);

    /// <summary>
    /// Checks the password with lockout and signs in. Every failure (unknown email, wrong password, locked out) is the
    /// same <c>auth.invalid_credentials</c>, so the answer never reveals which.
    /// </summary>
    Task<Result<Guid>> PasswordSignInAsync(string email, string password, CancellationToken ct);

    /// <summary>Issues a guest session valid for one room (ADR-0012). Called by room join (BE-10).</summary>
    Task SignInGuestAsync(Guid guestId, Guid roomId, string roomCode);

    /// <summary>Clears the session cookie (user or guest). Idempotent.</summary>
    Task SignOutAsync();
}
