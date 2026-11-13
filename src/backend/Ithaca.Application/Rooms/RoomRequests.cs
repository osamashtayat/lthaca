using System.ComponentModel.DataAnnotations;
using Ithaca.Application.Decks;
using Ithaca.Application.Moods;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Moods;

namespace Ithaca.Application.Rooms;

/// <summary><c>POST /api/rooms</c>, e.g. "Friday Film Club".</summary>
public sealed record CreateRoomRequest(
    [Required, StringLength(CreateRoomRequest.NameMaxLength, MinimumLength = 1)] string Name,
    RoomMode Mode,
    DateTimeOffset? StartsAt)
{
    public const int NameMaxLength = 40;
}

/// <summary><c>POST /api/rooms/{code}/join</c>. The name is required (1–30) when anonymous and ignored for users (service rule).</summary>
public sealed record JoinRoomRequest([StringLength(JoinRoomRequest.DisplayNameMaxLength, MinimumLength = 1)] string? DisplayName)
{
    public const int DisplayNameMaxLength = 30;
}

/// <summary><c>PUT /api/rooms/{code}/mood</c>. Hard nos are private to the member.</summary>
public sealed record SetRoomMoodRequest(
    [MaxLength(MoodQuery.MaxMoods)] IReadOnlyList<Mood> Moods,
    RuntimeLimit MaxRuntime,
    IReadOnlyList<HardNo> HardNos,
    [MaxLength(MoodQuery.TextMaxLength)] string? Text);

/// <summary><c>POST /api/rooms/{code}/votes</c>. <see cref="VoteId"/> makes retries idempotent (ADR-0015).</summary>
public sealed record CastVoteRequest(
    Guid VoteId,
    Guid DeckItemId,
    SwipeDirection Direction,
    SeenVerdict? SeenVerdict);
