namespace Lares.Api.Domain;

// Floor qruplaşdırması M8-də nullable FK kimi əlavə olunacaq (indi lazım deyil).
public class Area
{
    public Guid Id { get; set; }

    public Guid HomeId { get; set; }
    public Home Home { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
