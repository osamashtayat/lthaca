using Ithaca.Api.Auth;
using Ithaca.Api.Errors;
using Ithaca.Api.OpenApi;
using Ithaca.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>
/// The signed-in user's profile and account (api-contract.md §14). Users only; guests get 401 (ADR-0023). Password
/// change and deletion change the caller's session, so they issue a fresh <c>XSRF-TOKEN</c>.
/// </summary>
[ApiController]
[Route("api/profile")]
[Tags("Profile")]
[Authorize(Policy = AuthPolicies.User)]
public sealed class ProfileController(ProfileService profile) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken ct) =>
        this.ToActionResult(await profile.GetAsync(ct));

    [HttpPut]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult<ProfileDto>> Update(UpdateProfileRequest request, CancellationToken ct) =>
        this.ToActionResult(await profile.UpdateAsync(request, ct));

    [HttpPut("password")]
    [IssueXsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct) =>
        this.ToActionResult(await profile.ChangePasswordAsync(request, ct));

    [HttpDelete]
    [IssueXsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult> DeleteAccount([FromBody] DeleteAccountRequest request, CancellationToken ct) =>
        this.ToActionResult(await profile.DeleteAccountAsync(request, ct));
}
