using System.ComponentModel.DataAnnotations;

namespace Ithaca.Application.Auth;

/// <summary><c>POST /api/auth/register</c> (api-contract.md §4).</summary>
public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(RegisterRequest.EmailMaxLength)] string Email,
    [Required, StringLength(RegisterRequest.PasswordMaxLength, MinimumLength = RegisterRequest.PasswordMinLength)] string Password,
    [Required, StringLength(RegisterRequest.DisplayNameMaxLength, MinimumLength = 1)] string DisplayName)
{
    public const int EmailMaxLength = 256;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const int DisplayNameMaxLength = 40;
}

/// <summary><c>POST /api/auth/sign-in</c> (api-contract.md §4).</summary>
public sealed record SignInRequest(
    [Required, MaxLength(RegisterRequest.EmailMaxLength)] string Email,
    [Required, MaxLength(RegisterRequest.PasswordMaxLength)] string Password);
