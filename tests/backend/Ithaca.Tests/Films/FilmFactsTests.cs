using System.Reflection;
using Ithaca.Domain.Errors;
using Ithaca.Domain.Films;

namespace Ithaca.Tests.Films;

/// <summary>The Wikidata facts entity and its embedding-text builder (ADR-0017): deterministic, Wikidata-only.</summary>
public sealed class FilmFactsTests
{
    private static readonly string[] TmdbFieldNames = ["Title", "Overview", "PosterPath", "Popularity", "Tagline"];

    private static readonly DateTimeOffset Now = new(2026, 11, 13, 3, 0, 0, TimeSpan.Zero);

    private static WikidataFacts FightClub() => new(
        "Q190050",
        ["thriller film", "drama film"],
        ["alter ego"],
        ["David Fincher"],
        ["Brad Pitt", "Edward Norton"],
        ["United States"],
        ["English"],
        1999,
        139,
        ["Jupiter Award"]);

    [Fact]
    public void BuildEmbeddingText_WritesOneLabelledLinePerFact_InAFixedOrder() =>
        FilmFacts.BuildEmbeddingText(FightClub()).ShouldBe("""
            Genres: drama film, thriller film
            Subjects: alter ego
            Directed by: David Fincher
            Starring: Brad Pitt, Edward Norton
            Countries: United States
            Languages: English
            Year: 1999
            Runtime: 139 minutes
            Awards: Jupiter Award
            """.ReplaceLineEndings("\n"));

    [Fact]
    public void BuildEmbeddingText_IgnoresInputOrderAndDuplicates_ButKeepsCastRanking()
    {
        var shuffled = FightClub() with
        {
            Genres = ["drama film", " thriller film ", "Drama film"],
            Countries = ["United States", "united states"],
        };

        FilmFacts.BuildEmbeddingText(shuffled).ShouldBe(FilmFacts.BuildEmbeddingText(FightClub()));
        FilmFacts.BuildEmbeddingText(FightClub() with { Cast = ["Edward Norton", "Brad Pitt"] })
            .ShouldContain("Starring: Edward Norton, Brad Pitt");
    }

    [Fact]
    public void BuildEmbeddingText_SkipsEmptyFactsAndImpossibleNumbers()
    {
        var sparse = new WikidataFacts("Q1", ["drama film"], [], [], [], [], [], 1200, -5, []);

        FilmFacts.BuildEmbeddingText(sparse).ShouldBe("Genres: drama film");
    }

    [Fact]
    public void Create_CapsLongLists()
    {
        var cast = Enumerable.Range(1, 30).Select(i => $"Actor {i:D2}").ToList();

        var facts = FilmFacts.Create(Guid.NewGuid(), FightClub() with { Cast = cast }, Now);

        facts.Cast.Length.ShouldBe(FilmFacts.MaxCast);
        facts.Cast[0].ShouldBe("Actor 01", "cast keeps its ranking, best-known first");
    }

    [Theory]
    [InlineData("")]
    [InlineData("190050")]
    [InlineData("P4947")]
    [InlineData("Q12a")]
    public void Create_RejectsAnythingButAQid(string wikidataId) =>
        Should.Throw<DomainException>(() => FilmFacts.Create(Guid.NewGuid(), FightClub() with { WikidataId = wikidataId }, Now))
            .Code.ShouldBe(ErrorCodes.Validation);

    [Fact]
    public void Update_ReportsWhetherTheEmbeddingTextChanged()
    {
        var facts = FilmFacts.Create(Guid.NewGuid(), FightClub(), Now);

        facts.Update(FightClub(), Now.AddDays(31)).ShouldBeFalse();
        facts.FetchedAt.ShouldBe(Now.AddDays(31));
        facts.Update(FightClub() with { MainSubjects = ["insomnia"] }, Now.AddDays(62)).ShouldBeTrue();
        facts.EmbeddingText.ShouldContain("Subjects: insomnia");
    }

    /// <summary>
    /// ADR-0017: TMDB-derived text must never reach the embedding model. The builder's only input is Wikidata's record, and
    /// neither it nor the entity can see a <see cref="Film"/> (where title, overview, poster and TMDB genres live).
    /// </summary>
    [Fact]
    public void EmbeddingText_CanOnlyBeBuiltFromWikidataFacts()
    {
        typeof(FilmFacts).GetMethod(nameof(FilmFacts.BuildEmbeddingText))!.GetParameters()
            .Select(p => p.ParameterType).ShouldBe([typeof(WikidataFacts)]);

        Type[] wikidataOnly = [typeof(FilmFacts), typeof(WikidataFacts)];
        foreach (var type in wikidataOnly)
        {
            var members = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            members.ShouldNotContain(p => p.PropertyType == typeof(Film), $"{type.Name} must not reach TMDB data through Film");
            members.Select(p => p.Name).Intersect(TmdbFieldNames).ShouldBeEmpty($"{type.Name} must not carry TMDB fields");
        }
    }
}
