using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ithaca.Evals;

/// <summary>
/// Assertions an eval case can make on a model reply. Each returns null when it passes, or the failure reason.
/// Add a check type here (and to docs/ai.md) when a new AI feature needs one.
/// </summary>
internal static class EvalChecks
{
    public static readonly string[] Known = ["validJson", "excludesGenre", "doesNotIncludeGenre", "idsFromCandidates", "excludesTmdbId"];

    public static string? Run(EvalCheck check, EvalCase evalCase, string replyJson)
    {
        JsonNode? reply;
        try
        {
            reply = JsonNode.Parse(replyJson);
        }
        catch (JsonException e)
        {
            return $"reply is not JSON: {e.Message}";
        }

        return check.Type switch
        {
            "validJson" => reply is JsonObject ? null : "reply is not a JSON object",
            "excludesGenre" => Strings(reply?["excludeGenres"]).Contains(Text(check), StringComparer.OrdinalIgnoreCase)
                ? null : $"excludeGenres lacks '{Text(check)}'",
            "doesNotIncludeGenre" => Strings(reply?["includeGenres"]).Contains(Text(check), StringComparer.OrdinalIgnoreCase)
                ? $"includeGenres contains '{Text(check)}'" : null,
            "idsFromCandidates" => RankedIds(reply).Except(evalCase.Candidates ?? []).ToArray() is { Length: > 0 } invented
                ? $"invented ids not in candidates: {string.Join(", ", invented)}" : null,
            "excludesTmdbId" => RankedIds(reply).Contains(check.Value!.GetValue<int>())
                ? $"returned hard-no film {check.Value}" : null,
            _ => $"unknown check type '{check.Type}' (known: {string.Join(", ", Known)})",
        };
    }

    private static string Text(EvalCheck check) => check.Value?.GetValue<string>() ?? "";

    private static IEnumerable<string> Strings(JsonNode? node) =>
        node is JsonArray array ? array.Select(n => n?.GetValue<string>() ?? "") : [];

    private static IEnumerable<int> RankedIds(JsonNode? reply) =>
        reply?["ranked"] is JsonArray ranked ? ranked.Select(r => r?["tmdbId"]?.GetValue<int>() ?? 0) : [];
}
