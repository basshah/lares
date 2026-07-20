using System.Security.Claims;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Devices;
using Lares.Api.Contracts.Labels;
using Lares.Api.Data;
using Lares.Api.Domain;
using Lares.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController(LaresDbContext db, HomeAccessService homeAccess, IDeviceConnector connector) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<DeviceDto>> Create(CreateDeviceRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        if (request.AreaId is not null &&
            !await db.Areas.AnyAsync(a => a.Id == request.AreaId && a.HomeId == membership.HomeId))
            return BadRequest(new ApiError("AREA_NOT_FOUND"));

        var (state, attributes) = connector.Initialize(request.Type);

        var device = new Device
        {
            HomeId = membership.HomeId,
            Name = request.Name.Trim(),
            Type = request.Type,
            AreaId = request.AreaId,
            State = state,
            Attributes = attributes,
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        return await BuildDeviceDtoAsync(device);
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> List(Guid? areaId, Guid? labelId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var query = db.Devices.Include(d => d.Area).Where(d => d.HomeId == membership.HomeId);
        if (areaId is not null)
            query = query.Where(d => d.AreaId == areaId);
        if (labelId is not null)
            query = query.Where(d => db.DeviceLabels.Any(dl => dl.DeviceId == d.Id && dl.LabelId == labelId));

        var devices = await query.OrderBy(d => d.Name).ToListAsync();

        var labelsByDevice = await db.DeviceLabels
            .Where(dl => devices.Select(d => d.Id).Contains(dl.DeviceId))
            .Include(dl => dl.Label)
            .ToListAsync();
        var labelsLookup = labelsByDevice
            .GroupBy(dl => dl.DeviceId)
            .ToDictionary(g => g.Key, g => g.Select(dl => new LabelDto(dl.Label.Id, dl.Label.Name)).ToList());

        return devices
            .Select(d => ToDto(d, labelsLookup.GetValueOrDefault(d.Id, [])))
            .ToList();
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeviceDto>> Get(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var device = await db.Devices.Include(d => d.Area)
            .SingleOrDefaultAsync(d => d.Id == id && d.HomeId == membership.HomeId);
        if (device is null)
            return NotFound(new ApiError("DEVICE_NOT_FOUND"));

        return await BuildDeviceDtoAsync(device);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeviceDto>> Update(Guid id, UpdateDeviceRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var device = await db.Devices.Include(d => d.Area)
            .SingleOrDefaultAsync(d => d.Id == id && d.HomeId == membership.HomeId);
        if (device is null)
            return NotFound(new ApiError("DEVICE_NOT_FOUND"));

        if (request.AreaId is not null &&
            !await db.Areas.AnyAsync(a => a.Id == request.AreaId && a.HomeId == membership.HomeId))
            return BadRequest(new ApiError("AREA_NOT_FOUND"));

        var labelIds = request.LabelIds.Distinct().ToList();
        if (labelIds.Count > 0)
        {
            var validLabelCount = await db.Labels
                .CountAsync(l => l.HomeId == membership.HomeId && labelIds.Contains(l.Id));
            if (validLabelCount != labelIds.Count)
                return BadRequest(new ApiError("LABEL_NOT_FOUND"));
        }

        var attributes = ToDomain(request.Attributes);
        if (!DeviceCapabilityRegistry.AttributesMatchType(device.Type, attributes))
            return BadRequest(new ApiError("ATTRIBUTES_TYPE_MISMATCH"));

        device.Name = request.Name.Trim();
        device.AreaId = request.AreaId;
        device.Attributes = attributes;

        var existingLabels = await db.DeviceLabels.Where(dl => dl.DeviceId == device.Id).ToListAsync();
        db.DeviceLabels.RemoveRange(existingLabels);
        db.DeviceLabels.AddRange(labelIds.Select(labelId => new DeviceLabel { DeviceId = device.Id, LabelId = labelId }));

        await db.SaveChangesAsync();

        device = await db.Devices.Include(d => d.Area).SingleAsync(d => d.Id == device.Id);
        return await BuildDeviceDtoAsync(device);
    }

    [Authorize]
    [HttpPost("{id:guid}/actions")]
    public async Task<ActionResult<DeviceDto>> PerformAction(Guid id, DeviceActionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var device = await db.Devices.Include(d => d.Area)
            .SingleOrDefaultAsync(d => d.Id == id && d.HomeId == membership.HomeId);
        if (device is null)
            return NotFound(new ApiError("DEVICE_NOT_FOUND"));

        (string State, DeviceAttributes Attributes) result;
        try
        {
            result = connector.Execute(device, request.Action, request.Params);
        }
        catch (DeviceActionException ex)
        {
            return BadRequest(new ApiError(ex.Code));
        }

        device.State = result.State;
        device.Attributes = result.Attributes;

        db.DeviceLogs.Add(new DeviceLog
        {
            DeviceId = device.Id,
            Action = request.Action,
            ParamsJson = request.Params?.GetRawText(),
            Source = DeviceLogSource.User,
            UserId = userId,
        });

        await db.SaveChangesAsync();

        return await BuildDeviceDtoAsync(device);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == id && d.HomeId == membership.HomeId);
        if (device is null)
            return NotFound(new ApiError("DEVICE_NOT_FOUND"));

        db.Devices.Remove(device);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<DeviceDto> BuildDeviceDtoAsync(Device device)
    {
        var labels = await db.DeviceLabels
            .Where(dl => dl.DeviceId == device.Id)
            .Include(dl => dl.Label)
            .Select(dl => new LabelDto(dl.Label.Id, dl.Label.Name))
            .ToListAsync();

        return ToDto(device, labels);
    }

    private static DeviceDto ToDto(Device device, IReadOnlyList<LabelDto> labels) => new(
        device.Id,
        device.Name,
        device.Type.ToString(),
        device.AreaId,
        device.Area?.Name,
        device.State,
        ToDto(device.Attributes),
        labels,
        device.CreatedAtUtc);

    private static DeviceAttributesDto ToDto(DeviceAttributes a) =>
        new(a.Light, a.Socket, a.Thermostat, a.Camera, a.Tv);

    private static DeviceAttributes ToDomain(DeviceAttributesDto d) =>
        new() { Light = d.Light, Socket = d.Socket, Thermostat = d.Thermostat, Camera = d.Camera, Tv = d.Tv };
}
