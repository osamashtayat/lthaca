using Ithaca.Domain.Errors;
using Ithaca.Domain.Library;

namespace Ithaca.Tests.Library;

/// <summary>The <c>UserFilm</c> entity's rules (A8, A9): a rating implies seen, ratings are 1–5, the watchlist toggles alone.</summary>
public sealed class UserFilmTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);

    private static UserFilm New() => UserFilm.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

    [Fact]
    public void Set_WithARating_MarksSeen()
    {
        var userFilm = New();

        userFilm.Set(liked: false, onWatchlist: false, seen: false, rating: 3, Now);

        userFilm.Seen.ShouldBeTrue();
        userFilm.Rating.ShouldBe(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Set_RatingOutOfRange_Throws(int rating) =>
        Should.Throw<DomainException>(() => New().Set(false, false, false, rating, Now)).Code.ShouldBe(ErrorCodes.Validation);

    [Fact]
    public void IsEmpty_OnlyWhenNothingIsSet()
    {
        var userFilm = New();
        userFilm.IsEmpty.ShouldBeTrue();

        userFilm.Set(liked: true, onWatchlist: false, seen: false, rating: null, Now);
        userFilm.IsEmpty.ShouldBeFalse();

        userFilm.Set(false, false, false, null, Now);
        userFilm.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void SetWatchlist_ChangesOnlyThatFlag_AndReportsNoOps()
    {
        var userFilm = New();
        userFilm.Set(liked: true, onWatchlist: false, seen: true, rating: 4, Now);

        userFilm.SetWatchlist(true, Now.AddMinutes(1)).ShouldBeTrue();
        userFilm.SetWatchlist(true, Now.AddMinutes(2)).ShouldBeFalse();

        userFilm.UpdatedAt.ShouldBe(Now.AddMinutes(1), "a no-op leaves the timestamp alone");
        (userFilm.Liked, userFilm.Seen, userFilm.Rating).ShouldBe((true, true, (int?)4));
    }
}
