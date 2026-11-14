using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Auth;

namespace Ithaca.Application.Users;

/// <summary><c>PUT /api/profile</c> (api-contract.md §14): the display name, same rules as at registration (trimmed, 1–40).</summary>
public sealed record UpdateProfileRequest(
    [Required, StringLength(RegisterRequest.DisplayNameMaxLength, MinimumLength = 1)] string DisplayName);

/// <summary><c>PUT /api/profile/password</c> (api-contract.md §14). The new password follows registration's rule (8–128).</summary>
public sealed record ChangePasswordRequest(
    [Required, MaxLength(RegisterRequest.PasswordMaxLength)] string CurrentPassword,
    [Required, StringLength(RegisterRequest.PasswordMaxLength, MinimumLength = RegisterRequest.PasswordMinLength)] string NewPassword);

/// <summary><c>DELETE /api/profile</c> (api-contract.md §14): the current password confirms the deletion.</summary>
public sealed record DeleteAccountRequest(
    [Required, MaxLength(RegisterRequest.PasswordMaxLength)] string Password);
