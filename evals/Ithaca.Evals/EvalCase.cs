using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ithaca.Evals;

/// <summary>One fixed test case for an AI feature. Shape documented in docs/ai.md ("Eval cases").</summary>
internal sealed record EvalCase(
    string Id,
    string Description,
    string? Kind,
    string Prompt,
    Dictionary<string, string>? Variables,
    string Input,
    int[]? Candidates,
    JsonObject RecordedResponse,
    EvalCheck[] Checks)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The case kinds (ADR-0022): a prompt's suite covers all three.</summary>
    public static readonly string[] Kinds = ["representative", "edge", "adversarial"];

    public static EvalCase Load(string path)
    {
        var evalCase = JsonSerializer.Deserialize<EvalCase>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException($"{path} is empty.");
        return evalCase.Kind is null || Kinds.Contains(evalCase.Kind, StringComparer.Ordinal)
            ? evalCase
            : throw new InvalidDataException($"{path}: kind must be one of {string.Join(", ", Kinds)}.");
    }
}

/// <param name="Type">One of <see cref="EvalChecks.Known"/>.</param>
/// <param name="Value">The check's argument, e.g. a genre name.</param>
internal sealed record EvalCheck(string Type, JsonNode? Value);
