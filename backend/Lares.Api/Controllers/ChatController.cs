using System.Security.Claims;
using Lares.Api.Contracts.Auth;
using Lares.Api.Contracts.Chat;
using Lares.Api.Domain;
using Lares.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lares.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController(HomeAccessService homeAccess, IAiChatService aiChatService) : ControllerBase
{
    [Authorize]
    [HttpPost("messages")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(SendChatMessageRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var assistantMessage = await aiChatService.SendMessageAsync(membership.HomeId, userId, request.Message);
        return ToDto(assistantMessage);
    }

    [Authorize]
    [HttpGet("messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var membership = await homeAccess.GetMembershipAsync(userId);
        if (membership is null)
            return NotFound(new ApiError("NOT_IN_A_HOME"));

        var history = await aiChatService.GetHistoryAsync(membership.HomeId, userId);
        return history.Select(ToDto).ToList();
    }

    private static ChatMessageDto ToDto(ChatMessage m) => new(m.Id, m.Role.ToString(), m.Content, m.CreatedAtUtc);
}
