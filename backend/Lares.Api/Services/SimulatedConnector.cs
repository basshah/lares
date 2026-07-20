using Lares.Api.Domain;

namespace Lares.Api.Services;

public class SimulatedConnector : IDeviceConnector
{
    public (string State, DeviceAttributes Attributes) Initialize(DeviceType type) =>
        DeviceCapabilityRegistry.CreateDefault(type);
}
