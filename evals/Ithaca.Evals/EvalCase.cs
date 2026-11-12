using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ithaca.Evals;

/// <summary>One fixed test case for an AI feature. Shape documented in docs/ai.md ("Eval cases").</summary>
internal sealed record EvalCase(
    string Id,
    string Description,
    string Prompt,
    Dictionary<string, string>? Variables,
    string Input,
    int[]? Candidates,
    JsonObject RecordedResponse,
    EvalCheck[] Checks)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static EvalCase Load(string path) =>
        JsonSerializer.Deserialize<EvalCase>(File.ReadAllText(path), Json)
            ?? throw new InvalidDataException($"{path} is empty.");
}

/// <param name="Type">One of <see cref="EvalChecks.Known"/>.</param>
/// <param name="Value">The check's argument, e.g. a genre name.</param>
internal sealed record EvalCheck(string Type, JsonNode? Value);
