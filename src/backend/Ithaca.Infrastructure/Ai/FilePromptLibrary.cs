using System.Collections.Concurrent;
using Ithaca.Application.Ai;
using Microsoft.Extensions.Options;

namespace Ithaca.Infrastructure.Ai;

/// <summary>
/// Reads <c>{PromptsPath}/{name}.v{version}.md</c> and its <c>.schema.json</c> once, then fills <c>{{variable}}</c> placeholders.
/// A prompt without a schema file is an error: every structured output has a schema.
/// </summary>
internal sealed class FilePromptLibrary(IOptions<AiOptions> options) : IPromptLibrary
{
    private readonly ConcurrentDictionary<PromptId, (string Template, string Schema)> _cache = new();

    public async Task<RenderedPrompt> RenderAsync(PromptId id, IReadOnlyDictionary<string, string> variables, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(variables);
        if (!_cache.TryGetValue(id, out var files))
        {
            var folder = ResolveFolder(options.Value.PromptsPath);
            files = (await ReadAsync(Path.Combine(folder, id.FileName), id, ct), await ReadAsync(Path.Combine(folder, id.SchemaFileName), id, ct));
            _cache.TryAdd(id, files);
        }

        var text = variables.Aggregate(files.Template, (current, v) => current.Replace("{{" + v.Key + "}}", v.Value, StringComparison.Ordinal));
        return new RenderedPrompt(id, text, files.Schema);
    }

    private static async Task<string> ReadAsync(string path, PromptId id, CancellationToken ct) =>
        File.Exists(path)
            ? await File.ReadAllTextAsync(path, ct)
            : throw new FileNotFoundException($"Prompt {id}: {Path.GetFileName(path)} not found in {Path.GetDirectoryName(path)}.", path);

    internal static string ResolveFolder(string configured) =>
        Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);
}
