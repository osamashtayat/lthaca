using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ithaca.Application.Common;
using Ithaca.Application.Wikidata;
using Ithaca.Domain.Films;
using Microsoft.Extensions.Logging;

namespace Ithaca.Infrastructure.Wikidata;

/// <summary>
/// Wikidata over its SPARQL endpoint (<c>Wikidata:Client = Http</c>), as a typed <see cref="HttpClient"/>; base address and
/// the descriptive User-Agent are set at registration. One POST per batch of TMDB ids, joined on P4947 (TMDB movie ID).
/// Any non-success status, timeout or unreadable body becomes <see cref="WikidataErrors.Unavailable"/>.
/// </summary>
internal sealed partial class WikidataHttpClient(HttpClient http, ILogger<WikidataHttpClient> logger) : IWikidataClient
{
    /// <summary>
    /// One row per value, tagged with its <c>kind</c>. Each value branch looks up its own English label: a label lookup
    /// outside the branches would see an unbound value and match every labelled entity. Cast rows carry the actor's
    /// sitelink count in <c>num</c> (how well-known they are) since Wikidata keeps no billing order.
    /// </summary>
    private const string QueryTemplate = """
        SELECT ?tmdb ?item ?kind ?label ?ord ?num WHERE {
          VALUES ?tmdb { {{TMDB_IDS}} }
          ?item wdt:P4947 ?tmdb .
          {
            BIND("item" AS ?kind)
          } UNION {
            ?item wdt:P136 ?v . ?v rdfs:label ?label . FILTER(LANG(?label) = "en") BIND("genre" AS ?kind)
          } UNION {
            ?item wdt:P921 ?v . ?v rdfs:label ?label . FILTER(LANG(?label) = "en") BIND("subject" AS ?kind)
          } UNION {
            ?item wdt:P57 ?v . ?v rdfs:label ?label . FILTER(LANG(?label) = "en") BIND("director" AS ?kind)
          } UNION {
            ?item p:P161 ?st . ?st ps:P161 ?v . ?v rdfs:label ?label . FILTER(LANG(?label) = "en")
            OPTIONAL { ?st pq:P1545 ?ord } ?v wikibase:sitelinks ?num . BIND("cast" AS ?kind)
          } UNION {
            ?item wdt:P495 ?v . ?v rdfs:label ?label . FILTER(LANG(?label) = "en") BIND("country" AS ?kind)
          } UNION {
            ?item wdt:P364 ?v . ?v rdfs:label ?label . FILTER(LANG(?label) = "en") BIND("language" AS ?kind)
          } UNION {
            ?item wdt:P166 ?v . ?v rdfs:label ?label . FILTER(LANG(?label) = "en") BIND("award" AS ?kind)
          } UNION {
            ?item wdt:P577 ?date . BIND(YEAR(?date) AS ?num) BIND("year" AS ?kind)
          } UNION {
            ?item p:P2047/psn:P2047/wikibase:quantityAmount ?num . BIND("runtimeSeconds" AS ?kind)
          }
        }
        """;

    private const string EntityPrefix = "http://www.wikidata.org/entity/";

