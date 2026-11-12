using System.Text.Json;
using Ithaca.Application.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Ithaca.Api;

/// <summary>
/// List queries (<see cref="PageRequest"/> and the records deriving from it) bind camelCase names, like the JSON wire
/// format: <c>?page=2&amp;pageSize=50&amp;hideSeen=true</c>. ApiExplorer reads the same name, so the OpenAPI parameters
/// and validation error keys are camelCase too. Deliberately limited to those records: renaming every property of every
/// type made the build-time OpenAPI export run away.
/// </summary>
public sealed class CamelCaseBindingMetadataProvider : IBindingMetadataProvider
{
    public void CreateBindingMetadata(BindingMetadataProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Key is { MetadataKind: ModelMetadataKind.Property, Name: { } name, ContainerType: { } container }
            && typeof(PageRequest).IsAssignableFrom(container)
            && context.BindingMetadata.BinderModelName is null)
        {
            context.BindingMetadata.BinderModelName = JsonNamingPolicy.CamelCase.ConvertName(name);
        }
    }
}
