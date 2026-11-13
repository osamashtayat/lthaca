using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ithaca.Application.Common;
using Ithaca.Application.Films;
using Ithaca.Application.Tmdb;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Films;

/// <summary>
/// BE-07 against a real database: the trailer is fetched from TMDB once, then served from <c>film_trailers</c> (ADR-0003);
/// refreshed after <see cref="FilmTrailer.RefreshAfter"/>, never served past <see cref="FilmTrailer.MaxAge"/> (ADR-0017);
/// <c>tmdb.unavailable</c> only when TMDB fails and nothing servable is cached. Each test uses its own TMDB ids.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class FilmTrailerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TmdbVideo Teaser = new("YouTube", "teaser-key", "Teaser");
    private static readonly TmdbVideo VimeoTrailer = new("Vimeo", "vimeo-key", "Trailer on Vimeo");
    private static readonly TmdbVideo YouTubeTrailer = new("YouTube", "yt-key", "Official Trailer");
    private static readonly TmdbVideo SecondYouTubeTrailer = new("YouTube", "yt-key-2", "Trailer 2");

    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task FirstCall_ReturnsTheFirstYouTubeTrailer_SecondCallInsideTheWindow_MakesNoTmdbCall()
    {
        await AddFilmAsync(781001);
        var tmdb = new CountingTmdb { Videos = [VimeoTrailer, YouTubeTrailer, SecondYouTubeTrailer] };
        var clock = new Clock();

        var first = await GetAsync(781001, tmdb, clock);
        clock.Advance(FilmTrailer.RefreshAfter - TimeSpan.FromMinutes(1));
        var second = await GetAsync(781001, tmdb, clock);

        first.Value.Trailer.ShouldBe(new TrailerDto(TrailerSite.YouTube, "yt-key", "Official Trailer"));
        second.Value.ShouldBe(first.Value);
        tmdb.Calls.ShouldBe(1, "the second call is served from the cache");
    }

    [Fact]
    public async Task FilmWithoutATrailer_ReturnsNull_AndThatIsCachedToo()
    {
        await AddFilmAsync(781002);
        var tmdb = new CountingTmdb { Videos = [VimeoTrailer] };

        var first = await GetAsync(781002, tmdb, new Clock());
        var second = await GetAsync(781002, tmdb, new Clock());

        first.Value.Trailer.ShouldBeNull();
        second.Value.Trailer.ShouldBeNull();
        tmdb.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task UnknownFilm_IsNotFound_WithoutAskingTmdb()
    {
        var tmdb = new CountingTmdb { Videos = [YouTubeTrailer] };

        var result = await GetAsync(789999, tmdb, new Clock());

        result.Error.ShouldBe(FilmErrors.NotFound);
        tmdb.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task FilmTmdbNoLongerKnows_HasNoTrailer_WithoutAskingTmdb()
    {
        await AddFilmAsync(781003, unavailable: true);
        var tmdb = new CountingTmdb { Videos = [YouTubeTrailer] };

        var result = await GetAsync(781003, tmdb, new Clock());

        result.Value.Trailer.ShouldBeNull();
        tmdb.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task TmdbFails_WithNothingCached_IsUnavailable()
    {
        await AddFilmAsync(781004);

        var result = await GetAsync(781004, new CountingTmdb { Fail = true }, new Clock());

        result.Error.ShouldBe(TmdbErrors.Unavailable);
    }

    [Fact]
    public async Task PastRefreshAfter_AsksTmdbAgain_AndStoresTheNewAnswer()
    {
        await AddFilmAsync(781005);
        var clock = new Clock();
        (await GetAsync(781005, new CountingTmdb { Videos = [] }, clock)).Value.Trailer.ShouldBeNull();
        clock.Advance(FilmTrailer.RefreshAfter + TimeSpan.FromMinutes(1));

        var tmdb = new CountingTmdb { Videos = [YouTubeTrailer] };
        var refreshed = await GetAsync(781005, tmdb, clock);
        var cached = await GetAsync(781005, tmdb, clock);

        refreshed.Value.Trailer?.Key.ShouldBe("yt-key", "a trailer released since is picked up");
        cached.Value.ShouldBe(refreshed.Value);
        tmdb.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task TmdbFails_WithAStaleButServableCache_ServesTheCache()
    {
        await AddFilmAsync(781006);
        var clock = new Clock();
        await GetAsync(781006, new CountingTmdb { Videos = [Teaser, YouTubeTrailer] }, clock);
        clock.Advance(FilmTrailer.MaxAge - TimeSpan.FromDays(1));

        var tmdb = new CountingTmdb { Fail = true };
        var result = await GetAsync(781006, tmdb, clock);

        tmdb.Calls.ShouldBe(1, "past RefreshAfter TMDB is asked");
        result.Value.Trailer?.Key.ShouldBe("teaser-key");
    }

    [Fact]
    public async Task TmdbFails_WithACacheOlderThanMaxAge_IsUnavailable()
    {
        await AddFilmAsync(781007);
        var clock = new Clock();
        await GetAsync(781007, new CountingTmdb { Videos = [YouTubeTrailer] }, clock);
        clock.Advance(FilmTrailer.MaxAge + TimeSpan.FromMinutes(1));

        var result = await GetAsync(781007, new CountingTmdb { Fail = true }, clock);

        result.Error.ShouldBe(TmdbErrors.Unavailable, "TMDB data isn't served past 6 months (ADR-0017)");
    }

    [Fact]
    public async Task Endpoint_ReturnsTheDto_AndNotFoundProblem()
    {
        using var client = _factory.CreateClient();

        var trailer = await client.GetFromJsonAsync<JsonElement>("/api/films/550/trailer", Ct);
        using var missing = await client.GetAsync(new Uri("/api/films/999999/trailer", UriKind.Relative), Ct);

        trailer.GetProperty("trailer").ValueKind.ShouldBe(JsonValueKind.Null, "the Fake TMDB lists no videos");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var body = JsonDocument.Parse(await missing.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("code").GetString().ShouldBe("film.not_found");
    }

    private async Task<Result<FilmTrailerDto>> GetAsync(int tmdbId, ITmdbClient tmdb, TimeProvider clock)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await new FilmTrailerService(db, tmdb, clock).GetAsync(tmdbId, Ct);
    }

    private async Task AddFilmAsync(int tmdbId, bool unavailable = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var film = Film.Create(tmdbId, $"Trailer test {tmdbId}", DateTimeOffset.UtcNow);
        if (unavailable)
        {
            film.MarkUnavailable(DateTimeOffset.UtcNow);
        }

        db.Films.Add(film);
        await db.SaveChangesAsync(Ct);
    }

    /// <summary>A TMDB that lists <see cref="Videos"/> (already filtered to trailers, like the real client) or fails, counting calls.</summary>
    private sealed class CountingTmdb : ITmdbClient
    {
        public IReadOnlyList<TmdbVideo> Videos { get; init; } = [];

        public bool Fail { get; init; }

        public int Calls { get; private set; }

        public Task<Result<IReadOnlyList<TmdbFilmSummary>>> GetCatalogPageAsync(int page, CancellationToken ct) =>
            throw new InvalidOperationException("The trailer service never pages the catalog.");

        public Task<Result<TmdbFilmDetails?>> GetFilmDetailsAsync(int tmdbId, CancellationToken ct) =>
            throw new InvalidOperationException("The trailer service never fetches details.");

        public Task<Result<IReadOnlyList<TmdbVideo>>> GetTrailersAsync(int tmdbId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Fail ? Result<IReadOnlyList<TmdbVideo>>.Failure(TmdbErrors.Unavailable) : Result<IReadOnlyList<TmdbVideo>>.Success(Videos));
        }
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
