using Ithaca.Domain.Errors;
using Ithaca.Domain.Imports;
using Ithaca.Domain.Library;

namespace Ithaca.Tests.Imports;

/// <summary>§13 job rule 3 as a table: never remove, any existing rating wins, a rating implies seen; only real changes count.</summary>
public sealed class ImportMergeRulesTests
{
    private static readonly DateTimeOffset Before = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Existing state (liked, watchlist, seen, half stars) + file (seen, liked, watchlist, half stars) → result + changes.</summary>
    public static TheoryData<string, bool[], short?, bool[], short?, bool[], short?, ImportChanges> Rules => new()
    {
        { "empty row, watched", [false, false, false], null, [true, false, false], null, [false, false, true], null, new(0, 1, 0, 0, 0, 0) },
        { "rating implies seen", [false, false, false], null, [false, false, false], 7, [false, false, true], 7, new(0, 1, 1, 0, 0, 0) },
        { "liked and watchlist", [false, false, false], null, [false, true, true], null, [true, true, false], null, new(0, 0, 0, 1, 1, 0) },
        { "existing rating wins", [false, false, true], 4, [true, false, false], 9, [false, false, true], 4, new(0, 0, 0, 0, 0, 1) },
        { "same rating isn't 'kept'", [false, false, true], 9, [true, false, false], 9, [false, false, true], 9, new(0, 0, 0, 0, 0, 0) },
        { "never sets false", [true, true, true], 6, [false, false, false], null, [true, true, true], 6, new(0, 0, 0, 0, 0, 0) },
        { "already set: no change", [true, true, true], null, [true, true, true], null, [true, true, true], null, new(0, 0, 0, 0, 0, 0) },
        { "rating on a seen film", [false, false, true], null, [false, false, false], 3, [false, false, true], 3, new(0, 0, 1, 0, 0, 0) },
    };

    [Theory]
    [MemberData(nameof(Rules))]
    public void MergeImport_FollowsTheRules(
        string rule, bool[] existing, short? existingRating, bool[] file, short? fileRating, bool[] expected, short? expectedRating, ImportChanges changes)
    {
        var userFilm = UserFilm.Create(Guid.NewGuid(), Guid.NewGuid(), Before);
        userFilm.Set(existing[0], existing[1], existing[2], existingRating / 2m, Before);

        var result = userFilm.MergeImport(file[0], file[1], file[2], fileRating, Now);

        result.ShouldBe(changes, rule);
        new[] { userFilm.Liked, userFilm.OnWatchlist, userFilm.Seen }.ShouldBe(expected, rule);
        userFilm.RatingHalfStars.ShouldBe(expectedRating, rule);
        userFilm.UpdatedAt.ShouldBe(changes.Any ? Now : Before, rule + ": updatedAt moves only on a change");
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)11)]
    public void MergeImport_RatingOutsideHalfStars_Throws(short halfStars)
    {
        var userFilm = UserFilm.Create(Guid.NewGuid(), Guid.NewGuid(), Before);

        Should.Throw<DomainException>(() => userFilm.MergeImport(false, false, false, halfStars, Now));
    }
}
