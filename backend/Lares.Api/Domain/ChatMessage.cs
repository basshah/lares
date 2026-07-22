namespace Lares.Api.Domain;

public enum ChatMessageRole
{
    User,
    Assistant,
}

public class ChatMessage
{
    public Guid Id { get; set; }

    public Guid HomeId { get; set; }
    public Home Home { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public ChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
