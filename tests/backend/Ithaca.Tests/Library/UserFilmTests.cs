using Ithaca.Domain.Errors;
using Ithaca.Domain.Library;

namespace Ithaca.Tests.Library;

/// <summary>The <c>UserFilm</c> entity's rules (A8, A9): a rating implies seen, ratings are 0.5–5 in half steps (ADR-0024), the watchlist toggles alone.</summary>
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
        userFilm.Rating.ShouldBe(3m);
    }

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(1, 2)]
    [InlineData(3.5, 7)]
    [InlineData(5, 10)]
    public void Set_StoresHalfStars_AndReadsBackTheSameRating(double value, short halfStars)
    {
        var rating = (decimal)value;
        var userFilm = New();

        userFilm.Set(liked: false, onWatchlist: false, seen: false, rating, Now);

        userFilm.RatingHalfStars.ShouldBe(halfStars);
        userFilm.Rating.ShouldBe(rating);
        UserFilm.ToRating(halfStars).ShouldBe(rating);
    }

    [Fact]
    public void ClearingTheRating_ClearsTheStoredHalfStars()
    {
        var userFilm = New();
        userFilm.Set(false, false, false, 2.5m, Now);

        userFilm.Set(liked: true, onWatchlist: false, seen: true, rating: null, Now);

        (userFilm.RatingHalfStars, userFilm.Rating).ShouldBe((null, null));
        UserFilm.ToRating(null).ShouldBeNull();
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(4.5, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(3.25, false)]
    [InlineData(5.5, false)]
    [InlineData(7, false)]
    public void IsValidRating_AcceptsOnlyHalfStepsFrom05To5(double value, bool valid) =>
        UserFilm.IsValidRating((decimal)value).ShouldBe(valid);

    [Theory]
    [InlineData(0)]
    [InlineData(3.25)]
    [InlineData(5.5)]
    [InlineData(6)]
    public void Set_InvalidRating_Throws_AndChangesNothing(double value)
    {
        var userFilm = New();

        Should.Throw<DomainException>(() => userFilm.Set(true, false, false, (decimal)value, Now)).Code.ShouldBe(ErrorCodes.Validation);

        userFilm.IsEmpty.ShouldBeTrue();
    }

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
        (userFilm.Liked, userFilm.Seen, userFilm.Rating).ShouldBe((true, true, (decimal?)4m));
    }
}
