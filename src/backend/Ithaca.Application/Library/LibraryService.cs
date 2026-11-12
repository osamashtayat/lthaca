using Ithaca.Application.Common;

namespace Ithaca.Application.Library;

/// <summary>My Cinema: one <c>UserFilm</c> per user and film (api-contract.md §7, ADR-0019). Contract stub until BE-05.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
public sealed class LibraryService
{
    public Task<Result<PagedResult<UserFilmDto>>> ListAsync(LibraryListRequest request, CancellationToken ct) => ContractStub.Of<PagedResult<UserFilmDto>>();

    public Task<Result<LibrarySummaryDto>> GetSummaryAsync(CancellationToken ct) => ContractStub.Of<LibrarySummaryDto>();

    public Task<Result<UserFilmDto>> GetAsync(int tmdbId, CancellationToken ct) => ContractStub.Of<UserFilmDto>();

    public Task<Result<UserFilmDto>> UpdateAsync(int tmdbId, UpdateUserFilmRequest request, CancellationToken ct) => ContractStub.Of<UserFilmDto>();

    /// <summary>Idempotent; other flags untouched (the deck card's watchlist button).</summary>
    public Task<Result> AddToWatchlistAsync(int tmdbId, CancellationToken ct) => ContractStub.Command();

    /// <summary>Idempotent; other flags untouched.</summary>
    public Task<Result> RemoveFromWatchlistAsync(int tmdbId, CancellationToken ct) => ContractStub.Command();
}
