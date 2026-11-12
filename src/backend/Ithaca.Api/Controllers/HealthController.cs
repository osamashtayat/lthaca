using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>Liveness probe. Reveals nothing beyond "the process answers".</summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthDto>(StatusCodes.Status200OK)]
    public ActionResult<HealthDto> Get() => Ok(HealthDto.Ok);
}

public sealed record HealthDto(string Status)
{
    public static readonly HealthDto Ok = new("ok");
}
