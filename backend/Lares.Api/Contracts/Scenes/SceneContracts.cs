using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Lares.Api.Contracts.Scenes;

public record SceneStepRequest(
    [Required] Guid DeviceId,
    [Required, MaxLength(50)] string Action,
    JsonElement? Params);

public record CreateSceneRequest(
    [Required, MaxLength(100)] string Name,
    [Required] IReadOnlyList<SceneStepRequest> Steps);

public record UpdateSceneRequest(
    [Required, MaxLength(100)] string Name,
    [Required] IReadOnlyList<SceneStepRequest> Steps);

public record SceneStepDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    int Order,
    string Action,
    JsonElement? Params);

public record SceneDto(
    Guid Id,
    string Name,
    IReadOnlyList<SceneStepDto> Steps,
    DateTime CreatedAtUtc);

public record SceneStepResultDto(
    Guid DeviceId,
    string DeviceName,
    string Action,
    bool Success,
    string? ErrorCode);

public record SceneExecuteResultDto(
    Guid SceneId,
    string SceneName,
    IReadOnlyList<SceneStepResultDto> Results);
