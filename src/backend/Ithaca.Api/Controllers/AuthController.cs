using Ithaca.Api.Auth;
using Ithaca.Api.Errors;
using Ithaca.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>
/// Cookie sessions for users and guests (api-contract.md §4, ADR-0012). Every action issues a fresh <c>XSRF-TOKEN</c>:
/// <c>me</c> hands out the first one, the others change the caller the token is bound to.
/// </summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpGet("me")]
    [IssueXsrfToken]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct) =>
        this.ToActionResult(await auth.GetCurrentAsync(ct));

    [HttpPost("register")]
    [IssueXsrfToken]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CurrentUserDto>> Register(RegisterRequest request, CancellationToken ct) =>
        this.ToCreatedResult(await auth.RegisterAsync(request, ct), _ => "/api/auth/me");

    [HttpPost("sign-in")]
    [IssueXsrfToken]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserDto>> SignIn(SignInRequest request, CancellationToken ct) =>
        this.ToActionResult(await auth.SignInAsync(request, ct));

    [HttpPost("sign-out")]
    [IssueXsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> SignOut(CancellationToken ct) =>
        this.ToActionResult(await auth.SignOutAsync(ct));
}
