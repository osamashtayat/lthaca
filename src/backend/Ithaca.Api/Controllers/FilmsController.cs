using Ithaca.Api.Errors;
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
public sealed class FilmsController(FilmQueryService films) : ControllerBase
{
    [HttpGet("{tmdbId:int:min(1)}")]
    [ProducesResponseType<FilmDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FilmDto>> Get(int tmdbId, CancellationToken ct) =>
        this.ToActionResult(await films.GetByTmdbIdAsync(tmdbId, ct));
}
