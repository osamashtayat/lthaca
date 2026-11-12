using Ithaca.Infrastructure.Jobs;

namespace Ithaca.Tests.Jobs;

public sealed class CatalogSyncJobTests
{
    [Theory]
    [InlineData("2026-11-12T01:00:00Z", 2)]
    [InlineData("2026-11-12T03:00:00Z", 24)]
    [InlineData("2026-11-12T04:00:00Z", 23)]
    public void DelayUntilNextRun_PicksTheNextThreeAm(string now, int expectedHours) =>
        CatalogSyncJob.DelayUntilNextRun(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture), new TimeOnly(3, 0))
            .ShouldBe(TimeSpan.FromHours(expectedHours));
}
