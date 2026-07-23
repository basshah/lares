namespace Lares.Api.Domain;

public class Scene
{
    public Guid Id { get; set; }

    public Guid HomeId { get; set; }
    public Home Home { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public ICollection<SceneStep> Steps { get; set; } = new List<SceneStep>();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
