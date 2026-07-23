using System.Text;
using System.Text.Json;
using Lares.Api.Data;
using Lares.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Services;

public interface IAiChatService
{
    Task<ChatMessage> SendMessageAsync(Guid homeId, string userId, string userMessage, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(Guid homeId, string userId, CancellationToken ct = default);
}

public class AiChatService(
    LaresDbContext db,
    IDeviceConnector connector,
    DeviceHubNotifier hubNotifier,
    IAiClient aiClient) : IAiChatService
{
    private const string Model = "gemini-flash-latest";
    private const int MaxToolIterations = 5;

    private static readonly AiToolDefinition PerformDeviceActionTool = new(
        Name: "perform_device_action",
        Description: "Perform an action on a smart home device in the user's home (turn on/off, set brightness, " +
                      "set thermostat target temperature or mode, set TV volume, etc). Always use a deviceId from " +
                      "the home state listed in the system instruction — never invent one.",
        ParametersSchema: JsonSerializer.SerializeToElement(new
        {
            type = "OBJECT",
            properties = new
            {
                deviceId = new { type = "STRING", description = "The id (GUID) of the device to control, from the home state." },
                action = new { type = "STRING", description = "Action name: turnOn, turnOff, setBrightness, setTargetTemperature, setMode, setVolume." },
                @params = new
                {
                    type = "OBJECT",
                    description = "Action parameters. setBrightness/setVolume: {\"value\": <0-100 int>}. " +
                                   "setTargetTemperature: {\"value\": <double>}. setMode: {\"mode\": \"Off\"|\"Heat\"|\"Cool\"|\"Auto\"}. " +
                                   "Omit for turnOn/turnOff.",
                },
            },
            required = new[] { "deviceId", "action" },
        }));

    private static readonly AiToolDefinition RunSceneTool = new(
        Name: "run_scene",
        Description: "Run a named scene (a preset group of device actions, e.g. \"Movie night\") in the user's home. " +
                      "Always use a sceneId from the home state listed in the system instruction — never invent one.",
        ParametersSchema: JsonSerializer.SerializeToElement(new
        {
            type = "OBJECT",
            properties = new
            {
                sceneId = new { type = "STRING", description = "The id (GUID) of the scene to run, from the home state." },
            },
            required = new[] { "sceneId" },
        }));

    public async Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(Guid homeId, string userId, CancellationToken ct = default) =>
        await db.ChatMessages
            .Where(m => m.HomeId == homeId && m.UserId == userId)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<ChatMessage> SendMessageAsync(Guid homeId, string userId, string userMessage, CancellationToken ct = default)
    {
        var priorMessages = await GetHistoryAsync(homeId, userId, ct);
        var systemInstruction = await BuildSystemInstructionAsync(homeId, ct);

        var conversation = priorMessages
            .Select(m => new AiMessage(
                m.Role == ChatMessageRole.User ? "user" : "model",
                [new AiTextBlock(m.Content)]))
            .ToList();
        conversation.Add(new AiMessage("user", [new AiTextBlock(userMessage)]));

        var finalText = "Sorry, I couldn't complete that request.";
        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            var completion = await aiClient.CompleteAsync(
                new AiCompletionRequest(Model, systemInstruction, conversation, [PerformDeviceActionTool, RunSceneTool]), ct);

            var functionCalls = completion.Content.OfType<AiFunctionCallBlock>().ToList();
            var text = string.Concat(completion.Content.OfType<AiTextBlock>().Select(b => b.Text));
            if (text.Length > 0)
                finalText = text;

            if (functionCalls.Count == 0)
                break;

            conversation.Add(new AiMessage("model", completion.Content));

            var results = new List<AiContentBlock>();
            foreach (var call in functionCalls)
                results.Add(await ExecuteToolAsync(homeId, userId, call, ct));
            conversation.Add(new AiMessage("user", results));
        }

        var now = DateTime.UtcNow;
        var userRow = new ChatMessage { HomeId = homeId, UserId = userId, Role = ChatMessageRole.User, Content = userMessage, CreatedAtUtc = now };
        var assistantRow = new ChatMessage { HomeId = homeId, UserId = userId, Role = ChatMessageRole.Assistant, Content = finalText, CreatedAtUtc = now.AddMilliseconds(1) };
        db.ChatMessages.AddRange(userRow, assistantRow);
        await db.SaveChangesAsync(ct);

        return assistantRow;
    }

