namespace Ithaca.Application.Decks;

/// <summary>A candidate film for a deck, as the sampler sees it (our own catalog data only).</summary>
public sealed record SamplerFilm(Guid Id, IReadOnlyList<string> Genres, int? ReleaseYear, int? RuntimeMinutes, double Popularity, float[]? Embedding);

/// <summary>
/// One thing we know about the user's taste: a film they reacted to and how much it counts (positive = more like this,
/// negative = less). Built from the deck's swipes and the user's library (<see cref="DeckSignals"/>).
/// </summary>
public sealed record DeckSignal(IReadOnlyList<string> Genres, int? RuntimeMinutes, float[]? Embedding, double Weight);

/// <summary>
/// Picks the next cards of a "Just swipe" deck. Pure (no I/O), so variety and adaptation are unit-tested.
/// <list type="bullet">
/// <item>No signals yet: the <b>sampler</b>, varied by main genre and decade, more popular first.</item>
/// <item>With signals: <see cref="ExploitShare"/> of the batch is the best <see cref="Fit"/> (genres, runtime, embeddings
/// when both sides have them, a little popularity); the rest still comes from the sampler, so the deck keeps exploring.</item>
/// </list>
/// A little randomness (<paramref name="random"/>) keeps two decks from being identical.
/// </summary>
public static class DeckSampler
{
    /// <summary>Share of an adapted batch chosen by fit; the rest explores.</summary>
    public const double ExploitShare = 0.7;

    private const double GenreWeight = 0.45;
    private const double EmbeddingWeight = 0.35;
    private const double RuntimeWeight = 0.15;
    private const double PopularityWeight = 0.15;
    private const double Jitter = 0.05;

    /// <summary>Relative noise on popularity in the sampler (±10%).</summary>
    private const double SamplerJitter = 0.1;

    /// <summary>Runtime difference (minutes) at which the runtime fit reaches 0.</summary>
    private const double RuntimeTolerance = 60;

    public static IReadOnlyList<Guid> Pick(IReadOnlyList<SamplerFilm> candidates, IReadOnlyList<DeckSignal> signals, int count, Random random)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(random);
        if (count <= 0 || candidates.Count == 0)
        {
            return [];
        }

        var picked = new List<Guid>(count);
        if (signals.Count > 0)
        {
            var profile = ProfileOf(signals);
            var maxPopularity = MaxPopularity(candidates);
            var exploit = (int)Math.Ceiling(count * ExploitShare);
            picked.AddRange(candidates
                .Select(c => (c.Id, Score: Fit(c, profile, maxPopularity) + (random.NextDouble() * Jitter)))
                .OrderByDescending(x => x.Score)
                .Take(exploit)
                .Select(x => x.Id));
        }

