using Ithaca.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ithaca.Infrastructure.Configuration;

/// <summary>The one options pattern: bind the class's own section, validate DataAnnotations, fail at startup.</summary>
public static class OptionsRegistration
{
    public static OptionsBuilder<T> AddValidatedOptions<T>(this IServiceCollection services, IConfiguration configuration)
        where T : class, IConfigSection
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddOptions<T>()
            .Bind(configuration.GetSection(T.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
