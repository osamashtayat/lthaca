using Ithaca.Api.Errors;
using Ithaca.Api.OpenApi;
using Ithaca.Application.Common;
using Ithaca.Application.Films;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>
/// REFERENCE SLICE: copy this shape for every endpoint. One action = bind the input, call ONE service method, map the
/// Result with <see cref="ResultExtensions"/>. No DbContext, no business <c>if</c>, no try/catch, no mapping.
/// </summary>
[ApiController]
[Route("api/films")]
[Tags("Films")]
public sealed class FilmsController(FilmQueryService films, FilmTrailerService trailers) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<FilmSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<FilmSummaryDto>>> Browse([FromQuery] FilmBrowseRequest request, CancellationToken ct) =>
        this.ToActionResult(await films.BrowseAsync(request, ct));

    [HttpGet("genres")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> Genres(CancellationToken ct) =>
        this.ToActionResult(await films.GetGenresAsync(ct));

    [HttpGet("{tmdbId:int:min(1)}")]
    [ProducesResponseType<FilmDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FilmDto>> Get(int tmdbId, CancellationToken ct) =>
        this.ToActionResult(await films.GetByTmdbIdAsync(tmdbId, ct));

    [HttpGet("{tmdbId:int:min(1)}/trailer")]
    [ProducesResponseType<FilmTrailerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ContractStub]
    public async Task<ActionResult<FilmTrailerDto>> Trailer(int tmdbId, CancellationToken ct) =>
        this.ToActionResult(await trailers.GetAsync(tmdbId, ct));
}
