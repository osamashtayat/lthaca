using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ithaca.Api.Errors;
using Ithaca.Tests.Harness;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716", Scope = "namespace", Target = "~N:Ithaca.Tests.Imports", Justification = "C#-only codebase; mirrors the Imports feature folder.")]

namespace Ithaca.Tests.Imports;

/// <summary>
/// BE-15a: the import contract (api-contract.md §13) before its logic. Multipart uploads go through the XSRF check like
/// any unsafe request; the four actions are user-only and answer 501 until BE-15; an oversized body is 413
/// <c>request.too_large</c>.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ImportsContractTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Watched = "Date,Name,Year,Letterboxd URI\n2024-01-01,Glasshouse,2025,https://boxd.it/abc\n";

    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Upload_MultipartWithXsrfHeader_ReachesTheStub()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);
        using var form = Form(Encoding.UTF8.GetBytes(Watched), "watched.csv");

        using var response = await user.PostAsync(new Uri("/api/imports", UriKind.Relative), form, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented, await response.Content.ReadAsStringAsync(Ct));
        (await CodeAsync(response)).ShouldBe("server.not_implemented");
    }

    [Fact]
    public async Task Upload_WithoutXsrfHeader_IsRefused()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);
        using var plain = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/imports") { Content = Form(Encoding.UTF8.GetBytes(Watched), "watched.csv") };
        request.Headers.Add("Cookie", BrowserClient.CookieHeader(user));

        using var response = await plain.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeAsync(response)).ShouldBe("auth.antiforgery_failed");
    }

    [Fact]
    public async Task Upload_WithoutAFile_IsValidationError()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);
        using var form = new MultipartFormDataContent { { new StringContent("x"), "other" } };

        using var response = await user.PostAsync(new Uri("/api/imports", UriKind.Relative), form, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        problem.RootElement.GetProperty("code").GetString().ShouldBe("validation");
        problem.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldContain("file", problem.RootElement.ToString());
    }

    [Fact]
    public async Task EveryAction_GuestOrAnonymous_IsUnauthenticated()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        using var created = await host.PostAsJsonAsync("/api/rooms", new { name = "Room", mode = "justSwipe", startsAt = (string?)null }, Ct);
        var code = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        (await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Omar" }, Ct)).EnsureSuccessStatusCode();
        using var anonymous = await BrowserClient.StartAsync(_factory, Ct);
        var id = Guid.NewGuid();

        foreach (var client in new[] { guest, anonymous })
        {
            using var form = Form(Encoding.UTF8.GetBytes(Watched), "watched.csv");
            using var upload = await client.PostAsync(new Uri("/api/imports", UriKind.Relative), form, Ct);
            using var list = await client.GetAsync("/api/imports", Ct);
            using var one = await client.GetAsync($"/api/imports/{id}", Ct);
            using var notFound = await client.GetAsync($"/api/imports/{id}/not-found", Ct);

            foreach (var response in new[] { upload, list, one, notFound })
            {
                response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
                (await CodeAsync(response)).ShouldBe("auth.unauthenticated");
            }
        }
    }

    [Fact]
    public async Task List_PageSizeOutOfRange_IsValidationError()
    {
        using var user = await BrowserClient.RegisteredAsync(_factory, Ct);

        using var response = await user.GetAsync("/api/imports?pageSize=101", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BodyTooLarge_IsRequestTooLarge()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var handler = new ProblemDetailsExceptionHandler(
            scope.ServiceProvider.GetRequiredService<IProblemDetailsService>(), NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(http, new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge), Ct);

        handled.ShouldBeTrue();
        http.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
        http.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(http.Response.Body, cancellationToken: Ct);
        problem.RootElement.GetProperty("code").GetString().ShouldBe("request.too_large");
    }

    [Fact]
    public async Task OtherBadRequests_StayServerErrors()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var handler = new ProblemDetailsExceptionHandler(
            scope.ServiceProvider.GetRequiredService<IProblemDetailsService>(), NullLogger<ProblemDetailsExceptionHandler>.Instance);
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Response.Body = new MemoryStream();

        await handler.TryHandleAsync(http, new BadHttpRequestException("Malformed.", StatusCodes.Status400BadRequest), Ct);

        http.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    private static MultipartFormDataContent Form(byte[] bytes, string fileName)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
}
