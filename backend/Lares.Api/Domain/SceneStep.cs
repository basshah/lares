namespace Lares.Api.Domain;

public class SceneStep
{
    public Guid Id { get; set; }

    public Guid SceneId { get; set; }
    public Scene Scene { get; set; } = null!;

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public int Order { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? ParamsJson { get; set; }
}
