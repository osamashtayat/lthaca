using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Application.Catalog;

/// <summary><c>CatalogSync</c> section: when the nightly job runs and what the sync does (ADR-0003, ADR-0013, ADR-0017).</summary>
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
    /// Films per embedding request (and per save). Voyage allows 1,000 texts per request, but an account without a payment
    /// method has low rate limits, so keep batches modest; the client backs off on 429 either way.
    /// </summary>
    [Range(1, 1000)]
    public int EmbeddingBatchSize { get; init; } = 100;

    /// <summary>Films not refreshed for this long are deleted. TMDB's terms cap caching at 6 months.</summary>
    [Range(1, 180)]
    public int MaxAgeDays { get; init; } = 180;
}
