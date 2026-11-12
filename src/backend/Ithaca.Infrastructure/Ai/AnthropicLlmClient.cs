using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ithaca.Infrastructure.Ai;

/// <summary>
/// <see cref="ILlmClient"/> on the official Anthropic SDK (<c>Ai:LlmProvider = Anthropic</c>).
/// The prompt file is the system prompt, the caller's input is the single user message, and the prompt's schema file is sent
/// as a structured-output format, so the API returns JSON that matches it. With <see cref="AiOptions.UseRefusalFallback"/> a refusal
/// is re-served server-side by a fallback model; a refusal that still stands becomes <see cref="AiErrors.Refused"/>.
/// Transport failures become <see cref="AiErrors.Unavailable"/>; the SDK already retries 408/409/429/5xx twice.
/// </summary>
internal sealed partial class AnthropicLlmClient(AnthropicClient client, IOptions<AiOptions> options, ILogger<AnthropicLlmClient> logger) : ILlmClient
{
    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    public async Task<Result<LlmResponse>> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        var model = settings.ModelFor(request.Prompt.Id);
        var promptName = request.Prompt.Id.ToString();
        var parameters = BuildParameters(request, model, settings);

        BetaMessage message;
        try
        {
            message = await client.Beta.Messages.Create(parameters, ct);
        }
        catch (Exception e) when (e is AnthropicException or HttpRequestException || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // API error after the SDK's retries, network failure, or timeout.
            LogApiFailure(logger, e, promptName, model);
            return AiErrors.Unavailable;
        }

        var stopReason = message.StopReason?.ToString();
        if (stopReason == "refusal")
        {
            LogRefusal(logger, promptName, model);
        }

        var text = string.Concat(message.Content.Select(b => b.TryPickText(out var block) ? block.Text : ""));
        return MapReply(request.Prompt.Id, stopReason, text, message.Model.ToString());
    }

    /// <summary>Stop reason + text → result. Separate so every outcome is unit-tested without calling the API.</summary>
    internal static Result<LlmResponse> MapReply(PromptId prompt, string? stopReason, string text, string model) => stopReason switch
    {
        "refusal" => AiErrors.Refused,
        "max_tokens" => AiErrors.InvalidOutput(prompt, "a truncated reply (max_tokens)"),
        _ when string.IsNullOrWhiteSpace(text) => AiErrors.InvalidOutput(prompt, "no text"),
        _ => new LlmResponse(text, prompt, model),
    };

    internal static MessageCreateParams BuildParameters(LlmRequest request, string model, AiOptions settings)
    {
        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.Prompt.OutputSchemaJson)
            ?? throw new InvalidOperationException($"Prompt {request.Prompt.Id} has an empty schema file.");

        return new MessageCreateParams
        {
            Model = model,
            MaxTokens = request.MaxOutputTokens,
            System = request.Prompt.Text,
            Messages = [new BetaUserMessageParam { Content = request.Input }],
            OutputConfig = new BetaOutputConfig
            {
                Format = new BetaJsonOutputFormat { Schema = schema },
                Effort = settings.Effort is { } effort ? EffortFrom(effort) : null,
            },
            // "default": the server picks the fallback by refusal category; on a model without fallback support the
            // request simply runs on the requested model, so this is safe for every model.
            Betas = settings.UseRefusalFallback ? [FallbackBeta] : null,
            Fallbacks = settings.UseRefusalFallback ? (BetaFallbacksParam)new Default() : null,
        };
    }

    private static Effort EffortFrom(string value) => value switch
    {
        "low" => Effort.Low,
        "medium" => Effort.Medium,
        "high" => Effort.High,
        "xhigh" => Effort.Xhigh,
        "max" => Effort.Max,
        _ => throw new InvalidOperationException($"Unknown Ai:Effort '{value}'."),
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "LLM call for {Prompt} on {Model} failed")]
    private static partial void LogApiFailure(ILogger logger, Exception exception, string prompt, string model);

    [LoggerMessage(Level = LogLevel.Information, Message = "LLM refused {Prompt} on {Model}")]
    private static partial void LogRefusal(ILogger logger, string prompt, string model);
}
