using System.Text.Json;
using Lares.Api.Data;
using Lares.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Services;

public record DeviceActionResult(bool Success, string? ErrorCode);

public static class DeviceActionExecutor
{
    /// <summary>
    /// Executes one device action, mutates the device's in-memory State/Attributes, and adds a
    /// DeviceLog row to the context. Does NOT call SaveChangesAsync or notify the hub — the caller
    /// controls the transaction/notification boundary (once per HTTP request, once per scene/automation
    /// run, once per scheduler tick).
    ///
    /// If the action came from a non-Automation source and actually changed the device's state, this
    /// also evaluates and fires any enabled DeviceState-trigger automations watching this device+state,
    /// recursively through this same method with source=Automation. Because the recursive call's source
    /// is Automation, its own cascade check is skipped — this caps cascade depth at exactly one hop by
    /// construction, so no automation-triggered action can ever trigger a further automation.
    /// </summary>
    public static async Task<DeviceActionResult> ExecuteAsync(
        LaresDbContext db,
        IDeviceConnector connector,
        Device device,
        string action,
        JsonElement? actionParams,
        DeviceLogSource source,
        string? userId,
        CancellationToken ct = default)
    {
        var previousState = device.State;

        (string State, DeviceAttributes Attributes) result;
        try
        {
            result = connector.Execute(device, action, actionParams);
        }
        catch (DeviceActionException ex)
        {
            return new DeviceActionResult(false, ex.Code);
        }

        device.State = result.State;
        device.Attributes = result.Attributes;

        db.DeviceLogs.Add(new DeviceLog
        {
            DeviceId = device.Id,
            Action = action,
            ParamsJson = actionParams?.GetRawText(),
            Source = source,
            UserId = userId,
        });

        if (source != DeviceLogSource.Automation && device.State != previousState)
            await TriggerDeviceStateAutomationsAsync(db, connector, device, ct);

        return new DeviceActionResult(true, null);
    }

    private static async Task TriggerDeviceStateAutomationsAsync(
        LaresDbContext db, IDeviceConnector connector, Device device, CancellationToken ct)
    {
        var automations = await db.Automations
            .Include(a => a.Steps).ThenInclude(s => s.Device)
            .Where(a => a.HomeId == device.HomeId
                && a.IsEnabled
                && a.TriggerType == AutomationTriggerType.DeviceState
                && a.TriggerDeviceId == device.Id
                && a.TriggerState == device.State)
            .ToListAsync(ct);

        foreach (var automation in automations)
        {
            foreach (var step in automation.Steps.OrderBy(s => s.Order))
            {
                var stepParams = step.ParamsJson is null
                    ? (JsonElement?)null
                    : JsonDocument.Parse(step.ParamsJson).RootElement;
                await ExecuteAsync(db, connector, step.Device, step.Action, stepParams,
                    DeviceLogSource.Automation, userId: null, ct);
            }
        }
    }
}
