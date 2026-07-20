using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lares.Api.Contracts.Areas;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Devices;
using Lares.Api.Contracts.Homes;
using Lares.Api.Contracts.Labels;
using Lares.Api.Domain;

namespace Lares.Api.Tests;

public class DeviceFlowTests(LaresApiFactory factory) : IClassFixture<LaresApiFactory>
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

    [Theory]
    [InlineData(DeviceType.Light, "off")]
    [InlineData(DeviceType.Socket, "off")]
    [InlineData(DeviceType.Thermostat, "idle")]
    [InlineData(DeviceType.Camera, "online")]
    [InlineData(DeviceType.Tv, "off")]
    public async Task CreateDevice_ForEachInitialType_GetsSensibleDefaults(DeviceType type, string expectedState)
    {
        var token = await RegisterWithHomeAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/devices", token,
            new CreateDeviceRequest($"My {type}", type, null));
        response.EnsureSuccessStatusCode();
        var device = await response.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        Assert.Equal(expectedState, device!.State);
        Assert.Equal(type.ToString(), device.Type);
        Assert.Empty(device.Labels);
    }

    [Fact]
    public async Task UpdateDevice_ChangesNameAreaAndLabels()
    {
        var token = await RegisterWithHomeAsync();

        var areaResponse = await SendAsync(HttpMethod.Post, "/api/areas", token, new CreateAreaRequest("Living Room"));
        var area = await areaResponse.Content.ReadFromJsonAsync<AreaDto>();

        var labelResponse = await SendAsync(HttpMethod.Post, "/api/labels", token, new CreateLabelRequest("Downstairs"));
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelDto>();

        var createResponse = await SendAsync(HttpMethod.Post, "/api/devices", token,
            new CreateDeviceRequest("Ceiling Light", DeviceType.Light, null));
        var device = await createResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        var updateResponse = await SendAsync(HttpMethod.Put, $"/api/devices/{device!.Id}", token,
            new UpdateDeviceRequest("Renamed Light", area!.Id, [label!.Id], device.Attributes));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        Assert.Equal("Renamed Light", updated!.Name);
        Assert.Equal(area.Id, updated.AreaId);
        Assert.Equal("Living Room", updated.AreaName);
        Assert.Single(updated.Labels);
        Assert.Equal("Downstairs", updated.Labels[0].Name);
    }

    [Fact]
    public async Task UpdateDevice_WithMismatchedAttributes_ReturnsBadRequest()
    {
        var token = await RegisterWithHomeAsync();

        var createResponse = await SendAsync(HttpMethod.Post, "/api/devices", token,
            new CreateDeviceRequest("Ceiling Light", DeviceType.Light, null));
        var device = await createResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        var mismatchedAttributes = new DeviceAttributesDto(
            null, null, new ThermostatAttributes { TargetTemperatureC = 21, Mode = ThermostatMode.Off }, null, null);
        var updateResponse = await SendAsync(HttpMethod.Put, $"/api/devices/{device!.Id}", token,
            new UpdateDeviceRequest("Ceiling Light", null, [], mismatchedAttributes));

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        var error = await updateResponse.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("ATTRIBUTES_TYPE_MISMATCH", error!.Code);
    }

    [Fact]
    public async Task DeleteDevice_RemovesIt_And404sOnSubsequentGet()
    {
        var token = await RegisterWithHomeAsync();

        var createResponse = await SendAsync(HttpMethod.Post, "/api/devices", token,
            new CreateDeviceRequest("Socket", DeviceType.Socket, null));
        var device = await createResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        var deleteResponse = await SendAsync(HttpMethod.Delete, $"/api/devices/{device!.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await SendAsync(HttpMethod.Get, $"/api/devices/{device.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Devices_CrossHomeIsolation()
    {
        var tokenA = await RegisterWithHomeAsync();
        var tokenB = await RegisterWithHomeAsync();

        var createResponse = await SendAsync(HttpMethod.Post, "/api/devices", tokenA,
            new CreateDeviceRequest("A's Light", DeviceType.Light, null));
        var deviceA = await createResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions);

        var getAsB = await SendAsync(HttpMethod.Get, $"/api/devices/{deviceA!.Id}", tokenB);
        Assert.Equal(HttpStatusCode.NotFound, getAsB.StatusCode);

        var updateAsB = await SendAsync(HttpMethod.Put, $"/api/devices/{deviceA.Id}", tokenB,
            new UpdateDeviceRequest("Hijacked", null, [], deviceA.Attributes));
        Assert.Equal(HttpStatusCode.NotFound, updateAsB.StatusCode);

        var deleteAsB = await SendAsync(HttpMethod.Delete, $"/api/devices/{deviceA.Id}", tokenB);
        Assert.Equal(HttpStatusCode.NotFound, deleteAsB.StatusCode);

        var listAsB = await SendAsync(HttpMethod.Get, "/api/devices", tokenB);
        var devicesB = await listAsB.Content.ReadFromJsonAsync<List<DeviceDto>>(JsonOptions);
        Assert.DoesNotContain(devicesB!, d => d.Id == deviceA.Id);
    }
}
