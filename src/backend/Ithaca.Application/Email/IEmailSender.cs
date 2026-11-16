namespace Ithaca.Application.Email;

/// <summary>One email to one recipient: a plain-text and an HTML body with the same content.</summary>
public sealed record EmailMessage(string To, string Subject, string Text, string Html);

/// <summary>
/// Sends transactional email (password reset, BE-18) through the provider chosen by <see cref="EmailOptions.Provider"/>
/// (Resend, L4; Fake offline). Implemented in Infrastructure.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// False when the provider refused or couldn't be reached; the client has logged why (status only, never the address or
    /// content). Callers that must not reveal anything (forgot password) carry on either way.
    /// </summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct);
}
