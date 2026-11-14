using Ithaca.Application.Common;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716", Scope = "namespace", Target = "~N:Ithaca.Application.Imports", Justification = "C#-only codebase; the feature folder is Imports (backlog BE-15a).")]

namespace Ithaca.Application.Imports;

/// <summary>
/// Letterboxd imports for the signed-in user (api-contract.md §13, ADR-0024): upload, list, progress, not-found list.
/// Contract stub until BE-15 (parsing, the background job and the other limits).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
public sealed class ImportService
{
    /// <summary>Checks the upload and queues an <c>ImportJob</c>; the file itself is never stored.</summary>
    public Task<Result<ImportJobDto>> CreateAsync(ImportUpload upload, CancellationToken ct) => ContractStub.Of<ImportJobDto>();

    /// <summary>The caller's imports, newest first.</summary>
    public Task<Result<PagedResult<ImportJobDto>>> ListAsync(PageRequest page, CancellationToken ct) =>
        ContractStub.Of<PagedResult<ImportJobDto>>();

    public Task<Result<ImportJobDto>> GetAsync(Guid importId, CancellationToken ct) => ContractStub.Of<ImportJobDto>();

    /// <summary>Films the import couldn't match, by title then year; an empty page once a newer import replaced the list.</summary>
    public Task<Result<PagedResult<ImportNotFoundDto>>> ListNotFoundAsync(Guid importId, PageRequest page, CancellationToken ct) =>
        ContractStub.Of<PagedResult<ImportNotFoundDto>>();
}
