using System.Text.Json;
using Lares.Api.Data;
using Lares.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Lares.Api.Services;

public class AutomationSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<AutomationSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Automation scheduler tick failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    /// One deterministic scheduler tick: finds all enabled Time-trigger automations due right now
    /// (across all homes), runs their steps, and does a single save + per-home notify. Takes an
    /// IServiceProvider (not `this`) so tests can invoke exactly one tick against a known DB state
    /// without waiting on real wall-clock time or the background poll loop.
    /// </summary>
    public static async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<LaresDbContext>();
        var connector = services.GetRequiredService<IDeviceConnector>();
        var hubNotifier = services.GetRequiredService<DeviceHubNotifier>();

        var now = DateTime.UtcNow;
        var nowMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        var currentTime = TimeOnly.FromDateTime(now);

        var candidates = await db.Automations
            .Include(a => a.Steps).ThenInclude(s => s.Device)
            .Where(a => a.IsEnabled && a.TriggerType == AutomationTriggerType.Time)
            .ToListAsync(ct);

        var due = candidates.Where(a => IsDue(a, currentTime, now.DayOfWeek, nowMinute)).ToList();
        if (due.Count == 0)
            return;

        var affectedHomeIds = new HashSet<Guid>();
        foreach (var automation in due)
        {
            automation.LastTriggeredAtUtc = nowMinute;
            affectedHomeIds.Add(automation.HomeId);

            foreach (var step in automation.Steps.OrderBy(s => s.Order))
            {
                var stepParams = step.ParamsJson is null
                    ? (JsonElement?)null
                    : JsonDocument.Parse(step.ParamsJson).RootElement;
                await DeviceActionExecutor.ExecuteAsync(db, connector, step.Device, step.Action, stepParams,
                    DeviceLogSource.Automation, userId: null, ct);
            }
        }

        await db.SaveChangesAsync(ct);
        foreach (var homeId in affectedHomeIds)
            await hubNotifier.NotifyHomeChangedAsync(homeId);
    }

    private static bool IsDue(Automation a, TimeOnly currentTime, DayOfWeek today, DateTime nowMinute)
    {
        if (a.TriggerTimeOfDay is not { } t) return false;
        if (t.Hour != currentTime.Hour || t.Minute != currentTime.Minute) return false;
        if (!DayMatches(a.TriggerDaysOfWeekCsv, today)) return false;
        return a.LastTriggeredAtUtc is null || a.LastTriggeredAtUtc.Value < nowMinute;
    }

    private static bool DayMatches(string? daysCsv, DayOfWeek day)
    {
        if (string.IsNullOrWhiteSpace(daysCsv)) return true; // null/empty = every day
        return daysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Contains(day.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}
