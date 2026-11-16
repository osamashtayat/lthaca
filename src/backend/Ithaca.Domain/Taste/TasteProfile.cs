namespace Ithaca.Domain.Taste;

/// <summary>One film behind a <see cref="TasteProfile"/>: its embedding and how much it counts (positive = more like this).</summary>
public sealed record TasteFilm(float[] Embedding, double Weight);

/// <summary>
/// A user's (or a room's) taste (glossary: <c>TasteProfile</c>): a unit vector in the film-embedding space of one model, and
/// how many films built it. Made from the films a user liked and rated (<see cref="WeightOf"/>), imported or not (ADR-0024
/// decision 9); a room's is the <see cref="Blend"/> of its members'. Computed when needed, not stored (BE-14).
/// </summary>
public sealed record TasteProfile(float[] Vector, string EmbeddingModel, int FilmCount)
{
    /// <summary>A rating of at least 3.5 stars (7 half stars) is a positive signal for readiness (A16).</summary>
    public const short PositiveRatingHalfStars = 7;

    /// <summary>A rating of at most 2 stars (4 half stars) says "not for me" (the receipt's <c>notForYou</c>).</summary>
    public const short LowRatingHalfStars = 4;

    /// <summary>The rating that says nothing either way: 3 stars (6 half stars).</summary>
    private const double NeutralHalfStars = 6;

    /// <summary>Half stars between neutral and a full ±1 (2 stars: 5 stars is +1, 1 star is −1).</summary>
    private const double HalfStarsPerUnit = 4;

    /// <summary>True when a library row is a positive signal (A16): liked ("loved it" sets liked) or rated ≥ 3.5.</summary>
    public static bool IsPositive(bool liked, short? ratingHalfStars) => liked || ratingHalfStars >= PositiveRatingHalfStars;

    /// <summary>
    /// How much a library row counts, on the 0.5–5 scale (ADR-0024): liked +1; a rating adds (rating − 3) / 2, clamped to
    /// ±1 (5 → +1, 4 → +0.5, 3 → 0, 2 → −0.5, 1 and 0.5 → −1). Seen alone or the watchlist say nothing about taste: 0.
    /// </summary>
    public static double WeightOf(bool liked, short? ratingHalfStars)
    {
        double weight = liked ? 1 : 0;
        if (ratingHalfStars is { } h)
        {
            weight += Math.Clamp((h - NeutralHalfStars) / HalfStarsPerUnit, -1, 1);
        }

        return weight;
    }

    /// <summary>
    /// The weighted sum of the films' vectors (all from <paramref name="embeddingModel"/>, same length), scaled to unit length.
    /// Null when nothing counts (no films, all weights 0, or the sum is the zero vector).
    /// </summary>
    public static TasteProfile? Build(IEnumerable<TasteFilm> films, string embeddingModel)
    {
        ArgumentNullException.ThrowIfNull(films);
        float[]? sum = null;
        var count = 0;
        foreach (var film in films)
        {
            if (film.Weight == 0 || film.Embedding.Length == 0)
            {
                continue;
            }

            sum ??= new float[film.Embedding.Length];
            if (film.Embedding.Length != sum.Length)
            {
                throw new ArgumentException("Every embedding of a taste profile must have the same length.", nameof(films));
            }

            for (var i = 0; i < sum.Length; i++)
            {
                sum[i] += (float)(film.Embedding[i] * film.Weight);
            }

            count++;
        }

        return sum is not null && Normalised(sum) is { } vector ? new TasteProfile(vector, embeddingModel, count) : null;
    }

    /// <summary>
    /// A room's taste ("Blend your histories"): every member's profile counts the same (the mean of their unit vectors, scaled
    /// to unit length). Only profiles of the model most of them use; null when there are none.
    /// </summary>
    public static TasteProfile? Blend(IReadOnlyList<TasteProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var model = profiles.GroupBy(p => p.EmbeddingModel, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (model is null)
        {
            return null;
        }

        var members = model.Where(p => p.Vector.Length == model.First().Vector.Length).ToList();
        return Build(members.Select(p => new TasteFilm(p.Vector, 1)), model.Key) is { } blended
            ? blended with { FilmCount = members.Sum(p => p.FilmCount) }
            : null;
    }

    /// <summary>Cosine similarity with a film's vector (−1…1); 0 when the lengths differ or a vector is zero.</summary>
    public double SimilarityTo(float[] embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Length != Vector.Length)
        {
            return 0;
        }

        double dot = 0, norm = 0;
        for (var i = 0; i < Vector.Length; i++)
        {
            dot += Vector[i] * embedding[i];
            norm += embedding[i] * embedding[i];
        }

        return norm == 0 ? 0 : dot / Math.Sqrt(norm); // Vector is unit length
    }

    private static float[]? Normalised(float[] vector)
    {
        var norm = Math.Sqrt(vector.Sum(v => (double)v * v));
        if (norm == 0 || double.IsNaN(norm))
        {
            return null;
        }

        return vector.Select(v => (float)(v / norm)).ToArray();
    }
}
