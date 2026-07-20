namespace Lares.Api.Domain;

public class Home
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
