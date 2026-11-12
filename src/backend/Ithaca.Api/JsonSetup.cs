using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ithaca.Api;

/// <summary>Wire format rules, applied to MVC, SignalR and the OpenAPI generator alike.</summary>
public static class JsonSetup
{
    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Enums travel as camelCase strings only ("justSwipe", "notForMe"): an integer such as 99 would otherwise bind to an
        // undefined value. [JsonStringEnumMemberName] overrides the name where an outside spelling is kept ("YouTube").
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        // Numbers are numbers: the Web default also accepts "1", which would type every integer as number | string.
        options.NumberHandling = JsonNumberHandling.Strict;
        // Request records must send every constructor parameter (nullable ones as null), as contracts/openapi.json says:
        // otherwise a missing "direction" would bind as the first enum value (an unintended yes) and a missing id as Guid.Empty.
        options.RespectRequiredConstructorParameters = true;
    }
}
