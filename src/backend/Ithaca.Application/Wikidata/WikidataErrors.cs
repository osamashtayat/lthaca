using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Wikidata;

/// <summary>The Wikidata errors, defined once.</summary>
public static class WikidataErrors
{
    public static readonly Error Unavailable = new(ErrorCodes.WikidataUnavailable, "Wikidata is unavailable, rate limited, or answered with something we can't read.");
}
