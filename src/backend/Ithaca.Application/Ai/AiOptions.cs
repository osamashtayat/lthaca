using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Common;

namespace Ithaca.Application.Ai;

public enum LlmProvider
{
    Fake,
    Anthropic,
    OpenAI,
}

public enum EmbeddingProvider
{
    Fake,
    OpenAI,
}

/// <summary><c>Ai</c> section. Providers are swappable by config only; no caller knows which one runs (docs/ai.md).</summary>
public sealed class AiOptions : IConfigSection
{
    public static string SectionName => "Ai";

    public LlmProvider LlmProvider { get; init; } = LlmProvider.Fake;

    public EmbeddingProvider EmbeddingProvider { get; init; } = EmbeddingProvider.Fake;

    /// <summary>Model id for the chosen LLM provider (e.g. an Anthropic or OpenAI model name). Unused by Fake.</summary>
    public string? LlmModel { get; init; }

    /// <summary>Secret: env var / user-secrets only (<c>Ai__ApiKey</c>). Never in appsettings.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Folder holding the versioned prompt files; relative paths resolve against the app's base directory.</summary>
    [Required]
    public string PromptsPath { get; init; } = "prompts";
}
