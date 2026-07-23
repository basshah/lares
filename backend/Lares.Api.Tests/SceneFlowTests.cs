using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Devices;
using Lares.Api.Contracts.Homes;
using Lares.Api.Contracts.Scenes;
using Lares.Api.Data;
using Lares.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lares.Api.Tests;

public class SceneFlowTests(LaresApiFactory factory) : IClassFixture<LaresApiFactory>
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
            new CreateDeviceRequest($"My {type}-{Guid.NewGuid():N}", type, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions))!;
    }

    private static JsonElement ParamsOf(object value) => JsonSerializer.SerializeToElement(value, JsonOptions);

    private async Task<SceneDto> CreateSceneAsync(string token, string name, IReadOnlyList<SceneStepRequest> steps)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/scenes", token, new CreateSceneRequest(name, steps));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SceneDto>(JsonOptions))!;
    }

    [Fact]
    public async Task CreateScene_WithSteps_RoundTripsViaGetAndList()
    {
        var token = await RegisterWithHomeAsync();
        var light = await CreateDeviceAsync(token, DeviceType.Light);
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);

        var steps = new List<SceneStepRequest>
        {
            new(light.Id, "setBrightness", ParamsOf(new { value = 40 })),
            new(socket.Id, "turnOn", null),
        };
        var created = await CreateSceneAsync(token, "Movie night", steps);

        Assert.Equal("Movie night", created.Name);
        Assert.Equal(2, created.Steps.Count);
        Assert.Equal(light.Id, created.Steps[0].DeviceId);
        Assert.Equal("setBrightness", created.Steps[0].Action);
        Assert.Equal(socket.Id, created.Steps[1].DeviceId);

        var getResponse = await SendAsync(HttpMethod.Get, $"/api/scenes/{created.Id}", token);
        getResponse.EnsureSuccessStatusCode();
        var fetched = (await getResponse.Content.ReadFromJsonAsync<SceneDto>(JsonOptions))!;
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(2, fetched.Steps.Count);

        var listResponse = await SendAsync(HttpMethod.Get, "/api/scenes", token);
        listResponse.EnsureSuccessStatusCode();
        var list = (await listResponse.Content.ReadFromJsonAsync<List<SceneDto>>(JsonOptions))!;
        Assert.Single(list);
    }

    [Fact]
    public async Task UpdateScene_ReplacesSteps()
    {
        var token = await RegisterWithHomeAsync();
        var light = await CreateDeviceAsync(token, DeviceType.Light);
        var tv = await CreateDeviceAsync(token, DeviceType.Tv);

        var scene = await CreateSceneAsync(token, "Original", [new SceneStepRequest(light.Id, "turnOn", null)]);

        var updateResponse = await SendAsync(HttpMethod.Put, $"/api/scenes/{scene.Id}", token,
            new UpdateSceneRequest("Renamed", [new SceneStepRequest(tv.Id, "setVolume", ParamsOf(new { value = 50 }))]));
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.Content.ReadFromJsonAsync<SceneDto>(JsonOptions))!;

        Assert.Equal("Renamed", updated.Name);
        Assert.Single(updated.Steps);
        Assert.Equal(tv.Id, updated.Steps[0].DeviceId);
        Assert.Equal("setVolume", updated.Steps[0].Action);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        var remainingSteps = await db.SceneSteps.Where(s => s.SceneId == scene.Id).ToListAsync();
        Assert.Single(remainingSteps);
        Assert.Equal(tv.Id, remainingSteps[0].DeviceId);
    }

    [Fact]
    public async Task DeleteScene_RemovesSceneAndSteps()
    {
        var token = await RegisterWithHomeAsync();
        var light = await CreateDeviceAsync(token, DeviceType.Light);
        var scene = await CreateSceneAsync(token, "ToDelete", [new SceneStepRequest(light.Id, "turnOn", null)]);

        var deleteResponse = await SendAsync(HttpMethod.Delete, $"/api/scenes/{scene.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await SendAsync(HttpMethod.Get, $"/api/scenes/{scene.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        Assert.False(await db.SceneSteps.AnyAsync(s => s.SceneId == scene.Id));
    }

    [Fact]
    public async Task CreateScene_StepReferencingDeviceFromAnotherHome_ReturnsBadRequest()
    {
        var tokenA = await RegisterWithHomeAsync();
        var tokenB = await RegisterWithHomeAsync();
        var deviceB = await CreateDeviceAsync(tokenB, DeviceType.Light);

        var response = await SendAsync(HttpMethod.Post, "/api/scenes", tokenA,
            new CreateSceneRequest("Cross-home", [new SceneStepRequest(deviceB.Id, "turnOn", null)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        Assert.Equal("DEVICE_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task UpdateScene_StepReferencingDeviceFromAnotherHome_ReturnsBadRequest()
    {
        var tokenA = await RegisterWithHomeAsync();
        var tokenB = await RegisterWithHomeAsync();
        var deviceA = await CreateDeviceAsync(tokenA, DeviceType.Light);
        var deviceB = await CreateDeviceAsync(tokenB, DeviceType.Light);
        var scene = await CreateSceneAsync(tokenA, "Scene A", [new SceneStepRequest(deviceA.Id, "turnOn", null)]);

        var response = await SendAsync(HttpMethod.Put, $"/api/scenes/{scene.Id}", tokenA,
            new UpdateSceneRequest("Scene A", [new SceneStepRequest(deviceB.Id, "turnOn", null)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteScene_AppliesAllStepsAndWritesSceneDeviceLogs()
    {
        var token = await RegisterWithHomeAsync();
        var light = await CreateDeviceAsync(token, DeviceType.Light);
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);
        var scene = await CreateSceneAsync(token, "Evening", new List<SceneStepRequest>
        {
            new(light.Id, "setBrightness", ParamsOf(new { value = 60 })),
            new(socket.Id, "turnOn", null),
        });

        var executeResponse = await SendAsync(HttpMethod.Post, $"/api/scenes/{scene.Id}/execute", token);
        executeResponse.EnsureSuccessStatusCode();
        var result = (await executeResponse.Content.ReadFromJsonAsync<SceneExecuteResultDto>(JsonOptions))!;

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, r => Assert.True(r.Success));

        var lightResponse = await SendAsync(HttpMethod.Get, $"/api/devices/{light.Id}", token);
        var updatedLight = (await lightResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions))!;
        Assert.Equal(60, updatedLight.Attributes.Light!.Brightness);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        var logs = await db.DeviceLogs.Where(l => l.DeviceId == light.Id || l.DeviceId == socket.Id).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.All(logs, l => Assert.Equal(DeviceLogSource.Scene, l.Source));
    }

    [Fact]
    public async Task ExecuteScene_BestEffort_OneFailingStepDoesNotBlockOthers()
    {
        var token = await RegisterWithHomeAsync();
        var camera = await CreateDeviceAsync(token, DeviceType.Camera); // Camera has no supported actions
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);
        var scene = await CreateSceneAsync(token, "Mixed", new List<SceneStepRequest>
        {
            new(camera.Id, "turnOn", null),
            new(socket.Id, "turnOn", null),
        });

        var executeResponse = await SendAsync(HttpMethod.Post, $"/api/scenes/{scene.Id}/execute", token);
        executeResponse.EnsureSuccessStatusCode();
        var result = (await executeResponse.Content.ReadFromJsonAsync<SceneExecuteResultDto>(JsonOptions))!;

        var cameraResult = result.Results.Single(r => r.DeviceId == camera.Id);
        var socketResult = result.Results.Single(r => r.DeviceId == socket.Id);
        Assert.False(cameraResult.Success);
        Assert.Equal("UNKNOWN_ACTION", cameraResult.ErrorCode);
        Assert.True(socketResult.Success);

        var socketResponse = await SendAsync(HttpMethod.Get, $"/api/devices/{socket.Id}", token);
        var updatedSocket = (await socketResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions))!;
        Assert.Equal("on", updatedSocket.State);
    }

    [Fact]
    public async Task ExecuteScene_DeviceDeletedAfterSceneCreated_StepIsDroppedNotErrored()
    {
        var token = await RegisterWithHomeAsync();
        var doomed = await CreateDeviceAsync(token, DeviceType.Light);
        var survivor = await CreateDeviceAsync(token, DeviceType.Socket);
        var scene = await CreateSceneAsync(token, "Survives deletion", new List<SceneStepRequest>
        {
            new(doomed.Id, "turnOn", null),
            new(survivor.Id, "turnOn", null),
        });

        var deleteResponse = await SendAsync(HttpMethod.Delete, $"/api/devices/{doomed.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var executeResponse = await SendAsync(HttpMethod.Post, $"/api/scenes/{scene.Id}/execute", token);
        executeResponse.EnsureSuccessStatusCode();
        var result = (await executeResponse.Content.ReadFromJsonAsync<SceneExecuteResultDto>(JsonOptions))!;

        Assert.Single(result.Results);
        Assert.Equal(survivor.Id, result.Results[0].DeviceId);
        Assert.True(result.Results[0].Success);
    }

    [Fact]
    public async Task Scenes_CrossHomeIsolation_CannotViewUpdateDeleteOrExecuteAnotherHomesScene()
    {
        var tokenA = await RegisterWithHomeAsync();
        var tokenB = await RegisterWithHomeAsync();
        var deviceA = await CreateDeviceAsync(tokenA, DeviceType.Light);
        var scene = await CreateSceneAsync(tokenA, "Home A scene", [new SceneStepRequest(deviceA.Id, "turnOn", null)]);

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/scenes/{scene.Id}", tokenB)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, $"/api/scenes/{scene.Id}", tokenB,
            new UpdateSceneRequest("Hijacked", []))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"/api/scenes/{scene.Id}", tokenB)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, $"/api/scenes/{scene.Id}/execute", tokenB)).StatusCode);
    }
}
