using Ithaca.Api.Errors;
using Ithaca.Api.OpenApi;
using Ithaca.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>Cookie sessions for users and guests (api-contract.md §4, ADR-0012).</summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct) =>
        this.ToActionResult(await auth.GetCurrentAsync(ct));

    [HttpPost("register")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ContractStub]
    public async Task<ActionResult<CurrentUserDto>> Register(RegisterRequest request, CancellationToken ct) =>
        this.ToCreatedResult(await auth.RegisterAsync(request, ct), _ => "/api/auth/me");

    [HttpPost("sign-in")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult<CurrentUserDto>> SignIn(SignInRequest request, CancellationToken ct) =>
        this.ToActionResult(await auth.SignInAsync(request, ct));

    [HttpPost("sign-out")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ContractStub]
    public async Task<ActionResult> SignOut(CancellationToken ct) =>
        this.ToActionResult(await auth.SignOutAsync(ct));
}
