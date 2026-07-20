namespace Lares.Api.Domain;

public enum HomeRole
{
    Owner,
    Member,
}

public class Membership
{
    public Guid Id { get; set; }

    public Guid HomeId { get; set; }
    public Home Home { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public HomeRole Role { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
