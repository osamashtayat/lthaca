using System.Net;
using System.Text.Json;
using Ithaca.Application.Email;
using Ithaca.Infrastructure;
using Ithaca.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ithaca.Tests.Email;

/// <summary>BE-18: the Resend client against a stub handler (no network) and the email templates.</summary>
public sealed class EmailTests
{
    private static readonly EmailMessage Message = new("lina@example.com", "Subject", "Text body", "<p>Html body</p>");

    [Fact]
    public async Task Resend_PostsTheEmailWithTheKey()
    {
        var handler = new StubHandler(HttpStatusCode.OK);

        var sent = await Sender(handler).SendAsync(Message, TestContext.Current.CancellationToken);

        sent.ShouldBeTrue();
        handler.Request!.Method.ShouldBe(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().ShouldBe("https://api.resend.com/emails");
        handler.Request.Headers.Authorization!.ToString().ShouldBe("Bearer re_test");
        using var body = JsonDocument.Parse(handler.Body!);
        body.RootElement.GetProperty("from").GetString().ShouldBe("Ithaca <no-reply@mail.ithacamovies.com>");
        body.RootElement.GetProperty("to")[0].GetString().ShouldBe("lina@example.com");
        body.RootElement.GetProperty("subject").GetString().ShouldBe("Subject");
        body.RootElement.GetProperty("text").GetString().ShouldBe("Text body");
        body.RootElement.GetProperty("html").GetString().ShouldBe("<p>Html body</p>");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Resend_Refusal_IsFalse(HttpStatusCode status)
    {
        var sent = await Sender(new StubHandler(status)).SendAsync(Message, TestContext.Current.CancellationToken);

        sent.ShouldBeFalse();
    }

    [Fact]
    public async Task Resend_Unreachable_IsFalse()
    {
        var sent = await Sender(new StubHandler(HttpStatusCode.OK, fail: true)).SendAsync(Message, TestContext.Current.CancellationToken);

        sent.ShouldBeFalse();
    }

    [Fact]
    public void PasswordResetTemplate_FillsAndEncodes()
    {
        var message = EmailTemplates.Render("password-reset", "lina@example.com", new Dictionary<string, string>
        {
            ["displayName"] = "<Lina>",
            ["link"] = "http://localhost:5273/reset-password?token=abc&x=1",
            ["expiresIn"] = "1 hour",
        });

        message.To.ShouldBe("lina@example.com");
        message.Subject.ShouldBe("Reset your Ithaca password");
        message.Text.ShouldStartWith("Hi <Lina>,");
        message.Text.ShouldContain("http://localhost:5273/reset-password?token=abc&x=1");
        message.Text.ShouldNotContain("{{");
        message.Html.ShouldContain("Hi &lt;Lina&gt;,");
        message.Html.ShouldContain("token=abc&amp;x=1");
        message.Html.ShouldNotContain("{{");
    }

    [Fact]
    public void Startup_ResendWithoutAKey_FailsWithAClearMessage()
    {
        using var services = Services(new() { ["Email:Provider"] = "Resend" });

        var error = Should.Throw<OptionsValidationException>(() => services.GetRequiredService<IOptions<EmailOptions>>().Value);

        error.Message.ShouldContain("Email:ApiKey is required when Email:Provider is 'Resend'");
    }

    [Fact]
    public void Startup_ResendWithAKey_UsesTheResendClient()
    {
        using var services = Services(new() { ["Email:Provider"] = "Resend", ["Email:ApiKey"] = "re_test" });

        services.GetRequiredService<IEmailSender>().ShouldBeOfType<ResendEmailSender>();
    }

    [Fact]
    public void Startup_UnknownProvider_IsRefused()
    {
        using var services = Services(new() { ["Email:Provider"] = "Carrier pigeon" });

        Should.Throw<InvalidOperationException>(() => services.GetRequiredService<IOptions<EmailOptions>>().Value);
    }

    private static ServiceProvider Services(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System).AddInfrastructure(configuration).BuildServiceProvider();
    }

    private static ResendEmailSender Sender(StubHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") },
        Options.Create(new EmailOptions { Provider = EmailProvider.Resend, ApiKey = "re_test" }),
        NullLogger<ResendEmailSender>.Instance);

    private sealed class StubHandler(HttpStatusCode status, bool fail = false) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return fail ? throw new HttpRequestException("No route to host.") : new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }
}
