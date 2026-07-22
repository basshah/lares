using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Chat;
using Lares.Api.Contracts.Devices;
using Lares.Api.Contracts.Homes;
using Lares.Api.Data;
using Lares.Api.Domain;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lares.Api.Tests;

public class ChatFlowTests(LaresApiFactory factory) : IClassFixture<LaresApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterRequest NewUser() =>
        new($"user-{Guid.NewGuid():N}@test.az", "Passw0rd123", "Test User");

    private async Task<(string Token, string UserId)> RegisterWithHomeAsync()
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", NewUser());
        registerResponse.EnsureSuccessStatusCode();
        var auth = (await registerResponse.Content.ReadFromJsonAsync<AuthResponse>())!;

        var createHomeResponse = await SendAsync(HttpMethod.Post, "/api/homes/create", auth.AccessToken,
            new CreateHomeRequest("Test Home"));
        createHomeResponse.EnsureSuccessStatusCode();

        return (auth.AccessToken, auth.User.Id);
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
    public async Task SendMessage_PlainQuestion_PersistsUserAndAssistantTurns()
    {
        var (token, _) = await RegisterWithHomeAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/chat/messages", token,
            new SendChatMessageRequest("What's the temperature in the living room?"));
        response.EnsureSuccessStatusCode();
        var reply = (await response.Content.ReadFromJsonAsync<ChatMessageDto>(JsonOptions))!;
        Assert.Equal("Assistant", reply.Role);
        Assert.False(string.IsNullOrWhiteSpace(reply.Content));

        var historyResponse = await SendAsync(HttpMethod.Get, "/api/chat/messages", token);
        historyResponse.EnsureSuccessStatusCode();
        var history = (await historyResponse.Content.ReadFromJsonAsync<List<ChatMessageDto>>(JsonOptions))!;
        Assert.Equal(2, history.Count);
        Assert.Equal("User", history[0].Role);
        Assert.Equal("Assistant", history[1].Role);
    }

    [Fact]
    public async Task SendMessage_ActionRequest_ExecutesDeviceAction_LogsAiSource_AndBroadcastsHomeChanged()
    {
        var (token, userId) = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Light);

        await using var connection = BuildConnection(token);
        var tcs = new TaskCompletionSource();
        connection.On("homeChanged", () => tcs.TrySetResult());
        await connection.StartAsync();

        var actionJson = JsonSerializer.Serialize(new { deviceId = device.Id, action = "turnOn" });
        var response = await SendAsync(HttpMethod.Post, "/api/chat/messages", token,
            new SendChatMessageRequest($"ACTION:{actionJson}"));
        response.EnsureSuccessStatusCode();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(tcs.Task, completed);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        var updatedDevice = await db.Devices.SingleAsync(d => d.Id == device.Id);
        Assert.Equal("on", updatedDevice.State);

        var log = await db.DeviceLogs.SingleAsync(l => l.DeviceId == device.Id);
        Assert.Equal(DeviceLogSource.Ai, log.Source);
        Assert.Equal(userId, log.UserId);
    }

    [Fact]
    public async Task SendMessage_OffTopic_ReturnsDeclineText_WithoutDeviceLog()
    {
        var (token, _) = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Light);

        var response = await SendAsync(HttpMethod.Post, "/api/chat/messages", token,
            new SendChatMessageRequest("OFFTOPIC: how many euros is a dollar?"));
        response.EnsureSuccessStatusCode();
        var reply = (await response.Content.ReadFromJsonAsync<ChatMessageDto>(JsonOptions))!;
        Assert.Contains("only help", reply.Content, StringComparison.OrdinalIgnoreCase);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        Assert.False(await db.DeviceLogs.AnyAsync(l => l.DeviceId == device.Id));
    }

    [Fact]
    public async Task GetHistory_IsScopedPerUser_WithinSameHome()
    {
        var (ownerToken, _) = await RegisterWithHomeAsync();

        var inviteResponse = await SendAsync(HttpMethod.Get, "/api/homes/me", ownerToken);
        inviteResponse.EnsureSuccessStatusCode();
        var home = (await inviteResponse.Content.ReadFromJsonAsync<HomeDto>(JsonOptions))!;

        var memberRegister = await _client.PostAsJsonAsync("/api/auth/register", NewUser());
        memberRegister.EnsureSuccessStatusCode();
        var memberAuth = (await memberRegister.Content.ReadFromJsonAsync<AuthResponse>())!;
        var joinResponse = await SendAsync(HttpMethod.Post, "/api/homes/join", memberAuth.AccessToken,
            new JoinHomeRequest(home.InviteCode!));
        joinResponse.EnsureSuccessStatusCode();

        await SendAsync(HttpMethod.Post, "/api/chat/messages", ownerToken, new SendChatMessageRequest("Owner question"));
        await SendAsync(HttpMethod.Post, "/api/chat/messages", memberAuth.AccessToken, new SendChatMessageRequest("Member question"));

        var ownerHistory = (await (await SendAsync(HttpMethod.Get, "/api/chat/messages", ownerToken))
            .Content.ReadFromJsonAsync<List<ChatMessageDto>>(JsonOptions))!;
        var memberHistory = (await (await SendAsync(HttpMethod.Get, "/api/chat/messages", memberAuth.AccessToken))
            .Content.ReadFromJsonAsync<List<ChatMessageDto>>(JsonOptions))!;

        Assert.All(ownerHistory, m => Assert.DoesNotContain("Member question", m.Content));
        Assert.All(memberHistory, m => Assert.DoesNotContain("Owner question", m.Content));
    }

    [Fact]
    public async Task SendMessage_CrossHomeIsolation()
    {
        var (tokenA, _) = await RegisterWithHomeAsync();
        var (tokenB, _) = await RegisterWithHomeAsync();

        await SendAsync(HttpMethod.Post, "/api/chat/messages", tokenA, new SendChatMessageRequest("Home A question"));

        var historyB = (await (await SendAsync(HttpMethod.Get, "/api/chat/messages", tokenB))
            .Content.ReadFromJsonAsync<List<ChatMessageDto>>(JsonOptions))!;

        Assert.Empty(historyB);
    }
}
