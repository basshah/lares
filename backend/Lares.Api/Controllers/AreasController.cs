using System.Security.Claims;
using Lares.Api.Contracts.Areas;
using Lares.Api.Contracts.Auth;
using Lares.Api.Data;
using Lares.Api.Domain;
using Lares.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AreasController(LaresDbContext db, HomeAccessService homeAccess) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<AreaDto>> Create(CreateAreaRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var area = new Area { HomeId = membership.HomeId, Name = request.Name.Trim() };
        db.Areas.Add(area);
        await db.SaveChangesAsync();

        return new AreaDto(area.Id, area.Name, 0, area.CreatedAtUtc);
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AreaDto>>> List()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var deviceCounts = await db.Devices
            .Where(d => d.HomeId == membership.HomeId && d.AreaId != null)
            .GroupBy(d => d.AreaId!.Value)
            .Select(g => new { AreaId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.AreaId, g => g.Count);

        var areas = await db.Areas
            .Where(a => a.HomeId == membership.HomeId)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return areas
            .Select(a => new AreaDto(a.Id, a.Name, deviceCounts.GetValueOrDefault(a.Id), a.CreatedAtUtc))
            .ToList();
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AreaDto>> Update(Guid id, UpdateAreaRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var area = await db.Areas.SingleOrDefaultAsync(a => a.Id == id && a.HomeId == membership.HomeId);
        if (area is null)
            return NotFound(new ApiError("AREA_NOT_FOUND"));

        area.Name = request.Name.Trim();
        await db.SaveChangesAsync();

        var deviceCount = await db.Devices.CountAsync(d => d.AreaId == area.Id);
        return new AreaDto(area.Id, area.Name, deviceCount, area.CreatedAtUtc);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var area = await db.Areas.SingleOrDefaultAsync(a => a.Id == id && a.HomeId == membership.HomeId);
        if (area is null)
            return NotFound(new ApiError("AREA_NOT_FOUND"));

        db.Areas.Remove(area);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
