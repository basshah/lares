using System.Text.Json;
using Lares.Api.Domain;

namespace Lares.Api.Services;

public interface IDeviceConnector
{
    (string State, DeviceAttributes Attributes) Initialize(DeviceType type);

    (string State, DeviceAttributes Attributes) Execute(Device device, string action, JsonElement? actionParams);
}
