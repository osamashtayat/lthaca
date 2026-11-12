using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Infrastructure.Jobs;

/// <summary><c>CatalogSync</c> section. Off by default until the sync is built.</summary>
public sealed class CatalogSyncOptions : IConfigSection
{
    public static string SectionName => "CatalogSync";

    public bool Enabled { get; init; }

    /// <summary>Time of day (UTC) of the nightly run.</summary>
    [Range(typeof(TimeOnly), "00:00", "23:59")]
    public TimeOnly RunAtUtc { get; init; } = new(3, 0);
}
