using Ithaca.Application.Ai;
using Ithaca.Application.Decks;
using Ithaca.Application.Taste;
using Ithaca.Domain.Library;
using Ithaca.Domain.Taste;
using Ithaca.Infrastructure.Ai;

namespace Ithaca.Tests.Taste;

/// <summary>BE-14: the taste vector, its weights on the half-star scale, the room blend, and the receipt; no database.</summary>
public sealed class TasteProfileTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<float[]>> FakeVectorsAsync(params string[] texts) =>
        (await new FakeEmbeddingClient().EmbedAsync(texts, EmbeddingInputType.Document, Ct)).Value;

    [Theory]
    [InlineData(false, null, 0)]
    [InlineData(true, null, 1)]
    [InlineData(false, (short)10, 1)]     // 5 stars
    [InlineData(false, (short)8, 0.5)]    // 4 stars
    [InlineData(false, (short)7, 0.25)]   // 3.5 stars
    [InlineData(false, (short)6, 0)]      // 3 stars
    [InlineData(false, (short)4, -0.5)]   // 2 stars
    [InlineData(false, (short)2, -1)]     // 1 star
    [InlineData(false, (short)1, -1)]     // 0.5 stars, clamped
    [InlineData(true, (short)2, 0)]       // liked but rated 1
    [InlineData(true, (short)10, 2)]
    public void WeightOf_UsesTheHalfStarScale(bool liked, short? halfStars, double expected) =>
        TasteProfile.WeightOf(liked, halfStars).ShouldBe(expected, 1e-9);

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, (short)7, true)]
    [InlineData(false, (short)6, false)]
    [InlineData(false, null, false)]
    public void IsPositiveSignal_IsLikedOrRatedAtLeast3Point5(bool liked, short? halfStars, bool expected)
    {
        var row = UserFilm.Create(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        row.Set(liked, onWatchlist: false, seen: true, halfStars / 2m, DateTimeOffset.UtcNow);

        TasteProfile.IsPositiveSignal.Compile()(row).ShouldBe(expected);
    }

    [Fact]
    public async Task Build_IsTheNormalisedWeightedSum_OfFakeEmbeddings()
    {
        var v = await FakeVectorsAsync("loved", "liked", "hated");

        var profile = TasteProfile.Build([new TasteFilm(v[0], 2), new TasteFilm(v[1], 1), new TasteFilm(v[2], -1)], "fake")!;

        var expected = Enumerable.Range(0, v[0].Length).Select(i => (2.0 * v[0][i]) + v[1][i] - v[2][i]).ToArray();
        var norm = Math.Sqrt(expected.Sum(x => x * x));
        profile.EmbeddingModel.ShouldBe("fake");
        profile.Vector.Length.ShouldBe(v[0].Length);
        for (var i = 0; i < expected.Length; i++)
        {
            profile.Vector[i].ShouldBe((float)(expected[i] / norm), 1e-5f);
        }

        Math.Sqrt(profile.Vector.Sum(x => (double)x * x)).ShouldBe(1, 1e-5);
        profile.SimilarityTo(v[0]).ShouldBeGreaterThan(profile.SimilarityTo(v[1]));
        profile.SimilarityTo(v[2]).ShouldBeLessThan(0);
    }

    [Fact]
    public async Task Build_IsDeterministic_AndNullWhenNothingCounts()
    {
        var v = await FakeVectorsAsync("a", "b");
        TasteFilm[] films = [new(v[0], 1), new(v[1], 0.5)];

        TasteProfile.Build(films, "fake")!.Vector.ShouldBe(TasteProfile.Build(films, "fake")!.Vector);
        TasteProfile.Build([], "fake").ShouldBeNull();
        TasteProfile.Build([new TasteFilm(v[0], 0)], "fake").ShouldBeNull();
        TasteProfile.Build([new TasteFilm(v[0], 1), new TasteFilm(v[0], -1)], "fake").ShouldBeNull();
    }

    [Fact]
    public async Task Blend_CountsEveryMemberTheSame_AndKeepsOneModel()
    {
        var v = await FakeVectorsAsync("ada", "omar", "other model");
        var ada = TasteProfile.Build([new TasteFilm(v[0], 5)], "fake")!;
        var omar = TasteProfile.Build([new TasteFilm(v[1], 1)], "fake")!;
        var stray = TasteProfile.Build([new TasteFilm(v[2], 1)], "voyage-4")!;

        var blend = TasteProfile.Blend([ada, omar, stray])!;

        blend.EmbeddingModel.ShouldBe("fake");
        blend.SimilarityTo(v[0]).ShouldBe(blend.SimilarityTo(v[1]), 1e-5, "weights inside a profile don't make a member count more");
        TasteProfile.Blend([]).ShouldBeNull();
    }

    [Fact]
    public async Task Closeness_ScalesSimilarityAcrossCandidates_AndPickLeansTowardsTheTaste()
    {
        var v = await FakeVectorsAsync("taste", "far 1", "far 2", "far 3");
        var taste = TasteProfile.Build([new TasteFilm(v[0], 1)], "fake")!;
        var near = Enumerable.Range(0, 7).Select(i => new SamplerFilm(Guid.NewGuid(), ["Drama"], 2000, 100, 1, v[0])).ToList();
        var far = Enumerable.Range(0, 30).Select(i => new SamplerFilm(Guid.NewGuid(), ["Drama"], 1970 + i, 100, 100, v[1 + (i % 3)])).ToList();
        var bare = new SamplerFilm(Guid.NewGuid(), ["Drama"], 2000, 100, 1000, null);
        List<SamplerFilm> candidates = [.. far, bare, .. near];

        var closeness = DeckSampler.Closeness(candidates, taste);
        var picked = DeckSampler.Pick(candidates, [], 10, new Random(3), taste);

        closeness.ContainsKey(bare.Id).ShouldBeFalse("a film without a vector gets nothing");
        near.ShouldAllBe(f => Math.Abs(closeness[f.Id] - 1) < 1e-9);
        closeness.Values.Min().ShouldBe(0);
        picked.Take(7).ShouldBe(near.Select(f => f.Id), ignoreOrder: true);
        DeckSampler.Closeness(candidates, null).ShouldBeEmpty();
    }

    [Fact]
    public void Receipt_TopGenres_AreSharesOfGenreMentions()
    {
        string[][] liked = [["Drama"], ["Drama", "Comedy"], ["Drama", "Crime"], ["Comedy"], ["Horror"]];

        var top = TasteReceipt.TopGenres(liked);

        // 7 mentions: Drama 3, Comedy 2, Crime 1, Horror 1 (Crime before Horror by name).
        top.ShouldBe([new TasteGenreDto("Drama", 43), new TasteGenreDto("Comedy", 29), new TasteGenreDto("Crime", 14)]);
        TasteReceipt.TopGenres([]).ShouldBeEmpty();
    }

    [Fact]
    public void Receipt_NotForYou_NeedsTwoTurnedDownFilms_AndNeverATopGenre()
    {
        string[][] liked = [["Drama"], ["Drama"], ["Comedy"], ["Thriller"], ["Thriller"], ["Thriller"]];
        string[][] turnedDown = [["Horror"], ["Horror", "Thriller"], ["Western"], ["Drama", "Musical"], ["Musical"], ["Thriller"]];
        var top = TasteReceipt.TopGenres(liked); // Thriller, Drama, Comedy

        var notForYou = TasteReceipt.NotForYou(liked, turnedDown, top);

        notForYou.ShouldBe(["Horror", "Musical"]); // Western once only; Thriller and Drama are top genres
    }
}
