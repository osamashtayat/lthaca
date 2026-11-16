using Ithaca.Application.Common;
using Ithaca.Application.Decks;
using Ithaca.Application.Moods;
using Ithaca.Application.Realtime;
using Ithaca.Application.Taste;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Moods;
using Ithaca.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Rooms;

/// <summary>
/// Room swiping (api-contract.md §6, ADR-0015, ADR-0022; BE-11): the mood step, start, the shared deck, votes and progress.
/// <list type="bullet">
/// <item>Mood step (mood rooms, lobby only): the member's mood goes through <see cref="MoodInterpreter"/> with the same rules
/// as solo (<c>mood.unclear</c>, <c>textIgnored</c>), but the text isn't kept (issue #51); their hard nos stay private.
/// Answering makes them ready.</item>
/// <item>Start (host, lobby only): one shared <see cref="Deck"/> for the room, the first batch dealt from the members'
/// combined moods (<see cref="DeckFilter.ForRoom"/>), topped up by the sampler, every present member's hard nos excluded.
/// Taste rooms (BE-14) lean every batch towards the blend of the present users' taste profiles and skip any film a present
/// user has seen (guests have no library, so they add neither; A11).</item>
/// <item>Votes: one per card and member, never changed; the same <c>voteId</c> again is a no-op; they never touch the
/// library (A5). When the voter is <see cref="ExtendWhenLeft"/> cards from the end, the next batch is dealt, up to
/// <see cref="Deck.RoomMaxItems"/>.</item>
/// </list>
/// Events after the commit: <c>memberReady</c>, <c>roomStarted</c>, <c>voteCast</c> (no values), <c>deckExtended</c>. After a
/// vote's commit <see cref="RoomReferee"/> decides the room when a match or the end of the deck is reached (BE-12).
/// </summary>
public sealed class RoomDeckService(
    IAppDbContext db,
    RoomAccess access,
    RoomReader reader,
    MoodInterpreter moods,
    TasteProfileReader tastes,
    DeckDealer dealer,
    DeckItemReader cards,
    RoomReferee referee,
    IRoomEventPublisher events,
    TimeProvider time)
{
    /// <summary>Cards left for a voter at which the next batch is dealt.</summary>
    public const int ExtendWhenLeft = 3;

    /// <summary>Tries at saving a mood step answer when the same member answered at the same moment.</summary>
    private const int MoodAttempts = 3;

    /// <summary>Tries at saving a vote when a concurrent vote dealt the same extension.</summary>
    private const int VoteAttempts = 3;

    /// <summary>The caller's mood and private hard nos for a mood room's lobby; replaces an earlier answer.</summary>
    public async Task<Result<RoomMoodDto>> SetMoodAsync(string code, SetRoomMoodRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var membership = await access.MemberAsync(code, ct);
        if (!membership.IsSuccess)
        {
            return membership.Error;
        }

        var (roomId, memberId, actor) = membership.Value;
        var room = await db.Rooms.AsNoTracking().Where(r => r.Id == roomId).Select(r => new { r.Mode, r.Status }).SingleAsync(ct);
        if (room.Mode != RoomMode.Mood || room.Status != RoomStatus.Lobby)
        {
            return RoomErrors.InvalidState;
        }

        var input = new MoodInput(request.Moods ?? [], request.MaxRuntime, Language: null, request.Text);
        var interpreted = await moods.InterpretAsync(actor.IsUser ? actor.Id : null, input, ct);
        if (!interpreted.IsSuccess)
        {
            return interpreted.Error; // nothing changes (ADR-0022)
        }

        var mood = interpreted.Value;
        mood.BelongToRoomMember(memberId); // no text kept (issue #51)
        var member = await db.RoomMembers.SingleAsync(m => m.Id == memberId, ct);
        member.SetHardNos(request.HardNos ?? []);
        db.MoodQueries.Add(mood);
        List<MoodQuery> previous;
        for (var attempt = 1; ; attempt++)
        {
            previous = await db.MoodQueries.Where(q => q.RoomMemberId == memberId).ToListAsync(ct);
            db.MoodQueries.RemoveRange(previous);
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (Exception e) when (WriteConflicts.IsWriteConflict(e) && attempt < MoodAttempts)
            {
                // The same member answered at the same moment (one mood each): replace that answer instead.
                foreach (var stale in previous)
                {
                    db.MoodQueries.Entry(stale).State = EntityState.Detached;
                }
            }
        }

        if (previous.Count == 0)
        {
            await events.PublishAsync(roomId, RoomEvents.MemberReady, new RoomMemberEvent(roomId, memberId), ct);
        }

        var me = (await reader.MembersAsync(roomId, room.Mode, ct)).Single(m => m.Id == memberId);
        return new RoomMoodDto(me, mood.TextIgnored);
    }

    /// <summary>Host only, from the lobby: deals the shared deck and starts swiping. Members need not be ready.</summary>
    public async Task<Result<RoomDto>> StartAsync(string code, CancellationToken ct)
    {
        var host = await access.HostAsync(code, ct);
        if (!host.IsSuccess)
        {
            return host.Error;
        }

        var (roomId, memberId, _) = host.Value;
        var room = await db.Rooms.SingleAsync(r => r.Id == roomId, ct);
        if (!room.Start())
        {
            return RoomErrors.InvalidState;
        }

        var now = time.GetUtcNow();
        var deck = Deck.ForRoom(roomId, SourceOf(room.Mode), now);
        db.Decks.Add(deck);
        await DealAsync(deck.Id, roomId, room.Mode, dealt: 0, Deck.BatchSize, now, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (WriteConflicts.IsWriteConflict(e))
        {
            return RoomErrors.InvalidState; // a concurrent start dealt the room's deck first (one deck per room)
        }

        await events.PublishAsync(roomId, RoomEvents.RoomStarted, new RoomEvent(roomId), ct);
        return await reader.RoomDtoAsync(roomId, memberId, ct);
    }

    /// <summary>The shared deck from <paramref name="fromPosition"/> on (0 = all); each item's <c>swipe</c> is the caller's vote.</summary>
    public async Task<Result<IReadOnlyList<DeckItemDto>>> GetDeckAsync(string code, int fromPosition, CancellationToken ct)
    {
        var membership = await access.MemberAsync(code, ct);
        if (!membership.IsSuccess)
        {
            return membership.Error;
        }

        var (roomId, memberId, actor) = membership.Value;
        if (await StatusAsync(roomId, ct) == RoomStatus.Lobby)
        {
            return RoomErrors.InvalidState;
        }

        var mine = db.Votes.AsNoTracking()
            .Where(v => v.RoomMemberId == memberId)
            .Select(v => new CardGesture { DeckItemId = v.DeckItemId, Direction = v.Direction, SeenVerdict = v.SeenVerdict });
        var items = await cards.ItemDtosAsync(
            reader.DeckItemsOf(roomId).Where(i => i.Position >= fromPosition), mine, actor.IsUser ? actor.Id : null, ct);
        return Result<IReadOnlyList<DeckItemDto>>.Success(items);
    }

    /// <summary>
    /// Records the caller's vote. The same <c>voteId</c> on the same card again is a no-op success (even after swiping
    /// ended); another vote on a card they voted on is <c>room.already_voted</c>. Only while swiping.
    /// </summary>
    public async Task<Result> CastVoteAsync(string code, CastVoteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var membership = await access.MemberAsync(code, ct);
        if (!membership.IsSuccess)
        {
            return membership.Error;
        }

        var (roomId, memberId, _) = membership.Value;
        if (!Swipe.IsValidVerdict(request.Direction, request.SeenVerdict))
        {
            return DeckErrors.VerdictOnlyWithUp;
        }

        if (await RetriedAsync(request, memberId, ct) is { } retried)
        {
            if (retried.IsSuccess)
            {
                // The vote is stored, but the request that stored it may have failed before deciding the room.
                await referee.DecideIfDueAsync(roomId, ct);
            }

            return retried;
        }

        var room = await db.Rooms.AsNoTracking().Where(r => r.Id == roomId).Select(r => new { r.Mode, r.Status }).SingleAsync(ct);
        if (room.Status != RoomStatus.Swiping)
        {
            return room.Status == RoomStatus.Closed ? RoomErrors.Closed : RoomErrors.InvalidState;
        }

        var item = await reader.DeckItemsOf(roomId).Where(i => i.Id == request.DeckItemId).Select(i => new { i.Id, i.DeckId }).SingleOrDefaultAsync(ct);
        if (item is null)
        {
            return DeckErrors.ItemNotFound;
        }

        if (await db.Votes.AnyAsync(v => v.DeckItemId == item.Id && v.RoomMemberId == memberId, ct))
        {
            return RoomErrors.AlreadyVoted;
        }

        var now = time.GetUtcNow();
        db.Votes.Add(Vote.Create(request.VoteId, item.Id, memberId, request.Direction, request.SeenVerdict, now));
        List<DeckItem> extension;
        for (var attempt = 1; ; attempt++)
        {
            extension = await ExtendIfNearEndAsync(item.DeckId, roomId, room.Mode, memberId, now, ct);
            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (Exception e) when (WriteConflicts.IsWriteConflict(e))
            {
                // A concurrent request recorded this vote (a retry or a second vote on the card), or dealt the same extension.
                db.DeckItems.RemoveRange(extension);
                if (await RetriedAsync(request, memberId, ct) is { } raced)
                {
                    return raced;
                }

                if (await db.Votes.AnyAsync(v => v.DeckItemId == item.Id && v.RoomMemberId == memberId, ct))
                {
                    return RoomErrors.AlreadyVoted;
                }

                if (extension.Count == 0 || attempt >= VoteAttempts)
                {
                    throw;
                }
            }
        }

        await events.PublishAsync(roomId, RoomEvents.VoteCast, new VoteCastEvent(roomId, item.Id), ct);
        if (extension.Count > 0)
        {
            await events.PublishAsync(roomId, RoomEvents.DeckExtended, new DeckExtendedEvent(roomId, extension.Min(i => i.Position)), ct);
        }

        await referee.DecideIfDueAsync(roomId, ct); // after the commit, so concurrent last votes are seen (BE-12, ADR-0026)
        return Result.Success();
    }

    /// <summary>How many cards each present member has voted on (host first); no vote values.</summary>
    public async Task<Result<RoomProgressDto>> GetProgressAsync(string code, CancellationToken ct)
    {
        var membership = await access.MemberAsync(code, ct);
        if (!membership.IsSuccess)
        {
            return membership.Error;
        }

        var roomId = membership.Value.RoomId;
        var status = await StatusAsync(roomId, ct);
        var deckSize = await reader.DeckItemsOf(roomId).CountAsync(ct);
        var members = await RoomAccess.InHostOrder(db.RoomMembers.AsNoTracking().Where(m => m.RoomId == roomId && m.LeftAt == null))
            .Select(m => new MemberProgressDto(m.Id, db.Votes.Count(v => v.RoomMemberId == m.Id)))
            .ToListAsync(ct);
        return new RoomProgressDto(status, deckSize, members);
    }

    /// <summary>
    /// Deals the next batch when the voter (counting the vote being saved) is <see cref="ExtendWhenLeft"/> or fewer cards from
    /// the end and the deck is under <see cref="Deck.RoomMaxItems"/>. Not saved here.
    /// </summary>
    private async Task<List<DeckItem>> ExtendIfNearEndAsync(Guid deckId, Guid roomId, RoomMode mode, Guid memberId, DateTimeOffset now, CancellationToken ct)
    {
        var dealt = await db.DeckItems.CountAsync(i => i.DeckId == deckId, ct);
        var voted = await db.Votes.CountAsync(v => v.RoomMemberId == memberId, ct) + 1;
        if (dealt >= Deck.RoomMaxItems || dealt - voted > ExtendWhenLeft)
        {
            return [];
        }

        return await DealAsync(deckId, roomId, mode, dealt, Math.Min(Deck.BatchSize, Deck.RoomMaxItems - dealt), now, ct);
    }

    /// <summary>
    /// A batch for the room from its present members' moods (mood rooms) and hard nos; topped up by the sampler, hard nos
    /// still excluded, when the moods leave too few films. The combined mood genres also lean the picks (one strong signal).
    /// Taste rooms: the picks lean towards the present users' blended taste, and films any present user has seen are skipped.
    /// </summary>
    private async Task<List<DeckItem>> DealAsync(Guid deckId, Guid roomId, RoomMode mode, int dealt, int count, DateTimeOffset now, CancellationToken ct)
    {
        var present = db.RoomMembers.AsNoTracking().Where(m => m.RoomId == roomId && m.LeftAt == null);
        var hardNos = (await present.Select(m => m.HardNos).ToListAsync(ct)).SelectMany(h => h);
        var memberMoods = mode == RoomMode.Mood
            ? await db.MoodQueries.AsNoTracking().Where(q => present.Any(m => m.Id == q.RoomMemberId)).ToListAsync(ct)
            : [];
        var filter = DeckFilter.ForRoom(memberMoods, hardNos);
        IReadOnlyList<DeckFilter> filters = filter.IsOnlyHardNos ? [filter] : [filter, filter.OnlyHardNos()];
        IReadOnlyList<DeckSignal> signals = filter.IncludeGenres.Count > 0 ? [new DeckSignal(filter.IncludeGenres, null, null, DeckSignals.MoodWeight)] : [];
        if (mode != RoomMode.Taste)
        {
            return await dealer.DealAsync(deckId, dealt, count, filters, excludedFilms: null, signals, embeddingModel: null, taste: null, now, ct);
        }

        var userIds = present.Where(m => m.UserId != null).Select(m => m.UserId!.Value);
        var taste = await tastes.BlendAsync(await userIds.ToListAsync(ct), ct);
        var seen = db.UserFilms.Where(u => u.Seen && userIds.Contains(u.UserId)).Select(u => u.FilmId);
        return await dealer.DealAsync(deckId, dealt, count, filters, seen, signals, taste?.EmbeddingModel, taste, now, ct);
    }

    private static DeckSource SourceOf(RoomMode mode) => mode switch
    {
        RoomMode.Mood => DeckSource.Mood,
        RoomMode.Taste => DeckSource.Taste,
        _ => DeckSource.JustSwipe,
    };

    private Task<RoomStatus> StatusAsync(Guid roomId, CancellationToken ct) =>
        db.Rooms.AsNoTracking().Where(r => r.Id == roomId).Select(r => r.Status).SingleAsync(ct);

    /// <summary>Success when this <c>voteId</c> is already the caller's vote on this card; a conflict when used otherwise; null when new.</summary>
    private async Task<Result?> RetriedAsync(CastVoteRequest request, Guid memberId, CancellationToken ct)
    {
        var existing = await db.Votes.AsNoTracking().Where(v => v.Id == request.VoteId).Select(v => new { v.DeckItemId, v.RoomMemberId }).SingleOrDefaultAsync(ct);
        if (existing is null)
        {
            return null;
        }

        return existing.DeckItemId == request.DeckItemId && existing.RoomMemberId == memberId ? Result.Success() : RoomErrors.AlreadyVoted;
    }
}
