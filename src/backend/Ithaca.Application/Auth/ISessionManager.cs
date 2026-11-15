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

    /// <summary>Sets a user's display name (already trimmed and checked). False only when the account is gone.</summary>
    Task<bool> SetDisplayNameAsync(Guid userId, string displayName, CancellationToken ct);

    /// <summary>
    /// Re-authentication (api-contract.md §14): checks a signed-in user's password with the sign-in lockout, so a wrong one
    /// counts toward it. False when wrong, locked out or the account is gone, never saying which. A failure is saved at once,
    /// so call this before staging other changes.
    /// </summary>
    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken ct);

    /// <summary>
    /// Sets the user's new password (after <see cref="CheckPasswordAsync"/>). The security stamp changes, so the user's other
    /// sessions end; this one is re-issued and stays signed in. A <c>validation</c> error on <c>newPassword</c> on failure.
    /// </summary>
    Task<Result> ChangePasswordAsync(Guid userId, string newPassword, CancellationToken ct);

    /// <summary>
    /// Marks the account deleted in the shared unit of work: the caller's next <see cref="IAppDbContext.SaveChangesAsync"/>
    /// removes it, and with it (FK cascade) everything it owns. False when the account is already gone.
    /// </summary>
    Task<bool> StageAccountDeletionAsync(Guid userId, CancellationToken ct);
}
