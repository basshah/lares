using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Lares.Api.Domain;

namespace Lares.Api.Contracts.Automations;

public record AutomationStepRequest(
    [Required] Guid DeviceId,
    [Required, MaxLength(50)] string Action,
    JsonElement? Params);

public record AutomationStepDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    int Order,
    string Action,
    JsonElement? Params);

public record CreateAutomationRequest(
    [Required, MaxLength(100)] string Name,
    bool IsEnabled,
    [Required] AutomationTriggerType TriggerType,
    TimeOnly? TriggerTimeOfDay,
    IReadOnlyList<DayOfWeek>? TriggerDaysOfWeek,
    Guid? TriggerDeviceId,
    [MaxLength(50)] string? TriggerState,
    [Required] IReadOnlyList<AutomationStepRequest> Steps);

public record UpdateAutomationRequest(
    [Required, MaxLength(100)] string Name,
    bool IsEnabled,
    [Required] AutomationTriggerType TriggerType,
    TimeOnly? TriggerTimeOfDay,
    IReadOnlyList<DayOfWeek>? TriggerDaysOfWeek,
    Guid? TriggerDeviceId,
    [MaxLength(50)] string? TriggerState,
    [Required] IReadOnlyList<AutomationStepRequest> Steps);

public record SetAutomationEnabledRequest(bool IsEnabled);

public record AutomationDto(
    Guid Id,
    string Name,
    bool IsEnabled,
    AutomationTriggerType TriggerType,
    TimeOnly? TriggerTimeOfDay,
    IReadOnlyList<DayOfWeek>? TriggerDaysOfWeek,
    Guid? TriggerDeviceId,
    string? TriggerDeviceName,
    string? TriggerState,
    IReadOnlyList<AutomationStepDto> Steps,
    DateTime? LastTriggeredAtUtc,
    DateTime CreatedAtUtc);

public record AutomationStepResultDto(
    Guid DeviceId,
    string DeviceName,
    string Action,
    bool Success,
    string? ErrorCode);

public record AutomationExecuteResultDto(
    Guid AutomationId,
    string AutomationName,
    IReadOnlyList<AutomationStepResultDto> Results);
