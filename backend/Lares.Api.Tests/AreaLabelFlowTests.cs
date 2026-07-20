using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Lares.Api.Contracts.Areas;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Devices;
using Lares.Api.Contracts.Homes;
using Lares.Api.Contracts.Labels;
using Lares.Api.Domain;

namespace Lares.Api.Tests;

public class AreaLabelFlowTests(LaresApiFactory factory) : IClassFixture<LaresApiFactory>
{
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
            request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task CreateRenameDeleteArea_Basic()
    {
        var token = await RegisterWithHomeAsync();

        var createResponse = await SendAsync(HttpMethod.Post, "/api/areas", token, new CreateAreaRequest("Kitchen"));
        createResponse.EnsureSuccessStatusCode();
        var area = await createResponse.Content.ReadFromJsonAsync<AreaDto>();
        Assert.Equal("Kitchen", area!.Name);
        Assert.Equal(0, area.DeviceCount);

        var renameResponse = await SendAsync(HttpMethod.Put, $"/api/areas/{area.Id}", token, new UpdateAreaRequest("Kitchen Renamed"));
        renameResponse.EnsureSuccessStatusCode();
        var renamed = await renameResponse.Content.ReadFromJsonAsync<AreaDto>();
        Assert.Equal("Kitchen Renamed", renamed!.Name);

        var deleteResponse = await SendAsync(HttpMethod.Delete, $"/api/areas/{area.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await SendAsync(HttpMethod.Get, "/api/areas", token);
        var areas = await listResponse.Content.ReadFromJsonAsync<List<AreaDto>>();
        Assert.Empty(areas!);
    }

    [Fact]
    public async Task DeleteArea_DeviceInIt_AreaIdBecomesNull()
    {
        var token = await RegisterWithHomeAsync();

        var areaResponse = await SendAsync(HttpMethod.Post, "/api/areas", token, new CreateAreaRequest("Living Room"));
        var area = await areaResponse.Content.ReadFromJsonAsync<AreaDto>();

        var deviceResponse = await SendAsync(HttpMethod.Post, "/api/devices", token,
            new CreateDeviceRequest("Lamp", DeviceType.Light, area!.Id));
        var device = await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>();
        Assert.Equal(area.Id, device!.AreaId);

        var deleteAreaResponse = await SendAsync(HttpMethod.Delete, $"/api/areas/{area.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteAreaResponse.StatusCode);

        var getDeviceResponse = await SendAsync(HttpMethod.Get, $"/api/devices/{device.Id}", token);
        getDeviceResponse.EnsureSuccessStatusCode();
        var refetched = await getDeviceResponse.Content.ReadFromJsonAsync<DeviceDto>();
        Assert.Null(refetched!.AreaId);
    }

    [Fact]
    public async Task CreateRenameDeleteLabel_Basic()
    {
        var token = await RegisterWithHomeAsync();

        var createResponse = await SendAsync(HttpMethod.Post, "/api/labels", token, new CreateLabelRequest("Upstairs"));
        createResponse.EnsureSuccessStatusCode();
        var label = await createResponse.Content.ReadFromJsonAsync<LabelDto>();
        Assert.Equal("Upstairs", label!.Name);

        var renameResponse = await SendAsync(HttpMethod.Put, $"/api/labels/{label.Id}", token, new UpdateLabelRequest("Upstairs Renamed"));
        renameResponse.EnsureSuccessStatusCode();
        var renamed = await renameResponse.Content.ReadFromJsonAsync<LabelDto>();
        Assert.Equal("Upstairs Renamed", renamed!.Name);

        var deleteResponse = await SendAsync(HttpMethod.Delete, $"/api/labels/{label.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await SendAsync(HttpMethod.Get, "/api/labels", token);
        var labels = await listResponse.Content.ReadFromJsonAsync<List<LabelDto>>();
        Assert.Empty(labels!);
    }

    [Fact]
    public async Task DeleteLabel_DeviceLosesTag()
    {
        var token = await RegisterWithHomeAsync();

        var labelResponse = await SendAsync(HttpMethod.Post, "/api/labels", token, new CreateLabelRequest("Downstairs"));
        var label = await labelResponse.Content.ReadFromJsonAsync<LabelDto>();

        var deviceResponse = await SendAsync(HttpMethod.Post, "/api/devices", token,
            new CreateDeviceRequest("Socket", DeviceType.Socket, null));
        var device = await deviceResponse.Content.ReadFromJsonAsync<DeviceDto>();

        var updateResponse = await SendAsync(HttpMethod.Put, $"/api/devices/{device!.Id}", token,
            new UpdateDeviceRequest(device.Name, null, [label!.Id], device.Attributes));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<DeviceDto>();
        Assert.Single(updated!.Labels);

        var deleteLabelResponse = await SendAsync(HttpMethod.Delete, $"/api/labels/{label.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteLabelResponse.StatusCode);

        var getDeviceResponse = await SendAsync(HttpMethod.Get, $"/api/devices/{device.Id}", token);
        var refetched = await getDeviceResponse.Content.ReadFromJsonAsync<DeviceDto>();
        Assert.Empty(refetched!.Labels);
    }

    [Fact]
    public async Task Areas_And_Labels_CrossHomeIsolation()
    {
        var tokenA = await RegisterWithHomeAsync();
        var tokenB = await RegisterWithHomeAsync();

        var areaResponse = await SendAsync(HttpMethod.Post, "/api/areas", tokenA, new CreateAreaRequest("A's Room"));
        var areaA = await areaResponse.Content.ReadFromJsonAsync<AreaDto>();

        var labelResponse = await SendAsync(HttpMethod.Post, "/api/labels", tokenA, new CreateLabelRequest("A's Label"));
        var labelA = await labelResponse.Content.ReadFromJsonAsync<LabelDto>();

        // B tries to reference A's Area at device creation time.
        var createWithForeignArea = await SendAsync(HttpMethod.Post, "/api/devices", tokenB,
            new CreateDeviceRequest("B's Device", DeviceType.Light, areaA!.Id));
        Assert.Equal(HttpStatusCode.BadRequest, createWithForeignArea.StatusCode);
        var areaError = await createWithForeignArea.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("AREA_NOT_FOUND", areaError!.Code);

        // B tries to reference A's Label when updating its own device.
        var ownDeviceResponse = await SendAsync(HttpMethod.Post, "/api/devices", tokenB,
            new CreateDeviceRequest("B's Device", DeviceType.Light, null));
        var ownDevice = await ownDeviceResponse.Content.ReadFromJsonAsync<DeviceDto>();

        var updateWithForeignLabel = await SendAsync(HttpMethod.Put, $"/api/devices/{ownDevice!.Id}", tokenB,
            new UpdateDeviceRequest(ownDevice.Name, null, [labelA!.Id], ownDevice.Attributes));
        Assert.Equal(HttpStatusCode.BadRequest, updateWithForeignLabel.StatusCode);
        var labelError = await updateWithForeignLabel.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("LABEL_NOT_FOUND", labelError!.Code);

        // B cannot rename/delete A's area or label.
        var renameAsB = await SendAsync(HttpMethod.Put, $"/api/areas/{areaA.Id}", tokenB, new UpdateAreaRequest("Hijacked"));
        Assert.Equal(HttpStatusCode.NotFound, renameAsB.StatusCode);
        var deleteAsB = await SendAsync(HttpMethod.Delete, $"/api/labels/{labelA.Id}", tokenB);
        Assert.Equal(HttpStatusCode.NotFound, deleteAsB.StatusCode);
    }
}
