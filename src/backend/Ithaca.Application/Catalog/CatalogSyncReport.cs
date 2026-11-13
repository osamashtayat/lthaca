namespace Ithaca.Application.Catalog;

/// <summary>What one catalog sync did; logged by the job and printed by <c>--sync-catalog</c>.</summary>
public sealed record CatalogSyncReport(
    int Pages,
    int Added,
    int Updated,
    int Skipped,
    int Embedded,
    int Purged,
    int FactsEnriched,
    int FactsUnmatched,
    int ReferencedRefreshed,
    int ReferencedUnavailable);
