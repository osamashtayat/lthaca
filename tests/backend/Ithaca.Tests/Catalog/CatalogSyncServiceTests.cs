using Ithaca.Application.Ai;
using Ithaca.Application.Catalog;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Catalog;

/// <summary>The sync against a real pgvector database with the Fake TMDB client (3 sample films, already seeded in Development).</summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CatalogSyncServiceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Run_UpsertsPurgesStaleAndLeavesEmbeddingsOffByDefault()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stale = Film.Create(424242, "Old Film", DateTimeOffset.UtcNow.AddDays(-200));
        db.Films.Add(stale);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await scope.ServiceProvider.GetRequiredService<CatalogSyncService>().RunAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Pages.ShouldBe(1);
        (result.Value.Added + result.Value.Updated).ShouldBe(3);
        result.Value.Embedded.ShouldBe(0, "EmbedOverviews stays off: TMDB text must not reach AI (ADR-0017)");
        result.Value.Purged.ShouldBeGreaterThanOrEqualTo(1);
        (await db.Films.AnyAsync(f => f.TmdbId == 424242, TestContext.Current.CancellationToken)).ShouldBeFalse("older than the 6-month TMDB cache limit");
    }

    [Fact]
    public async Task Run_WithEmbeddingsOn_StoresVectorAndModel()
    {
        await using var factory = await IthacaApiFactory.StartAsync(
            postgres.ConnectionString, new Dictionary<string, string> { ["CatalogSync:EmbedOverviews"] = "true" });
        await using var scope = factory.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<CatalogSyncService>().RunAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var film = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Films.AsNoTracking()
            .SingleAsync(f => f.TmdbId == 550, TestContext.Current.CancellationToken);
        film.EmbeddingModel.ShouldBe("fake");
        film.Embedding!.Length.ShouldBe(Film.EmbeddingDimensions);
    }

    [Fact]
    public async Task Run_WhenTmdbFails_ReturnsTheErrorAndPurgesNothing()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Films.Add(Film.Create(434343, "Stale But Kept", DateTimeOffset.UtcNow.AddDays(-200)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new CatalogSyncService(
            db, new FailingTmdb(), scope.ServiceProvider.GetRequiredService<IEmbeddingClient>(),
            Options.Create(new CatalogSyncOptions()), TimeProvider.System);

        var result = await service.RunAsync(TestContext.Current.CancellationToken);

        result.Error.ShouldBe(TmdbErrors.Unavailable);
        (await db.Films.AnyAsync(f => f.TmdbId == 434343, TestContext.Current.CancellationToken)).ShouldBeTrue("a failed run must not purge");
    }

    private sealed class FailingTmdb : ITmdbClient
    {
        public Task<Result<IReadOnlyList<TmdbFilmSummary>>> GetCatalogPageAsync(int page, CancellationToken ct) =>
            Task.FromResult<Result<IReadOnlyList<TmdbFilmSummary>>>(TmdbErrors.Unavailable);

        public Task<Result<TmdbFilmDetails?>> GetFilmDetailsAsync(int tmdbId, CancellationToken ct) =>
            Task.FromResult<Result<TmdbFilmDetails?>>(TmdbErrors.Unavailable);

        public Task<Result<IReadOnlyList<TmdbVideo>>> GetTrailersAsync(int tmdbId, CancellationToken ct) =>
            Task.FromResult<Result<IReadOnlyList<TmdbVideo>>>(TmdbErrors.Unavailable);
    }
}
