using Ithaca.Application.Decks;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Errors;
using Ithaca.Domain.Moods;
using Ithaca.Domain.Rooms;

namespace Ithaca.Tests.Rooms;

/// <summary>BE-11's pure rules: combining room moods, what hard nos rule out, room decks, votes, the mood step, start.</summary>
public sealed class RoomSwipingRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 20, 0, 0, TimeSpan.Zero);

    private static MoodQuery Mood(string[] include, string[] exclude, int? runtime = null, int? from = null, int? to = null)
    {
        var mood = MoodQuery.Create(null, [], RuntimeLimit.Any, null, null, Now);
        mood.SetFilters(include, exclude, runtime, from, to, [], textIgnored: false);
        return mood;
    }

    [Fact]
    public void ForRoom_UnitesGenres_DropsExclusionsSomeoneAskedFor_AndTakesTheShortestRuntime()
    {
        var filter = DeckFilter.ForRoom(
            [Mood(["Comedy"], ["Horror", "War"], runtime: 120), Mood(["Horror", "Thriller"], ["Romance"], runtime: 89), Mood([], [])],
            []);

        filter.IncludeGenres.ShouldBe(["Comedy", "Horror", "Thriller"]);
        filter.ExcludeGenres.ShouldBe(["War", "Romance"], "Horror is wanted by someone");
        filter.MaxRuntimeMinutes.ShouldBe(89);
        (filter.HardNos.TmdbGenres.Count, filter.HardNos.WikidataGenreTerms.Count, filter.HardNos.NonEnglish).ShouldBe((0, 0, false));
    }

    [Fact]
    public void ForRoom_YearsApplyOnlyWhenEveryoneSetThem_ThenTheWidest()
    {
        DeckFilter.ForRoom([Mood([], [], from: 1990, to: 1999), Mood([], [], from: 1980, to: 2005)], []).ShouldSatisfyAllConditions(
            f => f.MinReleaseYear.ShouldBe(1980),
            f => f.MaxReleaseYear.ShouldBe(2005));
        var partly = DeckFilter.ForRoom([Mood([], [], from: 1990, to: 1999), Mood([], [])], []);
        (partly.MinReleaseYear, partly.MaxReleaseYear).ShouldBe((null, null));
    }

    [Fact]
    public void ForRoom_NoMoods_IsOnlyTheHardNos_AndOnlyHardNosKeepsThem()
    {
        var filter = DeckFilter.ForRoom([], [HardNo.Horror]);

        filter.IsOnlyHardNos.ShouldBeTrue();
        filter.HardNos.TmdbGenres.ShouldBe(["Horror"]);
        var withMood = DeckFilter.ForRoom([Mood(["Comedy"], [])], [HardNo.Subtitles]);
        withMood.IsOnlyHardNos.ShouldBeFalse();
        withMood.OnlyHardNos().ShouldSatisfyAllConditions(
            f => f.IsOnlyHardNos.ShouldBeTrue(),
            f => f.HardNos.NonEnglish.ShouldBeTrue());
    }

    [Fact]
    public void HardNoRules_CombineEveryRule()
    {
        var all = HardNoRules.For(Enum.GetValues<HardNo>());

        all.TmdbGenres.ShouldBe(["Horror", "Music", "War"], ignoreOrder: true);
        all.WikidataGenreTerms.ShouldBe(["splatter", "slasher", "body horror", "zombie", "gore", "musical", "tragedy"], ignoreOrder: true);
        all.WikidataGenreTerms.ShouldAllBe(t => t.All(c => !char.IsUpper(c)), "matched against lower(label)");
        all.NonEnglish.ShouldBeTrue();
        HardNoRules.For([]).ShouldSatisfyAllConditions(f => f.TmdbGenres.ShouldBeEmpty(), f => f.WikidataGenreTerms.ShouldBeEmpty(), f => f.NonEnglish.ShouldBeFalse());
        Enum.GetValues<HardNo>().ShouldAllBe(h => HardNoRules.Table.ContainsKey(h), "every hard no has a rule");
    }

    [Fact]
    public void Deck_ForRoom_HasARoomAndNoUserNorMood()
    {
        var roomId = Guid.NewGuid();

        var deck = Deck.ForRoom(roomId, DeckSource.Mood, Now);

        (deck.RoomId, deck.UserId, deck.MoodQueryId, deck.Source).ShouldBe((roomId, null, null, DeckSource.Mood));
        Should.Throw<DomainException>(() => Deck.ForRoom(Guid.Empty, DeckSource.JustSwipe, Now));
    }

    [Fact]
    public void Vote_VerdictOnlyWithUp()
    {
        Vote.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SwipeDirection.Up, SeenVerdict.Loved, Now).SeenVerdict.ShouldBe(SeenVerdict.Loved);
        Should.Throw<DomainException>(() => Vote.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SwipeDirection.Right, SeenVerdict.Loved, Now))
            .Code.ShouldBe(ErrorCodes.Validation);
        Should.Throw<DomainException>(() => Vote.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), SwipeDirection.Left, null, Now));
    }

    [Fact]
    public void SetMood_ReturnsTheReplacedMood_AndKeepsHardNosDistinct()
    {
        var member = RoomMember.ForGuest(Guid.NewGuid(), Guid.NewGuid(), seat: 2, Now);
        var first = Guid.NewGuid();

        member.SetMood(first, [HardNo.Gore, HardNo.Gore]).ShouldBeNull();
        member.SetMood(Guid.NewGuid(), [HardNo.Horror]).ShouldBe(first);

        member.HardNos.ShouldBe([HardNo.Horror]);
    }

    [Fact]
    public void Start_OnlyFromTheLobby()
    {
        var room = Room.Create("POP-428", "Friday", RoomMode.JustSwipe, null, Now);

        room.Start().ShouldBeTrue();
        room.Status.ShouldBe(RoomStatus.Swiping);
        room.Start().ShouldBeFalse();
        room.Close(Now);
        room.Start().ShouldBeFalse();
    }
}
