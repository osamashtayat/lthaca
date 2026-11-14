using Ithaca.Application.Auth;
using Ithaca.Application.Films;
using Ithaca.Domain.Rooms;

namespace Ithaca.Application.Rooms;

/// <summary>A <c>RoomMember</c> as everyone in the room sees it (never their hard nos or mood answers).</summary>
public sealed record RoomMemberDto(Guid Id, string DisplayName, PrincipalKind Kind, bool IsHost, bool IsReady, DateTimeOffset JoinedAt);

/// <summary>A room as its members see it; <see cref="Me"/> is the caller.</summary>
public sealed record RoomDto(
    Guid Id,
    string Code,
    string Name,
    RoomMode Mode,
    RoomStatus Status,
    DateTimeOffset? StartsAt,
    DateTimeOffset CreatedAt,
    int DeckSize,
    int MaxMembers,
    RoomMemberDto Me,
    IReadOnlyList<RoomMemberDto> Members);

/// <summary>
/// <c>PUT /api/rooms/{code}/mood</c>: the caller, now ready. <see cref="TextIgnored"/> is true when their text wasn't a mood,
/// so only their chips were used; it is only ever sent to that member (ADR-0022).
/// </summary>
public sealed record RoomMoodDto(RoomMemberDto Member, bool TextIgnored);

/// <summary>What anyone with the link sees before joining.</summary>
public sealed record RoomPreviewDto(
    string Code,
    string Name,
    string HostDisplayName,
    RoomMode Mode,
    RoomStatus Status,
    int MemberCount,
    int MaxMembers);

/// <summary>How many cards one member has voted on.</summary>
public sealed record MemberProgressDto(Guid MemberId, int VotedCount);

/// <summary><c>GET /api/rooms/{code}/progress</c>.</summary>
public sealed record RoomProgressDto(RoomStatus Status, int DeckSize, IReadOnlyList<MemberProgressDto> Members);

/// <summary>A picked film. Names who said yes and who has seen it; only counts the noes (A7).</summary>
public sealed record RoomPickDto(
    int Rank,
    FilmSummaryDto Film,
    int YesCount,
    int NoCount,
    IReadOnlyList<Guid> Yes,
    IReadOnlyList<Guid> Seen);

/// <summary>Match: exactly one pick. Compromise: up to three, best first.</summary>
public sealed record RoomResultDto(RoomResultKind Kind, IReadOnlyList<RoomPickDto> Picks, int MemberCount, DateTimeOffset DecidedAt);
