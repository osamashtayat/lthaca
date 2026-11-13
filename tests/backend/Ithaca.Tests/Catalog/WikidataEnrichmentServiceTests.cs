using Ithaca.Application.Ai;
using Ithaca.Application.Catalog;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Application.Wikidata;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Infrastructure.Wikidata;
using Ithaca.Tests.Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Catalog;

/// <summary>
/// Wikidata enrichment inside the catalog sync, against a real pgvector database. The Fake Wikidata client knows 550 and
/// 27205 but not 496243. The database is shared by the collection, so each test starts by clearing <c>film_facts</c>.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class WikidataEnrichmentServiceTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sync_GivesMatchedFilmsFactsAndAnEmbeddingText_AndSkipsUnmatched()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = await ClearedDbAsync(scope);

        var result = await scope.ServiceProvider.GetRequiredService<CatalogSyncService>().RunAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.FactsEnriched.ShouldBe(2);
        result.Value.FactsUnmatched.ShouldBeGreaterThanOrEqualTo(1);
        var facts = await FactsByTmdbIdAsync(db);
        facts.Keys.ToList().ShouldBe([550, 27205], ignoreOrder: true);
        facts[550].WikidataId.ShouldBe("Q190050");
        facts[550].Directors.ShouldBe(["David Fincher"]);
        facts[550].EmbeddingText.ShouldStartWith("Genres: drama film, psychological thriller, thriller film");
        facts[27205].EmbeddingText.ShouldContain("Subjects: dream world");
    }

    /// <summary>ADR-0017: whatever TMDB says about a film, none of it ends up in the text we embed.</summary>
    [Fact]
    public async Task Sync_EmbeddingTextNeverContainsTmdbText()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = await ClearedDbAsync(scope);
        var tmdb = new SentinelTmdb();
        var sync = new CatalogSyncService(
            db, tmdb, scope.ServiceProvider.GetRequiredService<IEmbeddingClient>(),
            scope.ServiceProvider.GetRequiredService<WikidataEnrichmentService>(),
            Options.Create(new CatalogSyncOptions { Pages = 1 }), TimeProvider.System);

        var result = await sync.RunAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        var film = await db.Films.AsNoTracking().SingleAsync(f => f.TmdbId == 550, Ct);
        film.Overview.ShouldBe(SentinelTmdb.Overview, "the TMDB text was stored for display");
        var text = (await FactsByTmdbIdAsync(db))[550].EmbeddingText;
        text.ShouldNotBeNullOrWhiteSpace();
        text.ShouldNotContain("TMDBSENTINEL", Case.Insensitive);
    }

    [Fact]
    public async Task Run_WhenWikidataFails_KeepsFinishedBatches_AndTheNextRunContinues()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = await ClearedDbAsync(scope);
        var flaky = new FlakyWikidata(failOnceFor: 27205);
        var service = Enricher(db, flaky, batchSize: 1);

        var first = await service.RunAsync(Ct);

        first.Error.ShouldBe(WikidataErrors.Unavailable);
        var afterFailure = await FactsByTmdbIdAsync(db);
        afterFailure.Keys.ToList().ShouldBe([550], "batches before the failing one (TMDB-id order) were saved");

        var second = await service.RunAsync(Ct);

        second.IsSuccess.ShouldBeTrue();
        (await FactsByTmdbIdAsync(db)).Keys.ToList().ShouldBe([550, 27205], ignoreOrder: true);
        flaky.RequestedIds.Count(id => id == 550).ShouldBe(1, "fresh facts are not fetched again");
    }

    [Fact]
    public async Task Run_RefetchesStaleFacts_AndRetriesUnmatchedFilms()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = await ClearedDbAsync(scope);
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        var counting = new FlakyWikidata(failOnceFor: null);
        var service = Enricher(db, counting, batchSize: 100, clock);
        (await service.RunAsync(Ct)).IsSuccess.ShouldBeTrue();
        counting.RequestedIds.Clear();

        (await service.RunAsync(Ct)).IsSuccess.ShouldBeTrue();
        counting.RequestedIds.ShouldContain(496243, "unmatched films are retried");
        counting.RequestedIds.ShouldNotContain(550, "facts are fresh for RefreshDays");

        clock.Now = clock.Now.AddDays(31);
        counting.RequestedIds.Clear();
        (await service.RunAsync(Ct)).IsSuccess.ShouldBeTrue();
        counting.RequestedIds.ShouldContain(550, "older than RefreshDays (30)");
    }

    private static WikidataEnrichmentService Enricher(AppDbContext db, IWikidataClient client, int batchSize, TimeProvider? time = null) =>
        new(db, client, Options.Create(new WikidataOptions { BatchSize = batchSize, RefreshDays = 30 }), time ?? TimeProvider.System,
            NullLogger<WikidataEnrichmentService>.Instance);

    private static async Task<AppDbContext> ClearedDbAsync(AsyncServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.FilmFacts.ExecuteDeleteAsync(Ct);
        return db;
    }

    private static async Task<Dictionary<int, FilmFacts>> FactsByTmdbIdAsync(AppDbContext db) =>
        await db.FilmFacts.AsNoTracking()
            .Join(db.Films, x => x.FilmId, f => f.Id, (x, f) => new { f.TmdbId, Facts = x })
            .ToDictionaryAsync(x => x.TmdbId, x => x.Facts, Ct);

    /// <summary>TMDB data for Fight Club with a marker in every TMDB text field.</summary>
    private sealed class SentinelTmdb : ITmdbClient
    {
        public const string Overview = "TMDBSENTINEL overview about soap.";

        private static readonly TmdbFilmDetails Film =
            new(550, "TMDBSENTINEL Title", 1999, Overview, "/tmdbsentinel.jpg", 139, ["TMDBSENTINEL-Genre", "Drama"], 50);

        public Task<Result<IReadOnlyList<TmdbFilmSummary>>> GetCatalogPageAsync(int page, CancellationToken ct) =>
            Task.FromResult<Result<IReadOnlyList<TmdbFilmSummary>>>(
                page == 1 ? new[] { new TmdbFilmSummary(550, Film.Title, 1999, 50) } : []);

        public Task<Result<TmdbFilmDetails?>> GetFilmDetailsAsync(int tmdbId, CancellationToken ct) =>
            Task.FromResult(Result<TmdbFilmDetails?>.Success(tmdbId == 550 ? Film : null));

        public Task<Result<IReadOnlyList<TmdbVideo>>> GetTrailersAsync(int tmdbId, CancellationToken ct) =>
            Task.FromResult<Result<IReadOnlyList<TmdbVideo>>>(Array.Empty<TmdbVideo>());
    }

    /// <summary>The Fake client's answers, failing once for the batch holding one id, recording every id asked for.</summary>
    private sealed class FlakyWikidata(int? failOnceFor) : IWikidataClient
    {
        private readonly FakeWikidataClient fake = new();
        private bool failed;

        public List<int> RequestedIds { get; } = [];

        public Task<Result<IReadOnlyList<WikidataFilm>>> GetFilmsAsync(IReadOnlyCollection<int> tmdbIds, CancellationToken ct)
        {
            if (!failed && failOnceFor is { } id && tmdbIds.Contains(id))
            {
                failed = true;
                return Task.FromResult<Result<IReadOnlyList<WikidataFilm>>>(WikidataErrors.Unavailable);
            }

            RequestedIds.AddRange(tmdbIds);
            return fake.GetFilmsAsync(tmdbIds, ct);
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
