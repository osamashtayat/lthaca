namespace Ithaca.Application.Health;

/// <summary>Liveness and readiness answer (<c>{ "status": "ok" }</c>).</summary>
public sealed record HealthDto(string Status)
{
    public static readonly HealthDto Ok = new("ok");
}
