using Ithaca.Domain.Errors;
using Ithaca.Domain.Rooms;

namespace Ithaca.Tests.Rooms;

/// <summary>BE-12's pure rules: when a room is decided, the match, and the best compromise's order and ties.</summary>
public sealed class RoomResultRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 20, 0, 0, TimeSpan.Zero);

    private static CardTally Card(int position, int yes, int no, int votes, double? popularity = 10) =>
        new(new Guid(position, 0, 0, new byte[8]), position, popularity, yes, no, votes);

    private static Guid Id(int position) => new(position, 0, 0, new byte[8]);

    [Fact]
    public void Match_NeedsEveryMember_AndPicksTheEarliestCard()
    {
        CardTally[] cards = [Card(1, yes: 2, no: 1, votes: 3), Card(2, yes: 3, no: 0, votes: 3), Card(3, yes: 3, no: 0, votes: 3)];

        RoomResultRules.Match(cards, memberCount: 3).ShouldBe(new RoomVerdict(RoomResultKind.Match, [Id(2)]), new MatchComparer());
        RoomResultRules.Match(cards, memberCount: 4).ShouldBeNull();
        RoomResultRules.Match(cards, memberCount: 0).ShouldBeNull();
    }

    [Fact]
    public void Due_IsNull_WhileSomeoneHasCardsLeft_AndACompromiseOnceEveryoneFinished()
    {
        CardTally[] going = [Card(1, yes: 1, no: 1, votes: 2), Card(2, yes: 1, no: 0, votes: 1)];
        CardTally[] done = [Card(1, yes: 1, no: 1, votes: 2), Card(2, yes: 1, no: 0, votes: 2)];

        RoomResultRules.Due(going, memberCount: 2).ShouldBeNull();
        var verdict = RoomResultRules.Due(done, memberCount: 2).ShouldNotBeNull();
        verdict.Kind.ShouldBe(RoomResultKind.Compromise);
        verdict.Picks.ShouldBe([Id(2), Id(1)]);
        RoomResultRules.Due([], memberCount: 2).ShouldBeNull("an empty deck is never finished");
    }

    [Fact]
    public void Due_PrefersAMatch_OverACompromise()
    {
        CardTally[] cards = [Card(1, yes: 1, no: 1, votes: 2, popularity: 999), Card(2, yes: 2, no: 0, votes: 2)];

        RoomResultRules.Due(cards, memberCount: 2)!.Kind.ShouldBe(RoomResultKind.Match);
        RoomResultRules.OnFinish(cards, memberCount: 2).Picks.ShouldBe([Id(2)]);
    }

    [Fact]
    public void Compromise_RanksByYes_ThenFewerNo_ThenPopularity_ThenPosition_TopThree()
    {
        CardTally[] cards =
        [
            Card(1, yes: 1, no: 2, votes: 3, popularity: 900),
            Card(2, yes: 2, no: 1, votes: 3, popularity: 1),
            Card(3, yes: 2, no: 0, votes: 3, popularity: 1),
            Card(4, yes: 1, no: 0, votes: 3, popularity: 50),
            Card(5, yes: 1, no: 0, votes: 3, popularity: 80),
            Card(6, yes: 1, no: 0, votes: 3, popularity: null),
        ];

        RoomResultRules.Compromise(cards).Picks.ShouldBe([Id(3), Id(2), Id(5)]);
    }

    [Fact]
    public void Compromise_TiesOnEverything_GoToTheEarlierCard_AndUnknownPopularityRanksLast()
    {
        CardTally[] cards =
        [
            Card(4, yes: 1, no: 0, votes: 2, popularity: null),
            Card(3, yes: 1, no: 0, votes: 2, popularity: 5),
            Card(2, yes: 1, no: 0, votes: 2, popularity: 5),
        ];

        RoomResultRules.Compromise(cards).Picks.ShouldBe([Id(2), Id(3), Id(4)]);
    }

    [Fact]
    public void Compromise_LeavesOutCardsNobodySaidYesTo()
    {
        CardTally[] cards = [Card(1, yes: 0, no: 0, votes: 2, popularity: 999), Card(2, yes: 0, no: 2, votes: 2)];

        RoomResultRules.Compromise(cards).Picks.ShouldBeEmpty();
        RoomResultRules.OnFinish([], memberCount: 2).ShouldBe(new RoomVerdict(RoomResultKind.Compromise, []), new MatchComparer());
    }

    [Fact]
    public void Decide_OnlyFromSwiping_Once_AndKeepsTheResultWhenClosed()
    {
        var room = Room.Create("POP-428", "Friday", RoomMode.JustSwipe, null, Now);
        var members = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var verdict = new RoomVerdict(RoomResultKind.Match, [Id(1)]);

        room.Decide(verdict, members, [], Now).ShouldBeFalse("still in the lobby");
        room.Start();
        Guid[] votes = [Guid.NewGuid()];
        room.Decide(verdict, members, votes, Now).ShouldBeTrue();
        room.Decide(new RoomVerdict(RoomResultKind.Compromise, []), members, [], Now).ShouldBeFalse("a room is decided once");
        room.Close(Now);

        room.Status.ShouldBe(RoomStatus.Closed);
        room.ResultKind.ShouldBe(RoomResultKind.Match);
        room.ResultPicks.ShouldBe([Id(1)]);
        room.ResultMembers.ShouldBe(members);
        room.ResultVotes.ShouldBe(votes);
        room.DecidedAt.ShouldBe(Now);
    }

    [Fact]
    public void Decide_RejectsAMatchWithoutExactlyOnePick_AndACompromiseOverThree()
    {
        var room = Room.Create("POP-428", "Friday", RoomMode.JustSwipe, null, Now);
        room.Start();
        Guid[] members = [Guid.NewGuid()];

        Should.Throw<DomainException>(() => room.Decide(new RoomVerdict(RoomResultKind.Match, []), members, [], Now));
        Should.Throw<DomainException>(() => room.Decide(new RoomVerdict(RoomResultKind.Compromise, [Id(1), Id(2), Id(3), Id(4)]), members, [], Now));
        Should.Throw<DomainException>(() => room.Decide(new RoomVerdict(RoomResultKind.Match, [Id(1)]), [], [], Now));
    }

    /// <summary>Records compare their list by reference; compare kind and picks instead.</summary>
    private sealed class MatchComparer : IEqualityComparer<RoomVerdict?>
    {
        public bool Equals(RoomVerdict? x, RoomVerdict? y) =>
            x is not null && y is not null && x.Kind == y.Kind && x.Picks.SequenceEqual(y.Picks);

        public int GetHashCode(RoomVerdict? obj) => obj?.Kind.GetHashCode() ?? 0;
    }
}
