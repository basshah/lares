namespace Lares.Api.Domain;

public enum DeviceLogSource
{
    User,
    Ai,
    Scene,
}

public class DeviceLog
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public string Action { get; set; } = string.Empty;
    public string? ParamsJson { get; set; }

    public DeviceLogSource Source { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
