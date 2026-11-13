using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Application.Wikidata;

public enum WikidataClientKind
{
    Fake,
    Http,
}

/// <summary><c>Wikidata</c> section: the SPARQL client and how the catalog sync enriches films (ADR-0017).</summary>
public sealed class WikidataOptions : IConfigSection
{
    public static string SectionName => "Wikidata";

    public WikidataClientKind Client { get; init; } = WikidataClientKind.Fake;

    [Required]
    [Url]
    public string Endpoint { get; init; } = "https://query.wikidata.org/sparql";

    /// <summary>
    /// Wikimedia's policy requires a descriptive User-Agent with a way to reach the operator: a project URL, never a
    /// personal email.
    /// </summary>
    [Required]
    [MinLength(10)]
    public string UserAgent { get; init; } = "IthacaCatalogBot/1.0 (https://github.com/osamashtayat/lthaca)";

    /// <summary>Films per SPARQL query; each batch is saved before the next one starts.</summary>
    [Range(1, 200)]
    public int BatchSize { get; init; } = 100;

    /// <summary>Facts older than this are fetched again; films Wikidata didn't know are retried every run.</summary>
    [Range(1, 365)]
    public int RefreshDays { get; init; } = 30;
}
