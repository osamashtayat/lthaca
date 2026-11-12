using Ithaca.Application.Common;
using Ithaca.Application.Decks;

namespace Ithaca.Application.Rooms;

/// <summary>
/// Group rooms and votes (api-contract.md §6, ADR-0012, ADR-0015). Contract stub until BE-10 (core), BE-11 (swiping) and
/// BE-12 (result). Codes like <c>POP-428</c> are case-insensitive.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
public sealed class RoomService
{
    public Task<Result<RoomDto>> CreateAsync(CreateRoomRequest request, CancellationToken ct) => ContractStub.Of<RoomDto>();

    public Task<Result<RoomPreviewDto>> GetPreviewAsync(string code, CancellationToken ct) => ContractStub.Of<RoomPreviewDto>();

    public Task<Result<RoomDto>> JoinAsync(string code, JoinRoomRequest request, CancellationToken ct) => ContractStub.Of<RoomDto>();

    public Task<Result<RoomDto>> GetAsync(string code, CancellationToken ct) => ContractStub.Of<RoomDto>();

    public Task<Result<RoomMemberDto>> SetMoodAsync(string code, SetRoomMoodRequest request, CancellationToken ct) => ContractStub.Of<RoomMemberDto>();

    public Task<Result<RoomDto>> StartAsync(string code, CancellationToken ct) => ContractStub.Of<RoomDto>();

    /// <summary>The shared deck from <paramref name="fromPosition"/> on; each item's <c>swipe</c> is the caller's vote.</summary>
    public Task<Result<IReadOnlyList<DeckItemDto>>> GetDeckAsync(string code, int fromPosition, CancellationToken ct) =>
        ContractStub.Of<IReadOnlyList<DeckItemDto>>();

    public Task<Result> CastVoteAsync(string code, CastVoteRequest request, CancellationToken ct) => ContractStub.Command();

    public Task<Result<RoomProgressDto>> GetProgressAsync(string code, CancellationToken ct) => ContractStub.Of<RoomProgressDto>();

    /// <summary>Host ends swiping early: the result is the best compromise unless there is already a match.</summary>
    public Task<Result<RoomResultDto>> FinishAsync(string code, CancellationToken ct) => ContractStub.Of<RoomResultDto>();

    public Task<Result<RoomResultDto>> GetResultAsync(string code, CancellationToken ct) => ContractStub.Of<RoomResultDto>();

    public Task<Result> LeaveAsync(string code, CancellationToken ct) => ContractStub.Command();

    public Task<Result> CloseAsync(string code, CancellationToken ct) => ContractStub.Command();
}
