namespace Lares.Api.Domain;

public class Label
{
    public Guid Id { get; set; }

    public Guid HomeId { get; set; }
    public Home Home { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
