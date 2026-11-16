using System.Globalization;

namespace Ithaca.Api.Monitoring;

/// <summary>
/// <c>dotnet Ithaca.Api.dll --probe-ready</c>: asks the running API's <c>/api/health/ready</c> on localhost and exits 0 when it
/// answers 2xx, else 1. The Docker healthcheck uses it because the runtime image has no curl or wget (ADR-0028).
/// </summary>
internal static class ReadinessProbe
{
    public const string Flag = "--probe-ready";

    private const int DefaultPort = 8080;

    public static async Task<int> RunAsync()
    {
        var port = PortFrom(Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS"));
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await http.GetAsync(new Uri($"http://127.0.0.1:{port}/api/health/ready"));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }

    /// <summary>The first port in <c>ASPNETCORE_HTTP_PORTS</c> (e.g. <c>8080;8081</c>); 8080, the image default, otherwise.</summary>
    internal static int PortFrom(string? ports) =>
        (ports ?? string.Empty).Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ? port : (int?)null)
            .FirstOrDefault(p => p is > 0 and < 65536) ?? DefaultPort;
}
