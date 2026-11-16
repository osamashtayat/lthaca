using Ithaca.Api.Errors;
using Ithaca.Application.Health;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>Liveness (the process answers) and readiness (the database answers too; ADR-0028). Reveal nothing else.</summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController(ReadinessService readiness) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthDto>(StatusCodes.Status200OK)]
    public ActionResult<HealthDto> Get() => Ok(HealthDto.Ok);

    [HttpGet("ready")]
    [ProducesResponseType<HealthDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthDto>> Ready(CancellationToken ct) => this.ToActionResult(await readiness.CheckAsync(ct));
}
