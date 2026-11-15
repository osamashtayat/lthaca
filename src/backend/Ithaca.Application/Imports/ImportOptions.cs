using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Application.Imports;

/// <summary>
/// <c>Imports</c> section: the upload limits of api-contract.md §13 (defaults are the contract's) and how the background
/// job paces itself. The upload size cap is a constant instead (<see cref="ImportLimits.MaxUploadBytes"/>).
/// </summary>
public sealed class ImportOptions : IConfigSection
{
    public static string SectionName => "Imports";

    /// <summary>Whether <c>ImportJobRunner</c> processes imports. On by default; tests turn it off to drive batches themselves.</summary>
    public bool JobEnabled { get; init; } = true;

    /// <summary>A ZIP with more entries (of any name) is refused as <c>import.invalid_file</c>.</summary>
    [Range(1, 100_000)]
    public int MaxZipEntries { get; init; } = 2_000;

    /// <summary>Uncompressed : compressed bytes allowed per file read, counted while reading; above it is <c>import.invalid_file</c>.</summary>
    [Range(1, 10_000)]
    public int MaxCompressionRatio { get; init; } = 100;

    /// <summary>Uncompressed bytes of the files read, in total, counted while reading; above it is 413 <c>import.file_too_large</c>.</summary>
    [Range(1L, 1L << 32)]
    public long MaxUncompressedBytes { get; init; } = 50L * 1024 * 1024;

    /// <summary>Distinct films per import; above it is 422 <c>import.too_many_films</c>.</summary>
    [Range(1, 1_000_000)]
    public int MaxFilms { get; init; } = 20_000;

    /// <summary>Films per batch: one save (progress, library changes) per batch.</summary>
    [Range(1, 1_000)]
    public int BatchSize { get; init; } = 50;

    /// <summary>How often the job looks for a queued import when it has nothing to do.</summary>
    [Range(typeof(TimeSpan), "00:00:00.1", "00:10:00")]
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Pause before every TMDB call, so one large import stays well under TMDB's rate limit.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:00:10")]
    public TimeSpan TmdbRequestDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Attempts per TMDB call before the film is <c>lookupFailed</c> (the job's retry budget).</summary>
    [Range(1, 10)]
    public int TmdbAttempts { get; init; } = 3;

    /// <summary>Back-off before the first retry; doubled for each further one.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:01:00")]
    public TimeSpan TmdbRetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}
