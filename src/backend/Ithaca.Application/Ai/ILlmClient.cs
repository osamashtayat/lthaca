using Ithaca.Application.Common;

namespace Ithaca.Application.Ai;

/// <summary>
/// The only way to call a language model. Provider-neutral: Anthropic or Fake is chosen by <see cref="AiOptions.LlmProvider"/>.
/// The reply is JSON that matches the prompt's schema file; parse it with <see cref="StructuredOutput.Parse{T}"/>.
/// Expected failures (provider down, rate limited, refusal) come back as <see cref="Error"/>s, never as exceptions.
/// </summary>
public interface ILlmClient
{
    Task<Result<LlmResponse>> CompleteAsync(LlmRequest request, CancellationToken ct);
}

/// <param name="Prompt">The rendered prompt (system text + its output schema) from the versioned files.</param>
/// <param name="Input">The user-turn content, e.g. the mood text or the candidate list as JSON. Never pasted into the prompt.</param>
/// <param name="MaxOutputTokens">Upper bound for the reply.</param>
public sealed record LlmRequest(RenderedPrompt Prompt, string Input, int MaxOutputTokens = 2048);

/// <param name="Json">The model's reply: one JSON object matching the prompt's schema.</param>
/// <param name="PromptId">Which prompt (name + version) produced it, for logs and evals.</param>
/// <param name="Model">The model that actually answered (may be a fallback model).</param>
public sealed record LlmResponse(string Json, PromptId PromptId, string Model);
