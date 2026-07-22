using System.ComponentModel.DataAnnotations;

namespace Lares.Api.Contracts.Chat;

public record SendChatMessageRequest([Required, MaxLength(2000)] string Message);

public record ChatMessageDto(Guid Id, string Role, string Content, DateTime CreatedAtUtc);
