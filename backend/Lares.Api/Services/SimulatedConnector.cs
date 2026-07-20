using System.Text.Json;
using Lares.Api.Domain;

namespace Lares.Api.Services;

public class SimulatedConnector : IDeviceConnector
{
    public (string State, DeviceAttributes Attributes) Initialize(DeviceType type) =>
        DeviceCapabilityRegistry.CreateDefault(type);

    public (string State, DeviceAttributes Attributes) Execute(Device device, string action, JsonElement? actionParams) =>
        DeviceCapabilityRegistry.Execute(device, action, actionParams);
}
