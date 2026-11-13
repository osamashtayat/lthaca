using Ithaca.Application.Ai;
using Ithaca.Application.Catalog;
using Ithaca.Application.Common;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Ai;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Infrastructure.Wikidata;
using Ithaca.Tests.Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Catalog;

/// <summary>
/// The sync's embedding step against a real pgvector database with the Fake embedding client. The Fake Wikidata client gives
/// facts to 550 and 27205, not 496243. The database is shared by the collection, so each test resets the state it reads.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class FilmEmbedderTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sync_EmbedsTheWikidataTextOfFilmsWithFacts_AndRecordsTheModel()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = await ResetAsync(scope);

        var result = await scope.ServiceProvider.GetRequiredService<CatalogSyncService>().RunAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Embedded.ShouldBeGreaterThanOrEqualTo(2);
        var (fightClub, facts) = await FilmWithFactsAsync(db, 550);
        fightClub.EmbeddingModel.ShouldBe("fake");
        fightClub.EmbeddingSourceHash.ShouldBe(facts!.EmbeddingTextHash);
        var expected = (await new FakeEmbeddingClient().EmbedAsync([facts.EmbeddingText], EmbeddingInputType.Document, Ct)).Value[0];
        fightClub.Embedding.ShouldBe(expected, "the vector is the Wikidata text's, nothing else");
        (await db.Films.AsNoTracking().SingleAsync(f => f.TmdbId == 496243, Ct)).Embedding
            .ShouldBeNull("no facts → no embedding: TMDB text is never embedded");
    }

    [Fact]
    public async Task Run_ReembedsOnlyFilmsWhoseTextOrModelChanged()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = await ResetAsync(scope);
        await scope.ServiceProvider.GetRequiredService<CatalogSyncService>().RunAsync(Ct);
        var fake = new CountingEmbeddings("fake");

        (await Embedder(db, fake).RunAsync(Ct)).Value.ShouldBe(0, "nothing changed since the sync");

        var (_, facts) = await FilmWithFactsAsync(db, 27205, tracked: true);
        facts!.Update(FakeWikidataFacts(27205) with { MainSubjects = ["dreams within dreams"] }, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(Ct);
        (await Embedder(db, fake).RunAsync(Ct)).Value.ShouldBe(1);
        fake.Texts.ShouldHaveSingleItem().ShouldContain("dreams within dreams");

        var voyage = new CountingEmbeddings("voyage-4");
        var reembedded = await Embedder(db, voyage).RunAsync(Ct);
        reembedded.Value.ShouldBeGreaterThanOrEqualTo(2, "a model switch re-embeds every film with facts");
        (await FilmWithFactsAsync(db, 550)).Film.EmbeddingModel.ShouldBe("voyage-4");
    }

    [Fact]
    public async Task Run_BatchesRequests_AndAFailureKeepsFinishedBatches()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = await ResetAsync(scope);
        await scope.ServiceProvider.GetRequiredService<WikidataEnricher>().RunAsync(Ct);
        var flaky = new CountingEmbeddings("fake") { FailOnceFor = FakeWikidataText(27205) };

        var first = await Embedder(db, flaky, batchSize: 1).RunAsync(Ct);

        first.Error.ShouldBe(AiErrors.Unavailable);
        flaky.BatchSizes.ShouldAllBe(size => size == 1);
        (await FilmWithFactsAsync(db, 550)).Film.Embedding.ShouldNotBeNull("550's batch came first and was saved");
        (await FilmWithFactsAsync(db, 27205)).Film.Embedding.ShouldBeNull();

        var second = await Embedder(db, flaky, batchSize: 1).RunAsync(Ct);

        second.Value.ShouldBe(1, "only the film left over");
        (await FilmWithFactsAsync(db, 27205)).Film.Embedding.ShouldNotBeNull();
    }

    private static FilmEmbedder Embedder(AppDbContext db, IEmbeddingClient client, int batchSize = 100) =>
        new(db, client, Options.Create(new CatalogSyncOptions { EmbeddingBatchSize = batchSize }));

    private static WikidataFacts FakeWikidataFacts(int tmdbId) => FakeWikidataClient.Films.Single(f => f.TmdbId == tmdbId).Facts;

    private static string FakeWikidataText(int tmdbId) => FilmFacts.BuildEmbeddingText(FakeWikidataFacts(tmdbId));

    /// <summary>Clears facts and vectors so each test sees films 550/27205/496243 freshly seeded.</summary>
    private static async Task<AppDbContext> ResetAsync(AsyncServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.FilmFacts.ExecuteDeleteAsync(Ct);
        await db.Films.ExecuteUpdateAsync(s => s.SetProperty(f => f.Embedding, (float[]?)null).SetProperty(f => f.EmbeddingModel, (string?)null).SetProperty(f => f.EmbeddingSourceHash, (string?)null), Ct);
        return db;
    }

    private static async Task<(Film Film, FilmFacts? Facts)> FilmWithFactsAsync(AppDbContext db, int tmdbId, bool tracked = false)
    {
        db.ChangeTracker.Clear();
        var films = tracked ? db.Films : db.Films.AsNoTracking();
        var film = await films.SingleAsync(f => f.TmdbId == tmdbId, Ct);
        var facts = tracked ? db.FilmFacts : db.FilmFacts.AsNoTracking();
        return (film, await facts.SingleOrDefaultAsync(x => x.FilmId == film.Id, Ct));
    }

    /// <summary>Fake vectors under any model name; records texts and batch sizes; can fail once for the batch holding a text.</summary>
    private sealed class CountingEmbeddings(string model) : IEmbeddingClient
    {
        private readonly FakeEmbeddingClient fake = new();
        private bool failed;

        public string ModelName => model;

        public string? FailOnceFor { get; init; }

        public List<string> Texts { get; } = [];

        public List<int> BatchSizes { get; } = [];

        public Task<Result<IReadOnlyList<float[]>>> EmbedAsync(IReadOnlyList<string> texts, EmbeddingInputType inputType, CancellationToken ct)
        {
            if (!failed && FailOnceFor is { } text && texts.Contains(text))
            {
                failed = true;
                return Task.FromResult<Result<IReadOnlyList<float[]>>>(AiErrors.Unavailable);
            }

            inputType.ShouldBe(EmbeddingInputType.Document);
            Texts.AddRange(texts);
            BatchSizes.Add(texts.Count);
            return fake.EmbedAsync(texts, inputType, ct);
        }
    }
}
