namespace Ithaca.Tests.Harness;

/// <summary>Locates repo-level files (contracts/, prompts/) from the test output folder.</summary>
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string File(params string[] parts) => Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(dir.FullName, "Ithaca.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Ithaca.slnx not found above " + AppContext.BaseDirectory);
    }
}
