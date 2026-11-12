using Ithaca.Application.Common;

namespace Ithaca.Application.Films;

/// <summary>Trailers from TMDB on first request, then cached (ADR-0003, ADR-0017). Contract stub until BE-07.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
public sealed class FilmTrailerService
{
    public Task<Result<FilmTrailerDto>> GetAsync(int tmdbId, CancellationToken ct) => ContractStub.Of<FilmTrailerDto>();
}
