namespace Ithaca.Application.Ai;

/// <summary>
/// The only way to call a language model. Provider-neutral: Anthropic, OpenAI or Fake is chosen by
/// <see cref="AiOptions.LlmProvider"/>. Returns raw JSON text; callers parse it into a typed DTO with
/// <see cref="StructuredOutput.Parse{T}"/>, so every structured output has a C# type (docs/ai.md).
/// </summary>
public interface ILlmClient
{
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct);
}

/// <param name="Prompt">The rendered prompt (system text from the versioned file plus variables).</param>
/// <param name="Input">The user-turn content, e.g. the mood text or the candidate list as JSON.</param>
/// <param name="MaxOutputTokens">Upper bound for the reply.</param>
public sealed record LlmRequest(RenderedPrompt Prompt, string Input, int MaxOutputTokens = 1024);

/// <param name="Json">The model's reply, expected to be a single JSON object.</param>
/// <param name="PromptId">Which prompt (name + version) produced it, for logs and evals.</param>
public sealed record LlmResponse(string Json, PromptId PromptId);
