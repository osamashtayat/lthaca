using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Application.Ai;

public enum LlmProvider
{
    Fake,
    Anthropic,

    /// <summary>Allowed by the brief ("Anthropic or OpenAI, swappable"); client not built.</summary>
    OpenAI,
}

public enum EmbeddingProvider
{
    Fake,

    /// <summary>Voyage AI (ADR-0013). Client not built yet: needs a Voyage key.</summary>
    Voyage,
}

/// <summary><c>Ai</c> section. Providers and models are swappable by config only; no caller knows which one runs (docs/ai.md).</summary>
public sealed class AiOptions : IConfigSection
{
    public static string SectionName => "Ai";

    public LlmProvider LlmProvider { get; init; } = LlmProvider.Fake;

    public EmbeddingProvider EmbeddingProvider { get; init; } = EmbeddingProvider.Fake;

    /// <summary>Default model for every prompt.</summary>
    [Required]
    public string LlmModel { get; init; } = "claude-opus-5-5";

    /// <summary>Per-prompt model, keyed by prompt name (e.g. <c>"mood-parse": "claude-haiku-5-5"</c>). Cost lever.</summary>
    public Dictionary<string, string> ModelOverrides { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Optional effort (<c>low</c>, <c>medium</c>, <c>high</c>…); unset = the model's default.</summary>
    [AllowedValues(null, "low", "medium", "high", "xhigh", "max")]
    public string? Effort { get; init; }

    /// <summary>
    /// Anthropic server-side refusal fallback in its <c>"default"</c> form: a policy decline is re-served by a server-chosen
    /// model inside the same call. Harmless on models without fallback support (the request just runs as is).
    /// </summary>
    public bool UseRefusalFallback { get; init; } = true;

    /// <summary>Secret: env var / user-secrets only (<c>Ai__ApiKey</c>). Never in appsettings.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Secret for the embedding provider (<c>Ai__EmbeddingApiKey</c>).</summary>
    public string? EmbeddingApiKey { get; init; }

    /// <summary>Folder holding the versioned prompt files; relative paths resolve against the app's base directory.</summary>
    [Required]
    public string PromptsPath { get; init; } = "prompts";

    public string ModelFor(PromptId prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return ModelOverrides.TryGetValue(prompt.Name, out var model) && !string.IsNullOrWhiteSpace(model) ? model : LlmModel;
    }
}
