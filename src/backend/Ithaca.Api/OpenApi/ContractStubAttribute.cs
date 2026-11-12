using Microsoft.AspNetCore.Mvc;

namespace Ithaca.Api.OpenApi;

/// <summary>
/// Marks an action whose service is still a contract stub (BE-01): documents its 501 <c>server.not_implemented</c> answer.
/// Remove it from the action when its backlog task lands; BE-17 deletes the attribute once nothing uses it.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ContractStubAttribute() : ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status501NotImplemented);
