namespace Ithaca.Application.Library;

/// <summary>A My Cinema shelf. There is no "watching" shelf (A9).</summary>
public enum LibraryShelf
{
    Liked,
    Watchlist,
    Seen,
}

/// <summary>Order of a shelf.</summary>
public enum LibrarySort
{
    Recent,
    Title,
    Rating,
}
