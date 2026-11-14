using Ithaca.Api.Auth;
using Ithaca.Api.Errors;
using Ithaca.Api.OpenApi;
using Ithaca.Application.Taste;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>The caller's taste: readiness for By taste and the taste receipt (api-contract.md §12). Signed-in users only.</summary>
[ApiController]
[Route("api/taste")]
[Tags("Taste")]
[Authorize(Policy = AuthPolicies.User)]
public sealed class TasteController(TasteService taste) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<TasteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult<TasteDto>> Get(CancellationToken ct) =>
        this.ToActionResult(await taste.GetAsync(ct));
}
