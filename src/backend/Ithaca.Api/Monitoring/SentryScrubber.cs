using System.Text.RegularExpressions;
using Sentry;

namespace Ithaca.Api.Monitoring;

/// <summary>
/// Last line of privacy before anything leaves for Sentry (ADR-0028): no cookies, bodies, auth or antiforgery headers, no
/// user details beyond the internal id, and no query strings: incoming ones carry user text (search), outgoing ones TMDB's
/// v3 key (<c>api_key</c>).
/// <c>SendDefaultPii</c> is off as well; this removes what the SDK would still attach.
/// </summary>
internal static partial class SentryScrubber
{
    /// <summary>Request headers worth keeping for debugging; everything else (Cookie, X-XSRF-TOKEN, Referer…) is dropped.</summary>
    private static readonly HashSet<string> KeptHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept", "Accept-Language", "Content-Type", "User-Agent",
    };

    /// <summary>Breadcrumb and span data keys that carry a query string or fragment.</summary>
    private static readonly string[] QueryKeys = ["http.query", "http.fragment"];

    public static SentryEvent Scrub(SentryEvent sentryEvent)
    {
        ArgumentNullException.ThrowIfNull(sentryEvent);
        Scrub(sentryEvent.Request);
        Scrub(sentryEvent.User);
        return sentryEvent;
    }

    public static SentryTransaction Scrub(SentryTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        Scrub(transaction.Request);
        Scrub(transaction.User);
        foreach (var span in transaction.Spans)
        {
            span.Description = WithoutQueries(span.Description);
            foreach (var (key, value) in span.Data.ToList())
            {
                if (QueryKeys.Contains(key, StringComparer.Ordinal))
                {
                    span.SetData(key, null);
                }
                else if (value is string text)
                {
                    span.SetData(key, WithoutQueries(text));
                }
            }
        }

        return transaction;
    }

    public static Breadcrumb Scrub(Breadcrumb breadcrumb)
    {
        ArgumentNullException.ThrowIfNull(breadcrumb);
        var data = breadcrumb.Data?
            .Where(pair => !QueryKeys.Contains(pair.Key, StringComparer.Ordinal))
            .ToDictionary(pair => pair.Key, pair => WithoutQueries(pair.Value) ?? string.Empty, StringComparer.Ordinal);
        // Runs as the breadcrumb is recorded, so the new one's timestamp is the same moment.
        return new Breadcrumb(WithoutQueries(breadcrumb.Message) ?? string.Empty, breadcrumb.Type ?? "default", data, breadcrumb.Category, breadcrumb.Level);
    }

    /// <summary>Removes the query string and fragment from every absolute URL in <paramref name="text"/>.</summary>
    internal static string? WithoutQueries(string? text) => text is null ? null : UrlWithQuery().Replace(text, "$1");

    private static void Scrub(SentryRequest request)
    {
        request.Cookies = null;
        request.Data = null;
        // Query strings carry user text (browse search, library filters): never sent.
        request.QueryString = null;
        request.Url = WithoutQueries(request.Url);
        request.Env.Clear();
        foreach (var name in request.Headers.Keys.Where(name => !KeptHeaders.Contains(name)).ToList())
        {
            request.Headers.Remove(name);
        }
    }

    private static void Scrub(SentryUser user)
    {
        user.Email = null;
        user.Username = null;
        user.IpAddress = null;
        user.Other.Clear();
    }

    [GeneratedRegex(@"(https?://[^\s?#""']+)[?#][^\s""']*", RegexOptions.CultureInvariant)]
    private static partial Regex UrlWithQuery();
}
