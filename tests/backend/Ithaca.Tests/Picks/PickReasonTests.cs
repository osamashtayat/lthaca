using Ithaca.Domain.Errors;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Rooms;

namespace Ithaca.Tests.Picks;

/// <summary>BE-13: the reason rules (<see cref="PickReason"/>) and storing a room's reasons once (<see cref="Room.Explain"/>).</summary>
public sealed class PickReasonTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 20, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("  Twisty and tense.  ", "Twisty and tense.")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Clean_TrimsAndDropsEmpty(string? reason, string? expected) => PickReason.Clean(reason).ShouldBe(expected);

    [Fact]
    public void Clean_DropsTooLongReasons_InsteadOfCuttingThem()
    {
        PickReason.Clean(new string('a', PickReason.MaxLength)).ShouldNotBeNull();
        PickReason.Clean(new string('a', PickReason.MaxLength + 1)).ShouldBeNull();
    }

    [Fact]
    public void Explain_StoresOneReasonPerPick_Once()
    {
        var room = DecidedRoom(picks: 2);

        room.Explain(["Everyone loves a heist.", null]).ShouldBeTrue();
        room.Explain(["Something else.", "And more."]).ShouldBeFalse("the first reasons stay");

        room.ResultReasons.ShouldBe(["Everyone loves a heist.", null]);
    }

    [Fact]
    public void Explain_RejectsTheWrongCount_UncleanReasons_AndUndecidedRooms()
    {
        var room = DecidedRoom(picks: 2);
        Should.Throw<DomainException>(() => room.Explain(["Only one."]));
        Should.Throw<DomainException>(() => room.Explain([" untrimmed ", null]));
        Should.Throw<DomainException>(() => room.Explain([new string('a', PickReason.MaxLength + 1), null]));

        var swiping = Room.Create("POP-428", "Friday", RoomMode.JustSwipe, null, Now);
        swiping.Start();
        Should.Throw<DomainException>(() => swiping.Explain([]));
        room.ResultReasons.ShouldBeNull();
    }

    private static Room DecidedRoom(int picks)
    {
        var room = Room.Create("POP-428", "Friday", RoomMode.JustSwipe, null, Now);
        room.Start();
        var ids = Enumerable.Range(0, picks).Select(_ => Guid.NewGuid()).ToList();
        room.Decide(new RoomVerdict(RoomResultKind.Compromise, ids), [Guid.NewGuid()], [], Now).ShouldBeTrue();
        return room;
    }
}
