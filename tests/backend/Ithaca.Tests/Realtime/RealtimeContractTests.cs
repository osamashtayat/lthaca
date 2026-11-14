using System.Reflection;
using System.Text.Json;
using Ithaca.Application.Realtime;
using Ithaca.Tests.Harness;

namespace Ithaca.Tests.Realtime;

/// <summary>RoomEvents (C#) and contracts/realtime-events.json (read by the frontend) must name the same events.</summary>
public sealed class RealtimeContractTests
{
    [Fact]
    public void RoomEvents_MatchContractFile()
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(RepoPaths.File("contracts", "realtime-events.json")));
        var fileEvents = contract.RootElement.GetProperty("serverToClient").EnumerateObject().Select(p => p.Name);
        var codeEvents = typeof(RoomEvents).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name != nameof(RoomEvents.HubPath))
            .Select(f => (string)f.GetRawConstantValue()!);

        codeEvents.ShouldBe(fileEvents, ignoreOrder: true);
        contract.RootElement.GetProperty("hubPath").GetString().ShouldBe(RoomEvents.HubPath);
    }
}
