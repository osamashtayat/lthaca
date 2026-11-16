using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Ithaca.Application.Email;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ithaca.Infrastructure.Email;

/// <summary>
/// Offline <see cref="IEmailSender"/> (tests, local development, the default): sends nothing and keeps the last
/// <see cref="Kept"/> messages for tests. In Development it logs the links in the text (so a reset link can be opened by
/// hand); never the rest of the body, which holds user-typed text (the display name). Elsewhere it logs the subject only,
/// because a link can carry a reset token.
/// </summary>
internal sealed partial class FakeEmailSender(IHostEnvironment environment, ILogger<FakeEmailSender> logger) : IEmailSender
{
    /// <summary>A cap, so a host left on Fake by mistake doesn't hoard messages (and tokens) without limit.</summary>
    public const int Kept = 100;

    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>The last <see cref="Kept"/> messages "sent", oldest first.</summary>
    public IReadOnlyList<EmailMessage> Sent => [.. _sent];

    public Task<bool> SendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        _sent.Enqueue(message);
        while (_sent.Count > Kept && _sent.TryDequeue(out _))
        {
        }

        if (environment.IsDevelopment())
        {
            var links = string.Join(' ', Links().Matches(message.Text).Select(m => m.Value));
            LogLinks(logger, message.Subject, links);
        }
        else
        {
            LogSubject(logger, message.Subject);
        }

        return Task.FromResult(true);
    }

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex Links();

    [LoggerMessage(Level = LogLevel.Information, Message = "Fake email (not sent): {Subject} {Links}")]
    private static partial void LogLinks(ILogger logger, string subject, string links);

    [LoggerMessage(Level = LogLevel.Information, Message = "Fake email (not sent): {Subject}")]
    private static partial void LogSubject(ILogger logger, string subject);
}
