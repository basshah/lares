using System.Security.Claims;
using Lares.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Lares.Api.Hubs;

[Authorize]
public class DeviceHub(HomeAccessService homeAccess) : Hub
{
    public static string GroupName(Guid homeId) => $"home:{homeId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(membership.HomeId));

        await base.OnConnectedAsync();
    }
}
