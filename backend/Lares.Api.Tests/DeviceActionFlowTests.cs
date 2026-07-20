using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Devices;
using Lares.Api.Contracts.Homes;
using Lares.Api.Data;
using Lares.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lares.Api.Tests;

public class DeviceActionFlowTests(LaresApiFactory factory) : IClassFixture<LaresApiFactory>
{
    // Matches Program.cs's JsonStringEnumConverter so enum fields (e.g. ThermostatAttributes.Mode)
    // round-trip the same way real clients (the frontend) see them.
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

    private static JsonElement ParamsOf(object value) => JsonSerializer.SerializeToElement(value, JsonOptions);

    [Fact]
    public async Task PerformAction_Light_TurnOn_UpdatesStateAndAttributes()
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Light);

        var response = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("turnOn", null));
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        Assert.Equal("on", updated!.State);
        Assert.True(updated.Attributes.Light!.IsOn);
    }

    [Fact]
    public async Task PerformAction_Light_SetBrightness_UpdatesBrightnessAndOnState()
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Light);

        var onResponse = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("setBrightness", ParamsOf(new { value = 60 })));
        onResponse.EnsureSuccessStatusCode();
        var on = await onResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);
        Assert.Equal(60, on!.Attributes.Light!.Brightness);
        Assert.True(on.Attributes.Light!.IsOn);
        Assert.Equal("on", on.State);

        var offResponse = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("setBrightness", ParamsOf(new { value = 0 })));
        var off = await offResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);
        Assert.False(off!.Attributes.Light!.IsOn);
        Assert.Equal("off", off.State);
    }

    [Fact]
    public async Task PerformAction_Socket_ToggleOnOff()
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Socket);

        var onResponse = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("turnOn", null));
        var on = await onResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);
        Assert.True(on!.Attributes.Socket!.IsOn);
        Assert.Equal("on", on.State);

        var offResponse = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("turnOff", null));
        var off = await offResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);
        Assert.False(off!.Attributes.Socket!.IsOn);
        Assert.Equal("off", off.State);
    }

    [Theory]
    [InlineData("Off", "idle")]
    [InlineData("Heat", "heating")]
    [InlineData("Cool", "cooling")]
    [InlineData("Auto", "auto")]
    public async Task PerformAction_Thermostat_SetMode_RecomputesState(string mode, string expectedState)
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Thermostat);

        var response = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("setMode", ParamsOf(new { mode })));
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        Assert.Equal(expectedState, updated!.State);
        Assert.Equal(mode, updated.Attributes.Thermostat!.Mode.ToString());
    }

    [Fact]
    public async Task PerformAction_Thermostat_SetTargetTemperature_UpdatesValue()
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Thermostat);

        var response = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("setTargetTemperature", ParamsOf(new { value = 23.5 })));
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        Assert.Equal(23.5, updated!.Attributes.Thermostat!.TargetTemperatureC);
    }

    [Fact]
    public async Task PerformAction_Tv_SetVolume_And_ToggleOnOff()
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Tv);

        var onResponse = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("turnOn", null));
        var on = await onResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);
        Assert.True(on!.Attributes.Tv!.IsOn);

        var volumeResponse = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("setVolume", ParamsOf(new { value = 45 })));
        var updated = await volumeResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);
        Assert.Equal(45, updated!.Attributes.Tv!.Volume);
    }

    [Fact]
    public async Task PerformAction_Camera_AnyAction_ReturnsUnknownAction()
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Camera);

        var response = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("turnOn", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("UNKNOWN_ACTION", error!.Code);
    }

    [Fact]
    public async Task PerformAction_UnknownActionName_OnValidType_ReturnsUnknownAction()
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Light);

        var response = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("doSomethingWeird", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("UNKNOWN_ACTION", error!.Code);
    }

    [Theory]
    [MemberData(nameof(MalformedParamsCases))]
    public async Task PerformAction_MissingOrMalformedParams_ReturnsInvalidActionParams(string action, JsonElement? malformedParams)
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Light);

        var response = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest(action, malformedParams));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("INVALID_ACTION_PARAMS", error!.Code);
    }

    public static IEnumerable<object?[]> MalformedParamsCases()
    {
        yield return ["setBrightness", null];
        yield return ["setBrightness", ParamsOf(new { value = "not-a-number" })];
        yield return ["setBrightness", ParamsOf(new { value = 150 })];
    }

    [Fact]
    public async Task PerformAction_CrossHomeIsolation_ReturnsDeviceNotFound()
    {
        var tokenA = await RegisterWithHomeAsync();
        var tokenB = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(tokenA, DeviceType.Light);

        var response = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", tokenB,
            new DeviceActionRequest("turnOn", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("DEVICE_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task PerformAction_CreatesDeviceLogRow()
    {
        var token = await RegisterWithHomeAsync();
        var device = await CreateDeviceAsync(token, DeviceType.Socket);

        var response = await SendAsync(HttpMethod.Post, $"/api/devices/{device.Id}/actions", token,
            new DeviceActionRequest("turnOn", null));
        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        var log = await db.DeviceLogs.SingleAsync(l => l.DeviceId == device.Id);

        Assert.Equal("turnOn", log.Action);
        Assert.Equal(DeviceLogSource.User, log.Source);
        Assert.NotNull(log.UserId);
        Assert.Null(log.ParamsJson);
    }
}
