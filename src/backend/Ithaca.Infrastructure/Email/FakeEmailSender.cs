using System.Collections.Concurrent;
using Ithaca.Application.Email;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ithaca.Infrastructure.Email;

/// <summary>
/// Offline <see cref="IEmailSender"/> (tests, local development): sends nothing and keeps what it would have sent.
/// In Development only it also logs the text body, so a reset link can be opened by hand; elsewhere it logs the subject,
/// because the body can hold a reset token.
/// </summary>
internal sealed partial class FakeEmailSender(IHostEnvironment environment, ILogger<FakeEmailSender> logger) : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>Every message "sent" so far, oldest first.</summary>
    public IReadOnlyList<EmailMessage> Sent => [.. _sent];

    public Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        _sent.Enqueue(message);
        if (environment.IsDevelopment())
        {
            LogBody(logger, message.Subject, message.Text);
        }
        else
        {
            LogSubject(logger, message.Subject);
        }

        return Task.FromResult(true);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Fake email (not sent): {Subject}\n{Text}")]
    private static partial void LogBody(ILogger logger, string subject, string text);

    [LoggerMessage(Level = LogLevel.Information, Message = "Fake email (not sent): {Subject}")]
    private static partial void LogSubject(ILogger logger, string subject);
}
