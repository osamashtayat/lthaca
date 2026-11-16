using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Application.Email;

public enum EmailProvider
{
    /// <summary>Sends nothing; tests read what would have been sent. In Development it logs the text body (reset links).</summary>
    Fake,

    /// <summary>Resend (L4); needs <see cref="EmailOptions.ApiKey"/>.</summary>
    Resend,
}

/// <summary><c>Email</c> section: who sends Ithaca's transactional email, from which address, and the site its links open.</summary>
public sealed class EmailOptions : IConfigSection
{
    public static string SectionName => "Email";

    public EmailProvider Provider { get; init; } = EmailProvider.Fake;

    /// <summary>Secret: env var / user-secrets only (<c>Email__ApiKey</c>). A Resend key with sending access only.</summary>
    public string? ApiKey { get; init; }

    /// <summary>The sender, on the verified sending subdomain (L4), e.g. <c>Ithaca &lt;no-reply@mail.ithacamovies.com&gt;</c>.</summary>
    [Required]
    public string From { get; init; } = "Ithaca <no-reply@mail.ithacamovies.com>";

    /// <summary>The public site address that links in emails open (the SPA, not the API), without a trailing path.</summary>
    [Required]
    [Url]
    public string SiteUrl { get; init; } = "http://localhost:5273";
}