    private Task<AiContentBlock> ExecuteToolAsync(Guid homeId, string userId, AiFunctionCallBlock call, CancellationToken ct) =>
        call.Name switch
        {
            "perform_device_action" => ExecutePerformDeviceActionAsync(homeId, userId, call, ct),
            "run_scene" => ExecuteRunSceneAsync(homeId, userId, call, ct),
            _ => Task.FromResult<AiContentBlock>(new AiFunctionResultBlock(call.Name, "Unknown tool.")),
        };

    private async Task<AiContentBlock> ExecutePerformDeviceActionAsync(Guid homeId, string userId, AiFunctionCallBlock call, CancellationToken ct)
    {
        if (!call.Args.TryGetProperty("deviceId", out var deviceIdProp) ||
            !Guid.TryParse(deviceIdProp.GetString(), out var deviceId) ||
            !call.Args.TryGetProperty("action", out var actionProp))
            return new AiFunctionResultBlock(call.Name, "Invalid tool call.");

        var action = actionProp.GetString()!;
        JsonElement? actionParams = call.Args.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object
            ? p
            : null;

        var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == deviceId && d.HomeId == homeId, ct);
        if (device is null)
            return new AiFunctionResultBlock(call.Name, "Device not found in this home.");

        var result = DeviceActionExecutor.Execute(db, connector, device, action, actionParams, DeviceLogSource.Ai, userId);
        if (!result.Success)
            return new AiFunctionResultBlock(call.Name, $"Action failed: {result.ErrorCode}");

        await db.SaveChangesAsync(ct);
        await hubNotifier.NotifyHomeChangedAsync(homeId);

        return new AiFunctionResultBlock(call.Name, $"{device.Name} is now {device.State}.");
    }

    private async Task<AiContentBlock> ExecuteRunSceneAsync(Guid homeId, string userId, AiFunctionCallBlock call, CancellationToken ct)
    {
        if (!call.Args.TryGetProperty("sceneId", out var sceneIdProp) || !Guid.TryParse(sceneIdProp.GetString(), out var sceneId))
            return new AiFunctionResultBlock(call.Name, "Invalid tool call.");

        var scene = await db.Scenes
            .Include(s => s.Steps).ThenInclude(s => s.Device)
            .SingleOrDefaultAsync(s => s.Id == sceneId && s.HomeId == homeId, ct);
        if (scene is null)
            return new AiFunctionResultBlock(call.Name, "Scene not found in this home.");

        var succeeded = 0;
        foreach (var step in scene.Steps.OrderBy(s => s.Order))
        {
            var actionParams = step.ParamsJson is null ? (JsonElement?)null : JsonDocument.Parse(step.ParamsJson).RootElement;
            var result = DeviceActionExecutor.Execute(db, connector, step.Device, step.Action, actionParams, DeviceLogSource.Scene, userId);
            if (result.Success)
                succeeded++;
        }

        await db.SaveChangesAsync(ct);
        await hubNotifier.NotifyHomeChangedAsync(homeId);

        return new AiFunctionResultBlock(call.Name, $"Ran scene '{scene.Name}': {succeeded}/{scene.Steps.Count} actions succeeded.");
    }

    private async Task<string> BuildSystemInstructionAsync(Guid homeId, CancellationToken ct)
    {
        var devices = await db.Devices.Include(d => d.Area).Where(d => d.HomeId == homeId).OrderBy(d => d.Name).ToListAsync(ct);
        var labels = await db.Labels.Where(l => l.HomeId == homeId).OrderBy(l => l.Name).Select(l => l.Name).ToListAsync(ct);
        var scenes = await db.Scenes.Include(s => s.Steps).Where(s => s.HomeId == homeId).OrderBy(s => s.Name).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("You are the Lares smart-home assistant. You may ONLY discuss this home, its devices, areas, " +
                       "and their state, and take device actions on the user's behalf via the perform_device_action tool " +
                       "or the run_scene tool. If asked about anything unrelated to this home, politely decline and steer " +
                       "back to home topics.");
        sb.AppendLine();
        sb.AppendLine("Current home state:");
        sb.AppendLine("Devices:");
        foreach (var d in devices)
            sb.AppendLine($"- id: {d.Id}, name: \"{d.Name}\", type: {d.Type}, area: {d.Area?.Name ?? "none"}, state: {d.State}");

        if (labels.Count > 0)
            sb.AppendLine($"Labels: {string.Join(", ", labels)}");

        if (scenes.Count > 0)
        {
            sb.AppendLine("Scenes:");
            foreach (var s in scenes)
                sb.AppendLine($"- id: {s.Id}, name: \"{s.Name}\" ({s.Steps.Count} step(s))");
        }

        return sb.ToString();
    }
}
