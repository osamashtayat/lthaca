using Ithaca.Api.Errors;
using Ithaca.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Ithaca.Api.OpenApi;

/// <summary>
/// The OpenAPI document is the executable contract (contracts/openapi.json, written by scripts/export-openapi.sh) and the
/// source of the frontend's generated types. These transformers make it say what the API really sends.
/// </summary>
public static class OpenApiSetup
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddIthacaOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo { Title = "Ithaca API", Version = "v1", Description = "Generated from the API." };
                // Server URLs depend on the host that served the document; leaving them out keeps the export deterministic.
                document.Servers = [];
                return Task.CompletedTask;
            });
            options.AddSchemaTransformer(DescribeProblemExtensions);
            options.AddSchemaTransformer(RequireAllResponseProperties);
            options.AddSchemaTransformer(DescribeEnums);
        });

    /// <summary>Every ProblemDetails we send carries <c>code</c> and <c>traceId</c>.</summary>
    private static Task DescribeProblemExtensions(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken _)
    {
        if (!typeof(ProblemDetails).IsAssignableFrom(context.JsonTypeInfo.Type) || schema.Properties is null)
        {
            return Task.CompletedTask;
        }

        schema.Properties[ProblemDetailsSetup.CodeKey] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Stable error code, e.g. film.not_found." };
        schema.Properties[ProblemDetailsSetup.TraceIdKey] = new OpenApiSchema { Type = JsonSchemaType.String };
        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        schema.Required.Add(ProblemDetailsSetup.CodeKey);
        schema.Required.Add(ProblemDetailsSetup.TraceIdKey);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Response DTOs (<c>…Dto</c>, and <see cref="PagedResult{T}"/>) always send every property (nullable ones as
    /// <c>null</c>), so mark them all required; otherwise generated TypeScript would make every field optional.
    /// </summary>
    private static Task RequireAllResponseProperties(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken _)
    {
        var type = context.JsonTypeInfo.Type;
        var isResponse = type.Name.EndsWith("Dto", StringComparison.Ordinal)
            || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PagedResult<>));
        if (context.JsonPropertyInfo is not null || !isResponse || schema.Properties is not { Count: > 0 })
        {
            return Task.CompletedTask;
        }

        schema.Required = new HashSet<string>(schema.Properties.Keys, StringComparer.Ordinal);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Enums are strings (JsonSetup). A shared enum schema lists only its values: when the first use is nullable
    /// (<c>SeenVerdict?</c>) the generator would put <c>null</c> into the shared list; nullability belongs to the property.
    /// </summary>
    private static Task DescribeEnums(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken _)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (!type.IsEnum || schema.Enum is null)
        {
            return Task.CompletedTask;
        }

        schema.Type = JsonSchemaType.String;
        schema.Enum = [.. schema.Enum.Where(value => value is not null)];
        return Task.CompletedTask;
    }
}
