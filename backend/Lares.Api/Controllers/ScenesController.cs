using System.Security.Claims;
using System.Text.Json;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Scenes;
using Lares.Api.Data;
using Lares.Api.Domain;
using Lares.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScenesController(
    LaresDbContext db,
    HomeAccessService homeAccess,
    IDeviceConnector connector,
    DeviceHubNotifier hubNotifier) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<SceneDto>> Create(CreateSceneRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        if (!await StepsReferenceOnlyHomeDevicesAsync(request.Steps, membership.HomeId))
            return BadRequest(new ApiError("DEVICE_NOT_FOUND"));

        var scene = new Scene { HomeId = membership.HomeId, Name = request.Name.Trim() };
        db.Scenes.Add(scene);
        db.SceneSteps.AddRange(ToSteps(scene.Id, request.Steps));

        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return await BuildSceneDtoAsync(scene.Id);
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SceneDto>>> List()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var scenes = await db.Scenes
            .Include(s => s.Steps).ThenInclude(s => s.Device)
            .Where(s => s.HomeId == membership.HomeId)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return scenes.Select(ToDto).ToList();
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SceneDto>> Get(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var scene = await db.Scenes
            .Include(s => s.Steps).ThenInclude(s => s.Device)
            .SingleOrDefaultAsync(s => s.Id == id && s.HomeId == membership.HomeId);
        if (scene is null)
            return NotFound(new ApiError("SCENE_NOT_FOUND"));

        return ToDto(scene);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SceneDto>> Update(Guid id, UpdateSceneRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var scene = await db.Scenes.SingleOrDefaultAsync(s => s.Id == id && s.HomeId == membership.HomeId);
        if (scene is null)
            return NotFound(new ApiError("SCENE_NOT_FOUND"));

        if (!await StepsReferenceOnlyHomeDevicesAsync(request.Steps, membership.HomeId))
            return BadRequest(new ApiError("DEVICE_NOT_FOUND"));

        scene.Name = request.Name.Trim();

        var existingSteps = await db.SceneSteps.Where(s => s.SceneId == scene.Id).ToListAsync();
        db.SceneSteps.RemoveRange(existingSteps);
        db.SceneSteps.AddRange(ToSteps(scene.Id, request.Steps));

        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return await BuildSceneDtoAsync(scene.Id);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var scene = await db.Scenes.SingleOrDefaultAsync(s => s.Id == id && s.HomeId == membership.HomeId);
        if (scene is null)
            return NotFound(new ApiError("SCENE_NOT_FOUND"));

        db.Scenes.Remove(scene);
        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/execute")]
    public async Task<ActionResult<SceneExecuteResultDto>> Execute(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var scene = await db.Scenes
            .Include(s => s.Steps).ThenInclude(s => s.Device)
            .SingleOrDefaultAsync(s => s.Id == id && s.HomeId == membership.HomeId);
        if (scene is null)
            return NotFound(new ApiError("SCENE_NOT_FOUND"));

        var results = new List<SceneStepResultDto>();
        foreach (var step in scene.Steps.OrderBy(s => s.Order))
        {
            var actionParams = ParseParams(step.ParamsJson);
            var result = DeviceActionExecutor.Execute(db, connector, step.Device, step.Action, actionParams, DeviceLogSource.Scene, userId);
            results.Add(new SceneStepResultDto(step.DeviceId, step.Device.Name, step.Action, result.Success, result.ErrorCode));
        }

        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return new SceneExecuteResultDto(scene.Id, scene.Name, results);
    }

    private async Task<bool> StepsReferenceOnlyHomeDevicesAsync(IReadOnlyList<SceneStepRequest> steps, Guid homeId)
    {
        var deviceIds = steps.Select(s => s.DeviceId).Distinct().ToList();
        var validCount = await db.Devices.CountAsync(d => d.HomeId == homeId && deviceIds.Contains(d.Id));
        return validCount == deviceIds.Count;
    }

    private static IEnumerable<SceneStep> ToSteps(Guid sceneId, IReadOnlyList<SceneStepRequest> steps) =>
        steps.Select((s, i) => new SceneStep
        {
            SceneId = sceneId,
            DeviceId = s.DeviceId,
            Order = i,
            Action = s.Action,
            ParamsJson = s.Params?.GetRawText(),
        });

    private static JsonElement? ParseParams(string? paramsJson) =>
        paramsJson is null ? null : JsonDocument.Parse(paramsJson).RootElement;

    private async Task<SceneDto> BuildSceneDtoAsync(Guid sceneId)
    {
        var scene = await db.Scenes
            .Include(s => s.Steps).ThenInclude(s => s.Device)
            .SingleAsync(s => s.Id == sceneId);
        return ToDto(scene);
    }

    private static SceneDto ToDto(Scene scene) => new(
        scene.Id,
        scene.Name,
        scene.Steps
            .OrderBy(s => s.Order)
            .Select(s => new SceneStepDto(s.Id, s.DeviceId, s.Device.Name, s.Order, s.Action, ParseParams(s.ParamsJson)))
            .ToList(),
        scene.CreatedAtUtc);
}
