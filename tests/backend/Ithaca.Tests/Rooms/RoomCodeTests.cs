using Ithaca.Domain.Rooms;

namespace Ithaca.Tests.Rooms;

public sealed class RoomCodeTests
{
    [Fact]
    public void New_IsThreeLettersDashThreeDigits()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = RoomCode.New();
            code.ShouldMatch("^[A-Z]{3}-[1-9][0-9]{2}$");
            RoomCode.Normalize(code).ShouldBe(code);
        }
    }

    [Theory]
    [InlineData(" pop-428 ", "POP-428")]
    [InlineData("Pop-428", "POP-428")]
    [InlineData("POP428", null)]
    [InlineData("PO-4281", null)]
    [InlineData("P0P-428", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalize_UpperCasesOrRejects(string? input, string? expected) => RoomCode.Normalize(input).ShouldBe(expected);
}
