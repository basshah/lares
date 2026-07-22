using Lares.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Lares.Api.Services;

public class DeviceHubNotifier(IHubContext<DeviceHub> hubContext)
{
    public Task NotifyHomeChangedAsync(Guid homeId) =>
        hubContext.Clients.Group(DeviceHub.GroupName(homeId)).SendAsync("homeChanged");
}
