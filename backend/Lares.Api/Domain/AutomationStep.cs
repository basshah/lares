namespace Lares.Api.Domain;

public class AutomationStep
{
    public Guid Id { get; set; }

    public Guid AutomationId { get; set; }
    public Automation Automation { get; set; } = null!;

    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public int Order { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? ParamsJson { get; set; }
}
