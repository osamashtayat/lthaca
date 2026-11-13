using System.Net;
using System.Net.Http.Json;
using Ithaca.Application.Common;
using Ithaca.Application.Films;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Films;

/// <summary>
/// BE-06: <c>GET /api/films</c> and <c>/genres</c> (api-contract.md §9) through the real API. The database is shared by the
/// collection, so every test inserts films whose titles carry its own token and searches for that token.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class FilmBrowseTests(PostgresFixture postgres) : IAsyncLifetime
{
    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private sealed record Seed(string Title, int? Year, int? Runtime, string[] Genres, double? Popularity, double? Vote);

    /// <summary>Inserts the films under a fresh token; returns the token and the TMDB ids in the order given.</summary>
    private async Task<(string Token, int[] Ids)> SeedAsync(params Seed[] seeds)
    {
        var token = "Zq" + Guid.NewGuid().ToString("N")[..10];
        var firstId = Random.Shared.Next(900_000_000, 1_900_000_000);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = new int[seeds.Length];
        for (var i = 0; i < seeds.Length; i++)
        {
            var s = seeds[i];
            ids[i] = firstId + i;
            var film = Film.Create(ids[i], $"{token} {s.Title}", DateTimeOffset.UtcNow);
            film.UpdateDetails($"{token} {s.Title}", s.Year, "overview", "/p.jpg", s.Runtime, s.Genres, s.Popularity, "/b.jpg", s.Vote, 100, "en", DateTimeOffset.UtcNow);
            db.Films.Add(film);
        }

        await db.SaveChangesAsync(Ct);
        return (token, ids);
    }

    private async Task<PagedResult<FilmSummaryDto>> BrowseAsync(string query, HttpClient? client = null)
    {
        var http = client ?? _factory.CreateClient();
        return (await http.GetFromJsonAsync<PagedResult<FilmSummaryDto>>($"/api/films?{query}", Ct))!;
    }

    private static int[] TmdbIds(PagedResult<FilmSummaryDto> page) => [.. page.Items.Select(f => f.TmdbId)];

    [Fact]
    public async Task Browse_IsOpenToAnonymousCallers_AndReturnsSummaries()
    {
        var (token, ids) = await SeedAsync(new Seed("Alpha", 2001, 100, ["Drama"], 5, 7.5));

        var page = await BrowseAsync($"search={token}");

        page.TotalCount.ShouldBe(1);
        var film = page.Items.Single();
        film.TmdbId.ShouldBe(ids[0]);
        film.VoteAverage.ShouldBe(7.5);
        film.Genres.ShouldBe(["Drama"]);
    }

    [Fact]
    public async Task Search_IsACaseInsensitiveSubstring_AndTreatsPercentLiterally()
    {
        var (token, ids) = await SeedAsync(
            new Seed("The Big Sleep", 1946, 114, ["Crime"], 3, 7.9),
            new Seed("Other", 2000, 100, ["Crime"], 2, 6));

        TmdbIds(await BrowseAsync($"search={token.ToUpperInvariant()}%20THE%20big%20SLEEP")).ShouldBe([ids[0]]);
        (await BrowseAsync($"search={token}%25")).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Genre_KeepsOnlyFilmsWithThatGenre()
    {
        var (token, ids) = await SeedAsync(
            new Seed("A", 2001, 100, ["Drama", "Comedy"], 3, 7),
            new Seed("B", 2002, 100, ["Horror"], 2, 7));

        TmdbIds(await BrowseAsync($"search={token}&genre=Comedy")).ShouldBe([ids[0]]);
    }

    [Fact]
    public async Task MaxRuntime_Under90_UpTo2h_AndAny()
    {
        var (token, ids) = await SeedAsync(
            new Seed("Short", 2001, 85, [], 5, 7),
            new Seed("Medium", 2002, 120, [], 4, 7),
            new Seed("Long", 2003, 121, [], 3, 7),
            new Seed("Unknown", 2004, null, [], 2, 7));

        TmdbIds(await BrowseAsync($"search={token}&maxRuntime=under90")).ShouldBe([ids[0]]);
        TmdbIds(await BrowseAsync($"search={token}&maxRuntime=upTo2h")).ShouldBe([ids[0], ids[1]]);
        (await BrowseAsync($"search={token}&maxRuntime=any")).TotalCount.ShouldBe(4);
    }

    [Fact]
    public async Task MinVoteAverage_KeepsFilmsAtOrAboveIt()
    {
        var (token, ids) = await SeedAsync(
            new Seed("Good", 2001, 100, [], 5, 7.0),
            new Seed("Meh", 2002, 100, [], 4, 6.9),
            new Seed("Unrated", 2003, 100, [], 3, null));

        TmdbIds(await BrowseAsync($"search={token}&minVoteAverage=7")).ShouldBe([ids[0]]);
    }

    [Fact]
    public async Task Filters_Combine()
    {
        var (token, ids) = await SeedAsync(
            new Seed("Match", 2001, 95, ["Comedy"], 5, 8),
            new Seed("WrongGenre", 2002, 95, ["Drama"], 4, 8),
            new Seed("TooLong", 2003, 150, ["Comedy"], 3, 8),
            new Seed("TooLow", 2004, 95, ["Comedy"], 2, 5));

        var page = await BrowseAsync($"search={token}&genre=Comedy&maxRuntime=upTo2h&minVoteAverage=7");

        TmdbIds(page).ShouldBe([ids[0]]);
    }

    [Fact]
    public async Task Sort_Popular_Newest_Rating_EachPutsMissingValuesLast()
    {
        var (token, ids) = await SeedAsync(
            new Seed("A", 2001, 100, [], 10, 6),
            new Seed("B", 2020, 100, [], 30, 9),
            new Seed("C", 2010, 100, [], 20, 7),
            new Seed("D", null, 100, [], null, null));

        TmdbIds(await BrowseAsync($"search={token}")).ShouldBe([ids[1], ids[2], ids[0], ids[3]], "default sort is popular");
        TmdbIds(await BrowseAsync($"search={token}&sort=popular")).ShouldBe([ids[1], ids[2], ids[0], ids[3]]);
        TmdbIds(await BrowseAsync($"search={token}&sort=newest")).ShouldBe([ids[1], ids[2], ids[0], ids[3]]);
        TmdbIds(await BrowseAsync($"search={token}&sort=rating")).ShouldBe([ids[1], ids[2], ids[0], ids[3]]);
    }

    [Fact]
    public async Task Paging_SplitsTheResult_AndReportsTheTotal()
    {
        var (token, ids) = await SeedAsync(
            new Seed("1", 2001, 100, [], 5, 7), new Seed("2", 2002, 100, [], 4, 7), new Seed("3", 2003, 100, [], 3, 7),
            new Seed("4", 2004, 100, [], 2, 7), new Seed("5", 2005, 100, [], 1, 7));

        var first = await BrowseAsync($"search={token}&pageSize=2&page=1");
        var last = await BrowseAsync($"search={token}&pageSize=2&page=3");
        var beyond = await BrowseAsync($"search={token}&pageSize=2&page=4");

        first.TotalCount.ShouldBe(5);
        TmdbIds(first).ShouldBe([ids[0], ids[1]]);
        TmdbIds(last).ShouldBe([ids[4]]);
        beyond.Items.ShouldBeEmpty();
        beyond.TotalCount.ShouldBe(5);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("minVoteAverage=11")]
    [InlineData("maxRuntime=forever")]
    [InlineData("sort=sideways")]
    public async Task OutOfRangeInput_Is400(string query)
    {
        using var response = await _factory.CreateClient().GetAsync(new Uri($"/api/films?{query}", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HideSeen_DropsTheCallersSeenFilms_AndIsIgnoredForAnonymous()
    {
        var (token, ids) = await SeedAsync(new Seed("Seen", 2001, 100, [], 5, 7), new Seed("Fresh", 2002, 100, [], 4, 7));
        var user = await BrowserClient.RegisteredAsync(_factory, Ct);
        using var put = await user.PutAsJsonAsync($"/api/library/{ids[0]}", new { liked = false, onWatchlist = false, seen = true, rating = (int?)null }, Ct);
        put.EnsureSuccessStatusCode();

        TmdbIds(await BrowseAsync($"search={token}&hideSeen=true", user)).ShouldBe([ids[1]]);
        TmdbIds(await BrowseAsync($"search={token}", user)).ShouldBe([ids[0], ids[1]]);
        TmdbIds(await BrowseAsync($"search={token}&hideSeen=true")).ShouldBe([ids[0], ids[1]]);
    }

    [Fact]
    public async Task UnavailableFilms_AreNotListed()
    {
        var (token, ids) = await SeedAsync(new Seed("Gone", 2001, 100, ["Zzgenre"], 5, 7), new Seed("Here", 2002, 100, ["Zzgenre"], 4, 7));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Films.Single(f => f.TmdbId == ids[0]).MarkUnavailable(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        TmdbIds(await BrowseAsync($"search={token}")).ShouldBe([ids[1]]);
    }

    [Fact]
    public async Task Genres_AreDistinctAndAlphabetical()
    {
        await SeedAsync(
            new Seed("A", 2001, 100, ["Zulu", "Alpha-Zq"], 5, 7),
            new Seed("B", 2002, 100, ["Alpha-Zq"], 4, 7));

        var genres = await _factory.CreateClient().GetFromJsonAsync<string[]>("/api/films/genres", Ct);

        genres.ShouldNotBeNull();
        genres.ShouldBe([.. genres.Order(StringComparer.Ordinal)], "alphabetical");
        genres.Distinct().Count().ShouldBe(genres.Length);
        genres.ShouldContain("Alpha-Zq");
        genres.ShouldContain("Drama");
    }

    [Fact]
    public async Task Get_ReturnsTheNewDisplayFields()
    {
        var film = await _factory.CreateClient().GetFromJsonAsync<FilmDto>("/api/films/550", Ct);

        film.ShouldNotBeNull();
        film.BackdropPath.ShouldBe("/fight-club-backdrop.jpg");
        film.VoteAverage.ShouldBe(8.4);
        film.VoteCount.ShouldBe(29000);
        film.OriginalLanguage.ShouldBe("en");
    }
}
