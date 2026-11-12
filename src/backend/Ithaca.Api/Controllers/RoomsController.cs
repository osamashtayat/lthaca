using System.ComponentModel.DataAnnotations;
using Ithaca.Api.Auth;
using Ithaca.Api.Errors;
using Ithaca.Api.OpenApi;
using Ithaca.Application.Decks;
using Ithaca.Application.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>Group rooms and votes (api-contract.md §6, ADR-0015). Votes are HTTP; the hub only broadcasts.</summary>
[ApiController]
[Route("api/rooms")]
[Tags("Rooms")]
public sealed class RoomsController(RoomService rooms) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthPolicies.User)]
    [ProducesResponseType<RoomDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult<RoomDto>> Create(CreateRoomRequest request, CancellationToken ct) =>
        this.ToCreatedResult(await rooms.CreateAsync(request, ct), room => $"/api/rooms/{room.Code}");

    [HttpGet("{code}/preview")]
    [ProducesResponseType<RoomPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult<RoomPreviewDto>> Preview(string code, CancellationToken ct) =>
        this.ToActionResult(await rooms.GetPreviewAsync(code, ct));

    [HttpPost("{code}/join")]
    [ProducesResponseType<RoomDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ContractStub]
    public async Task<ActionResult<RoomDto>> Join(string code, JoinRoomRequest request, CancellationToken ct) =>
        this.ToActionResult(await rooms.JoinAsync(code, request, ct));

    [HttpGet("{code}")]
    [ProducesResponseType<RoomDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult<RoomDto>> Get(string code, CancellationToken ct) =>
        this.ToActionResult(await rooms.GetAsync(code, ct));

    [HttpPut("{code}/mood")]
    [ProducesResponseType<RoomMemberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ContractStub]
    public async Task<ActionResult<RoomMemberDto>> SetMood(string code, SetRoomMoodRequest request, CancellationToken ct) =>
        this.ToActionResult(await rooms.SetMoodAsync(code, request, ct));

    [HttpPost("{code}/start")]
    [ProducesResponseType<RoomDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ContractStub]
    public async Task<ActionResult<RoomDto>> Start(string code, CancellationToken ct) =>
        this.ToActionResult(await rooms.StartAsync(code, ct));

    [HttpGet("{code}/deck")]
    [ProducesResponseType<IReadOnlyList<DeckItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ContractStub]
    public async Task<ActionResult<IReadOnlyList<DeckItemDto>>> Deck(
        string code, [FromQuery, Range(0, int.MaxValue)] int fromPosition, CancellationToken ct) =>
        this.ToActionResult(await rooms.GetDeckAsync(code, fromPosition, ct));

    [HttpPost("{code}/votes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ContractStub]
    public async Task<ActionResult> Vote(string code, CastVoteRequest request, CancellationToken ct) =>
        this.ToActionResult(await rooms.CastVoteAsync(code, request, ct));

    [HttpGet("{code}/progress")]
    [ProducesResponseType<RoomProgressDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult<RoomProgressDto>> Progress(string code, CancellationToken ct) =>
        this.ToActionResult(await rooms.GetProgressAsync(code, ct));

    [HttpPost("{code}/finish")]
    [ProducesResponseType<RoomResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ContractStub]
    public async Task<ActionResult<RoomResultDto>> Finish(string code, CancellationToken ct) =>
        this.ToActionResult(await rooms.FinishAsync(code, ct));

    [HttpGet("{code}/result")]
    [ProducesResponseType<RoomResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ContractStub]
    public async Task<ActionResult<RoomResultDto>> Result(string code, CancellationToken ct) =>
        this.ToActionResult(await rooms.GetResultAsync(code, ct));

    [HttpPost("{code}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult> Leave(string code, CancellationToken ct) =>
        this.ToActionResult(await rooms.LeaveAsync(code, ct));

    [HttpPost("{code}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult> Close(string code, CancellationToken ct) =>
        this.ToActionResult(await rooms.CloseAsync(code, ct));
}
