using System.Text.Json;
using System.Text.Json.Nodes;
using Ithaca.Application.Ai;
using Ithaca.Application.Common;

namespace Ithaca.Infrastructure.Ai;

/// <summary>
/// Offline LLM stand-in (<c>Ai:LlmProvider = Fake</c>). Returns fixed, schema-valid JSON per prompt so the plumbing
/// (prompt → client → <see cref="StructuredOutput"/>) runs without a network or a key. It has no intelligence:
/// <c>rank-candidates</c> echoes the candidate ids in the order given and <c>explain-pick</c> v2 gives each pick a fixed
/// reason, so neither can invent a film.
/// </summary>
internal sealed class FakeLlmClient : ILlmClient
{
    public Task<Result<LlmResponse>> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = request.Prompt.Id;
        var json = id.Name switch
        {
            "mood-parse" => """{"intent":"mood","includeGenres":[],"excludeGenres":[],"maxRuntimeMinutes":null,"minReleaseYear":null,"maxReleaseYear":null,"keywords":[]}""",
            "rank-candidates" => Echo("ranked", request.Input, "candidates"),
            "explain-pick" when id.Version >= 2 => Echo("reasons", request.Input, "picks"),
            "explain-pick" => """{"explanation":"A fake explanation."}""",
            _ => "{}",
        };
        return Task.FromResult<Result<LlmResponse>>(new LlmResponse(json, id, "fake"));
    }

    /// <summary>
    /// The ids in <paramref name="input"/>, in order, each with a fixed reason, under <paramref name="key"/>. The input is
    /// the v2 object (ids in <c>{list}[].tmdbId</c>) or, for v1 and the evals, a JSON array of ids.
    /// </summary>
    private static string Echo(string key, string input, string list)
    {
        int[] ids;
        try
        {
            ids = JsonNode.Parse(input) switch
            {
                JsonArray array => array.Select(n => n?.GetValue<int>() ?? 0).ToArray(),
                JsonObject obj when obj[list] is JsonArray items => items.Select(n => n?["tmdbId"]?.GetValue<int>() ?? 0).ToArray(),
                _ => [],
            };
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            ids = [];
        }

        return new JsonObject { [key] = new JsonArray([.. ids.Select(i => (JsonNode)new JsonObject { ["tmdbId"] = i, ["reason"] = "A fake reason." })]) }.ToJsonString();
    }
}
