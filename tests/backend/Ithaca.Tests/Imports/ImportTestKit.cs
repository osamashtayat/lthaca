using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ithaca.Application.Common;
using Ithaca.Application.Imports;
using Ithaca.Application.Tmdb;
using Ithaca.Tests.Harness;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Imports;

/// <summary>Shared pieces of the import tests: archives, the fixture export, a scripted TMDB, driving the job by hand.</summary>
internal static class ImportTestKit
{
    /// <summary>Settings for import test hosts: no background runner (tests drive batches), no pacing, small batches.</summary>
    public static readonly Dictionary<string, string> Settings = new(StringComparer.Ordinal)
    {
        ["Imports:JobEnabled"] = "false",
        ["Imports:TmdbRequestDelay"] = "00:00:00",
        ["Imports:TmdbRetryDelay"] = "00:00:00",
        ["Imports:BatchSize"] = "3",
    };

    /// <summary>The fixture's files, by path inside the export ZIP (Letterboxd's layout, anonymised).</summary>
    public static IReadOnlyList<(string Path, byte[] Bytes)> FixtureFiles()
    {
        var root = RepoPaths.File("tests", "backend", "Ithaca.Tests", "Imports", "Fixtures", "letterboxd-export");
        return Directory.EnumerateFiles(root, "*.csv", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(f => (Path.GetRelativePath(root, f).Replace('\\', '/'), File.ReadAllBytes(f)))
            .ToList();
    }

    public static byte[] FixtureZip() => Zip(FixtureFiles());

    public static byte[] Zip(IEnumerable<(string Path, byte[] Bytes)> entries, CompressionLevel level = CompressionLevel.Optimal)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, bytes) in entries)
            {
                using var entry = zip.CreateEntry(path, level).Open();
                entry.Write(bytes);
            }
        }

        return buffer.ToArray();
    }

    public static byte[] Zip(params (string Path, string Text)[] entries) => Zip(entries.Select(e => (e.Path, Encoding.UTF8.GetBytes(e.Text))));

    /// <summary>Overwrites the stored bytes of one entry with garbage, so opening it fails: proves an entry is never read.</summary>
    public static byte[] CorruptEntry(byte[] zip, string path, byte[] original)
    {
        var copy = (byte[])zip.Clone();
        var name = Encoding.ASCII.GetBytes(path);
        for (var at = 0; at + 30 + name.Length < copy.Length; at++)
        {
            // Local file header: PK\x03\x04 … name length at +26, extra length at +28, name at +30.
            if (copy[at] != 'P' || copy[at + 1] != 'K' || copy[at + 2] != 3 || copy[at + 3] != 4
                || BitConverter.ToUInt16(copy, at + 26) != name.Length
                || !copy.AsSpan(at + 30, name.Length).SequenceEqual(name))
            {
                continue;
            }

            var dataAt = at + 30 + name.Length + BitConverter.ToUInt16(copy, at + 28);
            copy.AsSpan(dataAt, original.Length).Fill(0xFF);
            return copy;
        }

        throw new InvalidOperationException($"{path} not found in the archive.");
    }

    public static MultipartFormDataContent Form(byte[] bytes, string fileName)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string fileName, CancellationToken ct)
    {
        using var form = Form(bytes, fileName);
        return await client.PostAsync(new Uri("/api/imports", UriKind.Relative), form, ct);
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ct);

    /// <summary>Runs the job until nothing is queued or running, like the runner would; returns the outcomes seen.</summary>
    public static async Task<List<ImportBatchOutcome>> DrainAsync(WebApplicationFactory<Program> factory, CancellationToken ct)
    {
        var outcomes = new List<ImportBatchOutcome>();
        for (var round = 0; round < 10_000; round++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ImportService>().ProcessNextBatchAsync(ct);
            if (result.Outcome == ImportBatchOutcome.Idle)
            {
                return outcomes;
            }

            outcomes.Add(result.Outcome);
        }

        throw new InvalidOperationException("The import job never went idle.");
    }
}

/// <summary>
/// A TMDB for import tests: the dev seed's three films plus films only TMDB knows. Search matches by normalised title
/// containment, filtered by release year when one is given (like TMDB's <c>year</c>). Can fail or throw, and counts calls.
/// </summary>
internal sealed class ScriptedTmdb : ITmdbClient
{
    public List<(TmdbSearchResult Result, TmdbFilmDetails? Details)> Films { get; } =
    [
        (new(550, "Fight Club", null, 1999, 100), null),
        (new(27205, "Inception", null, 2010, 99), null),
        (new(496243, "Parasite", "기생충", 2019, 98), null),
        (new(194, "Amélie", "Le Fabuleux Destin d'Amélie Poulain", 2001, 40), Details(194, "Amélie", 2001)),
        (new(900002, "The Glasshouse Affair", null, 2024, 5), Details(900002, "The Glasshouse Affair", 2024)),
        (new(10549, "Hamlet", null, 1996, 10), Details(10549, "Hamlet", 1996)),
        (new(900003, "Hamlet", null, 1996, 2), Details(900003, "Hamlet", 1996)),
        (new(900004, "Dune: Part Three", null, 2026, 50), Details(900004, "Dune: Part Three", 2026)),
        (new(900005, "Untitled Project", null, 2021, 1), Details(900005, "Untitled Project", 2021)),
    ];

    /// <summary>Searches for these titles fail (TMDB unavailable).</summary>
    public HashSet<string> FailingSearches { get; } = new(StringComparer.Ordinal);

    /// <summary>Searches for these titles throw: an unexpected error, not a TMDB failure.</summary>
    public HashSet<string> ThrowingSearches { get; } = new(StringComparer.Ordinal);

    public int SearchCalls { get; private set; }

    public int DetailsCalls { get; private set; }

    public static TmdbFilmDetails Details(int id, string title, int year) =>
        new(id, title, year, "Overview.", null, 100, ["Drama"], 1, null, 7, 100, "en");

    public Task<Result<IReadOnlyList<TmdbFilmSummary>>> GetCatalogPageAsync(int page, CancellationToken ct) =>
        Task.FromResult<Result<IReadOnlyList<TmdbFilmSummary>>>(Array.Empty<TmdbFilmSummary>());

    public Task<Result<TmdbFilmDetails?>> GetFilmDetailsAsync(int tmdbId, CancellationToken ct)
    {
        DetailsCalls++;
        return Task.FromResult(Result<TmdbFilmDetails?>.Success(Films.FirstOrDefault(f => f.Result.TmdbId == tmdbId).Details));
    }

    public Task<Result<IReadOnlyList<TmdbVideo>>> GetTrailersAsync(int tmdbId, CancellationToken ct) =>
        Task.FromResult<Result<IReadOnlyList<TmdbVideo>>>(Array.Empty<TmdbVideo>());

    public Task<Result<IReadOnlyList<TmdbSearchResult>>> SearchFilmsAsync(string title, int? year, CancellationToken ct)
    {
        SearchCalls++;
        if (ThrowingSearches.Contains(title))
        {
            throw new InvalidOperationException("Unexpected.");
        }

        if (FailingSearches.Contains(title))
        {
            return Task.FromResult<Result<IReadOnlyList<TmdbSearchResult>>>(TmdbErrors.Unavailable);
        }

        var query = TitleNormalizer.Normalize(title);
        return Task.FromResult<Result<IReadOnlyList<TmdbSearchResult>>>(Films
            .Select(f => f.Result)
            .Where(r => TitleNormalizer.Normalize(r.Title).Contains(query, StringComparison.Ordinal) && (year is null || r.ReleaseYear == year))
            .ToList());
    }
}
