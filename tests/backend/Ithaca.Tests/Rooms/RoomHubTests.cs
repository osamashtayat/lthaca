using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ithaca.Api.Hubs;
using Ithaca.Tests.Harness;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Ithaca.Tests.Rooms;

/// <summary>
/// BE-10 hub membership (docs/realtime.md, ADR-0012): only members' connections join a room's group, and the events of
/// join, leave and close reach them. Uses a real SignalR client over long polling against the in-memory server, carrying
/// the browser client's cookies (users and guests alike), which is why the tests reference Microsoft.AspNetCore.SignalR.Client.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class RoomHubTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private IthacaApiFactory _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _factory = await IthacaApiFactory.StartAsync(postgres.ConnectionString);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task JoinRoom_Member_ReceivesJoinLeaveAndCloseEvents()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var room = await RoomsEndpointTests.CreateAsync(host);
        var code = RoomsEndpointTests.Code(room);
        await using var connection = await ConnectAsync(host);
        var joined = Expect(connection, "memberJoined");
        var left = Expect(connection, "memberLeft");
        var closed = Expect(connection, "roomClosed");

        await connection.InvokeAsync("JoinRoom", code.ToLowerInvariant(), Ct);
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        using var join = await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Omar" }, Ct);
        var guestMemberId = (await join.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("me").GetProperty("id").GetString();

        var joinedPayload = await joined.WaitAsync(EventTimeout, Ct);
        joinedPayload.GetProperty("roomId").GetString().ShouldBe(room.GetProperty("id").GetString());
        joinedPayload.GetProperty("memberId").GetString().ShouldBe(guestMemberId);

        (await guest.PostAsync(new Uri($"/api/rooms/{code}/leave", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await left.WaitAsync(EventTimeout, Ct)).GetProperty("memberId").GetString().ShouldBe(guestMemberId);

        (await host.PostAsync(new Uri($"/api/rooms/{code}/close", UriKind.Relative), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await closed.WaitAsync(EventTimeout, Ct)).GetProperty("roomId").GetString().ShouldBe(room.GetProperty("id").GetString());
    }

    [Fact]
    public async Task JoinRoom_Guest_JoinsOnlyTheirOwnRoomsGroup()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var codeA = RoomsEndpointTests.Code(await RoomsEndpointTests.CreateAsync(host));
        var codeB = RoomsEndpointTests.Code(await RoomsEndpointTests.CreateAsync(host));
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        (await guest.PostAsJsonAsync($"/api/rooms/{codeA}/join", new { displayName = "Omar" }, Ct)).EnsureSuccessStatusCode();
        await using var connection = await ConnectAsync(guest);

        await connection.InvokeAsync("JoinRoom", codeA, Ct);
        var refused = await Should.ThrowAsync<HubException>(() => connection.InvokeAsync("JoinRoom", codeB, Ct));

        refused.Message.ShouldContain("room.not_member");
    }

    [Fact]
    public async Task JoinRoom_NonMembers_AreRefusedAndHearNothing()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = RoomsEndpointTests.Code(await RoomsEndpointTests.CreateAsync(host));
        using var anonymous = await BrowserClient.StartAsync(_factory, Ct);
        using var stranger = await BrowserClient.RegisteredAsync(_factory, Ct);
        await using var anonymousConnection = await ConnectAsync(anonymous);
        await using var strangerConnection = await ConnectAsync(stranger);
        var overheard = Expect(strangerConnection, "memberJoined");

        var unauthenticated = await Should.ThrowAsync<HubException>(() => anonymousConnection.InvokeAsync("JoinRoom", code, Ct));
        var notMember = await Should.ThrowAsync<HubException>(() => strangerConnection.InvokeAsync("JoinRoom", code, Ct));
        var notFound = await Should.ThrowAsync<HubException>(() => strangerConnection.InvokeAsync("JoinRoom", "ZZZ-999", Ct));
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        (await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Omar" }, Ct)).EnsureSuccessStatusCode();

        unauthenticated.Message.ShouldContain("auth.unauthenticated");
        notMember.Message.ShouldContain("room.not_member");
        notFound.Message.ShouldContain("room.not_found");
        (await Task.WhenAny(overheard, Task.Delay(TimeSpan.FromSeconds(1), Ct))).ShouldNotBe(overheard);
    }

    [Fact]
    public async Task LeaveRoom_StopsTheEvents()
    {
        using var host = await BrowserClient.RegisteredAsync(_factory, Ct);
        var code = RoomsEndpointTests.Code(await RoomsEndpointTests.CreateAsync(host));
        await using var connection = await ConnectAsync(host);
        var joined = Expect(connection, "memberJoined");
        await connection.InvokeAsync("JoinRoom", code, Ct);

        await connection.InvokeAsync("LeaveRoom", code, Ct);
        using var guest = await BrowserClient.StartAsync(_factory, Ct);
        (await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Omar" }, Ct)).EnsureSuccessStatusCode();

        (await Task.WhenAny(joined, Task.Delay(TimeSpan.FromSeconds(1), Ct))).ShouldNotBe(joined);
    }

    [Fact]
    public async Task FailedPublish_NeverFailsTheRequest()
    {
        await using var broken = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IHubContext<RoomHub>>(new ThrowingHubContext())));
        using var host = await BrowserClient.RegisteredAsync(broken, Ct);
        var code = RoomsEndpointTests.Code(await RoomsEndpointTests.CreateAsync(host));
        using var guest = await BrowserClient.StartAsync(broken, Ct);

        using var join = await guest.PostAsJsonAsync($"/api/rooms/{code}/join", new { displayName = "Omar" }, Ct);
        using var leave = await guest.PostAsync(new Uri($"/api/rooms/{code}/leave", UriKind.Relative), null, Ct);
        using var close = await host.PostAsync(new Uri($"/api/rooms/{code}/close", UriKind.Relative), null, Ct);

        join.StatusCode.ShouldBe(HttpStatusCode.OK);
        leave.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        close.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<HubConnection> ConnectAsync(HttpClient browser)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/rooms"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Headers["Cookie"] = BrowserClient.CookieHeader(browser);
            })
            .Build();
        await connection.StartAsync(Ct);
        return connection;
    }

    private static Task<JsonElement> Expect(HubConnection connection, string eventName)
    {
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>(eventName, payload => received.TrySetResult(payload));
        return received.Task;
    }

    private sealed class ThrowingHubContext : IHubContext<RoomHub>
    {
        public IHubClients Clients { get; } = new ThrowingClients();

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class ThrowingClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new ThrowingProxy();

        public IClientProxy All => Proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Client(string connectionId) => Proxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;

        public IClientProxy Group(string groupName) => Proxy;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;

        public IClientProxy User(string userId) => Proxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class ThrowingProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The hub is down.");
    }
}
