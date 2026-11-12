using Ithaca.Api.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Auth;

/// <summary>Cookie flags per environment (ADR-0012: Secure in Production; Development is plain HTTP, ADR-0009).</summary>
public sealed class AuthSetupTests
{
    [Theory]
    [InlineData("Production", CookieSecurePolicy.Always)]
    [InlineData("Development", CookieSecurePolicy.SameAsRequest)]
    public void Cookies_AreSecureOnlyInProduction(string environment, CookieSecurePolicy expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddIthacaAuth(new HostingEnvironment { EnvironmentName = environment });
        using var provider = services.BuildServiceProvider();

        var session = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var antiforgery = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value;

        session.Cookie.SecurePolicy.ShouldBe(expected);
        session.Cookie.HttpOnly.ShouldBeTrue();
        session.Cookie.SameSite.ShouldBe(SameSiteMode.Lax);
        session.ExpireTimeSpan.ShouldBe(TimeSpan.FromDays(30));
        session.SlidingExpiration.ShouldBeTrue();
        antiforgery.Cookie.SecurePolicy.ShouldBe(expected);
        antiforgery.Cookie.HttpOnly.ShouldBeTrue();
        antiforgery.Cookie.SameSite.ShouldBe(SameSiteMode.Lax);
        antiforgery.HeaderName.ShouldBe("X-XSRF-TOKEN");
    }
}
