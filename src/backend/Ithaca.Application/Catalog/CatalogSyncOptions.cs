using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Application.Catalog;

/// <summary><c>CatalogSync</c> section: when the nightly job runs and what the sync does (ADR-0003, ADR-0017).</summary>
public sealed class CatalogSyncOptions : IConfigSection
{
    public static string SectionName => "CatalogSync";

    /// <summary>Whether the nightly job runs. The <c>--sync-catalog</c> command runs a sync regardless.</summary>
    public bool Enabled { get; init; }

    /// <summary>Time of day (UTC) of the nightly run.</summary>
    [Range(typeof(TimeOnly), "00:00", "23:59")]
    public TimeOnly RunAtUtc { get; init; } = new(3, 0);

    /// <summary>TMDB discover pages to sync (20 films each), most popular first.</summary>
    [Range(1, 500)]
    public int Pages { get; init; } = 50;

    /// <summary>
    /// Embed title + overview into <c>films.embedding</c>. Must stay OFF: that text comes from TMDB, whose terms restrict AI use
    /// (ADR-0017). Embeddings will be built from Wikidata data instead; this switch then goes away.
    /// </summary>
    public bool EmbedOverviews { get; init; }

    /// <summary>Films not refreshed for this long are deleted. TMDB's terms cap caching at 6 months.</summary>
    [Range(1, 180)]
    public int MaxAgeDays { get; init; } = 180;
}
