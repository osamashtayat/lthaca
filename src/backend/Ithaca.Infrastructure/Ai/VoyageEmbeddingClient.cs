using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Ithaca.Domain.Films;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ithaca.Infrastructure.Ai;

/// <summary>
/// Voyage AI embeddings (<c>Ai:EmbeddingProvider = Voyage</c>, ADR-0013) as a typed <see cref="HttpClient"/>: there is no
/// official .NET SDK. <c>POST /v1/embeddings</c> with <see cref="AiOptions.EmbeddingModel"/>, <c>output_dimension</c>
/// <see cref="Film.EmbeddingDimensions"/>, and the input type. Up to <see cref="MaxTextsPerRequest"/> texts per request.
/// 429 and 5xx are retried up to <see cref="MaxAttempts"/> times, waiting as long as <c>Retry-After</c> says, or
/// 1, 2, 4… seconds without it (capped at <see cref="MaxDelay"/>). Every other failure is <see cref="AiErrors.Unavailable"/>;
/// a reply with the wrong number or size of vectors is <c>ai.invalid_output</c>. Never logs the texts.
/// </summary>
internal sealed partial class VoyageEmbeddingClient(
    HttpClient http, IOptions<AiOptions> options, TimeProvider time, ILogger<VoyageEmbeddingClient> logger) : IEmbeddingClient
{
    public const int MaxTextsPerRequest = 1000;
    public const int MaxAttempts = 6;
    public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(120);

    public string ModelName => options.Value.EmbeddingModel;

    public async Task<Result<IReadOnlyList<float[]>>> EmbedAsync(IReadOnlyList<string> texts, EmbeddingInputType inputType, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var vectors = new List<float[]>(texts.Count);
        foreach (var chunk in texts.Chunk(MaxTextsPerRequest))
        {
            var result = await EmbedChunkAsync(chunk, inputType, ct);
            if (!result.IsSuccess)
            {
                return result.Error;
            }

            vectors.AddRange(result.Value);
        }

        return vectors;
    }

    private async Task<Result<IReadOnlyList<float[]>>> EmbedChunkAsync(string[] texts, EmbeddingInputType inputType, CancellationToken ct)
    {
        var body = new EmbeddingRequest(
            texts, ModelName, inputType == EmbeddingInputType.Query ? "query" : "document", Film.EmbeddingDimensions, Truncation: true);

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "embeddings") { Content = JsonContent.Create(body) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.EmbeddingApiKey);
            try
            {
                using var response = await http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return Vectors(await response.Content.ReadFromJsonAsync<EmbeddingResponse>(ct), texts.Length);
                }

                var status = (int)response.StatusCode;
                if (!IsRetryable(response.StatusCode) || attempt == MaxAttempts)
                {
                    LogFailure(logger, status, attempt, texts.Length);
                    return AiErrors.Unavailable;
                }

                var delay = DelayFor(response.Headers.RetryAfter, attempt, time.GetUtcNow());
                LogRetry(logger, status, delay.TotalSeconds, attempt);
                await Task.Delay(delay, time, ct);
            }
            catch (Exception e) when (e is HttpRequestException or JsonException || (e is TaskCanceledException && !ct.IsCancellationRequested))
            {
                // Network failure, HttpClient timeout, or a body that isn't the JSON we expect: never an exception to the caller.
                LogNetworkFailure(logger, e, texts.Length);
                return AiErrors.Unavailable;
            }
        }
    }

    /// <summary><c>Retry-After</c> (seconds or a date) when given, otherwise 1, 2, 4… seconds; never more than <see cref="MaxDelay"/>.</summary>
    internal static TimeSpan DelayFor(RetryConditionHeaderValue? retryAfter, int attempt, DateTimeOffset now)
    {
        var delay = retryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - now,
            _ => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
        };
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay > MaxDelay ? MaxDelay : delay;
    }

    private static bool IsRetryable(HttpStatusCode status) => status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    private static Result<IReadOnlyList<float[]>> Vectors(EmbeddingResponse? response, int expected)
    {
        var data = response?.Data;
        if (data is null || data.Count != expected)
        {
            return AiErrors.InvalidEmbedding($"{data?.Count ?? 0} vectors for {expected} texts");
        }

        var ordered = data.OrderBy(d => d.Index).ToList();
        if (ordered.Select(d => d.Index).Where((index, position) => index != position).Any())
        {
            return AiErrors.InvalidEmbedding("vectors with missing or duplicate indexes");
        }

        if (ordered.Any(d => d.Embedding is not { Length: Film.EmbeddingDimensions }))
        {
            return AiErrors.InvalidEmbedding($"a vector that isn't {Film.EmbeddingDimensions} dimensions");
        }

        return ordered.Select(d => d.Embedding!).ToList();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Voyage embeddings answered {Status} on attempt {Attempt} for {Count} texts; giving up")]
    private static partial void LogFailure(ILogger logger, int status, int attempt, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Voyage embeddings answered {Status}; retrying in {Seconds}s (attempt {Attempt})")]
    private static partial void LogRetry(ILogger logger, int status, double seconds, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Voyage embeddings failed for {Count} texts (network, timeout or unexpected body)")]
    private static partial void LogNetworkFailure(ILogger logger, Exception exception, int count);

    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input_type")] string InputType,
        [property: JsonPropertyName("output_dimension")] int OutputDimension,
        [property: JsonPropertyName("truncation")] bool Truncation);

    private sealed record EmbeddingResponse([property: JsonPropertyName("data")] List<EmbeddingData>? Data);

    private sealed record EmbeddingData(
        [property: JsonPropertyName("embedding")] float[]? Embedding,
        [property: JsonPropertyName("index")] int Index);
}
