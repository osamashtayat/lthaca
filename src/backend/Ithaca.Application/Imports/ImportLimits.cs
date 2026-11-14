namespace Ithaca.Application.Imports;

/// <summary>
/// Import limits that must be compile-time constants (§13). The upload cap feeds <c>[RequestSizeLimit]</c>, so it can't
/// come from configuration; the other limits live in <c>ImportOptions</c> (BE-15).
/// </summary>
public static class ImportLimits
{
    /// <summary>25 MB. A larger request body is refused by the pipeline with 413 <c>request.too_large</c>.</summary>
    public const long MaxUploadBytes = 25L * 1024 * 1024;
}
