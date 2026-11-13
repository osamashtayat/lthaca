using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ithaca.Infrastructure.Tmdb;

/// <summary>
/// TMDB v3 over HTTP (<c>Tmdb:Client = Http</c>), as a typed <see cref="HttpClient"/>. Auth: a JWT-shaped key is the
/// "API Read Access Token" (Bearer header); anything else is the v3 "API Key" (<c>api_key</c> query parameter).
/// Any non-success status or network failure becomes <see cref="TmdbErrors.Unavailable"/>; 404 on a film is <c>null</c>.
/// </summary>
internal sealed partial class TmdbHttpClient(HttpClient http, IOptions<TmdbOptions> options, ILogger<TmdbHttpClient> logger) : ITmdbClient
{
    public async Task<Result<IReadOnlyList<TmdbFilmSummary>>> GetCatalogPageAsync(int page, CancellationToken ct)
    {
        var response = await GetAsync<DiscoverPage>(
            $"discover/movie?sort_by=popularity.desc&include_adult=false&include_video=false&page={page.ToString(CultureInfo.InvariantCulture)}", ct);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        // TMDB serves at most 500 pages; beyond total_pages it returns an empty list or an error status.
        return response.Value is { } body && page <= body.TotalPages
            ? body.Results.Select(r => new TmdbFilmSummary(r.Id, r.Title ?? "", YearOf(r.ReleaseDate), r.Popularity)).ToList()
            : [];
    }

    public async Task<Result<TmdbFilmDetails?>> GetFilmDetailsAsync(int tmdbId, CancellationToken ct)
    {
        var response = await GetAsync<MovieDetails>($"movie/{tmdbId.ToString(CultureInfo.InvariantCulture)}", ct);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        return response.Value is not { } m
            ? Result<TmdbFilmDetails?>.Success(null)
            : new TmdbFilmDetails(
                m.Id, m.Title ?? "", YearOf(m.ReleaseDate), Blank(m.Overview), Blank(m.PosterPath),
                m.Runtime is > 0 ? m.Runtime : null, m.Genres.Select(g => g.Name).ToList(), m.Popularity,
                Blank(m.BackdropPath), m.VoteAverage, m.VoteCount, Blank(m.OriginalLanguage));
    }

    public async Task<Result<IReadOnlyList<TmdbVideo>>> GetTrailersAsync(int tmdbId, CancellationToken ct)
    {
        var response = await GetAsync<VideoList>($"movie/{tmdbId.ToString(CultureInfo.InvariantCulture)}/videos", ct);
        if (!response.IsSuccess)
        {
            return response.Error;
        }

        return (response.Value?.Results ?? [])
            .Where(v => v.Type == "Trailer")
            .Select(v => new TmdbVideo(v.Site, v.Key, v.Name))
            .ToList();
    }

    /// <summary>One GET. Null value = 404. Never throws for HTTP or network failures.</summary>
    private async Task<Result<T?>> GetAsync<T>(string relativeUrl, CancellationToken ct)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, WithAuthAndLanguage(relativeUrl));
        if (IsReadAccessToken(options.Value.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        }

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result<T?>.Success(null);
            }

            if (!response.IsSuccessStatusCode)
            {
                LogFailure(logger, relativeUrl.Split('?')[0], (int)response.StatusCode);
                return TmdbErrors.Unavailable;
            }

            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Network failure, HttpClient timeout, or a body that isn't the JSON we expect: never an exception to the caller.
            LogNetworkFailure(logger, e, relativeUrl.Split('?')[0]);
            return TmdbErrors.Unavailable;
        }
    }

    private string WithAuthAndLanguage(string relativeUrl)
    {
        var separator = relativeUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var url = $"{relativeUrl}{separator}language={Uri.EscapeDataString(options.Value.Language)}";
        var key = options.Value.ApiKey;
        return key is not null && !IsReadAccessToken(key) ? $"{url}&api_key={Uri.EscapeDataString(key)}" : url;
    }

    private static bool IsReadAccessToken(string? key) => key is not null && key.StartsWith("eyJ", StringComparison.Ordinal);

    private static int? YearOf(string? date) =>
        date is { Length: >= 4 } && int.TryParse(date.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year) ? year : null;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    [LoggerMessage(Level = LogLevel.Warning, Message = "TMDB {Path} answered {Status}")]
    private static partial void LogFailure(ILogger logger, string path, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "TMDB {Path} failed (network, timeout or unexpected body)")]
    private static partial void LogNetworkFailure(ILogger logger, Exception exception, string path);

    private sealed record DiscoverPage(
        [property: JsonPropertyName("page")] int Page,
        [property: JsonPropertyName("total_pages")] int TotalPages,
        [property: JsonPropertyName("results")] List<DiscoverResult> Results);

    private sealed record DiscoverResult(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("release_date")] string? ReleaseDate,
        [property: JsonPropertyName("popularity")] double Popularity);

    private sealed record MovieDetails(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("release_date")] string? ReleaseDate,
        [property: JsonPropertyName("overview")] string? Overview,
        [property: JsonPropertyName("poster_path")] string? PosterPath,
        [property: JsonPropertyName("runtime")] int? Runtime,
        [property: JsonPropertyName("popularity")] double? Popularity,
        [property: JsonPropertyName("backdrop_path")] string? BackdropPath,
        [property: JsonPropertyName("vote_average")] double? VoteAverage,
        [property: JsonPropertyName("vote_count")] int? VoteCount,
        [property: JsonPropertyName("original_language")] string? OriginalLanguage,
        [property: JsonPropertyName("genres")] List<Genre> Genres);

    private sealed record Genre([property: JsonPropertyName("name")] string Name);

    private sealed record VideoList([property: JsonPropertyName("results")] List<Video> Results);

    private sealed record Video(
        [property: JsonPropertyName("site")] string Site,
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type);
}
