using Lares.Api.Domain;

namespace Lares.Api.Services;

public static class DeviceCapabilityRegistry
{
    public static (string State, DeviceAttributes Attributes) CreateDefault(DeviceType type) => type switch
    {
        DeviceType.Light => ("off", new DeviceAttributes { Light = new LightAttributes { IsOn = false, Brightness = 0 } }),
        DeviceType.Socket => ("off", new DeviceAttributes { Socket = new SocketAttributes { IsOn = false } }),
        DeviceType.Thermostat => ("idle", new DeviceAttributes
        {
            Thermostat = new ThermostatAttributes { TargetTemperatureC = 21, Mode = ThermostatMode.Off },
        }),
        DeviceType.Camera => ("online", new DeviceAttributes { Camera = new CameraAttributes { IsOnline = true } }),
        DeviceType.Tv => ("off", new DeviceAttributes { Tv = new TvAttributes { IsOn = false, Volume = 20 } }),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static bool AttributesMatchType(DeviceType type, DeviceAttributes attrs)
    {
        var populated = new object?[] { attrs.Light, attrs.Socket, attrs.Thermostat, attrs.Camera, attrs.Tv }
            .Count(g => g is not null);
        if (populated != 1)
            return false;

        return type switch
        {
            DeviceType.Light => attrs.Light is not null,
            DeviceType.Socket => attrs.Socket is not null,
            DeviceType.Thermostat => attrs.Thermostat is not null,
            DeviceType.Camera => attrs.Camera is not null,
            DeviceType.Tv => attrs.Tv is not null,
            _ => false,
        };
    }
}
