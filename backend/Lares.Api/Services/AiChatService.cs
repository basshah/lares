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
                new AiCompletionRequest(Model, systemInstruction, conversation, [PerformDeviceActionTool]), ct);

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

    private async Task<AiContentBlock> ExecuteToolAsync(Guid homeId, string userId, AiFunctionCallBlock call, CancellationToken ct)
    {
        if (call.Name != "perform_device_action" ||
            !call.Args.TryGetProperty("deviceId", out var deviceIdProp) ||
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

        (string State, DeviceAttributes Attributes) result;
        try
        {
            result = connector.Execute(device, action, actionParams);
        }
        catch (DeviceActionException ex)
        {
            return new AiFunctionResultBlock(call.Name, $"Action failed: {ex.Code}");
        }

        device.State = result.State;
        device.Attributes = result.Attributes;
        db.DeviceLogs.Add(new DeviceLog
        {
            DeviceId = device.Id,
            Action = action,
            ParamsJson = actionParams?.GetRawText(),
            Source = DeviceLogSource.Ai,
            UserId = userId,
        });
        await db.SaveChangesAsync(ct);
        await hubNotifier.NotifyHomeChangedAsync(homeId);

        return new AiFunctionResultBlock(call.Name, $"{device.Name} is now {result.State}.");
    }

    private async Task<string> BuildSystemInstructionAsync(Guid homeId, CancellationToken ct)
    {
        var devices = await db.Devices.Include(d => d.Area).Where(d => d.HomeId == homeId).OrderBy(d => d.Name).ToListAsync(ct);
        var labels = await db.Labels.Where(l => l.HomeId == homeId).OrderBy(l => l.Name).Select(l => l.Name).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("You are the Lares smart-home assistant. You may ONLY discuss this home, its devices, areas, " +
                       "and their state, and take device actions on the user's behalf via the perform_device_action tool. " +
                       "If asked about anything unrelated to this home, politely decline and steer back to home topics.");
        sb.AppendLine();
        sb.AppendLine("Current home state:");
        sb.AppendLine("Devices:");
        foreach (var d in devices)
            sb.AppendLine($"- id: {d.Id}, name: \"{d.Name}\", type: {d.Type}, area: {d.Area?.Name ?? "none"}, state: {d.State}");

        if (labels.Count > 0)
            sb.AppendLine($"Labels: {string.Join(", ", labels)}");

        return sb.ToString();
    }
}
