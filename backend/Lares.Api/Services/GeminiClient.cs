using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lares.Api.Services;

public sealed class GeminiClient(HttpClient httpClient, IConfiguration configuration) : IAiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Gemini:ApiKey is not configured. Run: dotnet user-secrets set \"Gemini:ApiKey\" \"<key>\" --project backend/Lares.Api");

        var body = new GeminiRequestBody
        {
            SystemInstruction = new GeminiContent { Parts = [new GeminiPart { Text = request.SystemInstruction }] },
            Contents = request.Messages.Select(ToGeminiContent).ToList(),
            Tools = request.Tools.Count == 0
                ? null
                : [new GeminiTool { FunctionDeclarations = request.Tools.Select(ToGeminiFunctionDeclaration).ToList() }],
        };

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{request.Model}:generateContent");
        httpRequest.Headers.Add("x-goog-api-key", apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(httpRequest, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini API request failed ({(int)response.StatusCode}): {responseText}");

        var parsed = JsonSerializer.Deserialize<GeminiResponseBody>(responseText, JsonOptions);
        var candidate = parsed?.Candidates?.FirstOrDefault();
        if (candidate?.Content is null)
            return new AiCompletion("STOP", [new AiTextBlock(string.Empty)]);

        var contentBlocks = candidate.Content.Parts.Select(ToAiContentBlock).ToList();
        return new AiCompletion(candidate.FinishReason ?? "STOP", contentBlocks);
    }

    private static GeminiContent ToGeminiContent(AiMessage message) => new()
    {
        Role = message.Role,
        Parts = message.Content.Select(ToGeminiPart).ToList(),
    };

    private static GeminiPart ToGeminiPart(AiContentBlock block) => block switch
    {
        AiTextBlock t => new GeminiPart { Text = t.Text },
        AiFunctionCallBlock f => new GeminiPart
        {
            FunctionCall = new GeminiFunctionCall { Name = f.Name, Args = f.Args },
            ThoughtSignature = f.ThoughtSignature,
        },
        AiFunctionResultBlock r => new GeminiPart
        {
            FunctionResponse = new GeminiFunctionResponse
            {
                Name = r.Name,
                Response = JsonSerializer.SerializeToElement(new { result = r.Result }, JsonOptions),
            },
        },
        _ => throw new NotSupportedException(block.GetType().Name),
    };

    private static AiContentBlock ToAiContentBlock(GeminiPart part)
    {
        if (part.FunctionCall is not null)
            return new AiFunctionCallBlock(part.FunctionCall.Name, part.FunctionCall.Args, part.ThoughtSignature);
        return new AiTextBlock(part.Text ?? string.Empty);
    }

    private static GeminiFunctionDeclaration ToGeminiFunctionDeclaration(AiToolDefinition tool) => new()
    {
        Name = tool.Name,
        Description = tool.Description,
        Parameters = tool.ParametersSchema,
    };

    private sealed class GeminiRequestBody
    {
        [JsonPropertyName("systemInstruction")]
        public GeminiContent? SystemInstruction { get; set; }

        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = [];

        [JsonPropertyName("tools")]
        public List<GeminiTool>? Tools { get; set; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("functionCall")]
        public GeminiFunctionCall? FunctionCall { get; set; }

        [JsonPropertyName("functionResponse")]
        public GeminiFunctionResponse? FunctionResponse { get; set; }

        [JsonPropertyName("thoughtSignature")]
        public string? ThoughtSignature { get; set; }
    }

    private sealed class GeminiFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public JsonElement Args { get; set; }
    }

    private sealed class GeminiFunctionResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("response")]
        public JsonElement Response { get; set; }
    }

    private sealed class GeminiTool
    {
        [JsonPropertyName("functionDeclarations")]
        public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = [];
    }

    private sealed class GeminiFunctionDeclaration
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public JsonElement Parameters { get; set; }
    }

    private sealed class GeminiResponseBody
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }
}
