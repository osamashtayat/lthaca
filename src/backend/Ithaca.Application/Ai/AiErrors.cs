using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Ai;

/// <summary>The AI errors, defined once.</summary>
public static class AiErrors
{
    public static readonly Error Unavailable = new(ErrorCodes.AiUnavailable, "The AI provider is unavailable or rate limited.");

    /// <summary>Today's share of the AI budget is spent (<see cref="AiBudget"/>); callers treat it like any AI failure.</summary>
    public static readonly Error BudgetReached = new(ErrorCodes.AiUnavailable, "The daily AI budget is used up.");

    /// <summary>No answer within the caller's timeout (<see cref="AiBudget.CompleteAsync"/>).</summary>
    public static readonly Error TimedOut = new(ErrorCodes.AiUnavailable, "The AI provider didn't answer in time.");

    public static readonly Error Refused = new(ErrorCodes.AiRefused, "The AI provider declined this request.");

    public static Error InvalidOutput(PromptId prompt, string reason) =>
        new(ErrorCodes.AiInvalidOutput, $"Prompt {prompt} returned {reason}.");

    public static Error InvalidEmbedding(string reason) =>
        new(ErrorCodes.AiInvalidOutput, $"The embedding provider returned {reason}.");
}
