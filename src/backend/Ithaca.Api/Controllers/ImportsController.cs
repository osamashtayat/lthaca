using Ithaca.Api.Auth;
using Ithaca.Api.Errors;
using Ithaca.Api.OpenApi;
using Ithaca.Application.Common;
using Ithaca.Application.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.Controllers;

/// <summary>Letterboxd imports (api-contract.md §13, ADR-0024). Signed-in users only; guests get 401 (A15).</summary>
[ApiController]
[Route("api/imports")]
[Tags("Imports")]
[Authorize(Policy = AuthPolicies.User)]
public sealed class ImportsController(ImportService imports) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportLimits.MaxUploadBytes)]
    [ReadFormFirst] // so an oversized body is 413 request.too_large, not a 400 from form binding
    [ProducesResponseType<ImportJobDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ContractStub]
    public async Task<ActionResult<ImportJobDto>> Create([FromForm] CreateImportRequest request, CancellationToken ct) =>
        this.ToCreatedResult(await imports.CreateAsync(request.ToImportUpload(), ct), job => $"/api/imports/{job.Id}");

    [HttpGet]
    [ProducesResponseType<PagedResult<ImportJobDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ContractStub]
    public async Task<ActionResult<PagedResult<ImportJobDto>>> List([FromQuery] PageRequest page, CancellationToken ct) =>
        this.ToActionResult(await imports.ListAsync(page, ct));

    [HttpGet("{importId:guid}")]
    [ProducesResponseType<ImportJobDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult<ImportJobDto>> Get(Guid importId, CancellationToken ct) =>
        this.ToActionResult(await imports.GetAsync(importId, ct));

    [HttpGet("{importId:guid}/not-found")]
    [ProducesResponseType<PagedResult<ImportNotFoundDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ContractStub]
    public async Task<ActionResult<PagedResult<ImportNotFoundDto>>> ListNotFound(Guid importId, [FromQuery] PageRequest page, CancellationToken ct) =>
        this.ToActionResult(await imports.ListNotFoundAsync(importId, page, ct));
}
