using System.Text.Json;
using Lares.Api.Services;

namespace Lares.Api.Tests.Fakes;

/// <summary>
/// Deterministic stand-in for IAiClient, driven by markers in the latest user message
/// so tests can steer it without making real (paid/rate-limited) network calls.
/// </summary>
public sealed class FakeAiClient : IAiClient
{
    public Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        var lastMessage = request.Messages[^1];

        if (lastMessage.Content.Any(c => c is AiFunctionResultBlock))
            return Task.FromResult(new AiCompletion("STOP", [new AiTextBlock("Done.")]));

        var lastUserText = lastMessage.Content.OfType<AiTextBlock>().First().Text;

        if (lastUserText.StartsWith("ACTION:", StringComparison.Ordinal))
        {
            var args = JsonSerializer.Deserialize<JsonElement>(lastUserText["ACTION:".Length..]);
            return Task.FromResult(new AiCompletion("STOP",
                [new AiFunctionCallBlock("perform_device_action", args)]));
        }

        if (lastUserText.Contains("OFFTOPIC", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new AiCompletion("STOP",
                [new AiTextBlock("I can only help with your home and its devices.")]));

        return Task.FromResult(new AiCompletion("STOP",
            [new AiTextBlock($"home-state-included:{request.SystemInstruction.Contains("Devices:")}")]));
    }
}
