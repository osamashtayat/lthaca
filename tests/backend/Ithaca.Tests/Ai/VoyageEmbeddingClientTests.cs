using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Ithaca.Application.Ai;
using Ithaca.Domain.Errors;
using Ithaca.Domain.Films;
using Ithaca.Infrastructure;
using Ithaca.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Ai;

/// <summary>Voyage embeddings against a stub handler: request shape, batching, 429 back-off, vector checks. No network.</summary>
public sealed class VoyageEmbeddingClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (VoyageEmbeddingClient Client, StubHandler Handler, RecordingTime Time) Create(params Func<HttpRequestMessage, string, HttpResponseMessage>[] replies)
    {
        var handler = new StubHandler(replies);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.voyageai.com/v1/") };
        var time = new RecordingTime();
        var options = Options.Create(new AiOptions { EmbeddingProvider = EmbeddingProvider.Voyage, EmbeddingApiKey = "pa-test-key" });
        return (new VoyageEmbeddingClient(http, options, time, NullLogger<VoyageEmbeddingClient>.Instance), handler, time);
    }

    /// <summary>A valid reply: one vector per input text, the first value carrying the text's position in the request.</summary>
    private static HttpResponseMessage Ok(HttpRequestMessage _, string body)
    {
        using var request = JsonDocument.Parse(body);
        var count = request.RootElement.GetProperty("input").GetArrayLength();
        var data = Enumerable.Range(0, count).Reverse() // out of order on purpose: the client sorts by index
            .Select(i => new { @object = "embedding", embedding = VectorStartingWith(i), index = i });
        return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { @object = "list", data, model = "voyage-4", usage = new { total_tokens = count } }));
    }

    private static float[] VectorStartingWith(int first, int dimensions = Film.EmbeddingDimensions)
    {
        var vector = new float[dimensions];
        vector[0] = first;
        return vector;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage TooManyRequests(TimeSpan? retryAfter)
    {
        var response = Json(HttpStatusCode.TooManyRequests, """{"detail":"rate limited"}""");
        if (retryAfter is { } delay)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(delay);
        }

        return response;
    }

    [Fact]
    public async Task Embed_SendsTheDocumentedRequest()
    {
        var (client, handler, _) = Create(Ok);

        var result = await client.EmbedAsync(["Genres: drama film", "Genres: heist film"], EmbeddingInputType.Document, Ct);

        result.IsSuccess.ShouldBeTrue();
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.ShouldBe(new Uri("https://api.voyageai.com/v1/embeddings"));
        request.Authorization.ShouldBe("Bearer pa-test-key");
        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetProperty("model").GetString().ShouldBe("voyage-4");
        body.RootElement.GetProperty("input_type").GetString().ShouldBe("document");
        body.RootElement.GetProperty("output_dimension").GetInt32().ShouldBe(1024);
        body.RootElement.GetProperty("input").EnumerateArray().Select(e => e.GetString()).ShouldBe(["Genres: drama film", "Genres: heist film"]);
    }

    [Fact]
    public async Task Embed_QueryText_UsesTheQueryInputType()
    {
        var (client, handler, _) = Create(Ok);

        await client.EmbedAsync(["something cosy"], EmbeddingInputType.Query, Ct);

        using var body = JsonDocument.Parse(handler.Requests.Single().Body);
        body.RootElement.GetProperty("input_type").GetString().ShouldBe("query");
    }

    [Fact]
    public async Task Embed_ReturnsOneVectorPerText_InInputOrder()
    {
        var (client, _, _) = Create(Ok);

        var result = await client.EmbedAsync(["a", "b", "c"], EmbeddingInputType.Document, Ct);

        result.Value.Select(v => v[0]).ShouldBe([0f, 1f, 2f], "the reply came in reverse; vectors follow the input order");
        result.Value.ShouldAllBe(v => v.Length == Film.EmbeddingDimensions);
    }

    [Fact]
    public async Task Embed_MoreThanOneRequestsWorth_SplitsIntoBatchesOf1000()
    {
        var (client, handler, _) = Create(Ok, Ok, Ok);
        var texts = Enumerable.Range(0, 2500).Select(i => $"text {i}").ToList();

        var result = await client.EmbedAsync(texts, EmbeddingInputType.Document, Ct);

        result.Value.Count.ShouldBe(2500);
        handler.Requests.Select(r => JsonDocument.Parse(r.Body).RootElement.GetProperty("input").GetArrayLength()).ShouldBe([1000, 1000, 500]);
        result.Value[1000][0].ShouldBe(0f, "the second request's first vector");
    }

    [Fact]
    public async Task Embed_NoTexts_SendsNothing()
    {
        var (client, handler, _) = Create();

        var result = await client.EmbedAsync([], EmbeddingInputType.Document, Ct);

        result.Value.ShouldBeEmpty();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RateLimited_WaitsAsLongAsRetryAfterSays_ThenRetries()
    {
        var (client, handler, time) = Create((_, _) => TooManyRequests(TimeSpan.FromSeconds(7)), Ok);

        var result = await client.EmbedAsync(["a"], EmbeddingInputType.Document, Ct);

        result.IsSuccess.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(2);
        time.Delays.ShouldBe([TimeSpan.FromSeconds(7)]);
    }

    [Fact]
    public async Task RateLimitedWithoutRetryAfter_BacksOffExponentially()
    {
        var (client, _, time) = Create((_, _) => TooManyRequests(null), (_, _) => TooManyRequests(null), (_, _) => Json(HttpStatusCode.ServiceUnavailable, "{}"), Ok);

        var result = await client.EmbedAsync(["a"], EmbeddingInputType.Document, Ct);

        result.IsSuccess.ShouldBeTrue();
        time.Delays.ShouldBe([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)]);
    }

    [Fact]
    public async Task RateLimitedEveryTime_GivesUpAsUnavailable()
    {
        var replies = Enumerable.Repeat<Func<HttpRequestMessage, string, HttpResponseMessage>>((_, _) => TooManyRequests(TimeSpan.FromMinutes(10)), 10).ToArray();
        var (client, handler, time) = Create(replies);

        var result = await client.EmbedAsync(["a"], EmbeddingInputType.Document, Ct);

        result.Error.ShouldBe(AiErrors.Unavailable);
        handler.Requests.Count.ShouldBe(VoyageEmbeddingClient.MaxAttempts);
        time.Delays.ShouldAllBe(d => d == VoyageEmbeddingClient.MaxDelay, "a huge Retry-After is capped");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ClientError_IsUnavailable_WithoutRetrying(HttpStatusCode status)
    {
        var (client, handler, _) = Create((_, _) => Json(status, """{"detail":"no"}"""), Ok);

        var result = await client.EmbedAsync(["a"], EmbeddingInputType.Document, Ct);

        result.Error.ShouldBe(AiErrors.Unavailable);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task WrongDimensions_IsInvalidOutput()
    {
        var reply = JsonSerializer.Serialize(new { data = new[] { new { embedding = VectorStartingWith(0, 512), index = 0 } } });
        var (client, _, _) = Create((_, _) => Json(HttpStatusCode.OK, reply));

        var result = await client.EmbedAsync(["a"], EmbeddingInputType.Document, Ct);

        result.Error!.Code.ShouldBe(ErrorCodes.AiInvalidOutput);
    }

    [Fact]
    public async Task WrongNumberOfVectors_IsInvalidOutput()
    {
        var reply = JsonSerializer.Serialize(new { data = new[] { new { embedding = VectorStartingWith(0), index = 0 } } });
        var (client, _, _) = Create((_, _) => Json(HttpStatusCode.OK, reply));

        var result = await client.EmbedAsync(["a", "b"], EmbeddingInputType.Document, Ct);

        result.Error!.Code.ShouldBe(ErrorCodes.AiInvalidOutput);
    }

    [Fact]
    public async Task DuplicateIndexes_AreInvalidOutput()
    {
        var reply = JsonSerializer.Serialize(new { data = new[] { new { embedding = VectorStartingWith(0), index = 0 }, new { embedding = VectorStartingWith(1), index = 0 } } });
        var (client, _, _) = Create((_, _) => Json(HttpStatusCode.OK, reply));

        var result = await client.EmbedAsync(["a", "b"], EmbeddingInputType.Document, Ct);

        result.Error!.Code.ShouldBe(ErrorCodes.AiInvalidOutput);
    }

    [Fact]
    public async Task MalformedBody_IsUnavailable()
    {
        var (client, _, _) = Create((_, _) => Json(HttpStatusCode.OK, "<html>gateway</html>"));

        var result = await client.EmbedAsync(["a"], EmbeddingInputType.Document, Ct);

        result.Error.ShouldBe(AiErrors.Unavailable);
    }

    [Fact]
    public void DelayFor_HonoursADateRetryAfter()
    {
        var now = new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

        VoyageEmbeddingClient.DelayFor(new RetryConditionHeaderValue(now.AddSeconds(30)), 1, now).ShouldBe(TimeSpan.FromSeconds(30));
        VoyageEmbeddingClient.DelayFor(new RetryConditionHeaderValue(now.AddSeconds(-5)), 1, now).ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Startup_VoyageWithoutAKey_FailsWithAClearMessage()
    {
        using var services = Services(new() { ["Ai:EmbeddingProvider"] = "Voyage" });

        var error = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<AiOptions>>().Value);

        error.Message.ShouldContain("Ai:EmbeddingApiKey is required when Ai:EmbeddingProvider is 'Voyage'");
    }

    [Fact]
    public void Startup_VoyageWithAKey_UsesTheVoyageClient()
    {
        using var services = Services(new() { ["Ai:EmbeddingProvider"] = "Voyage", ["Ai:EmbeddingApiKey"] = "pa-test-key" });

        var client = services.GetRequiredService<IEmbeddingClient>();

        client.ShouldBeOfType<VoyageEmbeddingClient>().ModelName.ShouldBe("voyage-4");
    }

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System).AddInfrastructure(configuration).BuildServiceProvider();
    }

    private sealed record SentRequest(HttpMethod Method, Uri? Uri, string? Authorization, string Body);

    /// <summary>Answers each request with the next reply in turn and records what was sent.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, string, HttpResponseMessage>[] replies) : HttpMessageHandler
    {
        public List<SentRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new SentRequest(request.Method, request.RequestUri, request.Headers.Authorization?.ToString(), body));
            return replies[Requests.Count - 1](request, body);
        }
    }

    /// <summary>Records every delay the client asks for and lets it pass at once, so back-off tests don't sleep.</summary>
    private sealed class RecordingTime : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            return System.CreateTimer(callback, state, TimeSpan.Zero, period);
        }
    }
}
