namespace Ithaca.Application.Ai;

/// <summary>
/// Loads versioned prompt files (<c>prompts/{name}.v{version}.md</c>) and their output schemas
/// (<c>prompts/{name}.v{version}.schema.json</c>). Prompts are never string literals in code;
/// a new wording is a new version file, so evals can compare versions (docs/ai.md).
/// </summary>
public interface IPromptLibrary
{
    Task<RenderedPrompt> RenderAsync(PromptId id, IReadOnlyDictionary<string, string> variables, CancellationToken ct);
}

/// <summary>A prompt's identity, e.g. <c>new PromptId("mood-parse", 1)</c> → <c>mood-parse.v1.md</c>.</summary>
public sealed record PromptId(string Name, int Version)
{
    public string FileName => $"{Name}.v{Version}.md";

    public string SchemaFileName => $"{Name}.v{Version}.schema.json";

    public override string ToString() => $"{Name}.v{Version}";
}

/// <summary>
/// The prompt text with <c>{{variables}}</c> filled in, plus the JSON Schema the reply must match. The Anthropic client sends
/// the schema as a structured-output format, so the API enforces it; the Fake client ignores it.
/// </summary>
public sealed record RenderedPrompt(PromptId Id, string Text, string OutputSchemaJson);

/// <summary>The prompts that exist. Use these, not ad-hoc ids.</summary>
public static class Prompts
{
    public static readonly PromptId MoodParse = new("mood-parse", 1);
    public static readonly PromptId RankCandidates = new("rank-candidates", 1);
    public static readonly PromptId ExplainPick = new("explain-pick", 1);
}
