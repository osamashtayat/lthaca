using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Users;

/// <summary>The password reset errors, defined once (api-contract.md §2, §4).</summary>
public static class PasswordResetErrors
{
    /// <summary>Malformed, unknown, expired or already used: the answer never says which.</summary>
    public static readonly Error InvalidToken = new(ErrorCodes.AuthInvalidResetToken, "The reset link is invalid or has expired.");
}
