namespace Lares.Api.Domain;

public class DeviceAttributes
{
    public LightAttributes? Light { get; set; }
    public SocketAttributes? Socket { get; set; }
    public ThermostatAttributes? Thermostat { get; set; }
    public CameraAttributes? Camera { get; set; }
    public TvAttributes? Tv { get; set; }
}

public class LightAttributes
{
    public bool IsOn { get; set; }
    public int? Brightness { get; set; }
    public string? ColorHex { get; set; }
}

public class SocketAttributes
{
    public bool IsOn { get; set; }
}

public enum ThermostatMode
{
    Off,
    Heat,
    Cool,
    Auto,
}

public class ThermostatAttributes
{
    public double TargetTemperatureC { get; set; }
    public double? CurrentTemperatureC { get; set; }
    public ThermostatMode Mode { get; set; }
}

public class CameraAttributes
{
    public bool IsOnline { get; set; }
}

public class TvAttributes
{
    public bool IsOn { get; set; }
    public int Volume { get; set; }
    public string? Source { get; set; }
}
