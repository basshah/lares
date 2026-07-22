using System.Text.Json;

namespace Lares.Api.Services;

public interface IAiClient
{
    Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default);
}

public sealed record AiCompletionRequest(
    string Model,
    string SystemInstruction,
    IReadOnlyList<AiMessage> Messages,
    IReadOnlyList<AiToolDefinition> Tools);

public sealed record AiMessage(string Role, IReadOnlyList<AiContentBlock> Content); // Role: "user" | "model"

public abstract record AiContentBlock;
public sealed record AiTextBlock(string Text) : AiContentBlock;

// ThoughtSignature is an opaque token Gemini's "thinking" models attach to function-call parts.
// It must be echoed back verbatim when the call is replayed as history in a follow-up request,
// or the API rejects the request with a 400 (missing thought_signature).
public sealed record AiFunctionCallBlock(string Name, JsonElement Args, string? ThoughtSignature = null) : AiContentBlock;
public sealed record AiFunctionResultBlock(string Name, string Result) : AiContentBlock;

public sealed record AiToolDefinition(string Name, string Description, JsonElement ParametersSchema);

public sealed record AiCompletion(string FinishReason, IReadOnlyList<AiContentBlock> Content);
