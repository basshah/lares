using Lares.Api.Domain;

namespace Lares.Api.Services;

public interface IDeviceConnector
{
    (string State, DeviceAttributes Attributes) Initialize(DeviceType type);
}
