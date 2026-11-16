using System.Net;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Tests.Harness;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ithaca.Tests.Errors;

/// <summary>
/// The harness's <see cref="ServerErrorLog"/> (#68): a 500 leaves its exception there, so a flaky test can report the cause
/// instead of just the status. Errors other categories log (EF's handled write conflicts) leave nothing.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ServerErrorLogTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UnhandledException_IsCaptured_WithItsMessage()
    {
        // A fresh database, so the dev-seeded film 550 has no cached trailer and the endpoint asks the (throwing) TMDB.
        await using var factory = await IthacaApiFactory.StartAsync(await postgres.CreateDatabaseAsync(Ct));
        using var client = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<ITmdbClient>(new ThrowingTmdb())))
            .CreateClient();

        // EF logs every handled write conflict at Error with its exception; that must not land in the log.
        factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Microsoft.EntityFrameworkCore.Update")
            .Log(LogLevel.Error, default, "An exception occurred while saving changes.", new InvalidOperationException("handled conflict"), (state, _) => state);
        factory.ServerErrors.Errors.ShouldBeEmpty("only what reaches ProblemDetailsExceptionHandler counts");

        using var response = await client.GetAsync(new Uri("/api/films/550/trailer", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        factory.ServerErrors.Errors.ShouldHaveSingleItem().ShouldContain(ThrowingTmdb.Message);
        factory.ServerErrors.Describe().ShouldContain(ThrowingTmdb.Message);
    }

    private sealed class ThrowingTmdb : ITmdbClient
    {
        public const string Message = "server-error-log test: TMDB blew up";

        public Task<Result<IReadOnlyList<TmdbFilmSummary>>> GetCatalogPageAsync(int page, CancellationToken ct) => throw new InvalidOperationException(Message);

        public Task<Result<TmdbFilmDetails?>> GetFilmDetailsAsync(int tmdbId, CancellationToken ct) => throw new InvalidOperationException(Message);

        public Task<Result<IReadOnlyList<TmdbVideo>>> GetTrailersAsync(int tmdbId, CancellationToken ct) => throw new InvalidOperationException(Message);

        public Task<Result<IReadOnlyList<TmdbSearchResult>>> SearchFilmsAsync(string title, int? year, CancellationToken ct) => throw new InvalidOperationException(Message);
    }
}
