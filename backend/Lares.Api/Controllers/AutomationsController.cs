using System.Security.Claims;
using System.Text.Json;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Automations;
using Lares.Api.Data;
using Lares.Api.Domain;
using Lares.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AutomationsController(
    LaresDbContext db,
    HomeAccessService homeAccess,
    IDeviceConnector connector,
    DeviceHubNotifier hubNotifier) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<AutomationDto>> Create(CreateAutomationRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var triggerError = await ValidateTriggerAsync(request.TriggerType, request.TriggerTimeOfDay,
            request.TriggerDeviceId, request.TriggerState, membership.HomeId);
        if (triggerError is not null)
            return BadRequest(new ApiError(triggerError));

        if (!await StepsReferenceOnlyHomeDevicesAsync(request.Steps, membership.HomeId))
            return BadRequest(new ApiError("DEVICE_NOT_FOUND"));

        var automation = new Automation { HomeId = membership.HomeId, Name = request.Name.Trim(), IsEnabled = request.IsEnabled };
        ApplyTrigger(automation, request.TriggerType, request.TriggerTimeOfDay, request.TriggerDaysOfWeek,
            request.TriggerDeviceId, request.TriggerState);

        db.Automations.Add(automation);
        db.AutomationSteps.AddRange(ToSteps(automation.Id, request.Steps));

        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return await BuildAutomationDtoAsync(automation.Id);
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AutomationDto>>> List()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var automations = await db.Automations
            .Include(a => a.Steps).ThenInclude(s => s.Device)
            .Include(a => a.TriggerDevice)
            .Where(a => a.HomeId == membership.HomeId)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return automations.Select(ToDto).ToList();
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AutomationDto>> Get(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var automation = await db.Automations
            .Include(a => a.Steps).ThenInclude(s => s.Device)
            .Include(a => a.TriggerDevice)
            .SingleOrDefaultAsync(a => a.Id == id && a.HomeId == membership.HomeId);
        if (automation is null)
            return NotFound(new ApiError("AUTOMATION_NOT_FOUND"));

        return ToDto(automation);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AutomationDto>> Update(Guid id, UpdateAutomationRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var automation = await db.Automations.SingleOrDefaultAsync(a => a.Id == id && a.HomeId == membership.HomeId);
        if (automation is null)
            return NotFound(new ApiError("AUTOMATION_NOT_FOUND"));

        var triggerError = await ValidateTriggerAsync(request.TriggerType, request.TriggerTimeOfDay,
            request.TriggerDeviceId, request.TriggerState, membership.HomeId);
        if (triggerError is not null)
            return BadRequest(new ApiError(triggerError));

        if (!await StepsReferenceOnlyHomeDevicesAsync(request.Steps, membership.HomeId))
            return BadRequest(new ApiError("DEVICE_NOT_FOUND"));

        automation.Name = request.Name.Trim();
        automation.IsEnabled = request.IsEnabled;
        ApplyTrigger(automation, request.TriggerType, request.TriggerTimeOfDay, request.TriggerDaysOfWeek,
            request.TriggerDeviceId, request.TriggerState);

        var existingSteps = await db.AutomationSteps.Where(s => s.AutomationId == automation.Id).ToListAsync();
        db.AutomationSteps.RemoveRange(existingSteps);
        db.AutomationSteps.AddRange(ToSteps(automation.Id, request.Steps));

        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return await BuildAutomationDtoAsync(automation.Id);
    }

    [Authorize]
    [HttpPatch("{id:guid}/enabled")]
    public async Task<ActionResult<AutomationDto>> SetEnabled(Guid id, SetAutomationEnabledRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var automation = await db.Automations.SingleOrDefaultAsync(a => a.Id == id && a.HomeId == membership.HomeId);
        if (automation is null)
            return NotFound(new ApiError("AUTOMATION_NOT_FOUND"));

        automation.IsEnabled = request.IsEnabled;
        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return await BuildAutomationDtoAsync(automation.Id);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var automation = await db.Automations.SingleOrDefaultAsync(a => a.Id == id && a.HomeId == membership.HomeId);
        if (automation is null)
            return NotFound(new ApiError("AUTOMATION_NOT_FOUND"));

        db.Automations.Remove(automation);
        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/run")]
    public async Task<ActionResult<AutomationExecuteResultDto>> Run(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var automation = await db.Automations
            .Include(a => a.Steps).ThenInclude(s => s.Device)
            .SingleOrDefaultAsync(a => a.Id == id && a.HomeId == membership.HomeId);
        if (automation is null)
            return NotFound(new ApiError("AUTOMATION_NOT_FOUND"));

        var results = new List<AutomationStepResultDto>();
        foreach (var step in automation.Steps.OrderBy(s => s.Order))
        {
            var actionParams = ParseParams(step.ParamsJson);
            var result = await DeviceActionExecutor.ExecuteAsync(db, connector, step.Device, step.Action, actionParams, DeviceLogSource.Automation, userId);
            results.Add(new AutomationStepResultDto(step.DeviceId, step.Device.Name, step.Action, result.Success, result.ErrorCode));
        }

        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return new AutomationExecuteResultDto(automation.Id, automation.Name, results);
    }

    private async Task<string?> ValidateTriggerAsync(
        AutomationTriggerType type, TimeOnly? time, Guid? deviceId, string? state, Guid homeId)
    {
        if (type == AutomationTriggerType.Time)
            return time is null ? "INVALID_TRIGGER" : null;

        if (deviceId is null || string.IsNullOrWhiteSpace(state))
            return "INVALID_TRIGGER";

        return await db.Devices.AnyAsync(d => d.Id == deviceId && d.HomeId == homeId) ? null : "DEVICE_NOT_FOUND";
    }

    private static void ApplyTrigger(Automation automation, AutomationTriggerType type, TimeOnly? time,
        IReadOnlyList<DayOfWeek>? days, Guid? deviceId, string? state)
    {
        automation.TriggerType = type;
        if (type == AutomationTriggerType.Time)
        {
            automation.TriggerTimeOfDay = time;
            automation.TriggerDaysOfWeekCsv = days is { Count: > 0 } ? string.Join(',', days.Distinct()) : null;
            automation.TriggerDeviceId = null;
            automation.TriggerState = null;
        }
        else
        {
            automation.TriggerTimeOfDay = null;
            automation.TriggerDaysOfWeekCsv = null;
            automation.TriggerDeviceId = deviceId;
            automation.TriggerState = state!.Trim();
        }
    }

    private async Task<bool> StepsReferenceOnlyHomeDevicesAsync(IReadOnlyList<AutomationStepRequest> steps, Guid homeId)
    {
        var deviceIds = steps.Select(s => s.DeviceId).Distinct().ToList();
        var validCount = await db.Devices.CountAsync(d => d.HomeId == homeId && deviceIds.Contains(d.Id));
        return validCount == deviceIds.Count;
    }

    private static IEnumerable<AutomationStep> ToSteps(Guid automationId, IReadOnlyList<AutomationStepRequest> steps) =>
        steps.Select((s, i) => new AutomationStep
        {
            AutomationId = automationId,
            DeviceId = s.DeviceId,
            Order = i,
            Action = s.Action,
            ParamsJson = s.Params?.GetRawText(),
        });

    private static JsonElement? ParseParams(string? paramsJson) =>
        paramsJson is null ? null : JsonDocument.Parse(paramsJson).RootElement;

    private async Task<AutomationDto> BuildAutomationDtoAsync(Guid id)
    {
        var automation = await db.Automations
            .Include(a => a.Steps).ThenInclude(s => s.Device)
            .Include(a => a.TriggerDevice)
            .SingleAsync(a => a.Id == id);
        return ToDto(automation);
    }

    private static AutomationDto ToDto(Automation a) => new(
        a.Id,
        a.Name,
        a.IsEnabled,
        a.TriggerType,
        a.TriggerTimeOfDay,
        a.TriggerDaysOfWeekCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<DayOfWeek>).ToList(),
        a.TriggerDeviceId,
        a.TriggerDevice?.Name,
        a.TriggerState,
        a.Steps.OrderBy(s => s.Order).Select(s => new AutomationStepDto(s.Id, s.DeviceId, s.Device.Name, s.Order, s.Action, ParseParams(s.ParamsJson))).ToList(),
        a.LastTriggeredAtUtc,
        a.CreatedAtUtc);
}
