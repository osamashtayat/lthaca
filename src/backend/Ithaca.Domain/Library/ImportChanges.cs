namespace Ithaca.Domain.Library;

/// <summary>
/// What merging one or more files' rows changed in My Cinema, as counts (§13 <c>ImportChangesDto</c>). For one film each
/// field is 0 or 1; the job adds them up.
/// </summary>
public readonly record struct ImportChanges(int NewFilm, int Seen, int Rated, int Liked, int Watchlist, int RatingKept)
{
    public static ImportChanges operator +(ImportChanges a, ImportChanges b) =>
        new(a.NewFilm + b.NewFilm, a.Seen + b.Seen, a.Rated + b.Rated, a.Liked + b.Liked, a.Watchlist + b.Watchlist, a.RatingKept + b.RatingKept);

    public static ImportChanges Add(ImportChanges a, ImportChanges b) => a + b;

    public bool Any => NewFilm + Seen + Rated + Liked + Watchlist > 0;
}