    public async Task<Result<IReadOnlyList<WikidataFilm>>> GetFilmsAsync(IReadOnlyCollection<int> tmdbIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tmdbIds);
        if (tmdbIds.Count == 0)
        {
            return Array.Empty<WikidataFilm>();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, http.BaseAddress)
        {
            Content = new FormUrlEncodedContent([new("query", Query(tmdbIds))]),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                LogFailure(logger, (int)response.StatusCode, tmdbIds.Count);
                return WikidataErrors.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<SparqlResults>(ct);
            return body?.Results?.Bindings is { } rows ? Result<IReadOnlyList<WikidataFilm>>.Success(Map(rows)) : WikidataErrors.Unavailable;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Network failure, HttpClient timeout, or a body that isn't SPARQL JSON: never an exception to the caller.
            LogNetworkFailure(logger, e, tmdbIds.Count);
            return WikidataErrors.Unavailable;
        }
    }

    /// <summary>P4947 values are strings, so the ids are quoted literals. Ints only: nothing user-supplied reaches the query.</summary>
    internal static string Query(IEnumerable<int> tmdbIds) =>
        QueryTemplate.Replace(
            "{{TMDB_IDS}}",
            string.Join(' ', tmdbIds.Distinct().Select(id => $"\"{id.ToString(CultureInfo.InvariantCulture)}\"")),
            StringComparison.Ordinal);

    /// <summary>
    /// Rows → one <see cref="WikidataFilm"/> per TMDB id. Should two items claim the same id, the oldest (lowest Q number)
    /// wins. Earliest release year and shortest runtime win; cast is ordered by billing (if given), fame, then name.
    /// </summary>
    internal static IReadOnlyList<WikidataFilm> Map(IReadOnlyList<Dictionary<string, SparqlValue>> rows)
    {
        var films = new List<WikidataFilm>();
        var byTmdb = rows
            .Select(r => new Row(
                IntOf(Value(r, "tmdb")),
                Value(r, "item") is { } uri && uri.StartsWith(EntityPrefix, StringComparison.Ordinal) ? uri[EntityPrefix.Length..] : null,
                Value(r, "kind") ?? "",
                Value(r, "label"),
                IntOf(Value(r, "ord")),
                DecimalOf(Value(r, "num"))))
            .Where(r => r.TmdbId is > 0 && IsQid(r.Item))
            .GroupBy(r => r.TmdbId!.Value);

        foreach (var group in byTmdb.OrderBy(g => g.Key))
        {
            var item = group.Select(r => r.Item!).Distinct(StringComparer.Ordinal).MinBy(QNumber)!;
            var mine = group.Where(r => r.Item == item).ToList();
            var cast = mine.Where(r => r.Kind == "cast" && r.Label is not null)
                .GroupBy(r => r.Label!, StringComparer.Ordinal)
                .Select(g => (Label: g.Key, Ord: g.Min(r => r.Ord ?? int.MaxValue), Fame: g.Max(r => r.Num ?? 0)))
                .OrderBy(c => c.Ord).ThenByDescending(c => c.Fame).ThenBy(c => c.Label, StringComparer.Ordinal)
                .Select(c => c.Label)
                .ToList();
            var year = mine.Where(r => r.Kind == "year" && r.Num is not null).Select(r => (int?)decimal.ToInt32(r.Num!.Value)).Min();
            var seconds = mine.Where(r => r.Kind == "runtimeSeconds" && r.Num > 0).Select(r => r.Num).Min();

            films.Add(new WikidataFilm(group.Key, new WikidataFacts(
                item,
                Labels(mine, "genre"),
                Labels(mine, "subject"),
                Labels(mine, "director"),
                cast,
                Labels(mine, "country"),
                Labels(mine, "language"),
                year,
                seconds is { } s ? (int)Math.Round(s / 60m, MidpointRounding.AwayFromZero) : null,
                Labels(mine, "award"))));
        }

        return films;
    }

    private static List<string> Labels(List<Row> rows, string kind) =>
        [.. rows.Where(r => r.Kind == kind && r.Label is not null).Select(r => r.Label!).Distinct(StringComparer.Ordinal)];

    /// <summary>Only items (<c>Q123</c>) can be films; anything else in the reply is skipped, never thrown on.</summary>
    private static bool IsQid(string? item) =>
        item is { Length: > 1 and <= FilmFacts.WikidataIdMaxLength } && item[0] == 'Q' && item.AsSpan(1).IndexOfAnyExceptInRange('0', '9') < 0;

    private static string? Value(Dictionary<string, SparqlValue> row, string name) => row.TryGetValue(name, out var v) ? v.Value : null;

    private static long QNumber(string item) =>
        item.Length > 1 && long.TryParse(item.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : long.MaxValue;

    private static int? IntOf(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static decimal? DecimalOf(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Wikidata SPARQL answered {Status} for a batch of {Count} films")]
    private static partial void LogFailure(ILogger logger, int status, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Wikidata SPARQL failed for a batch of {Count} films (network, timeout or unexpected body)")]
    private static partial void LogNetworkFailure(ILogger logger, Exception exception, int count);

    private sealed record Row(int? TmdbId, string? Item, string Kind, string? Label, int? Ord, decimal? Num);

    internal sealed record SparqlResults([property: JsonPropertyName("results")] SparqlBindings? Results);

    internal sealed record SparqlBindings([property: JsonPropertyName("bindings")] List<Dictionary<string, SparqlValue>>? Bindings);

    internal sealed record SparqlValue([property: JsonPropertyName("value")] string? Value);
}