        var taken = picked.ToHashSet();
        picked.AddRange(Varied(candidates.Where(c => !taken.Contains(c.Id)).ToList(), count - picked.Count, random));
        return picked;
    }

    /// <summary>
    /// Orders films by genre fit with the signals, then popularity (the top 3 until BE-13). Ties end on the given order.
    /// </summary>
    public static IReadOnlyList<Guid> RankByGenreFit(IReadOnlyList<SamplerFilm> films, IReadOnlyList<DeckSignal> signals)
    {
        ArgumentNullException.ThrowIfNull(films);
        var profile = ProfileOf(signals);
        return films
            .Select((f, i) => (f.Id, Fit: GenreAffinity(f.Genres, profile.Genres), f.Popularity, Index: i))
            .OrderByDescending(x => Math.Round(x.Fit, 6))
            .ThenByDescending(x => x.Popularity)
            .ThenBy(x => x.Index)
            .Select(x => x.Id)
            .ToList();
    }

    /// <summary>How well a film fits the profile: about −1 (unlike anything liked) to 1 (very like it).</summary>
    public static double Fit(SamplerFilm film, SwipeProfile profile, double maxPopularity)
    {
        ArgumentNullException.ThrowIfNull(film);
        ArgumentNullException.ThrowIfNull(profile);
        var genre = GenreAffinity(film.Genres, profile.Genres);
        var runtime = profile.PreferredRuntime is { } preferred && film.RuntimeMinutes is { } minutes
            ? 1 - Math.Min(Math.Abs(minutes - preferred) / RuntimeTolerance, 1)
            : 0.5;
        var embedding = profile.Centroid is { } centroid && film.Embedding is { } vector && vector.Length == centroid.Length
            ? Cosine(vector, centroid)
            : 0;
        var popularity = maxPopularity > 0 ? film.Popularity / maxPopularity : 0;
        return (GenreWeight * genre) + (EmbeddingWeight * embedding) + (RuntimeWeight * runtime) + (PopularityWeight * popularity);
    }

    /// <summary>
    /// What the signals add up to: a weight per genre (a film's weight is shared between its genres), the runtime of liked
    /// films, and the direction in embedding space (liked minus half of disliked) when signals have embeddings.
    /// </summary>
    public static SwipeProfile ProfileOf(IReadOnlyList<DeckSignal> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);
        var genres = new Dictionary<string, double>(StringComparer.Ordinal);
        double runtimeSum = 0, runtimeWeight = 0;
        float[]? centroid = null;
        foreach (var signal in signals)
        {
            if (signal.Genres.Count > 0)
            {
                var share = signal.Weight / Math.Sqrt(signal.Genres.Count);
                foreach (var genre in signal.Genres)
                {
                    genres[genre] = genres.GetValueOrDefault(genre) + share;
                }
            }

            if (signal.Weight > 0 && signal.RuntimeMinutes is { } minutes)
            {
                runtimeSum += minutes * signal.Weight;
                runtimeWeight += signal.Weight;
            }

            if (signal.Embedding is { Length: > 0 } vector)
            {
                centroid ??= new float[vector.Length];
                if (vector.Length == centroid.Length)
                {
                    var factor = (float)(signal.Weight > 0 ? signal.Weight : signal.Weight / 2);
                    for (var i = 0; i < vector.Length; i++)
                    {
                        centroid[i] += vector[i] * factor;
                    }
                }
            }
        }

        var max = genres.Count == 0 ? 0 : genres.Values.Max(Math.Abs);
        var normalised = max > 0 ? genres.ToDictionary(g => g.Key, g => g.Value / max, StringComparer.Ordinal) : genres;
        return new SwipeProfile(
            normalised,
            runtimeWeight > 0 ? runtimeSum / runtimeWeight : null,
            centroid is not null && centroid.Any(v => v != 0) ? centroid : null);
    }

    /// <summary>
    /// The sampler: deals one film per main genre in turn (genres with the most popular films first), and within a genre one
    /// per decade in turn, each decade's most popular film first. Popularity gets ±<see cref="SamplerJitter"/> noise so two
    /// decks differ without letting obscure films jump ahead.
    /// </summary>
    private static List<Guid> Varied(List<SamplerFilm> candidates, int count, Random random)
    {
        if (count <= 0)
        {
            return [];
        }

        var genres = candidates
            .Select(c => (Film: c, Score: c.Popularity * (1 + (SamplerJitter * ((random.NextDouble() * 2) - 1)))))
            .GroupBy(x => x.Film.Genres.Count > 0 ? x.Film.Genres[0] : "")
            .Select(genre => genre
                .GroupBy(x => x.Film.ReleaseYear / 10)
                .Select(decade => decade.OrderByDescending(x => x.Score).ToList())
                .OrderByDescending(decade => decade[0].Score)
                .Select(decade => new Queue<(SamplerFilm Film, double Score)>(decade))
                .ToList())
            .OrderByDescending(decades => decades[0].Peek().Score)
            .Select(decades => new GenreDealer(decades))
            .ToList();

        var picked = new List<Guid>(count);
        while (picked.Count < count && genres.Count > 0)
        {
            foreach (var genre in genres.ToList())
            {
                if (picked.Count == count)
                {
                    break;
                }

                picked.Add(genre.Next());
                if (genre.IsEmpty)
                {
                    genres.Remove(genre);
                }
            }
        }

        return picked;
    }

    private static double GenreAffinity(IReadOnlyList<string> genres, IReadOnlyDictionary<string, double> weights) =>
        genres.Count == 0 || weights.Count == 0 ? 0 : genres.Average(g => weights.GetValueOrDefault(g));

    private static double MaxPopularity(IReadOnlyList<SamplerFilm> films) => films.Count == 0 ? 0 : films.Max(f => f.Popularity);

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}

/// <summary>One genre's films for the sampler: deals its decades in turn, each decade's best first.</summary>
internal sealed class GenreDealer(List<Queue<(SamplerFilm Film, double Score)>> decades)
{
    private int _next;

    public bool IsEmpty => decades.Count == 0;

    public Guid Next()
    {
        _next %= decades.Count;
        var decade = decades[_next];
        var film = decade.Dequeue().Film;
        if (decade.Count == 0)
        {
            decades.RemoveAt(_next);
        }
        else
        {
            _next++;
        }

        return film.Id;
    }
}

/// <summary>What <see cref="DeckSampler.ProfileOf" derives from the signals; genre weights are scaled to −1…1.</summary>
public sealed record SwipeProfile(IReadOnlyDictionary<string, double> Genres, double? PreferredRuntime, float[]? Centroid);
