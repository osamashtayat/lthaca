using Ithaca.Application.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Application.Tmdb;

/// <summary>The TMDB errors, defined once.</summary>
public static class TmdbErrors
{
    public static readonly Error Unavailable = new(ErrorCodes.TmdbUnavailable, "TMDB is unavailable, rate limited, or rejected the API key.");
}
