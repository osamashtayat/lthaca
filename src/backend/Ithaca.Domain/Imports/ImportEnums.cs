[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716", Scope = "namespace", Target = "~N:Ithaca.Domain.Imports", Justification = "C#-only codebase; the feature folder is Imports (backlog BE-15a).")]

namespace Ithaca.Domain.Imports;

/// <summary>An <c>ImportJob</c>'s lifecycle (api-contract.md §13). The client polls while queued or running.</summary>
public enum ImportStatus
{
    Queued,
    Running,
    Completed,
    Failed,
}

/// <summary>Why a film from the export wasn't imported: no catalog or TMDB match, or TMDB kept failing (§13 job rule 2).</summary>
public enum ImportNotFoundReason
{
    NoMatch,
    LookupFailed,
}
