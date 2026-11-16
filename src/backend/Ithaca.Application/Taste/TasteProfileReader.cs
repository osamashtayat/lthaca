using Ithaca.Application.Common;
using Ithaca.Domain.Taste;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ithaca.Application.Taste;

/// <summary>
/// Readiness and <see cref="TasteProfile"/>s from users' libraries (collaborator of <see cref="TasteService"/>,
/// <c>DeckService</c>, <c>RoomService</c> and <c>RoomDeckService</c>, not a use-case service). Every <c>UserFilm</c> counts,
/// imported or not (ADR-0024 decision 9). Only Wikidata-based film vectors are used (ADR-0017); no text is embedded here.
/// </summary>
public sealed class TasteProfileReader(IAppDbContext db, IOptions<TasteOptions> options)
{
    /// <summary>Most recently changed liked or rated films a profile is built from.</summary>
    private const int ProfileFilmLimit = 1_000;

    public int SignalsNeeded => options.Value.SignalsNeeded;

    /// <summary>Films in the user's library with a positive signal: liked ("loved it" sets liked) or rated ≥ 3.5 (A16).</summary>
    public Task<int> SignalCountAsync(Guid userId, CancellationToken ct) =>
        db.UserFilms.CountAsync(u => u.UserId == userId && (u.Liked || u.RatingHalfStars >= TasteProfile.PositiveRatingHalfStars), ct);

    /// <summary>True when the user has at least <see cref="SignalsNeeded"/> positive signals (api-contract.md §12).</summary>
    public async Task<bool> IsReadyAsync(Guid userId, CancellationToken ct) => await SignalCountAsync(userId, ct) >= SignalsNeeded;

    /// <summary>
    /// The user's taste from their liked and rated films that have a vector (<see cref="TasteProfile.WeightOf"/>), using the
    /// embedding model most of those films share. Null when none count.
    /// </summary>
    public async Task<TasteProfile?> ProfileAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.UserFilms.AsNoTracking()
            .Where(u => u.UserId == userId && (u.Liked || u.RatingHalfStars != null))
            .Join(db.Films.AsNoTracking().Where(f => f.EmbeddingModel != null), u => u.FilmId, f => f.Id, (u, f) => new
            {
                u.Liked,
                u.RatingHalfStars,
                u.UpdatedAt,
                f.Embedding,
                f.EmbeddingModel,
            })
            .OrderByDescending(x => x.UpdatedAt)
            .Take(ProfileFilmLimit)
            .ToListAsync(ct);

        var model = rows.Where(r => r.Embedding is not null)
            .GroupBy(r => r.EmbeddingModel!, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        return model is null
            ? null
            : TasteProfile.Build(model.Select(r => new TasteFilm(r.Embedding!, TasteProfile.WeightOf(r.Liked, r.RatingHalfStars))), model.Key);
    }

    /// <summary>A group's taste: the <see cref="TasteProfile.Blend"/> of the users' profiles; users without one add nothing.</summary>
    public async Task<TasteProfile?> BlendAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        var profiles = new List<TasteProfile>(userIds.Count);
        foreach (var userId in userIds.Distinct())
        {
            if (await ProfileAsync(userId, ct) is { } profile)
            {
                profiles.Add(profile);
            }
        }

        return TasteProfile.Blend(profiles);
    }
}
