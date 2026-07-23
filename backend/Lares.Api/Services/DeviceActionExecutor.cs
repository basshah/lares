using System.Text.Json;
using Lares.Api.Data;
using Lares.Api.Domain;

namespace Lares.Api.Services;

public record DeviceActionResult(bool Success, string? ErrorCode);

public static class DeviceActionExecutor
{
    /// <summary>
    /// Executes one device action, mutates the device's in-memory State/Attributes, and adds a
    /// DeviceLog row to the context. Does NOT call SaveChangesAsync or notify the hub — the caller
    /// controls the transaction/notification boundary (once per HTTP request, or once per scene run).
    /// </summary>
    public static DeviceActionResult Execute(
        LaresDbContext db,
        IDeviceConnector connector,
        Device device,
        string action,
        JsonElement? actionParams,
        DeviceLogSource source,
        string? userId)
    {
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

        return new DeviceActionResult(true, null);
    }
}
