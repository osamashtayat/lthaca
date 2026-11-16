using Ithaca.Application.Auth;
using Ithaca.Application.Common;

namespace Ithaca.Application.Users;

/// <summary>
/// Password reset by email (api-contract.md §4, owner's decisions 2026-11-16): a link valid for one hour and one use; the
/// reset signs the user in, ends their other sessions and lifts a sign-in lockout. Contract stub until BE-18.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
public sealed class PasswordResetService
{
    /// <summary>Emails a reset link when the address has an account. Always succeeds, so it never reveals whether it has.</summary>
    public Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct) => ContractStub.Command();

    /// <summary>
    /// Sets the new password if the token is valid, lifts any lockout, ends the user's other sessions and signs this caller
    /// in (a guest caller is upgraded, as at sign-in). <c>auth.invalid_reset_token</c> otherwise.
    /// </summary>
    public Task<Result<CurrentUserDto>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct) => ContractStub.Of<CurrentUserDto>();
}
