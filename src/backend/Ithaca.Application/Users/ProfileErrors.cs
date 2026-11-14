using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Users;

/// <summary>The profile errors, defined once (api-contract.md §2, §14).</summary>
public static class ProfileErrors
{
    /// <summary>The current password didn't match (also while the account is locked out, like sign-in).</summary>
    public static readonly Error WrongPassword = new(ErrorCodes.ProfileWrongPassword, "The current password is wrong.");
}
