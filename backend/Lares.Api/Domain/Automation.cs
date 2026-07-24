namespace Lares.Api.Domain;

public enum AutomationTriggerType
{
    Time,
    DeviceState,
}

public class Automation
{
    public Guid Id { get; set; }

    public Guid HomeId { get; set; }
    public Home Home { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    public AutomationTriggerType TriggerType { get; set; }

    // Time trigger (null when TriggerType == DeviceState)
    public TimeOnly? TriggerTimeOfDay { get; set; }
    public string? TriggerDaysOfWeekCsv { get; set; } // DayOfWeek names, comma-separated; null = every day

    // DeviceState trigger (null when TriggerType == Time)
    public Guid? TriggerDeviceId { get; set; }
    public Device? TriggerDevice { get; set; }
    public string? TriggerState { get; set; }

    // Set only by the time-based scheduler, to avoid double-firing within one matching minute.
    public DateTime? LastTriggeredAtUtc { get; set; }

    public ICollection<AutomationStep> Steps { get; set; } = new List<AutomationStep>();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
