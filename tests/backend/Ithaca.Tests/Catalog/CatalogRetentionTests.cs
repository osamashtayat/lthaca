using System.Net.Http.Json;
using Ithaca.Application.Catalog;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Infrastructure.Tmdb;
using Ithaca.Tests.Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Catalog;

/// <summary>
/// ADR-0019 against a real database: films users reference are refreshed by id, never purged; a TMDB 404 keeps the row but
/// clears its TMDB fields; only orphans are purged; the database itself refuses to delete a referenced film.
/// Each test uses its own TMDB ids (the database is shared by the collection).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CatalogRetentionTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset LongAgo = DateTimeOffset.UtcNow.AddDays(-200);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sync_RefreshesReferencedFilms_MarksTmdb404sUnavailable_AndPurgesOnlyOrphans()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await AddStaleFilmsAsync(db, 771001, 771002, 771003);
        var user = await BrowserClient.RegisteredAsync(factory, Ct);
        await LikeAsync(user, 771001); // TMDB still knows it, but it left the popular pages
        await LikeAsync(user, 771002); // TMDB answers 404
        // 771003: nobody references it

        var tmdb = new StubTmdb(known: new() { [771001] = "Refreshed Title" });
        var result = await Sync(scope, db, tmdb).RunAsync(Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ReferencedRefreshed.ShouldBeGreaterThanOrEqualTo(1);
        result.Value.ReferencedUnavailable.ShouldBeGreaterThanOrEqualTo(1);
        tmdb.DetailsRequested.ShouldContain(771001, "referenced films are fetched by id");
        tmdb.DetailsRequested.ShouldContain(771002);
        tmdb.DetailsRequested.ShouldNotContain(771003, "orphans aren't refreshed");
        db.ChangeTracker.Clear();

        var refreshed = await db.Films.AsNoTracking().SingleAsync(f => f.TmdbId == 771001, Ct);
        refreshed.Title.ShouldBe("Refreshed Title");
        refreshed.SyncedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5), "past MaxAgeDays, refreshed instead of purged");
        refreshed.IsAvailable.ShouldBeTrue();

        var gone = await db.Films.AsNoTracking().SingleAsync(f => f.TmdbId == 771002, Ct);
        gone.IsAvailable.ShouldBeFalse();
        gone.Overview.ShouldBeNull("TMDB fields are cleared");
        gone.PosterPath.ShouldBeNull();
        gone.Title.ShouldBe("Stale 771002", "the row and our ids stay");
        (await user.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/library/771002", Ct)).GetProperty("liked").GetBoolean()
            .ShouldBeTrue("the user's library survives");

        (await db.Films.AnyAsync(f => f.TmdbId == 771003, Ct)).ShouldBeFalse("an unreferenced stale film is purged");
    }

    [Fact]
    public async Task Sync_FilmComingBackToTmdb_IsAvailableAgain()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await AddStaleFilmsAsync(db, 772001);
        var user = await BrowserClient.RegisteredAsync(factory, Ct);
        await LikeAsync(user, 772001);
        (await Sync(scope, db, new StubTmdb(known: [])).RunAsync(Ct)).IsSuccess.ShouldBeTrue();

        (await Sync(scope, db, new StubTmdb(known: new() { [772001] = "Back Again" })).RunAsync(Ct)).IsSuccess.ShouldBeTrue();

        db.ChangeTracker.Clear();
        var film = await db.Films.AsNoTracking().SingleAsync(f => f.TmdbId == 772001, Ct);
        film.IsAvailable.ShouldBeTrue();
        film.Overview.ShouldBe("Overview of Back Again");
    }

    [Fact]
    public async Task Sync_ReferencedFilmServedWithoutATitle_IsMarkedUnavailable()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await AddStaleFilmsAsync(db, 775001);
        var user = await BrowserClient.RegisteredAsync(factory, Ct);
        await LikeAsync(user, 775001);

        (await Sync(scope, db, new StubTmdb(known: new() { [775001] = "   " })).RunAsync(Ct)).IsSuccess.ShouldBeTrue();

        db.ChangeTracker.Clear();
        var film = await db.Films.AsNoTracking().SingleAsync(f => f.TmdbId == 775001, Ct);
        film.IsAvailable.ShouldBeFalse();
        film.Overview.ShouldBeNull("its old TMDB data doesn't outlive MaxAgeDays");
        film.SyncedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Sync_WhenTheRefreshFails_PurgesNothing()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await AddStaleFilmsAsync(db, 773001, 773002);
        var user = await BrowserClient.RegisteredAsync(factory, Ct);
        await LikeAsync(user, 773001);

        var result = await Sync(scope, db, new StubTmdb(known: []) { FailDetails = true }).RunAsync(Ct);

        result.Error.ShouldBe(TmdbErrors.Unavailable);
        (await db.Films.AnyAsync(f => f.TmdbId == 773002, Ct)).ShouldBeTrue("a failed run must not purge, orphans included");
    }

    [Fact]
    public async Task Database_RefusesToDeleteAReferencedFilm()
    {
        await using var factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await AddStaleFilmsAsync(db, 774001);
        var user = await BrowserClient.RegisteredAsync(factory, Ct);
        await LikeAsync(user, 774001);

        var error = await Should.ThrowAsync<Exception>(() => db.Films.Where(f => f.TmdbId == 774001).ExecuteDeleteAsync(Ct));

        error.ToString().ShouldContain("fk_user_films_films_film_id", customMessage: "ON DELETE RESTRICT (ADR-0019)");
        (await db.Films.AnyAsync(f => f.TmdbId == 774001, Ct)).ShouldBeTrue();
    }

    private static CatalogSyncService Sync(AsyncServiceScope scope, AppDbContext db, ITmdbClient tmdb) =>
        new(db, tmdb, scope.ServiceProvider.GetRequiredService<WikidataEnricher>(), scope.ServiceProvider.GetRequiredService<FilmEmbedder>(),
            Options.Create(new CatalogSyncOptions { Pages = 1, MaxAgeDays = 180 }), TimeProvider.System);

    private static async Task AddStaleFilmsAsync(AppDbContext db, params int[] tmdbIds)
    {
        foreach (var id in tmdbIds)
        {
            var film = Film.Create(id, $"Stale {id}", LongAgo);
            film.UpdateDetails($"Stale {id}", 2001, $"Overview {id}", $"/poster-{id}.jpg", 100, ["Drama"], 1, null, null, null, null, LongAgo);
            db.Films.Add(film);
        }

        await db.SaveChangesAsync(Ct);
    }

    private static async Task LikeAsync(HttpClient user, int tmdbId)
    {
        using var response = await user.PutAsJsonAsync($"/api/library/{tmdbId}", new { liked = true, onWatchlist = false, seen = false, rating = (int?)null }, Ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A TMDB whose popular pages are empty; details for the known ids and for the Fake TMDB sample films (other tests' users
    /// reference those, and the refresh step must not clear them), 404 for the rest, or a failure.
    /// </summary>
    private sealed class StubTmdb(Dictionary<int, string> known) : ITmdbClient
    {
        public bool FailDetails { get; init; }

        public List<int> DetailsRequested { get; } = [];

        public Task<Result<IReadOnlyList<TmdbFilmSummary>>> GetCatalogPageAsync(int page, CancellationToken ct) =>
            Task.FromResult<Result<IReadOnlyList<TmdbFilmSummary>>>(Array.Empty<TmdbFilmSummary>());

        public Task<Result<TmdbFilmDetails?>> GetFilmDetailsAsync(int tmdbId, CancellationToken ct)
        {
            DetailsRequested.Add(tmdbId);
            if (FailDetails)
            {
                return Task.FromResult<Result<TmdbFilmDetails?>>(TmdbErrors.Unavailable);
            }

            return Task.FromResult(Result<TmdbFilmDetails?>.Success(known.TryGetValue(tmdbId, out var title)
                ? new TmdbFilmDetails(tmdbId, title, 2001, $"Overview of {title}", "/fresh.jpg", 101, ["Drama"], 2)
                : FakeTmdbClient.Films.FirstOrDefault(f => f.TmdbId == tmdbId)));
        }

        public Task<Result<IReadOnlyList<TmdbVideo>>> GetTrailersAsync(int tmdbId, CancellationToken ct) =>
            Task.FromResult<Result<IReadOnlyList<TmdbVideo>>>(Array.Empty<TmdbVideo>());
    }
}
