using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Ai;

/// <summary>The AI errors, defined once.</summary>
public static class AiErrors
{
    public static readonly Error Unavailable = new(ErrorCodes.AiUnavailable, "The AI provider is unavailable or rate limited.");

    public static readonly Error Refused = new(ErrorCodes.AiRefused, "The AI provider declined this request.");

    public static Error InvalidOutput(PromptId prompt, string reason) =>
        new(ErrorCodes.AiInvalidOutput, $"Prompt {prompt} returned {reason}.");

    public static Error InvalidEmbedding(string reason) =>
        new(ErrorCodes.AiInvalidOutput, $"The embedding provider returned {reason}.");
}
