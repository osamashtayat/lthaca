namespace Ithaca.Application.Common;

/// <summary>One page of a list.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
