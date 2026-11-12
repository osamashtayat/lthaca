using System.Text.Json;
using Ithaca.Application.Ai;

namespace Ithaca.Evals;

/// <summary>Runs cases: render the versioned prompt → get a reply (recorded or live) → run every check.</summary>
internal sealed class EvalRunner(IPromptLibrary prompts, ILlmClient? liveClient)
{
    public async Task<bool> RunAsync(IReadOnlyList<EvalCase> cases, CancellationToken ct)
    {
        var failures = 0;
        foreach (var evalCase in cases)
        {
            var reasons = await RunCaseAsync(evalCase, ct);
            failures += reasons.Count > 0 ? 1 : 0;
            Console.WriteLine($"{(reasons.Count == 0 ? "PASS" : "FAIL")}  {evalCase.Id}  ({evalCase.Description})");
            foreach (var reason in reasons)
            {
                Console.WriteLine($"      - {reason}");
            }
        }

        Console.WriteLine($"{cases.Count - failures}/{cases.Count} passed ({(liveClient is null ? "recorded replies" : "live client")}).");
        return failures == 0;
    }

    private async Task<List<string>> RunCaseAsync(EvalCase evalCase, CancellationToken ct)
    {
        var id = ParsePromptId(evalCase.Prompt);
        var prompt = await prompts.RenderAsync(id, evalCase.Variables ?? [], ct);
        var input = evalCase.Candidates is { } candidates ? JsonSerializer.Serialize(candidates) : evalCase.Input;
        string reply;
        if (liveClient is null)
        {
            reply = evalCase.RecordedResponse.ToJsonString();
        }
        else
        {
            var result = await liveClient.CompleteAsync(new LlmRequest(prompt, input), ct);
            if (!result.IsSuccess)
            {
                return [$"LLM call failed: {result.Error.Code} ({result.Error.Message})"];
            }

            reply = result.Value.Json;
            Console.WriteLine($"      reply ({result.Value.Model}): {reply}");
        }

        return [.. evalCase.Checks.Select(c => EvalChecks.Run(c, evalCase, reply)).OfType<string>()];
    }

    /// <summary><c>"mood-parse.v1"</c> → <c>PromptId("mood-parse", 1)</c>.</summary>
    internal static PromptId ParsePromptId(string value)
    {
        var at = value.LastIndexOf(".v", StringComparison.Ordinal);
        return at > 0 && int.TryParse(value.AsSpan(at + 2), out var version)
            ? new PromptId(value[..at], version)
            : throw new FormatException($"Prompt '{value}' must look like 'name.vN'.");
    }
}
