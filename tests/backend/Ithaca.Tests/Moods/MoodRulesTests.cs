using Ithaca.Domain.Errors;
using Ithaca.Domain.Moods;

namespace Ithaca.Tests.Moods;

/// <summary>BE-09: the chip table and the <see cref="MoodQuery"/> rules, without a database.</summary>
public sealed class MoodRulesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Chips_EveryMoodHasAnEntry() =>
        Enum.GetValues<Mood>().ShouldAllBe(m => MoodChips.Table.ContainsKey(m));

    [Fact]
    public void Chips_Laugh_IsComedyWithoutHorror()
    {
        var filters = MoodChips.For([Mood.Laugh]);

        filters.IncludeGenres.ShouldBe(["Comedy"]);
        filters.ExcludeGenres.ShouldContain("Horror");
    }

    [Fact]
    public void Chips_TwoMoods_AddUp_AndOneChipsGenreIsNeverExcludedByTheOther()
    {
        // Cozy excludes Thriller and Crime; Keep me guessing includes them.
        var filters = MoodChips.For([Mood.Cozy, Mood.KeepGuessing]);

        filters.IncludeGenres.ShouldContain("Comedy");
        filters.IncludeGenres.ShouldContain("Thriller");
        filters.ExcludeGenres.ShouldNotContain("Thriller");
        filters.ExcludeGenres.ShouldNotContain("Crime");
        filters.ExcludeGenres.ShouldContain("Horror");
    }

    [Fact]
    public void Chips_Surprise_SetsNoFilter()
    {
        var filters = MoodChips.For([Mood.Surprise]);

        filters.IncludeGenres.ShouldBeEmpty();
        filters.ExcludeGenres.ShouldBeEmpty();
    }

    [Fact]
    public void Create_Normalises_DistinctMoods_LowercaseLanguage_BlankTextIsNull()
    {
        var query = MoodQuery.Create(Guid.NewGuid(), [Mood.Laugh, Mood.Laugh], RuntimeLimit.Any, "KO", "   ", Now);

        query.Moods.ShouldBe([Mood.Laugh]);
        query.Language.ShouldBe("ko");
        query.Text.ShouldBeNull();
    }

    [Theory]
    [InlineData("e1")]
    [InlineData("eng")]
    public void Create_RejectsALanguageThatIsntTwoLetters(string language) =>
        Should.Throw<DomainException>(() => MoodQuery.Create(Guid.NewGuid(), [], RuntimeLimit.Any, language, null, Now))
            .Field.ShouldBe("language");

    [Fact]
    public void Create_RejectsThreeMoods() =>
        Should.Throw<DomainException>(() => MoodQuery.Create(Guid.NewGuid(), [Mood.Cozy, Mood.Laugh, Mood.Surprise], RuntimeLimit.Any, null, null, Now))
            .Field.ShouldBe("moods");

    [Fact]
    public void SetFilters_NeverExcludesAnIncludedGenre_DropsAnInvertedYearRange_AndCapsKeywords()
    {
        var query = MoodQuery.Create(Guid.NewGuid(), [], RuntimeLimit.Any, null, null, Now);

        query.SetFilters(["Comedy"], ["Comedy", "Horror"], 0, 2000, 1990, Enumerable.Range(0, 20).Select(i => $"k{i}"), textIgnored: false);

        query.ExcludeGenres.ShouldBe(["Horror"]);
        query.MaxRuntimeMinutes.ShouldBeNull("0 is not a limit");
        query.MinReleaseYear.ShouldBeNull();
        query.MaxReleaseYear.ShouldBeNull();
        query.Keywords.Length.ShouldBe(MoodQuery.MaxKeywords);
    }
}
