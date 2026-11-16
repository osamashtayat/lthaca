using Ithaca.Api.Monitoring;
using Ithaca.Infrastructure.Jobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;

namespace Ithaca.Tests.Monitoring;

/// <summary>BE-21: what may leave for Sentry (ADR-0028), and the options that decide it.</summary>
public sealed class SentryScrubberTests
{
    [Fact]
    public void Event_LosesCookiesBodyHeadersAndUserDetails_KeepsTheId()
    {
        var sentryEvent = new SentryEvent();
        sentryEvent.Request.Url = "http://localhost/api/films?search=my+secret+crush";
        sentryEvent.Request.QueryString = "search=my+secret+crush";
        sentryEvent.Request.Cookies = ".AspNetCore.Identity=secret";
        sentryEvent.Request.Data = "{\"email\":\"ada@example.com\"}";
        sentryEvent.Request.Headers["Cookie"] = "a=b";
        sentryEvent.Request.Headers["X-XSRF-TOKEN"] = "token";
        sentryEvent.Request.Headers["Referer"] = "https://ithaca.test/reset-password?token=abc";
        sentryEvent.Request.Headers["User-Agent"] = "Mobile Safari";
        sentryEvent.Request.Env["REMOTE_ADDR"] = "203.0.113.9";
        sentryEvent.User = new SentryUser { Id = "42", Email = "ada@example.com", Username = "Ada", IpAddress = "203.0.113.9" };

        SentryScrubber.Scrub(sentryEvent);

        sentryEvent.Request.Url.ShouldBe("http://localhost/api/films");
        sentryEvent.Request.QueryString.ShouldBeNull();
        sentryEvent.Request.Cookies.ShouldBeNull();
        sentryEvent.Request.Data.ShouldBeNull();
        sentryEvent.Request.Headers.Keys.ShouldBe(["User-Agent"]);
        sentryEvent.Request.Env.ShouldBeEmpty();
        sentryEvent.User.Id.ShouldBe("42");
        sentryEvent.User.Email.ShouldBeNull();
        sentryEvent.User.Username.ShouldBeNull();
        sentryEvent.User.IpAddress.ShouldBeNull();
    }

    [Fact]
    public void Breadcrumb_ForAnOutgoingCall_LosesItsQueryString()
    {
        var breadcrumb = new Breadcrumb(
            "GET https://api.themoviedb.org/3/movie/550?api_key=SECRET",
            "http",
            new Dictionary<string, string> { ["url"] = "https://api.themoviedb.org/3/movie/550?api_key=SECRET", ["http.query"] = "api_key=SECRET", ["method"] = "GET" },
            "http.client");

        var scrubbed = SentryScrubber.Scrub(breadcrumb);

        scrubbed.Message.ShouldBe("GET https://api.themoviedb.org/3/movie/550");
        scrubbed.Data!["url"].ShouldBe("https://api.themoviedb.org/3/movie/550");
        scrubbed.Data.ShouldNotContainKey("http.query");
        scrubbed.Data["method"].ShouldBe("GET");
        scrubbed.Type.ShouldBe("http");
        scrubbed.Category.ShouldBe("http.client");
    }

    [Theory]
    [InlineData("https://a.test/x?k=1#f", "https://a.test/x")]
    [InlineData("call 'https://a.test/x?k=1' failed", "call 'https://a.test/x' failed")]
    [InlineData("no url here?really", "no url here?really")]
    public void WithoutQueries_StripsQueriesOfAbsoluteUrlsOnly(string text, string expected) =>
        SentryScrubber.WithoutQueries(text).ShouldBe(expected);

    [Theory]
    [InlineData("Development", 1.0)]
    [InlineData("Production", 0.1)]
    public void Configure_SamplesByEnvironment_AndNeverSendsDefaultPii(string environment, double traces)
    {
        var sentry = new SentryAspNetCoreOptions();

        ErrorTracking.Configure(sentry, new ErrorTrackingOptions { Dsn = "https://key@o1.ingest.de.sentry.io/2" }, new Environment(environment));

        sentry.Dsn.ShouldBe("https://key@o1.ingest.de.sentry.io/2");
        sentry.Environment.ShouldBe(environment.ToLowerInvariant());
        sentry.TracesSampleRate.ShouldBe(traces);
        sentry.SendDefaultPii.ShouldBeFalse();
        sentry.MaxRequestBodySize.ShouldBe(RequestSize.None);
        sentry.EnableLogs.ShouldBeTrue();
    }

    [Fact]
    public void Configure_WithoutADsn_TurnsTheSdkOffExplicitly()
    {
        var sentry = new SentryAspNetCoreOptions();

        ErrorTracking.Configure(sentry, new ErrorTrackingOptions(), new Environment("Production"));

        sentry.Dsn.ShouldBe(string.Empty, "empty, so a stray SENTRY_DSN variable can't switch it on");
    }

    [Theory]
    [InlineData(SentryLogLevel.Info, "Ithaca.Application.Moods.MoodService", false)]
    [InlineData(SentryLogLevel.Warning, "Ithaca.Infrastructure.Tmdb.TmdbHttpClient", true)]
    [InlineData(SentryLogLevel.Error, "Ithaca.Api.Errors.ProblemDetailsExceptionHandler", true)]
    [InlineData(SentryLogLevel.Error, "Microsoft.EntityFrameworkCore.Update", false)]
    [InlineData(SentryLogLevel.Warning, null, true)]
    public void SentryLogs_GetWarningsAndAbove_ButNotEfCore(SentryLogLevel level, string? category, bool sent) =>
        ErrorTracking.ShouldSendLog(level, category).ShouldBe(sent);

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://key@o1.ingest.de.sentry.io/2", true)]
    [InlineData("http://key@o1.ingest.de.sentry.io/2", false)]
    [InlineData("https://o1.ingest.de.sentry.io/2", false)]
    [InlineData("not a url", false)]
    public void Options_AcceptOnlyEmptyOrAnHttpsDsn(string? dsn, bool valid)
    {
        var options = new ErrorTrackingOptions { Dsn = dsn };

        options.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(options)).Any().ShouldBe(!valid);
    }

    [Theory]
    [InlineData(null, 8080)]
    [InlineData("", 8080)]
    [InlineData("5000", 5000)]
    [InlineData("8081;8082", 8081)]
    [InlineData("nope", 8080)]
    public void Probe_UsesTheFirstConfiguredHttpPort(string? ports, int expected) =>
        ReadinessProbe.PortFrom(ports).ShouldBe(expected);

    [Theory]
    [InlineData("03:00", "0 3 * * *")]
    [InlineData("23:45", "45 23 * * *")]
    public void CatalogSyncMonitor_RunsDailyAtTheConfiguredTime(string runAt, string crontab) =>
        CatalogSyncJob.Crontab(TimeOnly.Parse(runAt, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(crontab);

    private sealed class Environment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Ithaca.Api";

        public string WebRootPath { get; set; } = "";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = "";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
