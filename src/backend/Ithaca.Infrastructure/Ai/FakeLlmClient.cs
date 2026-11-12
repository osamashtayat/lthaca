using System.Text.Json;
using Ithaca.Application.Ai;
using Ithaca.Application.Common;

namespace Ithaca.Infrastructure.Ai;

/// <summary>
/// Offline LLM stand-in (<c>Ai:LlmProvider = Fake</c>). Returns fixed, schema-valid JSON per prompt so the plumbing
/// (prompt → client → <see cref="StructuredOutput"/>) runs without a network or a key. It has no intelligence:
/// <c>rank-candidates</c> echoes the candidate ids in the order given, so it can never invent a film.
/// </summary>
internal sealed class FakeLlmClient : ILlmClient
{
    public Task<Result<LlmResponse>> CompleteAsync(LlmRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = request.Prompt.Id;
        var json = id.Name switch
        {
            "mood-parse" => """{"includeGenres":[],"excludeGenres":[],"maxRuntimeMinutes":null,"minReleaseYear":null,"maxReleaseYear":null,"keywords":[]}""",
            "rank-candidates" => EchoCandidates(request.Input),
            "explain-pick" => """{"explanation":"A fake explanation."}""",
            _ => "{}",
        };
        return Task.FromResult<Result<LlmResponse>>(new LlmResponse(json, id, "fake"));
    }

    /// <summary>Input is a JSON array of candidate TMDB ids.</summary>
    private static string EchoCandidates(string input)
    {
        int[] ids;
        try
        {
            ids = JsonSerializer.Deserialize<int[]>(input) ?? [];
        }
        catch (JsonException)
        {
            ids = [];
        }

        return JsonSerializer.Serialize(new { ranked = ids.Select(i => new { tmdbId = i, reason = "fake" }) });
    }
}
