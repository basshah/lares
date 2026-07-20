using System.Text.Json;
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

    public static (string State, DeviceAttributes Attributes) Execute(Device device, string action, JsonElement? actionParams) =>
        device.Type switch
        {
            DeviceType.Light => ExecuteLight(device.Attributes.Light!, action, actionParams),
            DeviceType.Socket => ExecuteSocket(device.Attributes.Socket!, action, actionParams),
            DeviceType.Thermostat => ExecuteThermostat(device.Attributes.Thermostat!, action, actionParams),
            DeviceType.Camera => throw new DeviceActionException("UNKNOWN_ACTION"),
            DeviceType.Tv => ExecuteTv(device.Attributes.Tv!, action, actionParams),
            _ => throw new ArgumentOutOfRangeException(nameof(device)),
        };

    private static (string, DeviceAttributes) ExecuteLight(LightAttributes attrs, string action, JsonElement? p)
    {
        switch (action)
        {
            case "turnOn":
                attrs.IsOn = true;
                if (attrs.Brightness is null or 0)
                    attrs.Brightness = 100;
                return ("on", new DeviceAttributes { Light = attrs });
            case "turnOff":
                attrs.IsOn = false;
                return ("off", new DeviceAttributes { Light = attrs });
            case "setBrightness":
                var value = ReadIntParam(p, "value", 0, 100);
                attrs.Brightness = value;
                attrs.IsOn = value > 0;
                return (value > 0 ? "on" : "off", new DeviceAttributes { Light = attrs });
            default:
                throw new DeviceActionException("UNKNOWN_ACTION");
        }
    }

    private static (string, DeviceAttributes) ExecuteSocket(SocketAttributes attrs, string action, JsonElement? p)
    {
        switch (action)
        {
            case "turnOn":
                attrs.IsOn = true;
                return ("on", new DeviceAttributes { Socket = attrs });
            case "turnOff":
                attrs.IsOn = false;
                return ("off", new DeviceAttributes { Socket = attrs });
            default:
                throw new DeviceActionException("UNKNOWN_ACTION");
        }
    }

    private static (string, DeviceAttributes) ExecuteThermostat(ThermostatAttributes attrs, string action, JsonElement? p)
    {
        switch (action)
        {
            case "setTargetTemperature":
                attrs.TargetTemperatureC = ReadDoubleParam(p, "value");
                break;
            case "setMode":
                attrs.Mode = ReadEnumParam<ThermostatMode>(p, "mode");
                break;
            default:
                throw new DeviceActionException("UNKNOWN_ACTION");
        }

        var state = attrs.Mode switch
        {
            ThermostatMode.Off => "idle",
            ThermostatMode.Heat => "heating",
            ThermostatMode.Cool => "cooling",
            ThermostatMode.Auto => "auto",
            _ => "idle",
        };
        return (state, new DeviceAttributes { Thermostat = attrs });
    }

    private static (string, DeviceAttributes) ExecuteTv(TvAttributes attrs, string action, JsonElement? p)
    {
        switch (action)
        {
            case "turnOn":
                attrs.IsOn = true;
                return ("on", new DeviceAttributes { Tv = attrs });
            case "turnOff":
                attrs.IsOn = false;
                return ("off", new DeviceAttributes { Tv = attrs });
            case "setVolume":
                attrs.Volume = ReadIntParam(p, "value", 0, 100);
                return (attrs.IsOn ? "on" : "off", new DeviceAttributes { Tv = attrs });
            default:
                throw new DeviceActionException("UNKNOWN_ACTION");
        }
    }

    private static int ReadIntParam(JsonElement? p, string name, int min, int max)
    {
        if (p is not { } element || element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Number ||
            !prop.TryGetInt32(out var value) || value < min || value > max)
            throw new DeviceActionException("INVALID_ACTION_PARAMS");
        return value;
    }

    private static double ReadDoubleParam(JsonElement? p, string name)
    {
        if (p is not { } element || element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Number ||
            !prop.TryGetDouble(out var value))
            throw new DeviceActionException("INVALID_ACTION_PARAMS");
        return value;
    }

    private static TEnum ReadEnumParam<TEnum>(JsonElement? p, string name) where TEnum : struct, Enum
    {
        if (p is not { } element || element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String ||
            !Enum.TryParse<TEnum>(prop.GetString(), out var value))
            throw new DeviceActionException("INVALID_ACTION_PARAMS");
        return value;
    }
}
