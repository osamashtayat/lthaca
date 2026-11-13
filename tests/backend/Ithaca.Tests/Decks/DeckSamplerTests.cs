using Ithaca.Application.Decks;
using Ithaca.Domain.Decks;

namespace Ithaca.Tests.Decks;

/// <summary>BE-08: the sampler's variety and how batches adapt to signals, without a database.</summary>
public sealed class DeckSamplerTests
{
    private static readonly string[] Genres = ["Drama", "Comedy", "Horror", "Action", "Animation"];

    /// <summary>5 genres × 4 decades × 5 films; popularity falls with the index, and by 30% per film within a group.</summary>
    private static List<SamplerFilm> Catalog(Func<int, float[]?>? embedding = null)
    {
        var films = new List<SamplerFilm>();
        var i = 0;
        foreach (var genre in Genres)
        {
            foreach (var decade in new[] { 1970, 1990, 2000, 2020 })
            {
                for (var k = 0; k < 5; k++, i++)
                {
                    films.Add(new SamplerFilm(Guid.NewGuid(), [genre], decade + k, 90 + (k * 15), (1000 - i) * Math.Pow(0.7, k), embedding?.Invoke(i)));
                }
            }
        }

        return films;
    }

    private static SamplerFilm[] Picked(IEnumerable<SamplerFilm> catalog, IReadOnlyList<Guid> ids) =>
        ids.Select(id => catalog.Single(f => f.Id == id)).ToArray();

    [Fact]
    public void Pick_WithoutSignals_IsVariedAcrossGenresAndDecades()
    {
        var catalog = Catalog();

        var picked = Picked(catalog, DeckSampler.Pick(catalog, [], 10, new Random(1)));

        picked.Length.ShouldBe(10);
        picked.Select(f => f.Id).Distinct().Count().ShouldBe(10);
        picked.Select(f => f.Genres[0]).Distinct().Count().ShouldBe(5, "every main genre in one batch");
        picked.Select(f => f.ReleaseYear / 10).Distinct().Count().ShouldBeGreaterThanOrEqualTo(3, "several decades");
        picked.GroupBy(f => (f.Genres[0], f.ReleaseYear / 10)).Max(g => g.Count()).ShouldBe(1, "one per (genre, decade) group before repeating");
    }

    [Fact]
    public void Pick_WithoutSignals_PrefersPopularFilmsWithinAGroup()
    {
        var catalog = Catalog();

        var picked = Picked(catalog, DeckSampler.Pick(catalog, [], 20, new Random(2)));

        // 20 picks = one per (genre, decade) group, and each group's most popular film is its first year.
        picked.ShouldAllBe(f => f.ReleaseYear % 10 == 0, "the ±10% jitter never beats a 30% popularity gap");
    }

    [Fact]
    public void Pick_NeverReturnsMoreThanItHas_OrDuplicates()
    {
        var catalog = Catalog().Take(7).ToList();

        var picked = DeckSampler.Pick(catalog, [new DeckSignal(["Drama"], 100, null, 1)], 10, new Random(3));

        picked.Count.ShouldBe(7);
        picked.Distinct().Count().ShouldBe(7);
    }

    [Fact]
    public void Pick_AfterRightSwipesOnHorror_DealsMostlyHorror_ButKeepsExploring()
    {
        var catalog = Catalog();
        var horror = Enumerable.Repeat(new DeckSignal(["Horror"], 100, null, 1), 3).ToList();

        var picked = Picked(catalog, DeckSampler.Pick(catalog, horror, 10, new Random(4)));

        picked.Count(f => f.Genres[0] == "Horror").ShouldBe(7, $"{DeckSampler.ExploitShare:P0} by fit");
        picked.Count(f => f.Genres[0] != "Horror").ShouldBe(3, "the rest still explores");
    }

    [Fact]
    public void Pick_AfterLeftSwipes_AvoidsThatGenre()
    {
        var catalog = Catalog();
        var signals = new List<DeckSignal> { new(["Comedy"], 100, null, -1), new(["Comedy"], 100, null, -1), new(["Drama"], 100, null, 1) };

        var picked = Picked(catalog, DeckSampler.Pick(catalog, signals, 10, new Random(5)));

        picked.Take(7).ShouldNotContain(f => f.Genres[0] == "Comedy");
    }

    [Fact]
    public void Pick_UsesEmbeddings_WhenGenresDontDecide()
    {
        // Two directions in a 2-d "embedding space": even films point one way, odd films the other.
        var catalog = Catalog(i => i % 2 == 0 ? [1f, 0f] : [0f, 1f]);
        var likesEven = new List<DeckSignal> { new([], null, [1f, 0f], 1) };

        var picked = Picked(catalog, DeckSampler.Pick(catalog, likesEven, 10, new Random(6)));

        picked.Take(7).ShouldAllBe(f => f.Embedding![0] == 1f);
    }

    [Fact]
    public void Fit_PrefersTheRuntimeOfLikedFilms()
    {
        var profile = DeckSampler.ProfileOf([new DeckSignal(["Drama"], 95, null, 1)]);
        var short_ = new SamplerFilm(Guid.NewGuid(), ["Drama"], 2000, 95, 10, null);
        var long_ = new SamplerFilm(Guid.NewGuid(), ["Drama"], 2000, 170, 10, null);

        DeckSampler.Fit(short_, profile, 10).ShouldBeGreaterThan(DeckSampler.Fit(long_, profile, 10));
    }

    [Fact]
    public void RankByGenreFit_OrdersByFitThenPopularity()
    {
        var drama = new SamplerFilm(Guid.NewGuid(), ["Drama"], 2000, 100, 1, null);
        var popularComedy = new SamplerFilm(Guid.NewGuid(), ["Comedy"], 2000, 100, 99, null);
        var comedy = new SamplerFilm(Guid.NewGuid(), ["Comedy"], 2000, 100, 5, null);
        var signals = new List<DeckSignal> { new(["Comedy"], null, null, 1), new(["Comedy"], null, null, 1), new(["Drama"], null, null, 1) };

        var ranked = DeckSampler.RankByGenreFit([drama, comedy, popularComedy], signals);

        ranked.ShouldBe([popularComedy.Id, comedy.Id, drama.Id]);
    }

    [Theory]
    [InlineData(SwipeDirection.Right, null, 1)]
    [InlineData(SwipeDirection.Left, null, -1)]
    [InlineData(SwipeDirection.Up, SeenVerdict.Loved, 1.5)]
    [InlineData(SwipeDirection.Up, SeenVerdict.NotForMe, -1)]
    [InlineData(SwipeDirection.Up, null, 0.3)]
    public void OfSwipe_Weights(SwipeDirection direction, SeenVerdict? verdict, double weight) =>
        DeckSignals.OfSwipe(direction, verdict).ShouldBe(weight);

    [Fact]
    public void OfLibrary_LikedAndHighRatingCount_SeenAloneDoesNot()
    {
        DeckSignals.OfLibrary(liked: true, onWatchlist: false, rating: 5).ShouldBe(2 * DeckSignals.LibraryFactor);
        DeckSignals.OfLibrary(liked: false, onWatchlist: false, rating: 1).ShouldBe(-1 * DeckSignals.LibraryFactor);
        DeckSignals.OfLibrary(liked: false, onWatchlist: false, rating: null).ShouldBe(0);
    }
}
