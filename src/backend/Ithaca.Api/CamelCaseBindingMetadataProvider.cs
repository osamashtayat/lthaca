using System.Reflection;
using System.Text.Json;
using Ithaca.Application.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Ithaca.Api;

/// <summary>
/// camelCase binder names, like the JSON wire format, for exactly two things:
/// <list type="bullet">
/// <item>List queries (<see cref="PageRequest"/> and the records deriving from it): <c>?page=2&amp;pageSize=50</c>, in
/// the OpenAPI parameters and in validation error keys.</item>
/// <item>Our <c>…Request</c> records' constructor parameters: MVC keys their validation errors by the binder name, so without this
/// a 400 says <c>errors.Password</c> instead of the contract's <c>errors.password</c> (api-contract.md §2).</item>
/// </list>
/// Deliberately narrow: renaming every property of every type made the build-time OpenAPI export run away.
/// </summary>
public sealed class CamelCaseBindingMetadataProvider : IBindingMetadataProvider
{
    /// <summary>Where every <c>…Request</c> record lives (Ithaca.Application); framework and response types are left alone.</summary>
    private static readonly Assembly RequestAssembly = typeof(PageRequest).Assembly;

    public void CreateBindingMetadata(BindingMetadataProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.BindingMetadata.BinderModelName is not null)
        {
            return;
        }

        var name = context.Key switch
        {
            { MetadataKind: ModelMetadataKind.Property, Name: { } property, ContainerType: { } container }
                when typeof(PageRequest).IsAssignableFrom(container) => property,
            { MetadataKind: ModelMetadataKind.Parameter, ParameterInfo: { Member: ConstructorInfo ctor, Name: { } parameter } }
                when ctor.DeclaringType?.Assembly == RequestAssembly && ctor.DeclaringType.Name.EndsWith("Request", StringComparison.Ordinal) => parameter,
            _ => null,
        };

        if (name is not null)
        {
            context.BindingMetadata.BinderModelName = JsonNamingPolicy.CamelCase.ConvertName(name);
        }
    }
}
