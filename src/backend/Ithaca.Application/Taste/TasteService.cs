using Ithaca.Application.Common;

namespace Ithaca.Application.Taste;

/// <summary>
/// The caller's taste: readiness for By taste and the taste receipt (api-contract.md §12). Contract stub until BE-14
/// builds the <c>TasteProfile</c>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
public sealed class TasteService
{
    public Task<Result<TasteDto>> GetAsync(CancellationToken ct) => ContractStub.Of<TasteDto>();
}
