using Ithaca.Api.Auth;
using Ithaca.Api.Errors;
using Ithaca.Api.RateLimiting;
using Ithaca.Application.Decks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ithaca.Api.Controllers;

/// <summary>Solo decks and swipes, incl. by mood (api-contract.md §5). Signed-in users only.</summary>
[ApiController]
[Route("api/decks")]
[Tags("Decks")]
[Authorize(Policy = AuthPolicies.User)]
public sealed class DecksController(DeckService decks) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.DeckCreate)]
    [ProducesResponseType<DeckDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<DeckDto>> Create(CreateDeckRequest request, CancellationToken ct) =>
        this.ToCreatedResult(await decks.CreateAsync(request, ct), deck => $"/api/decks/{deck.Id}");

    [HttpGet("{deckId:guid}")]
    [ProducesResponseType<DeckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeckDto>> Get(Guid deckId, CancellationToken ct) =>
        this.ToActionResult(await decks.GetAsync(deckId, ct));

    [HttpPost("{deckId:guid}/items")]
    [ProducesResponseType<IReadOnlyList<DeckItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DeckItemDto>>> AddItems(Guid deckId, CancellationToken ct) =>
        this.ToActionResult(await decks.AddItemsAsync(deckId, ct));

    [HttpPost("{deckId:guid}/swipes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Swipe(Guid deckId, SwipeRequest request, CancellationToken ct) =>
        this.ToActionResult(await decks.SwipeAsync(deckId, request, ct));

    [HttpGet("{deckId:guid}/top")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [ProducesResponseType<DeckTopDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<DeckTopDto>> Top(Guid deckId, CancellationToken ct) =>
        this.ToActionResult(await decks.GetTopAsync(deckId, ct));
}
