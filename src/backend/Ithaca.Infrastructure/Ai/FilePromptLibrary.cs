using System.Collections.Concurrent;
using Ithaca.Application.Ai;
using Microsoft.Extensions.Options;

namespace Ithaca.Infrastructure.Ai;

/// <summary>Reads <c>{PromptsPath}/{name}.v{version}.md</c> once, then fills <c>{{variable}}</c> placeholders.</summary>
internal sealed class FilePromptLibrary(IOptions<AiOptions> options) : IPromptLibrary
{
    private readonly ConcurrentDictionary<PromptId, string> _cache = new();

    public async Task<RenderedPrompt> RenderAsync(PromptId id, IReadOnlyDictionary<string, string> variables, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(variables);
        if (!_cache.TryGetValue(id, out var template))
        {
            var path = Path.Combine(ResolveFolder(options.Value.PromptsPath), id.FileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Prompt {id} not found. Expected {path}.", path);
            }

            template = await File.ReadAllTextAsync(path, ct);
            _cache.TryAdd(id, template);
        }

        var text = variables.Aggregate(template, (current, v) => current.Replace("{{" + v.Key + "}}", v.Value, StringComparison.Ordinal));
        return new RenderedPrompt(id, text);
    }

    internal static string ResolveFolder(string configured) =>
        Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);
}
