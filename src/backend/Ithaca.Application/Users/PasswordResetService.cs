using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Application.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ithaca.Application.Users;

/// <summary>
/// Password reset by email (api-contract.md §4, owner's decisions 2026-11-16): a link valid for one hour and one use; the
/// reset signs the user in, ends their other sessions and lifts a sign-in lockout. The token itself is the session
/// manager's (Identity); the email goes through <see cref="IEmailSender"/> (Resend, L4). How often a caller may ask is
/// BE-19's (rate limits). Logs never carry the address or the token.
/// </summary>
public sealed partial class PasswordResetService(
    ISessionManager sessions,
    ICurrentActor current,
    SignInCompletion completion,
    IEmailSender email,
    IOptions<EmailOptions> options,
    ILogger<PasswordResetService> logger)
{
    private const string Template = "password-reset";

    /// <summary>Emails a reset link when the address has an account. Always succeeds, so it never reveals whether it has.</summary>
    public async Task<Result> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ticket = await sessions.CreatePasswordResetAsync(request.Email.Trim(), ct);
        if (ticket is null)
        {
            return Result.Success();
        }

        var link = $"{options.Value.SiteUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(ticket.Token)}";
        var message = EmailTemplates.Render(Template, ticket.Email, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["displayName"] = ticket.DisplayName,
            ["link"] = link,
        });

        if (!await email.SendAsync(message, ct))
        {
            // Still a success for the caller: the answer must not depend on the account (the user can ask again).
            LogNotSent(logger);
        }

        return Result.Success();
    }

    /// <summary>
    /// Sets the new password if the token is valid, lifts any lockout, ends the user's other sessions and signs this caller
    /// in (a guest caller is upgraded, as at sign-in). <c>auth.invalid_reset_token</c> otherwise.
    /// </summary>
    public async Task<Result<CurrentUserDto>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var previous = current.Actor; // read before the session switches to the user
        var reset = await sessions.ResetPasswordAsync(request.Token, request.NewPassword, ct);
        return reset.IsSuccess ? await completion.CompleteAsync(previous, reset.Value, ct) : reset.Error;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A password reset email could not be sent")]
    private static partial void LogNotSent(ILogger logger);
}
