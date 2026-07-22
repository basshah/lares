using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lares.Api.Contracts.Areas;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Devices;
using Lares.Api.Contracts.Homes;
using Lares.Api.Domain;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Lares.Api.Tests;

// WebApplicationFactory's in-memory TestServer has no real socket layer, so a real WebSocket upgrade
// hangs — the SignalR test client must be forced onto HttpTransportType.LongPolling, which works fine
// over TestServer's in-memory HTTP pipe via HttpMessageHandlerFactory.
public class DeviceHubTests(LaresApiFactory factory) : IClassFixture<LaresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterRequest NewUser() =>
        new($"user-{Guid.NewGuid():N}@test.az", "Passw0rd123", "Test User");

    private async Task<string> RegisterWithHomeAsync()
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", NewUser());
        registerResponse.EnsureSuccessStatusCode();
        var auth = (await registerResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        var createHomeResponse = await SendAsync(HttpMethod.Post, "/api/homes/create", auth.AccessToken,
            new CreateHomeRequest("Test Home"));
        createHomeResponse.EnsureSuccessStatusCode();

        return auth.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        return await _client.SendAsync(request);
    }

    private async Task<DeviceDto> CreateDeviceAsync(string token, DeviceType type)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/devices", token,
            new CreateDeviceRequest($"My {type}", type, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions))!;
    }

    private HubConnection BuildConnection(string accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/devices"), HttpTransportType.LongPolling, options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

    [Fact]
    public async Task PerformAction_BroadcastsHomeChanged_ToConnectedHomeMember()
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Light);

        await using var connection = BuildConnection(token);
        var tcs = new TaskCompletionSource();
        connection.On("homeChanged", () => tcs.TrySetResult());
        await connection.StartAsync();

        var response = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("turnOn", null));
        response.EnsureSuccessStatusCode();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(tcs.Task, completed);
    }

    [Fact]
    public async Task PerformAction_DoesNotBroadcast_ToMemberOfDifferentHome()
    {
        var tokenA = await RegisterWithHomeAsync();
        var tokenB = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(tokenA, DeviceType.Light);

        await using var connectionB = BuildConnection(tokenB);
        var receivedB = false;
        connectionB.On("homeChanged", () => receivedB = true);
        await connectionB.StartAsync();

        var response = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", tokenA,
            new DeviceActionRequest("turnOn", null));
        response.EnsureSuccessStatusCode();

        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.False(receivedB);
    }

    [Fact]
    public async Task AreaCreate_BroadcastsHomeChanged()
    {
        var token = await RegisterWithHomeAsync();

        await using var connection = BuildConnection(token);
        var tcs = new TaskCompletionSource();
        connection.On("homeChanged", () => tcs.TrySetResult());
        await connection.StartAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/areas", token, new CreateAreaRequest("Living Room"));
        response.EnsureSuccessStatusCode();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(tcs.Task, completed);
    }
}
