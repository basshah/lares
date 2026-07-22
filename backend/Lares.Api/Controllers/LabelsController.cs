using System.Security.Claims;
using Lares.Api.Contracts.Auth;
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
public class LabelsController(LaresDbContext db, HomeAccessService homeAccess, DeviceHubNotifier hubNotifier) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<LabelDto>> Create(CreateLabelRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var label = new Label { HomeId = membership.HomeId, Name = request.Name.Trim() };
        db.Labels.Add(label);
        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return new LabelDto(label.Id, label.Name);
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LabelDto>>> List()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var labels = await db.Labels
            .Where(l => l.HomeId == membership.HomeId)
            .OrderBy(l => l.Name)
            .Select(l => new LabelDto(l.Id, l.Name))
            .ToListAsync();

        return labels;
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LabelDto>> Update(Guid id, UpdateLabelRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var label = await db.Labels.SingleOrDefaultAsync(l => l.Id == id && l.HomeId == membership.HomeId);
        if (label is null)
            return NotFound(new ApiError("LABEL_NOT_FOUND"));

        label.Name = request.Name.Trim();
        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return new LabelDto(label.Id, label.Name);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var label = await db.Labels.SingleOrDefaultAsync(l => l.Id == id && l.HomeId == membership.HomeId);
        if (label is null)
            return NotFound(new ApiError("LABEL_NOT_FOUND"));

        db.Labels.Remove(label);
        await db.SaveChangesAsync();
        await hubNotifier.NotifyHomeChangedAsync(membership.HomeId);

        return NoContent();
    }
}
