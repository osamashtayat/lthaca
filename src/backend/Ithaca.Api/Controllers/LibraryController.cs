using Ithaca.Api.Errors;
using Ithaca.Api.OpenApi;
using Ithaca.Application.Common;
using Ithaca.Application.Library;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>My Cinema: the caller's <c>UserFilm</c>s (api-contract.md §7). Signed-in users only.</summary>
[ApiController]
[Route("api/library")]
[Tags("Library")]
public sealed class LibraryController(LibraryService library) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<UserFilmDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult<PagedResult<UserFilmDto>>> List([FromQuery] LibraryListRequest request, CancellationToken ct) =>
        this.ToActionResult(await library.ListAsync(request, ct));

    [HttpGet("summary")]
    [ProducesResponseType<LibrarySummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult<LibrarySummaryDto>> Summary(CancellationToken ct) =>
        this.ToActionResult(await library.GetSummaryAsync(ct));

    [HttpGet("{tmdbId:int:min(1)}")]
    [ProducesResponseType<UserFilmDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult<UserFilmDto>> Get(int tmdbId, CancellationToken ct) =>
        this.ToActionResult(await library.GetAsync(tmdbId, ct));

    [HttpPut("{tmdbId:int:min(1)}")]
    [ProducesResponseType<UserFilmDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult<UserFilmDto>> Update(int tmdbId, UpdateUserFilmRequest request, CancellationToken ct) =>
        this.ToActionResult(await library.UpdateAsync(tmdbId, request, ct));

    [HttpPut("{tmdbId:int:min(1)}/watchlist")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult> AddToWatchlist(int tmdbId, CancellationToken ct) =>
        this.ToActionResult(await library.AddToWatchlistAsync(tmdbId, ct));

    [HttpDelete("{tmdbId:int:min(1)}/watchlist")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult> RemoveFromWatchlist(int tmdbId, CancellationToken ct) =>
        this.ToActionResult(await library.RemoveFromWatchlistAsync(tmdbId, ct));
}
