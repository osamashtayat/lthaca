using System.Net;
using System.Text;
using Ithaca.Application.Tmdb;
using Ithaca.Infrastructure.Tmdb;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Tmdb;

/// <summary>TMDB over HTTP against a stub handler: auth detection, mapping, and the failure paths. No network.</summary>
public sealed class TmdbHttpClientTests
{
    private const string MovieJson = """
        {"id":550,"title":"Fight Club","release_date":"1999-10-15","overview":"An insomniac…","poster_path":"/p.jpg",
         "runtime":139,"popularity":61.4,"genres":[{"id":18,"name":"Drama"}]}
        """;

    private static (TmdbHttpClient Client, StubHandler Handler) Create(string apiKey, HttpStatusCode status = HttpStatusCode.OK, string body = MovieJson)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        var options = Options.Create(new TmdbOptions { Client = TmdbClientKind.Http, ApiKey = apiKey });
        return (new TmdbHttpClient(http, options, NullLogger<TmdbHttpClient>.Instance), handler);
    }

    [Fact]
    public async Task ApiKey_IsSentAsQueryParameter()
    {
        var (client, handler) = Create("abc123");

        await client.GetFilmDetailsAsync(550, TestContext.Current.CancellationToken);

        handler.LastRequest!.RequestUri!.Query.ShouldContain("api_key=abc123");
        handler.LastRequest.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAccessToken_IsSentAsBearer()
    {
        var (client, handler) = Create("eyJhbGciOiJIUzI1NiJ9.token");

        await client.GetFilmDetailsAsync(550, TestContext.Current.CancellationToken);

        handler.LastRequest!.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.LastRequest.RequestUri!.Query.ShouldNotContain("api_key");
    }

    [Fact]
    public async Task GetFilmDetails_MapsTheMovie()
    {
        var (client, _) = Create("k");

        var result = await client.GetFilmDetailsAsync(550, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new TmdbFilmDetails(550, "Fight Club", 1999, "An insomniac…", "/p.jpg", 139, result.Value!.Genres, 61.4));
        result.Value.Genres.ShouldBe(["Drama"]);
    }

    [Fact]
    public async Task UnknownFilm_IsNullNotAnError()
    {
        var (client, _) = Create("k", HttpStatusCode.NotFound, "{}");

        var result = await client.GetFilmDetailsAsync(1, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task FailureStatus_IsUnavailable(HttpStatusCode status)
    {
        var (client, _) = Create("k", status, "{}");

        var result = await client.GetCatalogPageAsync(1, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(TmdbErrors.Unavailable);
    }

    [Fact]
    public async Task CatalogPage_PastTheLastPage_IsEmpty()
    {
        var (client, _) = Create("k", body: """{"page":3,"total_pages":2,"results":[]}""");

        var result = await client.GetCatalogPageAsync(3, TestContext.Current.CancellationToken);

        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task MalformedBody_IsUnavailable()
    {
        var (client, _) = Create("k", body: "<html>gateway error</html>");

        var result = await client.GetFilmDetailsAsync(550, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(TmdbErrors.Unavailable);
    }

    [Fact]
    public async Task Timeout_IsUnavailable()
    {
        var handler = new StubHandler(HttpStatusCode.OK, MovieJson) { Throw = new TaskCanceledException("HttpClient.Timeout") };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        var client = new TmdbHttpClient(http, Options.Create(new TmdbOptions { ApiKey = "k" }), NullLogger<TmdbHttpClient>.Instance);

        var result = await client.GetFilmDetailsAsync(550, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(TmdbErrors.Unavailable);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public Exception? Throw { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Throw is not null
                ? Task.FromException<HttpResponseMessage>(Throw)
                : Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
