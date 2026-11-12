using Ithaca.Application.Common;

namespace Ithaca.Application.Decks;

/// <summary>Solo decks and swipes, incl. by mood (api-contract.md §5). Contract stub until BE-08 / BE-09.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
public sealed class DeckService
{
    public Task<Result<DeckDto>> CreateAsync(CreateDeckRequest request, CancellationToken ct) => ContractStub.Of<DeckDto>();

    public Task<Result<DeckDto>> GetAsync(Guid deckId, CancellationToken ct) => ContractStub.Of<DeckDto>();

    /// <summary>The next batch of cards; an empty list means nothing is left.</summary>
    public Task<Result<IReadOnlyList<DeckItemDto>>> AddItemsAsync(Guid deckId, CancellationToken ct) => ContractStub.Of<IReadOnlyList<DeckItemDto>>();

    public Task<Result> SwipeAsync(Guid deckId, SwipeRequest request, CancellationToken ct) => ContractStub.Command();

    public Task<Result<DeckTopDto>> GetTopAsync(Guid deckId, CancellationToken ct) => ContractStub.Of<DeckTopDto>();
}
