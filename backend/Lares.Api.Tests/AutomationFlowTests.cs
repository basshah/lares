using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Automations;
using Lares.Api.Contracts.Devices;
using Lares.Api.Contracts.Homes;
using Lares.Api.Data;
using Lares.Api.Domain;
using Lares.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lares.Api.Tests;

public class AutomationFlowTests(LaresApiFactory factory) : IClassFixture<LaresApiFactory>
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

    private async Task<AutomationDto> CreateAutomationAsync(string token, CreateAutomationRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/automations", token, request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutomationDto>(JsonOptions))!;
    }

    private static CreateAutomationRequest TimeTrigger(string name, TimeOnly time, Guid deviceId, string action) =>
        new(name, true, AutomationTriggerType.Time, time, null, null, null,
            [new AutomationStepRequest(deviceId, action, null)]);

    private static CreateAutomationRequest DeviceStateTrigger(
        string name, Guid triggerDeviceId, string triggerState, IReadOnlyList<AutomationStepRequest> steps) =>
        new(name, true, AutomationTriggerType.DeviceState, null, null, triggerDeviceId, triggerState, steps);

    [Fact]
    public async Task CreateAutomation_TimeTrigger_RoundTripsViaGetAndList()
    {
        var token = await RegisterWithHomeAsync();
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);

        var created = await CreateAutomationAsync(token, TimeTrigger("Morning", new TimeOnly(7, 30), socket.Id, "turnOn"));

        Assert.Equal("Morning", created.Name);
        Assert.True(created.IsEnabled);
        Assert.Equal(AutomationTriggerType.Time, created.TriggerType);
        Assert.Equal(new TimeOnly(7, 30), created.TriggerTimeOfDay);
        Assert.Single(created.Steps);

        var getResponse = await SendAsync(HttpMethod.Get, $"/api/automations/{created.Id}", token);
        getResponse.EnsureSuccessStatusCode();
        var fetched = (await getResponse.Content.ReadFromJsonAsync<AutomationDto>(JsonOptions))!;
        Assert.Equal(created.Id, fetched.Id);

        var listResponse = await SendAsync(HttpMethod.Get, "/api/automations", token);
        listResponse.EnsureSuccessStatusCode();
        var list = (await listResponse.Content.ReadFromJsonAsync<List<AutomationDto>>(JsonOptions))!;
        Assert.Single(list);
    }

    [Fact]
    public async Task CreateAutomation_DeviceStateTrigger_RoundTrips()
    {
        var token = await RegisterWithHomeAsync();
        var light = await CreateDeviceAsync(token, DeviceType.Light);
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);

        var created = await CreateAutomationAsync(token,
            DeviceStateTrigger("Light on -> Socket on", light.Id, "on", [new AutomationStepRequest(socket.Id, "turnOn", null)]));

        Assert.Equal(AutomationTriggerType.DeviceState, created.TriggerType);
        Assert.Equal(light.Id, created.TriggerDeviceId);
        Assert.Equal("on", created.TriggerState);
        Assert.Null(created.TriggerTimeOfDay);
    }

    [Fact]
    public async Task UpdateAutomation_ReplacesStepsAndTrigger()
    {
        var token = await RegisterWithHomeAsync();
        var light = await CreateDeviceAsync(token, DeviceType.Light);
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);

        var automation = await CreateAutomationAsync(token, TimeTrigger("Original", new TimeOnly(8, 0), socket.Id, "turnOn"));

        var updateRequest = new UpdateAutomationRequest("Switched", true, AutomationTriggerType.DeviceState, null, null,
            light.Id, "on", [new AutomationStepRequest(socket.Id, "turnOff", null)]);
        var updateResponse = await SendAsync(HttpMethod.Put, $"/api/automations/{automation.Id}", token, updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.Content.ReadFromJsonAsync<AutomationDto>(JsonOptions))!;

        Assert.Equal("Switched", updated.Name);
        Assert.Equal(AutomationTriggerType.DeviceState, updated.TriggerType);
        Assert.Null(updated.TriggerTimeOfDay);
        Assert.Equal(light.Id, updated.TriggerDeviceId);
        Assert.Single(updated.Steps);
        Assert.Equal("turnOff", updated.Steps[0].Action);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        var steps = await db.AutomationSteps.Where(s => s.AutomationId == automation.Id).ToListAsync();
        Assert.Single(steps);
    }

    [Fact]
    public async Task DeleteAutomation_RemovesAutomationAndSteps()
    {
        var token = await RegisterWithHomeAsync();
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);
        var automation = await CreateAutomationAsync(token, TimeTrigger("ToDelete", new TimeOnly(9, 0), socket.Id, "turnOn"));

        var deleteResponse = await SendAsync(HttpMethod.Delete, $"/api/automations/{automation.Id}", token);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await SendAsync(HttpMethod.Get, $"/api/automations/{automation.Id}", token);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        Assert.False(await db.AutomationSteps.AnyAsync(s => s.AutomationId == automation.Id));
    }

    [Fact]
    public async Task CreateAutomation_StepReferencingDeviceFromAnotherHome_ReturnsBadRequest()
    {
        var tokenA = await RegisterWithHomeAsync();
        var tokenB = await RegisterWithHomeAsync();
        var deviceB = await CreateDeviceAsync(tokenB, DeviceType.Socket);

        var response = await SendAsync(HttpMethod.Post, "/api/automations", tokenA,
            TimeTrigger("Cross-home", new TimeOnly(10, 0), deviceB.Id, "turnOn"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        Assert.Equal("DEVICE_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task CreateAutomation_DeviceStateTrigger_TriggerDeviceFromAnotherHome_ReturnsBadRequest()
    {
        var tokenA = await RegisterWithHomeAsync();
        var tokenB = await RegisterWithHomeAsync();
        var deviceA = await CreateDeviceAsync(tokenA, DeviceType.Socket);
        var deviceB = await CreateDeviceAsync(tokenB, DeviceType.Light);

        var response = await SendAsync(HttpMethod.Post, "/api/automations", tokenA,
            DeviceStateTrigger("Bad trigger", deviceB.Id, "on", [new AutomationStepRequest(deviceA.Id, "turnOn", null)]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        Assert.Equal("DEVICE_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task CreateAutomation_TimeTriggerMissingTimeOfDay_ReturnsInvalidTrigger()
    {
        var token = await RegisterWithHomeAsync();
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);

        var request = new CreateAutomationRequest("No time", true, AutomationTriggerType.Time, null, null, null, null,
            [new AutomationStepRequest(socket.Id, "turnOn", null)]);
        var response = await SendAsync(HttpMethod.Post, "/api/automations", token, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions);
        Assert.Equal("INVALID_TRIGGER", error!.Code);
    }

    [Fact]
    public async Task SetEnabled_TogglesFlag()
    {
        var token = await RegisterWithHomeAsync();
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);
        var automation = await CreateAutomationAsync(token, TimeTrigger("Toggle me", new TimeOnly(11, 0), socket.Id, "turnOn"));

        var response = await SendAsync(HttpMethod.Patch, $"/api/automations/{automation.Id}/enabled", token,
            new SetAutomationEnabledRequest(false));
        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<AutomationDto>(JsonOptions))!;
        Assert.False(updated.IsEnabled);
    }

    [Fact]
    public async Task RunAutomation_AppliesAllStepsAndWritesAutomationDeviceLogsWithActingUser()
    {
        var token = await RegisterWithHomeAsync();
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);
        var automation = await CreateAutomationAsync(token, TimeTrigger("Run me", new TimeOnly(12, 0), socket.Id, "turnOn"));

        var runResponse = await SendAsync(HttpMethod.Post, $"/api/automations/{automation.Id}/run", token);
        runResponse.EnsureSuccessStatusCode();
        var result = (await runResponse.Content.ReadFromJsonAsync<AutomationExecuteResultDto>(JsonOptions))!;
        Assert.Single(result.Results);
        Assert.True(result.Results[0].Success);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        var log = await db.DeviceLogs.SingleAsync(l => l.DeviceId == socket.Id);
        Assert.Equal(DeviceLogSource.Automation, log.Source);
        Assert.NotNull(log.UserId);
    }

    [Fact]
    public async Task Automations_CrossHomeIsolation_CannotViewUpdateDeleteOrRunAnotherHomesAutomation()
    {
        var tokenA = await RegisterWithHomeAsync();
        var tokenB = await RegisterWithHomeAsync();
        var socket = await CreateDeviceAsync(tokenA, DeviceType.Socket);
        var automation = await CreateAutomationAsync(tokenA, TimeTrigger("Home A", new TimeOnly(13, 0), socket.Id, "turnOn"));

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"/api/automations/{automation.Id}", tokenB)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, $"/api/automations/{automation.Id}", tokenB,
            new UpdateAutomationRequest("Hijacked", true, AutomationTriggerType.Time, new TimeOnly(1, 0), null, null, null, []))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"/api/automations/{automation.Id}", tokenB)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, $"/api/automations/{automation.Id}/run", tokenB)).StatusCode);
    }

    [Fact]
    public async Task DeviceStateTrigger_CascadesIntoAutomationSteps_AndLogsAutomationSource()
    {
        var token = await RegisterWithHomeAsync();
        var light = await CreateDeviceAsync(token, DeviceType.Light);
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);
        await CreateAutomationAsync(token, DeviceStateTrigger("On cascade", light.Id, "on", [new AutomationStepRequest(socket.Id, "turnOn", null)]));

        var actionResponse = await SendAsync(HttpMethod.Post, $"/api/devices/{light.Id}/actions", token,
            new DeviceActionRequest("turnOn", null));
        actionResponse.EnsureSuccessStatusCode();

        var socketResponse = await SendAsync(HttpMethod.Get, $"/api/devices/{socket.Id}", token);
        var updatedSocket = (await socketResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions))!;
        Assert.Equal("on", updatedSocket.State);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        var log = await db.DeviceLogs.SingleAsync(l => l.DeviceId == socket.Id);
        Assert.Equal(DeviceLogSource.Automation, log.Source);
        Assert.Null(log.UserId);
    }

    [Fact]
    public async Task DeviceStateTrigger_DoesNotRefireOnIdempotentSameStateAction()
    {
        var token = await RegisterWithHomeAsync();
        var light = await CreateDeviceAsync(token, DeviceType.Light);
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);
        await CreateAutomationAsync(token, DeviceStateTrigger("On cascade", light.Id, "on", [new AutomationStepRequest(socket.Id, "turnOn", null)]));

        // First turnOn: off -> on, a real transition, should cascade.
        (await SendAsync(HttpMethod.Post, $"/api/devices/{light.Id}/actions", token, new DeviceActionRequest("turnOn", null)))
            .EnsureSuccessStatusCode();
        // Second turnOn: on -> on, no transition, should NOT cascade again.
        (await SendAsync(HttpMethod.Post, $"/api/devices/{light.Id}/actions", token, new DeviceActionRequest("turnOn", null)))
            .EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        var socketLogCount = await db.DeviceLogs.CountAsync(l => l.DeviceId == socket.Id);
        Assert.Equal(1, socketLogCount);
    }

    [Fact]
    public async Task DeviceStateTrigger_CascadeCapsAtOneHop()
    {
        var token = await RegisterWithHomeAsync();
        var deviceA = await CreateDeviceAsync(token, DeviceType.Light);
        var deviceB = await CreateDeviceAsync(token, DeviceType.Socket);
        var deviceC = await CreateDeviceAsync(token, DeviceType.Tv);

        // X: A -> on ⇒ turn on B.
        await CreateAutomationAsync(token, DeviceStateTrigger("X", deviceA.Id, "on", [new AutomationStepRequest(deviceB.Id, "turnOn", null)]));
        // Y: B -> on ⇒ turn on C.
        await CreateAutomationAsync(token, DeviceStateTrigger("Y", deviceB.Id, "on", [new AutomationStepRequest(deviceC.Id, "turnOn", null)]));

        (await SendAsync(HttpMethod.Post, $"/api/devices/{deviceA.Id}/actions", token, new DeviceActionRequest("turnOn", null)))
            .EnsureSuccessStatusCode();

        var bResponse = await SendAsync(HttpMethod.Get, $"/api/devices/{deviceB.Id}", token);
        var updatedB = (await bResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions))!;
        Assert.Equal("on", updatedB.State); // hop 1: fired

        var cResponse = await SendAsync(HttpMethod.Get, $"/api/devices/{deviceC.Id}", token);
        var updatedC = (await cResponse.Content.ReadFromJsonAsync<DeviceDto>(JsonOptions))!;
        Assert.Equal("off", updatedC.State); // hop 2: blocked by the source=Automation guard
    }

    [Fact]
    public async Task RunOnceAsync_FiresDueTimeTrigger_AndDoesNotDoubleFireOnImmediateSecondTick()
    {
        var token = await RegisterWithHomeAsync();
        var socket = await CreateDeviceAsync(token, DeviceType.Socket);
        await CreateAutomationAsync(token, TimeTrigger("Now", TimeOnly.FromDateTime(DateTime.UtcNow), socket.Id, "turnOn"));

        using var scope = factory.Services.CreateScope();
        await AutomationSchedulerService.RunOnceAsync(scope.ServiceProvider, CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<LaresDbContext>();
        Assert.Equal(1, await db.DeviceLogs.CountAsync(l => l.DeviceId == socket.Id));

        await AutomationSchedulerService.RunOnceAsync(scope.ServiceProvider, CancellationToken.None);
        Assert.Equal(1, await db.DeviceLogs.CountAsync(l => l.DeviceId == socket.Id));
    }
}
