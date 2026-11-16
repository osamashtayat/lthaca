using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Auth;

namespace Ithaca.Application.Users;

/// <summary><c>POST /api/auth/forgot-password</c> (api-contract.md §4): where to send a reset link, if it has an account.</summary>
public sealed record ForgotPasswordRequest(
    [Required, EmailAddress, MaxLength(RegisterRequest.EmailMaxLength)] string Email);

/// <summary>
/// <c>POST /api/auth/reset-password</c> (api-contract.md §4): the opaque token from the emailed link and the new password,
/// which follows registration's rule (8–128, length only).
/// </summary>
public sealed record ResetPasswordRequest(
    [Required, MaxLength(ResetPasswordRequest.TokenMaxLength)] string Token,
    [Required, StringLength(RegisterRequest.PasswordMaxLength, MinimumLength = RegisterRequest.PasswordMinLength)] string NewPassword)
{
    public const int TokenMaxLength = 1024;
}
