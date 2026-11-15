using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ithaca.Application.Imports;
using Ithaca.Application.Tmdb;
using Ithaca.Domain.Imports;
using Ithaca.Infrastructure.Identity;
using Ithaca.Infrastructure.Persistence;
using Ithaca.Tests.Harness;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Imports;

/// <summary>
/// BE-15: the import end to end (api-contract.md §13) on a fresh database per test, with a scripted TMDB and the job
/// driven by hand (<see cref="ImportTestKit.DrainAsync"/>), except where the runner itself is under test. The fixture is
/// an anonymised export in Letterboxd's layout: 10 films, 3 already in the catalog, 5 only on TMDB, 2 nowhere.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ImportsEndpointTests(PostgresFixture postgres)
{
    private const string Header = "Date,Name,Year,Letterboxd URI";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_FixtureExport_ImportsWithTheExpectedCounts()
    {
        await using var app = await StartAsync();
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);

        using var created = await ImportTestKit.UploadAsync(user, ImportTestKit.FixtureZip(), "letterboxd-export.zip", Ct);

        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var queued = await ImportTestKit.JsonAsync(created, Ct);
        created.Headers.Location!.ToString().ShouldBe($"/api/imports/{queued.GetProperty("id").GetString()}");
        queued.GetProperty("status").GetString().ShouldBe("queued");
        queued.GetProperty("filmCount").GetInt32().ShouldBe(10);
        queued.GetProperty("processedCount").GetInt32().ShouldBe(0);
        queued.GetProperty("finishedAt").ValueKind.ShouldBe(JsonValueKind.Null);

        (await ImportTestKit.DrainAsync(app.Factory, Ct)).ShouldBe([ImportBatchOutcome.Progressed, ImportBatchOutcome.Progressed, ImportBatchOutcome.Progressed, ImportBatchOutcome.Progressed, ImportBatchOutcome.Completed]);

        var job = await GetJobAsync(user, created.Headers.Location);
        job.GetProperty("status").GetString().ShouldBe("completed");
        job.GetProperty("finishedAt").ValueKind.ShouldBe(JsonValueKind.String);
        Counts(job).ShouldBe((10, 10, 8, 5, 2));
        Changes(job).ShouldBe((8, 7, 3, 2, 2, 0));
        job.GetProperty("notFoundListKept").GetBoolean().ShouldBeTrue();

        (await LibraryAsync(user, 550)).ShouldBe((true, false, true, 4.5m));
        (await LibraryAsync(user, 27205)).ShouldBe((false, true, true, null));
        (await LibraryAsync(user, 496243)).ShouldBe((true, false, true, 5m));
        (await LibraryAsync(user, 194)).ShouldBe((false, false, true, 3.5m), "Amelie = Amélie (accents), added from TMDB");
        (await LibraryAsync(user, 900002)).ShouldBe((false, false, true, null), "2023 in the file, 2024 on TMDB: one year off");
        (await LibraryAsync(user, 10549)).ShouldBe((false, false, true, null), "two Hamlets of 1996: the most popular");
        (await LibraryAsync(user, 900004)).ShouldBe((false, true, false, null), "watchlist only");
        (await LibraryAsync(user, 900005)).ShouldBe((false, false, true, null), "no year, exactly one result with that title");
        using var otherHamlet = await user.GetAsync("/api/films/900003", Ct);
        otherHamlet.StatusCode.ShouldBe(HttpStatusCode.NotFound, "the less popular Hamlet wasn't added");

        var notFound = await JsonAsync(user, $"{created.Headers.Location}/not-found?page=1&pageSize=20");
        notFound.GetProperty("totalCount").GetInt32().ShouldBe(2);
        var items = notFound.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("title").GetString()).ShouldBe(["Lost Reel", "Nowhere Film, The \"Sequel\""], "sorted by title");
        items[0].GetProperty("year").GetInt32().ShouldBe(1931);
        items[0].GetProperty("letterboxdUri").GetString().ShouldBe("https://boxd.it/zz02");
        items[0].GetProperty("reason").GetString().ShouldBe("noMatch");

        await using var db = app.Db();
        (await db.ImportItems.CountAsync(Ct)).ShouldBe(2, "matched items are deleted when done; the not-found list stays");
        (await db.LetterboxdFilmLinks.CountAsync(Ct)).ShouldBe(8, "every URI match is remembered");
    }

    [Fact]
    public async Task Upload_SameExportTwice_ChangesNothingTheSecondTime()
    {
        await using var app = await StartAsync();
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        await ImportAndDrainAsync(app, user, ImportTestKit.FixtureZip());
        var before = await LibrarySnapshotAsync(app);
        var searchesBefore = app.Tmdb.SearchCalls;

        var second = await ImportAndDrainAsync(app, user, ImportTestKit.FixtureZip());

        Counts(second).ShouldBe((10, 10, 8, 0, 2));
        Changes(second).ShouldBe((0, 0, 0, 0, 0, 0));
        (await LibrarySnapshotAsync(app)).ShouldBe(before, "same rows, flags, ratings and updatedAt");
        (app.Tmdb.SearchCalls - searchesBefore).ShouldBe(4, "matched URIs are reused; only the two films not found are searched again (two searches each)");
    }

    [Fact]
    public async Task Upload_AnIthacaRating_SurvivesTheImport()
    {
        await using var app = await StartAsync();
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        (await user.PutAsJsonAsync("/api/library/550", new { liked = false, onWatchlist = false, seen = true, rating = 2m }, Ct)).EnsureSuccessStatusCode();

        var job = await ImportAndDrainAsync(app, user, ImportTestKit.FixtureZip());

        (await LibraryAsync(user, 550)).ShouldBe((true, false, true, 2m), "the file's 4.5 loses to the Ithaca rating; the like is added");
        Changes(job).ShouldBe((7, 6, 2, 2, 2, 1));
    }

    [Fact]
    public async Task Upload_CatalogFilms_NeedNoTmdbCall()
    {
        await using var app = await StartAsync();
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);

        var job = await ImportAndDrainAsync(app, user, Csv(Header, "2024-01-01,fight club,1999,", "2024-01-01,INCEPTION,2010,"), "watched.csv");

        Counts(job).ShouldBe((2, 2, 2, 0, 0));
        app.Tmdb.SearchCalls.ShouldBe(0);
        app.Tmdb.DetailsCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Upload_YearlessTitleWithSeveralResults_IsNoMatch()
    {
        await using var app = await StartAsync();
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);

        var job = await ImportAndDrainAsync(app, user, Csv(Header, "2024-01-01,Hamlet,,"), "watched.csv");

        Counts(job).ShouldBe((1, 1, 0, 0, 1));
    }

    [Fact]
    public async Task Upload_TmdbKeepsFailing_IsLookupFailedAndTheJobGoesOn()
    {
        await using var app = await StartAsync();
        app.Tmdb.FailingSearches.Add("Lost Reel");
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);

        var job = await ImportAndDrainAsync(app, user, ImportTestKit.FixtureZip());

        job.GetProperty("status").GetString().ShouldBe("completed");
        Counts(job).ShouldBe((10, 10, 8, 5, 2));
        var notFound = await JsonAsync(user, $"/api/imports/{job.GetProperty("id").GetString()}/not-found");
        notFound.GetProperty("items").EnumerateArray()
            .Select(i => (i.GetProperty("title").GetString(), i.GetProperty("reason").GetString()))
            .ShouldBe([("Lost Reel", "lookupFailed"), ("Nowhere Film, The \"Sequel\"", "noMatch")]);
    }

    [Fact]
    public async Task Upload_WhileAnImportIsActive_IsAlreadyRunning()
    {
        await using var app = await StartAsync();
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        using var first = await ImportTestKit.UploadAsync(user, ImportTestKit.FixtureZip(), "export.zip", Ct);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var second = await ImportTestKit.UploadAsync(user, ImportTestKit.FixtureZip(), "export.zip", Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeAsync(second)).ShouldBe("import.already_running");
    }

    [Fact]
    public async Task Upload_ConcurrentUploads_OnlyOneIsQueued()
    {
        await using var app = await StartAsync();
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        var zip = ImportTestKit.FixtureZip();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ImportTestKit.UploadAsync(user, zip, "export.zip", Ct)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(3);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Upload_RefusedFiles_AnswerTheirCodesAndStoreNothing()
    {
        await using var app = await StartAsync(new() { ["Imports:MaxFilms"] = "3", ["Imports:MaxUncompressedBytes"] = "4000" });
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        var oneFilmManyTimes = Csv(Header, [.. Enumerable.Repeat("2024-01-01,Same,2001,https://boxd.it/same", 200)]);

        var cases = new (byte[] Bytes, string Name, HttpStatusCode Status, string Code)[]
        {
            ([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], "watched.csv", HttpStatusCode.BadRequest, "import.invalid_file"),
            (Csv(Header, "2024-01-01,A,2001,"), "diary.csv", HttpStatusCode.BadRequest, "import.invalid_file"),
            (oneFilmManyTimes, "watched.csv", HttpStatusCode.RequestEntityTooLarge, "import.file_too_large"),
            (Csv(Header, "2024-01-01,A,2001,", "2024-01-01,B,2001,", "2024-01-01,C,2001,", "2024-01-01,D,2001,"), "watched.csv", (HttpStatusCode)422, "import.too_many_films"),
            (Csv(Header, "2024-01-01,,2001,"), "watched.csv", (HttpStatusCode)422, "import.nothing_to_import"),
        };

        foreach (var (bytes, name, status, code) in cases)
        {
            using var response = await ImportTestKit.UploadAsync(user, bytes, name, Ct);
            response.StatusCode.ShouldBe(status, code);
            (await CodeAsync(response)).ShouldBe(code);
        }

        (await JsonAsync(user, "/api/imports")).GetProperty("totalCount").GetInt32().ShouldBe(0);
        await using var db = app.Db();
        (await db.ImportItems.CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Upload_ZipBomb_IsInvalidFile()
    {
        await using var app = await StartAsync();
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        var bomb = ImportTestKit.Zip(("watched.csv", Header + "\n" + string.Concat(Enumerable.Repeat("2024-01-01,Same,2001,https://boxd.it/same\n", 200_000))));

        using var response = await ImportTestKit.UploadAsync(user, bomb, "export.zip", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).ShouldBe("import.invalid_file");
    }

    [Fact]
    public async Task NotFoundList_IsKeptForTheLatestImportOnly()
    {
        await using var app = await StartAsync();
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        var first = await ImportAndDrainAsync(app, user, ImportTestKit.FixtureZip());

        var second = await ImportAndDrainAsync(app, user, Csv(Header, "2024-01-01,Lost Reel,1931,https://boxd.it/zz02"), "watched.csv");

        var older = await JsonAsync(user, $"/api/imports/{first.GetProperty("id").GetString()}");
        older.GetProperty("notFoundListKept").GetBoolean().ShouldBeFalse();
        older.GetProperty("notFoundCount").GetInt32().ShouldBe(2, "counts stay");
        var olderList = await JsonAsync(user, $"/api/imports/{first.GetProperty("id").GetString()}/not-found");
        olderList.GetProperty("totalCount").GetInt32().ShouldBe(0);
        second.GetProperty("notFoundListKept").GetBoolean().ShouldBeTrue();
        (await JsonAsync(user, $"/api/imports/{second.GetProperty("id").GetString()}/not-found")).GetProperty("totalCount").GetInt32().ShouldBe(1);
        var list = await JsonAsync(user, "/api/imports?page=1&pageSize=1");
        list.GetProperty("totalCount").GetInt32().ShouldBe(2);
        list.GetProperty("items")[0].GetProperty("id").GetString().ShouldBe(second.GetProperty("id").GetString(), "newest first");
    }

    [Fact]
    public async Task AnotherUsersImport_IsNotFound()
    {
        await using var app = await StartAsync();
        using var owner = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        using var other = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        var job = await ImportAndDrainAsync(app, owner, ImportTestKit.FixtureZip());
        var id = job.GetProperty("id").GetString();

        foreach (var path in new[] { $"/api/imports/{id}", $"/api/imports/{id}/not-found", $"/api/imports/{Guid.NewGuid()}" })
        {
            using var response = await other.GetAsync(path, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
            (await CodeAsync(response)).ShouldBe("import.not_found");
        }

        (await JsonAsync(other, "/api/imports")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Job_InterruptedByARestart_Resumes()
    {
        var connectionString = await postgres.CreateDatabaseAsync(Ct);
        string location;
        await using (var before = await App.StartAsync(connectionString, null))
        {
            using var user = await BrowserClient.RegisteredAsync(before.Factory, Ct);
            using var created = await ImportTestKit.UploadAsync(user, ImportTestKit.FixtureZip(), "export.zip", Ct);
            location = created.Headers.Location!.ToString();
            await using var scope = before.Factory.Services.CreateAsyncScope();
            (await scope.ServiceProvider.GetRequiredService<ImportService>().ProcessNextBatchAsync(Ct)).Outcome.ShouldBe(ImportBatchOutcome.Progressed);
            var halfway = await JsonAsync(user, location);
            halfway.GetProperty("status").GetString().ShouldBe("running");
            halfway.GetProperty("processedCount").GetInt32().ShouldBe(3);
        }

        await using var after = await App.StartAsync(connectionString, null);
        await ImportTestKit.DrainAsync(after.Factory, Ct);

        await using var db = after.Db();
        var job = await db.ImportJobs.SingleAsync(Ct);
        job.Status.ShouldBe(ImportStatus.Completed);
        (job.ProcessedCount, job.MatchedCount, job.AddedToCatalogCount, job.NotFoundCount).ShouldBe((10, 8, 5, 2));
        (job.NewFilms, job.SeenSet, job.RatedSet, job.LikedSet, job.WatchlistSet).ShouldBe((8, 7, 3, 2, 2));
    }

    [Fact]
    public async Task Job_UnexpectedError_FailsAndKeepsEarlierBatches()
    {
        await using var app = await StartAsync();
        app.Tmdb.ThrowingSearches.Add("Hamlet"); // ZIP order: batch 1 Parasite, Fight Club, Amelie; batch 2 Inception, The Glasshouse Affair, Hamlet
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        using var created = await ImportTestKit.UploadAsync(user, ImportTestKit.FixtureZip(), "export.zip", Ct);

        (await ImportTestKit.DrainAsync(app.Factory, Ct)).ShouldBe([ImportBatchOutcome.Progressed, ImportBatchOutcome.Failed]);

        var job = await GetJobAsync(user, created.Headers.Location!);
        job.GetProperty("status").GetString().ShouldBe("failed");
        job.GetProperty("finishedAt").ValueKind.ShouldBe(JsonValueKind.String);
        Counts(job).ShouldBe((10, 3, 3, 1, 0));
        (await LibraryAsync(user, 550)).ShouldBe((true, false, true, 4.5m), "the first batch stays");
        (await LibraryAsync(user, 194)).ShouldBe((false, false, true, 3.5m), "the first batch stays");
        (await LibraryAsync(user, 27205)).ShouldBe((false, false, false, null), "the failed batch wrote nothing");
        using var glasshouse = await user.GetAsync("/api/films/900002", Ct);
        glasshouse.StatusCode.ShouldBe(HttpStatusCode.NotFound, "the failed batch added no film");
        await using var db = app.Db();
        (await db.ImportItems.CountAsync(Ct)).ShouldBe(0, "pending items of a failed import are dropped");

        using var again = await ImportTestKit.UploadAsync(user, ImportTestKit.FixtureZip(), "export.zip", Ct);
        again.StatusCode.ShouldBe(HttpStatusCode.Created, "a failed import isn't active");
    }

    [Fact]
    public async Task Runner_ProcessesQueuedImportsInTheBackground()
    {
        await using var app = await StartAsync(new() { ["Imports:JobEnabled"] = "true", ["Imports:PollInterval"] = "00:00:00.1" });
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        using var created = await ImportTestKit.UploadAsync(user, ImportTestKit.FixtureZip(), "export.zip", Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var job = await GetJobAsync(user, created.Headers.Location!);
        for (var attempt = 0; attempt < 300 && job.GetProperty("status").GetString() is "queued" or "running"; attempt++)
        {
            await Task.Delay(100, Ct);
            job = await GetJobAsync(user, created.Headers.Location!);
        }

        job.GetProperty("status").GetString().ShouldBe("completed");
        Counts(job).ShouldBe((10, 10, 8, 5, 2));
    }

    [Fact]
    public async Task DeletingTheUser_DeletesTheirImports()
    {
        await using var app = await StartAsync();
        using var user = await BrowserClient.RegisteredAsync(app.Factory, Ct);
        var job = await ImportAndDrainAsync(app, user, ImportTestKit.FixtureZip());
        var userId = (await JsonAsync(user, "/api/auth/me")).GetProperty("id").GetString()!;

        await using (var scope = app.Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = await users.FindByIdAsync(userId);
            (await users.DeleteAsync(account!)).Succeeded.ShouldBeTrue();
        }

        await using var db = app.Db();
        (await db.ImportJobs.AnyAsync(j => j.Id == Guid.Parse(job.GetProperty("id").GetString()!), Ct)).ShouldBeFalse();
        (await db.ImportItems.CountAsync(Ct)).ShouldBe(0);
        (await db.LetterboxdFilmLinks.CountAsync(Ct)).ShouldBe(8, "links hold ids only and aren't anyone's data");
    }

    private async Task<App> StartAsync(Dictionary<string, string>? settings = null) =>
        await App.StartAsync(await postgres.CreateDatabaseAsync(Ct), settings);

    private static async Task<JsonElement> ImportAndDrainAsync(App app, HttpClient user, byte[] bytes, string fileName = "export.zip")
    {
        using var created = await ImportTestKit.UploadAsync(user, bytes, fileName, Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        await ImportTestKit.DrainAsync(app.Factory, Ct);
        return await GetJobAsync(user, created.Headers.Location!);
    }

    private static Task<JsonElement> GetJobAsync(HttpClient user, Uri location) => JsonAsync(user, location.ToString());

    private static async Task<JsonElement> JsonAsync(HttpClient user, string path)
    {
        using var response = await user.GetAsync(new Uri(path, UriKind.Relative), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await ImportTestKit.JsonAsync(response, Ct);
    }

    private static async Task<(bool Liked, bool OnWatchlist, bool Seen, decimal? Rating)> LibraryAsync(HttpClient user, int tmdbId)
    {
        var row = await JsonAsync(user, $"/api/library/{tmdbId}");
        var rating = row.GetProperty("rating");
        return (row.GetProperty("liked").GetBoolean(), row.GetProperty("onWatchlist").GetBoolean(), row.GetProperty("seen").GetBoolean(),
            rating.ValueKind == JsonValueKind.Null ? null : rating.GetDecimal());
    }

    private static async Task<List<string>> LibrarySnapshotAsync(App app)
    {
        await using var db = app.Db();
        return await db.UserFilms.AsNoTracking()
            .OrderBy(u => u.FilmId)
            .Select(u => $"{u.FilmId} {u.Liked} {u.OnWatchlist} {u.Seen} {u.RatingHalfStars} {u.UpdatedAt:O}")
            .ToListAsync(Ct);
    }

    private static (int Films, int Processed, int Matched, int Added, int NotFound) Counts(JsonElement job) =>
        (job.GetProperty("filmCount").GetInt32(), job.GetProperty("processedCount").GetInt32(), job.GetProperty("matchedCount").GetInt32(),
            job.GetProperty("addedToCatalogCount").GetInt32(), job.GetProperty("notFoundCount").GetInt32());

    private static (int NewFilms, int Seen, int Rated, int Liked, int Watchlist, int RatingsKept) Changes(JsonElement job)
    {
        var c = job.GetProperty("changes");
        return (c.GetProperty("newFilms").GetInt32(), c.GetProperty("seen").GetInt32(), c.GetProperty("rated").GetInt32(),
            c.GetProperty("liked").GetInt32(), c.GetProperty("watchlist").GetInt32(), c.GetProperty("ratingsKept").GetInt32());
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    private static byte[] Csv(string header, params string[] rows) => Encoding.UTF8.GetBytes(string.Join("\n", [header, .. rows]) + "\n");

    /// <summary>
    /// A host on its own database with the scripted TMDB; disposing it stops the host (a "restart" when reopened). The base
    /// host never runs the import runner (it would use the plain Fake TMDB); <c>settings</c> apply to the scripted host only.
    /// </summary>
    private sealed class App : IAsyncDisposable
    {
        private readonly IthacaApiFactory _base;
        private readonly string _connectionString;

        private App(IthacaApiFactory @base, string connectionString, Dictionary<string, string>? settings)
        {
            _base = @base;
            _connectionString = connectionString;
            Factory = @base.WithWebHostBuilder(b =>
            {
                b.ConfigureTestServices(s => s.AddTransient<ITmdbClient>(_ => Tmdb));
                foreach (var (key, value) in settings ?? [])
                {
                    b.UseSetting(key, value);
                }
            });
        }

        public ScriptedTmdb Tmdb { get; } = new();

        public WebApplicationFactory<Program> Factory { get; }

        public static async Task<App> StartAsync(string connectionString, Dictionary<string, string>? settings) =>
            new(await IthacaApiFactory.StartAsync(connectionString, ImportTestKit.Settings), connectionString, settings);

        public AppDbContext Db()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>();
            PersistenceOptions.Configure(options, _connectionString);
            return new AppDbContext(options.Options);
        }

        public async ValueTask DisposeAsync()
        {
            await Factory.DisposeAsync();
            await _base.DisposeAsync();
        }
    }
}
