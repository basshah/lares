namespace Lares.Api.Domain;

public enum DeviceType
{
    Light,
    Socket,
    Thermostat,
    Camera,
    Tv,
}

public class Device
{
    public Guid Id { get; set; }

    public Guid HomeId { get; set; }
    public Home Home { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public DeviceType Type { get; set; }

    public Guid? AreaId { get; set; }
    public Area? Area { get; set; }

    public string State { get; set; } = string.Empty;
    public DeviceAttributes Attributes { get; set; } = new();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
