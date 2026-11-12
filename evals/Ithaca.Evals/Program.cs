using Ithaca.Application.Ai;
using Ithaca.Evals;
using Ithaca.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Usage: dotnet run --project evals/Ithaca.Evals [-- --live] [--cases <folder>]
// Exit code 0 = all cases passed.
var live = args.Contains("--live");
var casesArg = Array.IndexOf(args, "--cases");
var repoRoot = FindRepoRoot();
var casesFolder = casesArg >= 0 && casesArg + 1 < args.Length ? args[casesArg + 1] : Path.Combine(repoRoot, "evals", "cases");

// Same registrations and config keys as the API, so `--live` uses exactly the provider the API would.
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:PromptsPath"] = Path.Combine(repoRoot, "prompts") })
    .AddEnvironmentVariables()
    .Build();
var services = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System).AddInfrastructure(configuration).BuildServiceProvider();

var cases = Directory.GetFiles(casesFolder, "*.json").Order(StringComparer.Ordinal).Select(EvalCase.Load).ToList();
var runner = new EvalRunner(services.GetRequiredService<IPromptLibrary>(), live ? services.GetRequiredService<ILlmClient>() : null);
return await runner.RunAsync(cases, CancellationToken.None) ? 0 : 1;

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Ithaca.slnx")))
        {
            return dir.FullName;
        }
    }

    throw new InvalidOperationException("Run from inside the Ithaca repository.");
}
